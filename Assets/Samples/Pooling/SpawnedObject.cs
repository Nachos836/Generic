using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Pooling.Bulk;
using UnityEngine;

namespace Samples.Pooling
{
    internal sealed class SpawnedObject : MonoBehaviour
    {

    }

    internal readonly struct SpawnedObjectOperations : IPooledOperations
    {
        public UniTask Get(Span<GameObject> instances, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public UniTask Release(Span<GameObject> instances, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }
    }
}
