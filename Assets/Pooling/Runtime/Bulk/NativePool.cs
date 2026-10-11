#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace Pooling.Bulk
{
    using static Internals.CollectionsHelpers;

    [PublicAPI]
    public static class NativePool
    {
        public static UniTask<NativePool<TPooled, TPooledOperationRunner>> CreateAsync<TPooled, TPooledOperationRunner>
        (
            NativePoolServiceScene nativePoolServiceScene,
            TPooledOperationRunner operationRunner,
            TPooled prototype,
            int requestedCapacity,
            int requestedParallelism = -1,
            int stashLayer = -1,
            CancellationToken cancellation = default
        )
            where TPooled : MonoBehaviour
            where TPooledOperationRunner : struct, IPooledOperations<TPooled>
        {
            return NativePool<TPooled, TPooledOperationRunner>.CreateAsync
            (
                nativePoolServiceScene,
                operationRunner,
                prototype,
                requestedCapacity,
                requestedParallelism,
                stashLayer,
                cancellation
            );
        }
    }

    /// <summary>
    /// Orchestrates N independent buckets for real <see cref="UnityEngine.Jobs.IJobParallelForTransform"/> parallelism. <br/>
    /// Capacity is rounded up to a power of two; parallelism is rounded down to a power of two — the resulting per-pool capacity divides evenly.
    /// </summary>
    public sealed class NativePool<TPooled, TPooledOperationRunner> : IDisposable
        where TPooled : MonoBehaviour
        where TPooledOperationRunner : struct, IPooledOperations<TPooled>
    {
        private readonly Scene _scene;
        private readonly TPooled _prototype;
        private readonly int _parallelism;
        private readonly InstantiateParameters[] _instantiateParameter;
        /// <summary> Sibling-index paths from instance root to each top-most <see cref="SensitiveLogicRoot"/>. Empty when none. </summary>
        /// <summary> Sibling-index paths from instance root to each top-most <see cref="SensitiveLogicRoot"/>. Empty when none. </summary>
        private readonly int[][] _sensitivePaths;
        /// <summary> Sibling-index paths to collider-owning GameObjects warmed up on the stash layer. Empty when stashing is disabled. </summary>
        private readonly int[][] _stashPaths;
        /// <summary> Original layers for <see cref="_stashPaths"/>, restored after warmup. </summary>
        private readonly int[] _stashLayers;

        private NativeList<TransformHandle> _freeTransforms;
        private NativeList<EntityId> _freeEntities;
        private TransformAccessArray _jobTransforms;
        private NativeList<EntityId> _jobEntities;
        private NativeList<EntityId> _sensitiveRoots;

        private NativePoolServiceScene.Handle _serviceSceneHandle;
        private TPooledOperationRunner _operationRunner;

        private bool _disposed;

        internal static async UniTask<NativePool<TPooled, TPooledOperationRunner>> CreateAsync
        (
            NativePoolServiceScene nativePoolServiceScene,
            TPooledOperationRunner operationRunner,
            TPooled prototype,
            int requestedCapacity,
            int requestedParallelism,
            int stashLayer,
            CancellationToken cancellation
        ) {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException();
            if (prototype ==null) throw new ArgumentNullException(nameof(prototype));
            if (requestedCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedCapacity));
            if (stashLayer >= 0) ValidateStashLayer(stashLayer);

            var rawParallelism = requestedParallelism > 0 ? requestedParallelism : JobsUtility.JobWorkerCount;
            var parallelism = math.ceilpow2(math.min(rawParallelism, requestedCapacity));
            var totalCapacity = math.ceilpow2(requestedCapacity);
            var capacityPerRoot = math.max(1, totalCapacity / parallelism);

            var serviceSceneHandle = nativePoolServiceScene.AcquireSceneHandle();
            var instantiatePrototype = await Object.InstantiateAsync
            (
                prototype,
                position: new Vector3(x: 0, y: -999999, z: 0),
                rotation: Quaternion.identity,
                new InstantiateParameters
                {
                    originalImmutable = true,
                    scene = serviceSceneHandle.Scene
                },
                cancellation
            );
            var immutablePrototype = instantiatePrototype.Single();
            var prototypeGameObject = immutablePrototype.gameObject;
            prototypeGameObject.name = prototype.name;
            prototypeGameObject.SetActive(false);

            var sensitivePaths = CollectSensitivePaths(prototypeGameObject.transform);
            foreach (var path in sensitivePaths)
            {
                Resolve(prototypeGameObject.transform, path).gameObject.SetActive(false);
            }

            var (stashPaths, stashLayers) = StashColliders(prototypeGameObject.transform, stashLayer);

            var rootSample = new GameObject { isStatic = true };
            var scene = SceneManager.CreateScene($"[Pool] {prototypeGameObject.name}");
            var bucketRoots = await Object.InstantiateAsync(rootSample, parallelism, new InstantiateParameters
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
            var sensitiveRoots = new NativeList<EntityId>(freeTransforms.Capacity * sensitivePaths.Length, Allocator.Persistent);

            Object.Destroy(rootSample);

            return new NativePool<TPooled, TPooledOperationRunner>
            (
                operationRunner,
                scene,
                immutablePrototype,
                parallelism,
                freeTransforms,
                freeEntities,
                jobTransforms,
                jobEntities,
                sensitiveRoots,
                sensitivePaths,
                stashPaths,
                stashLayers,
                instantiateParameters,
                serviceSceneHandle
            );
        }

        private NativePool
        (
            TPooledOperationRunner operationRunner,
            Scene scene,
            TPooled prototype,
            int requestedParallelism,
            NativeList<TransformHandle> freeTransforms,
            NativeList<EntityId> freeEntities,
            TransformAccessArray jobTransforms,
            NativeList<EntityId> jobEntities,
            NativeList<EntityId> sensitiveRoots,
            int[][] sensitivePaths,
            int[][] stashPaths,
            int[] stashLayers,
            InstantiateParameters[] instantiateParameter,
            NativePoolServiceScene.Handle serviceSceneHandle
        ) {
            _operationRunner = operationRunner;
            _scene = scene;
            _prototype = prototype;
            _parallelism = requestedParallelism;
            _freeTransforms = freeTransforms;
            _freeEntities = freeEntities;
            _jobTransforms = jobTransforms;
            _jobEntities = jobEntities;
            _sensitiveRoots = sensitiveRoots;
            _sensitivePaths = sensitivePaths;
            _stashPaths = stashPaths;
            _stashLayers = stashLayers;
            _instantiateParameter = instantiateParameter;
            _serviceSceneHandle = serviceSceneHandle;
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
                    var parameter = _instantiateParameter[index];
                    var offset = index;
                    var instantiating = Object.InstantiateAsync(_prototype, elementsPerRoot, parameter, cancellation);
                    operations[index] = instantiating.ToUniTask(timing: PlayerLoopTiming.Initialization, cancellationToken: cancellation)
                                                     .ContinueWith(result => (result, offset));
                }

                var rootsPerInstance = _sensitivePaths.Length;
                _sensitiveRoots.ResizeUninitialized(desired * rootsPerInstance);
                var stashedPerInstance = _stashPaths.Length;
                var stashed = stashedPerInstance == 0 ? Array.Empty<GameObject>() : new GameObject[desired * stashedPerInstance];
                await foreach (var operation in UniTask.WhenEach(operations))
                {
                    var (instances, offset) = operation.Result;
                    _operationRunner.AdditionalWarmupAction?.Invoke(instances);

                    for (var index = 0; index != instances.Length; ++index)
                    {
                        var instance = instances[index];
                        var place = index * _parallelism + offset;
                        _freeEntities[place] = instance.gameObject.GetEntityId();
                        _freeTransforms[place] = instance.transformHandle;

                        for (var inner = 0; inner != rootsPerInstance; ++inner)
                        {
                            var sensitive = Resolve(instance.transform, _sensitivePaths[inner]);
                            _sensitiveRoots[place * rootsPerInstance + inner] = sensitive.gameObject.GetEntityId();
                        }

                        for (var inner = 0; inner != stashedPerInstance; ++inner)
                        {
                            stashed[place * stashedPerInstance + inner] = Resolve(instance.transform, _stashPaths[inner]).gameObject;
                        }
                    }
                }

                await PreActivateAsync(chunk: elementsPerRoot, stashed, cancellation);

                // Base roots are guaranteed inactive here: flipping activeSelf on children is callback-free.
                // From now on the very first Get activates the whole hierarchy in one wave.
                GameObject.SetGameObjectsActive(_sensitiveRoots.AsReadOnlySpan(), active: true);
            }
            finally
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS(formerBudget);
            }
        }

        public void Get
        (
            int amount,
            in NativeArray<Vector3>.ReadOnly positions,
            in NativeArray<Quaternion>.ReadOnly rotations,
            in NativeArray<Vector3>.ReadOnly scales,
            List<TPooled>? instances = null
        ) {
            CheckIfDisposed();
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > _freeEntities.Length) throw new ArgumentOutOfRangeException(nameof(amount));

            InitializeEntities(amount, in positions, in rotations, in scales);
            PopulateInstancesIfNeeded(instances, ..amount);

            ActivateEntities(offset: 0, count: amount);
        }

        public async UniTask GetAsync
        (
            int amount,
            NativeArray<Vector3>.ReadOnly positions,
            NativeArray<Quaternion>.ReadOnly rotations,
            NativeArray<Vector3>.ReadOnly scales,
            TimeSpan frameBudget,
            List<TPooled>? instances = null,
            CancellationToken cancellation = default
        ) {
            CheckIfDisposed();
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > _freeEntities.Length) throw new ArgumentOutOfRangeException(nameof(amount));
            if (frameBudget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(frameBudget));

            InitializeEntities(amount, positions, rotations, scales);
            PopulateInstancesIfNeeded(instances, ..amount);

            var budgetSeconds = frameBudget.TotalSeconds;
            var chunk = math.max(1, amount / _parallelism);
            var offset = 0;
            while (offset < amount && cancellation.IsCancellationRequested is false)
            {
                var count = math.min(chunk, amount - offset);
                var start = Stopwatch.GetTimestamp();

                ActivateEntities(offset, count);

                offset += count;

                var elapsedSeconds = (Stopwatch.GetTimestamp() - start) / (double) Stopwatch.Frequency;
                if (offset >= amount) break;

                var perObject = elapsedSeconds / count;
                chunk = math.clamp
                (
                    valueToClamp: (int) (budgetSeconds / math.max(perObject, 1e-7)),
                    lowerBound: 1,
                    upperBound: amount - offset
                );

                if (await UniTask.Yield(PlayerLoopTiming.Initialization, cancellation)
                    .SuppressCancellationThrow()) return;

                CheckIfDisposed();
            }
        }

        /// <summary>
        /// Pays first-activation costs (Awake, renderer registration, PhysX shape creation, shader warmup)
        /// during warmup instead of the first <see cref="Get"/>.
        /// </summary>
        private async UniTask PreActivateAsync(int chunk, GameObject[] stashed, CancellationToken cancellation)
        {
            var total = _freeEntities.Length;
            var restored = 0;
            try
            {
                for (var offset = 0; offset < total && cancellation.IsCancellationRequested is false; offset += chunk)
                {
                    var count = math.min(chunk, total - offset);

                    GameObject.SetGameObjectsActive(_freeEntities.AsReadOnlySpan().Slice(offset, count), active: true);
                    try
                    {
                        if (await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellation)
                                .SuppressCancellationThrow()) return;
                    }
                    finally
                    {
                        GameObject.SetGameObjectsActive(_freeEntities.AsReadOnlySpan().Slice(offset, count),
                            active: false);
                        // Inactive objects have no live PhysX shapes: layer change is filter-refresh free.
                        RestoreLayers(stashed, offset, count);
                        restored = offset + count;
                    }

                    if (await UniTask.Yield(PlayerLoopTiming.Initialization, cancellation)
                            .SuppressCancellationThrow()) return;
                }
            }
            finally
            {
                // Never leave not-yet-warmed instances on the stash layer: they would be collision-less forever.
                RestoreLayers(stashed, restored, total - restored);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RestoreLayers(GameObject[] stashed, int offset, int count)
        {
            var perInstance = _stashLayers.Length;
            if (perInstance == 0 || count <= 0) return;

            for (var place = offset; place != offset + count; ++place)
            {
                var start = place * perInstance;
                for (var k = 0; k != perInstance; ++k)
                {
                    stashed[start + k].layer = _stashLayers[k];
                }
            }
        }

        private static void ValidateStashLayer(int layer)
        {
            if ((uint) layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));

            // Self-collision included: all instances are warmed up overlapping each other.
            for (var other = 0; other != 32; ++other)
            {
                if (Physics.GetIgnoreLayerCollision(layer, other)) continue;

                throw new ArgumentException($"Stash layer '{LayerMask.LayerToName(layer)}' ({layer}) must ignore collisions with every layer, " +
                                            $"but collides with '{LayerMask.LayerToName(other)}' ({other}).", nameof(layer));
            }
        }

        /// <summary>
        /// Collects colliders explicitly declared by <see cref="SensitiveLogicRoot.StashedColliders"/>,
        /// remembers their original layers and moves them to <paramref name="stashLayer"/>. Instances inherit it on cloning.
        /// </summary>
        private static (int[][] Paths, int[] Layers) StashColliders(Transform root, int stashLayer)
        {
            var markers = root.GetComponentsInChildren<SensitiveLogicRoot>(includeInactive: true);
            if (markers.Length == 0) return (Array.Empty<int[]>(), Array.Empty<int>());

            var owners = new List<GameObject>();
            var visited = new HashSet<GameObject>();
            foreach (var marker in markers)
            {
                foreach (var collider in marker.StashedColliders)
                {
                    if (collider == null) continue;

                    var owner = collider.gameObject;
                    if (owner.transform.IsChildOf(root) is false)
                        throw new InvalidOperationException($"Stashed collider '{owner.name}' of '{marker.name}' is outside of the pooled hierarchy '{root.name}'");

                    if (IsUnderSensitiveRoot(root, owner.transform))
                    {
                        Debug.LogWarning($"[Pool] Stashed collider '{owner.name}' is inside a sensitive subtree and is skipped.", marker);
                        continue;
                    }

                    if (visited.Add(owner)) owners.Add(owner);
                }
            }

            if (owners.Count == 0) return (Array.Empty<int[]>(), Array.Empty<int>());
            if (stashLayer < 0)
                throw new ArgumentException($"'{root.name}' declares {owners.Count} stashed collider owner(s), but no stash layer was provided.", nameof(stashLayer));

            var paths = new int[owners.Count][];
            var layers = new int[owners.Count];
            for (var i = 0; i != owners.Count; ++i)
            {
                var owner = owners[i];
                paths[i] = PathOf(root, owner.transform);
                layers[i] = owner.layer;
                owner.layer = stashLayer;
            }

            return (paths, layers);
        }

        private static bool IsUnderSensitiveRoot(Transform root, Transform target)
        {
            for (var current = target; current != root; current = current.parent)
            {
                if (current.TryGetComponent<SensitiveLogicRoot>(out _)) return true;
            }
            return false;
        }

        private static int[] PathOf(Transform root, Transform target)
        {
            var depth = 0;
            for (var current = target; current != root; current = current.parent) ++depth;

            var path = new int[depth];
            for (var current = target; current != root; current = current.parent)
            {
                path[--depth] = current.GetSiblingIndex();
            }
            return path;
        }

        private static int[][] CollectSensitivePaths(Transform root)
        {
            var markers = root.GetComponentsInChildren<SensitiveLogicRoot>(includeInactive: true);
            if (markers.Length == 0) return Array.Empty<int[]>();

            using var pathsHandler = ListPool<int[]>.Get(out var paths);
            paths.Capacity = markers.Length;
            using var bufferHandler = ListPool<int>.Get(out var buffer);
            buffer.Capacity = 8;

            foreach (var marker in markers)
            {
                var target = marker.transform;
                if (target == root) throw new InvalidOperationException($"{nameof(SensitiveLogicRoot)} must not be placed on the pooled root itself: '{root.name}'");

                buffer.Clear();
                var isTopMost = true;
                for (var current = target; current != root; current = current.parent)
                {
                    if (current != target && current.TryGetComponent<SensitiveLogicRoot>(out _))
                    {
                        isTopMost = false;
                        break;
                    }
                    buffer.Add(current.GetSiblingIndex());
                }

                if (isTopMost is false) continue;

                buffer.Reverse();
                paths.Add(buffer.ToArray());
            }

            return paths.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Transform Resolve(Transform root, int[] path)
        {
            var current = root;
            foreach (var index in path)
            {
                current = current.GetChild(index);
            }
            return current;
        }

        private void PopulateInstancesIfNeeded(List<TPooled>? instances, Range range)
        {
            if (instances is null) return;

            var rawObjectsList = instances.GetObjectList();
            Resources.EntityIdsToObjectList(_freeEntities.SliceAsNativeArray(range), rawObjectsList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeEntities
        (
            int amount,
            in NativeArray<Vector3>.ReadOnly positions,
            in NativeArray<Quaternion>.ReadOnly rotations,
            in NativeArray<Vector3>.ReadOnly scales
        ) {
            _jobEntities.Clear();
            _jobTransforms.SetTransformHandles(_freeTransforms.AsArray().GetSubArray(start: 0, length: amount));
            _jobEntities.AddRangeNoResize(_freeEntities.AsReadOnlySpan()[..amount]);

            var initialization = new InitializeTransformsJob
            {
                Positions = positions,
                Rotations = rotations,
                Scales = scales
            };
            initialization.ScheduleByRef(_jobTransforms)
                .Complete();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ActivateEntities(int offset, int count)
        {
            if (_operationRunner.CustomGetAction is { } action)
            {
                action(_jobEntities.AsReadOnly(), offset..(offset + count));
            }
            else
            {
                var entities = _jobEntities.AsReadOnlySpan()
                    .Slice(offset, count);
                GameObject.SetGameObjectsActive(entities, active: true);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            Object.Destroy(_prototype);

            _freeTransforms.Dispose();
            _jobTransforms.Dispose();
            _freeEntities.Dispose();
            _jobEntities.Dispose();
            _sensitiveRoots.Dispose();

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
