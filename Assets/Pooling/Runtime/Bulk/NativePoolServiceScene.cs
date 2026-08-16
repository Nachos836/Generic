#nullable enable

using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pooling.Bulk
{
    public sealed class NativePoolServiceScene : IDisposable
    {
        private const string NativePoolGlobalExecutionContext = "NativePool.GlobalExecutionContext";

        private Scene? _scene;
        private int _references;
        private bool _disposed;

        private Scene Acquire()
        {
            Interlocked.Increment(ref _references);

            return _scene ??= CreateGlobalExecutionContextScene(NativePoolGlobalExecutionContext);
        }

        internal Handle AcquireSceneHandle() => new(this, Acquire());

        private void Release()
        {
            if (_scene is null) return;
            if (Interlocked.Decrement(ref _references) > 0) return;

            ForcefullyReleaseScene();
        }

        private void ForcefullyReleaseScene()
        {
            if (_scene is null) return;

            if (_scene is { isLoaded: true } scene)
            {
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation is { isDone: false })
                {
                    Awaitable.FromAsyncOperation(operation)
                        .LogExceptionsAndForget();
                }
            }

            _scene = null;
        }

        private static Scene CreateGlobalExecutionContextScene(string sceneName)
        {
            if (SceneManager.GetSceneByName(sceneName) is { isLoaded: true } scene) return scene;

            return SceneManager.CreateScene(sceneName, new CreateSceneParameters
            {
                localPhysicsMode = LocalPhysicsMode.Physics3D
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            ForcefullyReleaseScene();
        }

        internal struct Handle : IDisposable
        {
            private readonly NativePoolServiceScene _serviceScene;

            private bool _disposed;

            public Scene Scene { get; }

            internal Handle(NativePoolServiceScene serviceScene, Scene scene)
            {
                _serviceScene = serviceScene;
                _disposed = false;
                Scene = scene;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;
                _serviceScene.Release();
            }
        }
    }
}
