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
            _instance.Logger?.Log("[ROOT] Build Awake is called!");
        }

        private void OnDestroy()
        {
            if (_instance != this) throw new InvalidOperationException("Root is not set!");

            _instance.Logger?.Log("[ROOT] Build OnDestroy is called!");
            _instance = Disable(this);
        }
    }
}

#endif
