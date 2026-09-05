#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Pooling.Bulk;
using SerializableValueObjects;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Generic.Samples.Pooling
{
    internal sealed class PoolRoot : MonoBehaviour
    {
        [SerializeField] private SpawnedObject _prototype = default!;
        [SerializeField] private int _maxCapacity = 32;
        [SerializeField] private int _spawnAmount = 16;
        [SerializeField] private SerializableTimeSpan _warmupTimeBudget = TimeSpan.FromSeconds(3);

        private CancellationTokenSource? _enabled;
        private NativePoolServiceScene? _poolServiceScene;
        private NativePool<SpawnedObject, SpawnedObjectOperations>? _pool;
        private NativeArray<Vector3>? _positions;
        private NativeArray<Quaternion>? _rotations;
        private NativeArray<Vector3>? _scales;
        private List<SpawnedObject>? _obtainedInstances;
        private SpawnedObjectOperations _objectsOperations;
        private PooledObject<List<SpawnedObject>>? _instancesHandler;

        // ReSharper disable once Unity.IncorrectMethodSignature
        [UsedImplicitly]
        private async UniTaskVoid OnEnable()
        {
            _enabled = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, CancellationToken.None);
            _poolServiceScene = new NativePoolServiceScene();
            _objectsOperations = new SpawnedObjectOperations();
            _pool = await NativePool.CreateAsync
            (
                _poolServiceScene,
                _objectsOperations,
                _prototype,
                _maxCapacity,
                cancellation: _enabled.Token
            );
             _instancesHandler = ListPool<SpawnedObject>.Get(out _obtainedInstances);
        }

        [ContextMenu(nameof(WarmUp))]
        private void WarmUp()
        {
            _pool?.WarmupAsync(_warmupTimeBudget, destroyCancellationToken)
                .Forget();
        }

        [ContextMenu(nameof(Spawn))]
        private void Spawn()
        {
            _positions?.Dispose();
            _positions = PopulatePositions(_spawnAmount);
            _rotations?.Dispose();
            _rotations = PopulateRotations(_spawnAmount);
            _scales?.Dispose();
            _scales = PopulateScales(_spawnAmount);

            _pool?.Get(_spawnAmount,
                       _positions.Value.AsReadOnly(),
                       _rotations.Value.AsReadOnly(),
                       _scales.Value.AsReadOnly(),
                       _obtainedInstances!)
                   .Complete();
        }

        [ContextMenu(nameof(Release))]
        private void Release()
        {
            _positions?.Dispose();
            _positions = null;
            _rotations?.Dispose();
            _rotations = null;
            _scales?.Dispose();
            _scales = null;
        }

        private void OnDisable()
        {
            _positions?.Dispose();
            _positions = null;
            _rotations?.Dispose();
            _rotations = null;
            _scales?.Dispose();
            _scales = null;
            _poolServiceScene?.Dispose();
            _poolServiceScene = null;
            _enabled?.Cancel();
            _enabled?.Dispose();
            _enabled = null;
            if (_instancesHandler != null)
            {
                using (_instancesHandler) { /* to avoid boxing */ }
                _instancesHandler = null;
            }
            _pool?.Dispose();
            _pool = null!;
        }

        private static NativeArray<Vector3> PopulatePositions(int amount)
        {
            var collection = new NativeArray<Vector3>(amount, Allocator.Domain, NativeArrayOptions.UninitializedMemory);
            collection.AsSpan().Fill(Vector3.zero);

            return collection;
        }

        private static NativeArray<Quaternion> PopulateRotations(int amount)
        {
            var collection = new NativeArray<Quaternion>(amount, Allocator.Domain, NativeArrayOptions.UninitializedMemory);
            collection.AsSpan().Fill(Quaternion.identity);

            return collection;
        }

        private static NativeArray<Vector3> PopulateScales(int amount)
        {
            var collection = new NativeArray<Vector3>(amount, Allocator.Domain, NativeArrayOptions.UninitializedMemory);
            collection.AsSpan().Fill(Vector3.one);

            return collection;
        }
    }
}
