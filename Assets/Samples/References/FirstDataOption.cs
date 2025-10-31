using System;
using UnityEngine;

namespace Samples.References
{
    [Serializable]
    internal sealed class FirstDataOption : IData
    {
        [SerializeField] private string _unused = "Unused Data";

        string IData.Value => "First option";
    }
}
