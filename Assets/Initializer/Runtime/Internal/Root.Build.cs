#if !UNITY_EDITOR

#nullable enable

using System;
using UnityEngine;

namespace Initializer
{
    partial class Root
    {
        private void Awake()
        {
            if (_instance == this) throw new InvalidOperationException("Root should be set only once!");

            _instance = Enable(this);

            Debug.Log("[ROOT] Build Awake is called!");
        }

        private void OnDestroy()
        {
            if (_instance != this) throw new InvalidOperationException("Root is not set!");

            _instance = Disable(this);

            Debug.Log("[ROOT] Build OnDestroy is called!");
        }
    }
}

#endif
