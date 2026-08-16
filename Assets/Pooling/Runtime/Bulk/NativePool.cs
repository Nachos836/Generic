#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Generic.Core;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

namespace Pooling.Bulk
{
    [PublicAPI]
    public static class NativePool
    {
        public static UniTask<NativePool<TPooled, TPooledOperationRunner>> CreateAsync<TPooled, TPooledOperationRunner>
        (
            NativePoolServiceScene nativePoolServiceScene,
            TPooled prototype,
            int requestedCapacity,
            int requestedParallelism = -1,
            CancellationToken cancellation = default
        )
            where TPooled : MonoBehaviour
            where TPooledOperationRunner : struct, IPooledOperations
        {
            return NativePool<TPooled, TPooledOperationRunner>.CreateAsync(nativePoolServiceScene,
                                                                           prototype,
                                                                           requestedCapacity,
                                                                           requestedParallelism,
                                                                           cancellation);
        }
    }

    /// <summary>
    /// Orchestrates N independent buckets for real <see cref="UnityEngine.Jobs.IJobParallelForTransform"/> parallelism. <br/>
    /// Capacity is rounded up to a power of two; parallelism is rounded down to a power of two — the resulting per-pool capacity divides evenly.
    /// </summary>
    public sealed class NativePool<TPooled, TPooledOperationRunner> : IDisposable
        where TPooled : MonoBehaviour
        where TPooledOperationRunner : struct, IPooledOperations
    {
        private readonly Scene _scene;
        private readonly TPooled _prototype;
        private readonly int _parallelism;
        private readonly InstantiateParameters[] _instantiateParameters;

        private NativeList<TransformHandle> _freeTransforms;
        private NativeList<EntityId> _freeEntities;
        private TransformAccessArray _jobTransforms;
        private NativeList<EntityId> _jobEntities;
        private SwapbackArray<TPooled> _freeInstances;

        private NativePoolServiceScene.Handle _serviceSceneHandle;

        private bool _disposed;

        internal static async UniTask<NativePool<TPooled, TPooledOperationRunner>> CreateAsync
        (
            NativePoolServiceScene nativePoolServiceScene,
            TPooled prototype,
            int requestedCapacity,
            int requestedParallelism = -1,
            CancellationToken cancellation = default
        ) {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException();
            if (prototype ==null) throw new ArgumentNullException(nameof(prototype));
            if (requestedCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedCapacity));

            var rawParallelism = requestedParallelism > 0 ? requestedParallelism : JobsUtility.JobWorkerCount;
            var parallelism = math.ceilpow2(math.min(rawParallelism, requestedCapacity));
            var totalCapacity = math.ceilpow2(requestedCapacity);
            var capacityPerRoot = math.max(1, totalCapacity / parallelism);

            var serviceSceneHandle = nativePoolServiceScene.AcquireSceneHandle();
            var instantiatePrototype = await UnityEngine.Object.InstantiateAsync(prototype, new InstantiateParameters
            {
                originalImmutable = true,
                scene = serviceSceneHandle.Scene
            }, cancellation);
            var immutablePrototype = instantiatePrototype.Single();
            var prototypeGameObject = immutablePrototype.gameObject;
            prototypeGameObject.name = prototype.name;
            prototypeGameObject.SetActive(false);

            var rootSample = new GameObject { isStatic = true };
            var scene = SceneManager.CreateScene($"[Pool] {prototypeGameObject.name}");
            var bucketRoots = await UnityEngine.Object.InstantiateAsync(rootSample, parallelism, new InstantiateParameters
            {
                parent = null,
                scene = scene,
                worldSpace = false,
                originalImmutable = true
            }, cancellation);

            var instantiateParameters = new InstantiateParameters[parallelism];
            for (var i = 0; i < parallelism; ++i)
            {
                var root = bucketRoots[i];
                root.name = $"Bucket #{i:00} [{i * capacityPerRoot :0000} .. {(i + 1) * capacityPerRoot :0000}]";
                instantiateParameters[i] = new InstantiateParameters
                {
                    parent = root.GetComponent<Transform>(),
                    scene = scene,
                    worldSpace = false,
                    originalImmutable = true
                };
            }
            var freeTransforms = new NativeList<TransformHandle>(requestedCapacity, Allocator.Persistent);
            var freeEntities = new NativeList<EntityId>(freeTransforms.Capacity, Allocator.Persistent);
            var jobTransforms = new TransformAccessArray(freeTransforms.Capacity, desiredJobCount: parallelism);
            var jobEntities = new NativeList<EntityId>(freeTransforms.Capacity, Allocator.Persistent);

            UnityEngine.Object.Destroy(rootSample);

            return new NativePool<TPooled, TPooledOperationRunner>(scene,
                                                                   immutablePrototype,
                                                                   parallelism,
                                                                   freeTransforms,
                                                                   freeEntities,
                                                                   jobTransforms,
                                                                   jobEntities,
                                                                   instantiateParameters,
                                                                   serviceSceneHandle);
        }

        private NativePool
        (
            Scene scene,
            TPooled prototype,
            int requestedParallelism,
            NativeList<TransformHandle> freeTransforms,
            NativeList<EntityId> freeEntities,
            TransformAccessArray jobTransforms,
            NativeList<EntityId> jobEntities,
            InstantiateParameters[] instantiateParameters,
            NativePoolServiceScene.Handle serviceSceneHandle
        ) {
            _scene = scene;
            _prototype = prototype;
            _parallelism = requestedParallelism;
            _freeTransforms = freeTransforms;
            _freeEntities = freeEntities;
            _jobTransforms = jobTransforms;
            _jobEntities = jobEntities;
            _instantiateParameters = instantiateParameters;
            _serviceSceneHandle = serviceSceneHandle;
            _freeInstances = new(capacity: _freeEntities.Capacity);
        }

        public async UniTask WarmupAsync(TimeSpan timeBudget, CancellationToken cancellation = default)
        {
            if (timeBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeBudget));

            var step = timeBudget / _parallelism;
            var formerBudget = AsyncInstantiateOperation.GetIntegrationTimeMS();
            var desired = _freeTransforms.Capacity;
            var elementsPerRoot = desired / _parallelism;
            try
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS((float) step.TotalMilliseconds);
                var operations = new UniTask<(TPooled[] Instances, int Offset)>[_parallelism];

                for (var index = 0; index != _parallelism; ++index)
                {
                    var parameter = _instantiateParameters[index];
                    var offset = index;
                    var instantiating = UnityEngine.Object.InstantiateAsync(_prototype, elementsPerRoot, parameter, cancellation);
                    operations[index] = instantiating.ToUniTask(timing: PlayerLoopTiming.Initialization, cancellationToken: cancellation)
                                                     .ContinueWith(result => (result, offset));
                }

                _freeEntities.ResizeUninitialized(desired);
                _freeInstances.ResizeUninitialized(desired);
                _freeTransforms.ResizeUninitialized(desired);
                await foreach (var operation in UniTask.WhenEach(operations))
                {
                    var (instances, offset) = operation.Result;
                    for (var index = 0; index != instances.Length; ++index)
                    {
                        var instance = instances[index];
                        var place = index * _parallelism + offset;
                        _freeEntities[place] = instance.gameObject.GetEntityId();
                        _freeInstances[place] = instance;
                        _freeTransforms[place] = instance.transformHandle;
                    }
                }
            }
            finally
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS(formerBudget);
            }
        }

        public JobHandle Get
        (
            int amount,
            in NativeArray<Vector3>.ReadOnly positions,
            in NativeArray<Quaternion>.ReadOnly rotations,
            in NativeArray<Vector3>.ReadOnly scales,
            List<TPooled> instances,
            JobHandle dependency = default
        ) {
            CheckIfDisposed();
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            _jobEntities.RemoveRangeSwapBack(0, _jobEntities.Count);

            _jobTransforms.SetTransformHandles(_freeTransforms.AsArray().GetSubArray(start: 0, length: amount));
            _jobEntities.AddRangeNoResize(_freeEntities.AsReadOnlySpan()[..amount]);

            var initializationJob = new InitializeTransformsJob
            {
                Positions = positions,
                Rotations = rotations,
                Scales = scales
            }.Schedule(_jobTransforms, dependency);
            GameObject.SetGameObjectsActive(_jobEntities.AsReadOnlySpan()[..amount], active: true);

            instances.AddRange(_freeInstances[..amount]);

            return initializationJob;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            UnityEngine.Object.Destroy(_prototype);

            _freeTransforms.Dispose();
            _jobTransforms.Dispose();
            _freeEntities.Dispose();
            _jobEntities.Dispose();

            if (_scene.isLoaded)
            {
                var operation = SceneManager.UnloadSceneAsync(_scene);
                if (operation is { isDone: false })
                {
                    Awaitable.FromAsyncOperation(operation)
                        .LogExceptionsAndForget();
                }
            }
            _serviceSceneHandle.Dispose();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("DEBUG")]
        private void CheckIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativePool<TPooled, TPooledOperationRunner>));
        }

        [BurstCompile]
        private readonly struct InitializeTransformsJob : IJobParallelForTransform
        {
            [field: ReadOnly] public NativeArray<Vector3>.ReadOnly Positions { private get; init; }
            [field: ReadOnly] public NativeArray<Quaternion>.ReadOnly Rotations { private get; init; }
            [field: ReadOnly] public NativeArray<Vector3>.ReadOnly Scales { private get; init; }

            public void Execute(int index, TransformAccess transform)
            {
                transform.SetLocalPositionAndRotation(Positions[index], Rotations[index]);
                transform.localScale = Scales[index];
            }
        }
    }
}
