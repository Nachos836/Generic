#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using static System.Reflection.BindingFlags;

namespace Initializer
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    [SuppressMessage("ReSharper", "Unity.NoNullPatternMatching")]
    partial class Root
    {
        [Header("Services Load Order")]
        [SerializeField] private ServiceAsset[] _services = Array.Empty<ServiceAsset>();

        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var location = AssetDatabase.GetAssetPath(this);
            _services = AssetDatabase.LoadAllAssetsAtPath(location)
                .Where(static candidate => candidate is ServiceAsset)
                .Cast<ServiceAsset>()
                .ToArray();

            if (_services.Length == 0) return;
            if (_services.Any(static service => service == null))
            {
                Debug.LogError("[ROOT] Null service detected in services. Ensure all services are set!");
                return;
            }
            if (_services.Distinct().Count() != _services.Length)
            {
                Debug.LogError("[ROOT] Duplicated service detected. Ensure all services are unique!");
                return;
            }

            _services = TopologicalSort(_services, GetDependencies);
        }

        private static T[] TopologicalSort<T>(T[] items, Func<T, IEnumerable<T>> getDependencies) where T : class
        {
            var inDegree = items.ToDictionary(static item => item, static _ => 0);
            var graph = items.ToDictionary(static item => item, static _ => new List<T>());

            foreach (ref var item in items.AsSpan())
            {
                foreach (var dependency in getDependencies(item))
                {
                    if (inDegree.ContainsKey(dependency) is not true) continue;

                    graph[dependency].Add(item);
                    inDegree[item]++;
                }
            }

            var dependencies = inDegree.Where(static income => income.Value == 0)
                .Select(static income => income.Key);
            var dependenciesQueue = new Queue<T>(dependencies);
            var result = new List<T>(items.Length);

            while (dependenciesQueue.Count > 0)
            {
                var current = dependenciesQueue.Dequeue();
                result.Add(current);

                foreach (var candidate in graph[current])
                {
                    if (--inDegree[candidate] == 0)
                    {
                        dependenciesQueue.Enqueue(candidate);
                    }
                }
            }

            if (result.Count == items.Length) return result.ToArray();

            Debug.LogError("[ROOT] Cycle detected in services, sort failed.");
            return items;
        }

        private static IEnumerable<T> GetDependencies<T>(T? service) where T : class
        {
            if (service == null) yield break;

            foreach (var field in service.GetType().GetFields(BindingFlags.Instance | Public | NonPublic))
            {
                var publiclySerialized = field.IsPublic;
                var serializedByAttribute = field.GetCustomAttribute<SerializeField>() != null;
                if (publiclySerialized is false && serializedByAttribute is false) continue;

                if (typeof(T).IsAssignableFrom(field.FieldType) is false) continue;
                var dependency = (T?) field.GetValue(service);
                if (dependency != null) yield return dependency;
            }
        }
    }
}

#endif
