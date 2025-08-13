#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Initializer
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    [SuppressMessage("ReSharper", "Unity.NoNullPatternMatching")]
    public sealed partial class Root : ScriptableObject
    {
        private static Root? _instance;

        /// <summary>
        /// Controverts the main design philosophy! <br/>
        /// Always prefer serialized fields for direct getting a dependency! <br/>
        /// Use only for utilities and in the Editor-related scope
        /// </summary>
        /// <exception cref="InvalidOperationException">Shoots when Root is not ready</exception>
        [UsedImplicitly]
        public static Root Instance => _instance ? _instance : throw new InvalidOperationException("Root is not set!");

        /// <summary>
        /// Use only when there is no other choice but to have a direct request from the code!
        /// </summary>
        /// <typeparam name="T">A Type of requested service/dependency</typeparam>
        /// <returns>Singleton-like dependency from the Root scope</returns>
        /// <exception cref="InvalidOperationException">Shoots when there is no such a dependency within a root <br/>
        /// Or if there are multiple occasions of a requested type (ScriptableObject, for example)</exception>
        [UsedImplicitly]
        [MustUseReturnValue]
        public T GetDependency<T>() where T : class
        {
            if (TryGetDependency<T>(out var dependency) is false)
                throw new InvalidOperationException($"Can't resolve a single dependency of type { typeof(T).Name }");

            return dependency;
        }

        /// <summary>
        /// Non-throw version of <see cref="GetDependency{T}"/>
        /// </summary>
        [UsedImplicitly]
        [MustUseReturnValue]
        public bool TryGetDependency<T>([NotNullWhen(returnValue: true)] out T? dependency) where T : class
        {
            var candidate = _services.SingleOrDefault(static service => service is T);
            if (candidate is null)
            {
                dependency = null;
                return false;
            }
            else
            {
                dependency = (candidate as T)!;
                return true;
            }
        }

        [MustUseReturnValue]
        private static Root Enable(in Root root)
        {
            Debug.Log("[ROOT] Enable is called!");

            foreach (var service in root._services)
            {
                if (service && service is IInitializable initializable)
                {
                    initializable.Initialize();
                }
            }

            return root;
        }

        [MustUseReturnValue]
        private static Root? Disable(in Root root)
        {
            Debug.Log("[ROOT] Disable is called!");

            foreach (var service in root._services)
            {
                if (service && service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return null;
        }
    }
}
