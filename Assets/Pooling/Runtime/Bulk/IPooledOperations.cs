#nullable enable

using System;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;

namespace Pooling.Bulk
{
    [PublicAPI]
    public interface IPooledOperations<in TPooled> where TPooled : MonoBehaviour
    {
        Action<TPooled[]>? AdditionalWarmupAction { get; }
        Action<NativeArray<EntityId>.ReadOnly, Range>? CustomGetAction { get; }
    }
}
