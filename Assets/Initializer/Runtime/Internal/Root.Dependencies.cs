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
using Object = UnityEngine.Object;

namespace Initializer
{
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    [SuppressMessage("ReSharper", "Unity.NoNullPatternMatching")]
    partial class Root
    {
        private static readonly List<ServiceAsset> InstancesCache = new ();

        internal static IReadOnlyCollection<Type> AllPotentialServicesTypes { get; } = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(static assembly =>
            {
                try { return assembly.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(static type => type.IsAbstract is false && typeof(ServiceAsset).IsAssignableFrom(type))
            .ToArray();

        internal List<ServiceAsset> Services => _services;

        internal static IReadOnlyCollection<ServiceAsset> CreateInstances(IReadOnlyCollection<Type> types)
        {
            InstancesCache.Clear();

            foreach (var type in types)
            {
                var instance = (ServiceAsset) CreateInstance(type);
                instance.name = type.Name;
                InstancesCache.Add(instance);
            }

            return InstancesCache;
        }

        /// <summary>
        /// Includes 'this' Root itself
        /// </summary>
        /// <returns>Set of <see cref="ServiceAsset"/>s including <see cref="Root"/></returns>
        internal IReadOnlyCollection<Object> GetSubAssets()
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(this));
        }

        internal void AddServices(IReadOnlyCollection<ServiceAsset> services)
        {
            var path = AssetDatabase.GetAssetPath(this);
            foreach (var service in services)
            {
                AssetDatabase.AddObjectToAsset(service, path);
                if (_services.Contains(service) is not true)
                {
                    _services.Add(service);
                }
            }

            if (TryResolveDependencies(_services))
            {
                SortDependencies(_services);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        internal void RemoveServices(IReadOnlyCollection<ServiceAsset> services)
        {
            foreach (var service in services)
            {
                AssetDatabase.RemoveObjectFromAsset(service);
                if (_services.Contains(service))
                {
                    _services.Remove(service);
                }
            }

            if (TryResolveDependencies(_services))
            {
                SortDependencies(_services);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static bool TryResolveDependencies(in List<ServiceAsset> services)
        {
            if (services.Count == 0) return false;

            if (services.Distinct().Count() != services.Count)
            {
                Debug.LogError("[ROOT] Duplicated service detected. Ensure all services are unique!");

                return false;
            }

            foreach (var service in services)
            {
                var fields = service.GetType()
                    .GetFields(BindingFlags.Instance | Public | NonPublic)
                    .Where(static field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    .Where(static field => typeof(ServiceAsset).IsAssignableFrom(field.FieldType));

                foreach (var field in fields)
                {
                    var dependencyType = field.FieldType;

                    var dependency = services.SingleOrDefault(candidate => dependencyType.IsInstanceOfType(candidate));
                    if (dependency == null)
                    {
                        Debug.LogErrorFormat("[ROOT] Could not resolve dependency {0} in service {1}",
                            dependencyType.Name, service.name);

                        return false;
                    }

                    field.SetValue(service, dependency);
                    EditorUtility.SetDirty(service);
                }
            }

            return true;
        }

        private static void SortDependencies(List<ServiceAsset> services)
        {
            if (services.Count == 0) return;
            if (services.Any(static service => service == null))
            {
                Debug.LogError("[ROOT] Null service detected in services. Ensure all services are set!");
                return;
            }

            TopologicalSort(services);
        }

        private static void TopologicalSort<T>(List<T> items) where T : class
        {
            var inDegree = items.ToDictionary(static item => item, static _ => 0);
            var graph = items.ToDictionary(static item => item, static _ => new List<T>());

            foreach (var item in items)
            {
                if (TryGetDependencies(item, out var dependencies) is false) continue;

                foreach (var dependency in dependencies)
                {
                    if (inDegree.ContainsKey(dependency) is not true) continue;

                    graph[dependency].Add(item);
                    inDegree[item]++;
                }
            }

            var pureDependencies = inDegree.Where(static income => income.Value == 0)
                .Select(static income => income.Key);
            var dependenciesQueue = new Queue<T>(pureDependencies);
            var result = new List<T>(items.Count);

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

            if (result.Count == items.Count)
            {
                items.Clear();
                items.AddRange(result);
                result.Clear();

                return;
            }

            Debug.LogError("[ROOT] Cycle detected in services, sort failed.");
        }

        private static bool TryGetDependencies<T>(T service, [NotNullWhen(returnValue: true)] out IReadOnlyCollection<T>? dependencies) where T : class
        {
            var fields = service.GetType()
                .GetFields(BindingFlags.Instance | Public | NonPublic)
                .Where(static field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .Where(static filed => typeof(T).IsAssignableFrom(filed.FieldType));
            using var fieldsEnumerator = fields.GetEnumerator();

            if (fieldsEnumerator.MoveNext() is false)
            {
                dependencies = null;
                return false;
            }

            var dependenciesList = new List<T>();
            do
            {
                var dependency = (T?) fieldsEnumerator.Current?.GetValue(service);
                if (dependency == null)
                {
                    Debug.LogErrorFormat("[ROOT] Null dependency {0} detected in {1}!",
                        fieldsEnumerator.Current?.Name, service.GetType().Name);

                    dependencies = null;
                    return false;
                }

                dependenciesList.Add(dependency);

            } while (fieldsEnumerator.MoveNext());

            dependencies = dependenciesList;
            return true;
        }
    }
}

#endif
