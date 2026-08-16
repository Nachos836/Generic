#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace Pooling.Bulk
{
    [PublicAPI]
    public interface IPooledOperations
    {
        UniTask Get(Span<GameObject> instances, CancellationToken cancellation = default);
        UniTask Release(Span<GameObject> instances, CancellationToken cancellation = default);
    }
}
