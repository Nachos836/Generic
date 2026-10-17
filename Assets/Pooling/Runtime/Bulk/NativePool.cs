#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using dotNetCompat.Extensions;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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
        private NativeList<FixedList64Bytes<int>> _sensitivePaths;
        /// <summary> Stashed GameObjects: original layer plus sibling-index path. Empty when nothing is stashed. </summary>
        private NativeList<StashEntry> _stashEntries;

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
            CancellationToken cancellation
        ) {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException();
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            if (requestedCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(requestedCapacity));

            var rawParallelism = requestedParallelism > 0 ? requestedParallelism : JobsUtility.JobWorkerCount;
            var parallelism = math.ceilpow2(math.min(rawParallelism, requestedCapacity));
            var totalCapacity = math.ceilpow2(requestedCapacity);
            var capacityPerRoot = math.max(1, totalCapacity / parallelism);

            var serviceSceneHandle = nativePoolServiceScene.AcquireSceneHandle();
            GameObject? prototypeGameObject = null;
            Scene scene = default;
            var sensitivePaths = new NativeList<FixedList64Bytes<int>>(Allocator.Persistent);
            var stashEntries = new NativeList<StashEntry>(Allocator.Persistent);
            try
            {
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
                prototypeGameObject = immutablePrototype.gameObject;
                prototypeGameObject.name = prototype.name;
                prototypeGameObject.SetActive(false);

                var prototypeRoot = prototypeGameObject.transform;
                using (ListPool<SensitiveLogicRoot>.Get(out var markers))
                {
                    prototypeRoot.GetComponentsInChildren(includeInactive: true, markers);

                    CollectSensitivePaths(prototypeRoot, markers, sensitivePaths);
                    StashColliders(prototypeRoot, markers, stashEntries); // flips layers: clones inherit them

                    foreach (var path in sensitivePaths)
                    {
                        Resolve(prototypeRoot, path).gameObject.SetActive(false);
                    }

                    // Authoring data is fully consumed: clones are completely born without the marker.
                    // Immediate is safe: it is a scene copy, not an asset, and WarmupAsync may run this very frame.
                    foreach (var marker in markers)
                    {
                        Object.DestroyImmediate(marker);
                    }
                }

                scene = SceneManager.CreateScene($"[Pool] {prototypeGameObject.name}");
                var rootSample = new GameObject { isStatic = true };
                GameObject[] bucketRoots;
                try
                {
                    bucketRoots = await Object.InstantiateAsync(rootSample, parallelism, new InstantiateParameters
                    {
                        parent = null,
                        scene = scene,
                        worldSpace = false,
                        originalImmutable = true
                    }, cancellation);
                }
                finally
                {
                    Object.Destroy(rootSample);
                }

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

                // No awaits below: nothing can be interrupted after these allocations.
                var freeTransforms = new NativeList<TransformHandle>(requestedCapacity, Allocator.Persistent);
                var freeEntities = new NativeList<EntityId>(freeTransforms.Capacity, Allocator.Persistent);
                var jobTransforms = new TransformAccessArray(freeTransforms.Capacity, desiredJobCount: parallelism);
                var jobEntities = new NativeList<EntityId>(freeTransforms.Capacity, Allocator.Persistent);
                var sensitiveRoots = new NativeList<EntityId>(freeTransforms.Capacity * sensitivePaths.Length, Allocator.Persistent);

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
                    stashEntries,
                    instantiateParameters,
                    serviceSceneHandle
                );
            }
            catch
            {
                sensitivePaths.Dispose();
                stashEntries.Dispose();
                if (prototypeGameObject != null) Object.Destroy(prototypeGameObject);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                serviceSceneHandle.Dispose();
                throw;
            }
        }

        private NativePool
        (
            TPooledOperationRunner operationRunner,
            Scene scene,
            TPooled prototype,
            int parallelism,
            NativeList<TransformHandle> freeTransforms,
            NativeList<EntityId> freeEntities,
            TransformAccessArray jobTransforms,
            NativeList<EntityId> jobEntities,
            NativeList<EntityId> sensitiveRoots,
            NativeList<FixedList64Bytes<int>> sensitivePaths,
            NativeList<StashEntry> stashEntries,
            InstantiateParameters[] instantiateParameter,
            NativePoolServiceScene.Handle serviceSceneHandle
        ) {
            _operationRunner = operationRunner;
            _scene = scene;
            _prototype = prototype;
            _parallelism = parallelism;
            _freeTransforms = freeTransforms;
            _freeEntities = freeEntities;
            _jobTransforms = jobTransforms;
            _jobEntities = jobEntities;
            _sensitiveRoots = sensitiveRoots;
            _sensitivePaths = sensitivePaths;
            _stashEntries = stashEntries;
            _instantiateParameter = instantiateParameter;
            _serviceSceneHandle = serviceSceneHandle;
        }

        public async UniTask WarmupAsync(TimeSpan timeBudget, CancellationToken cancellation = default)
        {
            CheckIfDisposed();
            if (timeBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeBudget));

            var step = timeBudget / _parallelism;
            var formerBudget = AsyncInstantiateOperation.GetIntegrationTimeMS();
            var desired = _freeTransforms.Capacity;
            var elementsPerRoot = desired / _parallelism;
            var restorePlan = CreateRestorePlan(desired);
            var spawnDelay = TimeSpan.Zero;
            try
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS((float) step.TotalMilliseconds);
                using var operations = ArrayPool<UniTask<(TPooled[] Items, int Offset)>>.Shared.RentSegment(_parallelism);

                for (var index = 0; index != _parallelism; ++index)
                {
                    var parameter = _instantiateParameter[index];
                    var offset = index;

                    var operation = UniTask.Delay(spawnDelay, delayType: DelayType.Realtime, PlayerLoopTiming.Initialization, cancellation, cancelImmediately: true)
                        .ContinueWith(() => Object.InstantiateAsync(_prototype, elementsPerRoot, parameter, cancellation)
                                                  .ToUniTask(timing: PlayerLoopTiming.Initialization, cancellationToken: cancellation, cancelImmediately: true)
                                                  .ContinueWith(result => (result, offset)));

                    operations.Values[index] = operation;

                    spawnDelay += step;
                }

                _sensitiveRoots.ResizeUninitialized(desired * _sensitivePaths.Length);
                _freeEntities.ResizeUninitialized(desired);
                _freeTransforms.ResizeUninitialized(desired);

                var instantiateStream = operations.CanProvideArray(out var tasks)
                    ? UniTask.WhenEach(tasks)
                    : UniTask.WhenEach(operations.Values);
                await foreach (var operation in instantiateStream)
                {
                    var (instances, offset) = operation.Result;
                    _operationRunner.AdditionalWarmupAction?.Invoke(instances);

                    for (var index = 0; index != instances.Length; ++index)
                    {
                        RegisterInstance(instances[index], place: index * _parallelism + offset, restorePlan);
                    }
                }

                await PreActivateAsync(chunk: elementsPerRoot, cancellation);

                // Base roots are guaranteed inactive here: flipping activeSelf on children is callback-free.
                // From now on the very first Get activates the whole hierarchy in one wave.
                GameObject.SetGameObjectsActive(_sensitiveRoots.AsReadOnlySpan(), active: true);
            }
            finally
            {
                // Everything is inactive here: layer change is filter-refresh-free. Only collected instances are in the plan.
                RestoreLayers(restorePlan);
                DisposePlan(restorePlan);
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

        /// <summary> Registers one freshly instantiated clone: pool slot, sensitive roots, and stashed owners. </summary>
        private void RegisterInstance(TPooled instance, int place, NativeHashMap<int, NativeList<EntityId>> restorePlan)
        {
            var root = instance.transform;
            _freeEntities[place] = instance.gameObject.GetEntityId();
            _freeTransforms[place] = instance.transformHandle;

            var rootsPerInstance = _sensitivePaths.Length;
            for (var inner = 0; inner != rootsPerInstance; ++inner)
            {
                var sensitive = Resolve(root, _sensitivePaths.ElementAt(inner));
                _sensitiveRoots[place * rootsPerInstance + inner] = sensitive.gameObject.GetEntityId();
            }

            for (var inner = 0; inner != _stashEntries.Length; ++inner)
            {
                ref readonly var entry = ref _stashEntries.ElementAt(inner);
                var owners = restorePlan[entry.OriginalLayer]; // copy shares the buffer
                owners.AddNoResize(Resolve(root, entry.Path).gameObject.GetEntityId());
            }
        }

        /// <summary> Pre-sized "original layer → owners" map: capacity is exact, so registration never reallocates. </summary>
        private NativeHashMap<int, NativeList<EntityId>> CreateRestorePlan(int desired)
        {
            Span<int> perLayer = stackalloc int[32];
            perLayer.Clear();

            var groups = 0;
            foreach (var entry in _stashEntries)
            {
                if (perLayer[entry.OriginalLayer]++ == 0) ++groups;
            }

            var plan = new NativeHashMap<int, NativeList<EntityId>>(math.max(1, groups), Allocator.Persistent);
            for (var layer = 0; layer != perLayer.Length; ++layer)
            {
                if (perLayer[layer] == 0) continue;

                plan.Add(layer, new NativeList<EntityId>(desired * perLayer[layer], Allocator.Persistent));
            }
            return plan;
        }

        private async UniTask PreActivateAsync(int chunk, CancellationToken cancellation)
        {
            var total = _freeEntities.Length;
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
                    GameObject.SetGameObjectsActive(_freeEntities.AsReadOnlySpan().Slice(offset, count), active: false);
                }

                if (await UniTask.Yield(PlayerLoopTiming.Initialization, cancellation)
                    .SuppressCancellationThrow()) return;
            }
        }

        private static void RestoreLayers(NativeHashMap<int, NativeList<EntityId>> plan)
        {
            if (plan.IsEmpty) return;

            var largest = 0;
            foreach (var pair in plan)
            {
                largest = math.max(largest, pair.Value.Length);
            }

            using var _ = ListPool<Object>.Get(out var writeBuffer);
            var readBuffer = Unsafe.As<List<GameObject>>(writeBuffer);
            writeBuffer.Capacity = math.max(writeBuffer.Capacity, largest);

            foreach (var pair in plan)
            {
                writeBuffer.Clear();
                Resources.EntityIdsToObjectList(pair.Value.AsArray(), writeBuffer);

                // TODO: swap for a batch API once Unity exposes one — the plan is already "layer → entities".
                var originalLayer = pair.Key;
                foreach (ref readonly var owner in readBuffer.AsReadOnlySpan())
                {
                    owner.layer = originalLayer;
                }
            }
        }

        private static void DisposePlan(NativeHashMap<int, NativeList<EntityId>> plan)
        {
            foreach (var pair in plan)
            {
                pair.Value.Dispose();
            }
            plan.Dispose();
        }

        /// <summary> Collects paths to top-most markers only: nested ones are switched off together with their ancestor. </summary>
        private static void CollectSensitivePaths(Transform root, List<SensitiveLogicRoot> markers, NativeList<FixedList64Bytes<int>> paths)
        {
            foreach (ref readonly var marker in markers.AsReadOnlySpan())
            {
                var target = marker.transform;
                if (target == root)
                    throw new InvalidOperationException($"{nameof(SensitiveLogicRoot)} must not be placed on the pooled root itself: '{root.name}'");

                if (IsUnderSensitiveRoot(root, target.parent)) continue;

                paths.Add(PathOf(root, target));
            }
        }

        /// <summary>
        /// Runs once on the immutable prototype copy: moves declared owners to their marker's stash layer
        /// and records where they are and which layer to give back.
        /// </summary>
        private static void StashColliders(Transform root, List<SensitiveLogicRoot> markers, NativeList<StashEntry> entries)
        {
            if (markers.Count == 0) return;

            using var _ = HashSetPool<GameObject>.Get(out var visited);
            foreach (var marker in markers)
            {
                var stash = marker.StashLayer;
                foreach (var collider in marker.StashedColliders)
                {
                    if (collider == null) continue;

                    var owner = collider.gameObject;
                    // First marker wins: a second visit would read the stash layer as "original".
                    if (visited.Add(owner) is false) continue;

                    var target = owner.transform;
                    if (target.IsChildOf(root) is false)
                        throw new InvalidOperationException($"Stashed collider '{owner.name}' of '{marker.name}' is outside of the pooled hierarchy '{root.name}'");

                    if (IsUnderSensitiveRoot(root, target))
                    {
                        Debug.LogWarning($"[Pool] Stashed collider '{owner.name}' is inside a sensitive subtree and is skipped.", marker);
                        continue;
                    }

                    var original = owner.layer;
                    if (original == stash) continue;

                    entries.Add(new StashEntry(original, PathOf(root, target)));
                    owner.layer = stash;
                }
            }
        }

        /// <summary> <paramref name="target"/> itself or any ancestor below <paramref name="root"/> carries a marker. </summary>
        /// <remarks> Precondition: <paramref name="target"/> is <paramref name="root"/> or its descendant. </remarks>
        private static bool IsUnderSensitiveRoot(Transform root, Transform target)
        {
            for (var current = target; current != root; current = current.parent)
            {
                if (current.TryGetComponent<SensitiveLogicRoot>(out _)) return true;
            }
            return false;
        }

        private static FixedList64Bytes<int> PathOf(Transform root, Transform target)
        {
            var depth = 0;
            for (var current = target; current != root; current = current.parent) ++depth;

            var path = new FixedList64Bytes<int>();
            if (depth > path.Capacity)
                throw new InvalidOperationException($"'{target.name}' is {depth} levels deep in '{root.name}', max supported is {path.Capacity}");

            path.Length = depth;
            for (var current = target; current != root; current = current.parent)
            {
                path[--depth] = current.GetSiblingIndex();
            }
            return path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Transform Resolve(Transform root, in FixedList64Bytes<int> path)
        {
            var current = root;
            foreach (ref readonly var child in path.AsReadOnlySpan())
            {
                current = current.GetChild(child);
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
            var candidates = offset..(offset + count);
            if (_operationRunner.CustomGetAction is { } action)
            {
                action(_jobEntities.AsReadOnly(), candidates);
            }
            else
            {
                GameObject.SetGameObjectsActive(_jobEntities.AsReadOnlySpan()[candidates], active: true);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_prototype != null) Object.Destroy(_prototype.gameObject);

            _freeTransforms.Dispose();
            _jobTransforms.Dispose();
            _freeEntities.Dispose();
            _jobEntities.Dispose();
            _sensitiveRoots.Dispose();
            _sensitivePaths.Dispose();
            _stashEntries.Dispose();

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

        private readonly struct StashEntry
        {
            public readonly int OriginalLayer;
            public readonly FixedList64Bytes<int> Path; // sibling-index path from instance root to the owner

            public StashEntry(int originalLayer, FixedList64Bytes<int> path)
            {
                OriginalLayer = originalLayer;
                Path = path;
            }
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
