using System.Collections.Generic;
using System.Linq;
using InspectorAttributes;
using SerializableValueObjects;
using JetBrains.Annotations;
using UnityEngine;

using static SerializableValueObjects.SerializableDictionary;
using static SerializableValueObjects.SerializableTimeSpan;

namespace Generic.Samples
{
    internal sealed class BehaviourWithReadOnlyPropertyAttribute : MonoBehaviour
    {
        [Header("Default")]
        [SerializeField]
        [ReadOnly] private int _serializeFieldInt = 42;
        [ReadOnly] public int _publicFieldInt = 24;

        [Header("Custom")]
        [SerializeField]
        [ReadOnly] private SerializableTimeSpan _serializeFieldTimeSpan = FromMinutes(5);
        [ReadOnly] public SerializableTimeSpan _publicTimeSpan = FromDays(6);
        [SerializeField]
        [ReadOnly] private SerializableDictionary<int, string> _serializeFieldDictionary = Create(Enumerable.Empty<KeyValuePair<int, string>>().Append(new KeyValuePair<int, string>(1, "1")));
        [ReadOnly] public SerializableDictionary<int, string> _publicDictionary = Create(Enumerable.Empty<KeyValuePair<int, string>>().Append(new KeyValuePair<int, string>(2, "2")));
        [SerializeField]
        [ReadOnly] private SerializableDictionary<string, SerializableTimeSpan> _serializeFieldRichDictionary = Create(Enumerable.Empty<KeyValuePair<string, SerializableTimeSpan>>().Append(new KeyValuePair<string, SerializableTimeSpan>("Blah", FromTicks(69))));

        [field: Header("Properties")]
        [field: UsedImplicitly]
        [field: SerializeField, ReadOnly] public int SerializePropertyInt { get; private set; } = 42;

        private void OnEnable()
        {
            _ = _serializeFieldInt;
            _ = _publicFieldInt;
            _ = _serializeFieldTimeSpan;
            _ = _publicTimeSpan;
            _ = _serializeFieldDictionary;
            _ = _publicDictionary;
            _ = _serializeFieldRichDictionary;
            _ = _publicDictionary;
            _ = SerializePropertyInt;
        }
    }
}
