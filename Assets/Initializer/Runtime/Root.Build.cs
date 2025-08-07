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
            _instance = this;
        }

        private void OnEnable()
        {
            if (_instance == this) throw new InvalidOperationException("Root should be set only once!");

            _instance = Enable(this);

            Debug.Log("Build OnEnable is called!");
        }

        private void OnDisable()
        {
            if (_instance != this) throw new InvalidOperationException("Root is not set!");

            _instance = Disable(this);

            Debug.Log("Build OnDisable is called!");
        }
    }
}

#endif
