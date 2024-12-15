#if DEVELOPMENT_BUILD || !UNITY_EDITOR
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
#endif

using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Debug.Runtime
{
    internal sealed class DebugConsoleLoader : MonoBehaviour
    {
        #pragma warning disable CS0169 // Field is never used
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [SerializeField] private AssetReferenceGameObject _debugConsole;

#   if DEVELOPMENT_BUILD && !UNITY_EDITOR

        private bool _consoleWasLoaded;

        [UsedImplicitly] // ReSharper disable once Unity.IncorrectMethodSignature
        private async UniTaskVoid Awake()
        {
            if (_consoleWasLoaded) return;
            var (wasCanceled, _) = await _debugConsole.InstantiateAsync().ToUniTask
            (
                progress: null!,
                PlayerLoopTiming.Initialization,
                destroyCancellationToken,
                cancelImmediately: true,
                autoReleaseWhenCanceled: true

            ).SuppressCancellationThrow();

            if (wasCanceled) return;

            _consoleWasLoaded = true;
        }

        private void OnDestroy()
        {
            if (_consoleWasLoaded is false) return;

            _debugConsole.ReleaseAsset();
            _consoleWasLoaded = false;
        }

#   endif

    }
}
