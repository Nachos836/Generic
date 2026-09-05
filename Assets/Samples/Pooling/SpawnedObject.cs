#nullable enable

using System;
using Pooling.Bulk;
using Unity.Collections;
using UnityEngine;

namespace Generic.Samples.Pooling
{
    internal sealed class SpawnedObject : MonoBehaviour
    {

    }

    internal readonly struct SpawnedObjectOperations : IPooledOperations<SpawnedObject>
    {
        public Action<SpawnedObject[]>? AdditionalWarmupAction => null;
        public Action<NativeArray<EntityId>.ReadOnly, Range>? CustomGetAction => null;
    }
}
