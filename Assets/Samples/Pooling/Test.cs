using UnityEngine;
using UnityEngine.InputSystem;

namespace Generic
{
    public class Test : MonoBehaviour
    {
        private void Awake()
        {
            InputSystem.pollingFrequency = float.MaxValue;
        }
    }
}
