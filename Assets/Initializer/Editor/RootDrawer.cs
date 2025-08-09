#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Initializer.Editor
{
    using Internals;

    [CustomEditor(typeof(Root))]
    [SuppressMessage("ReSharper", "Unity.NoNullPatternMatching")]
    internal sealed class RootDrawer : UnityEditor.Editor
    {
        private const int FooterAddButtonShowThreshold = 5;

        private readonly DisposableList _disposables = new ();
        private readonly ObservableList<ServiceAsset> _services = new ();
        private readonly ObservableList<Type> _available = new ();

        [SerializeField] private VisualTreeAsset _mainAsset = default!;

        private static IEnumerable<Type> AllServiceTypes { get; } = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(static assembly =>
            {
                try { return assembly.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(static type => type.IsAbstract is false && typeof(ServiceAsset).IsAssignableFrom(type))
            .ToArray();

        private void OnDisable()
        {
            _disposables.Dispose();
            _services.Dispose();
            _available.Dispose();
        }

        public override VisualElement CreateInspectorGUI()
        {
            const string hidden = "hidden";

            var main = _mainAsset.CloneTree();
            var mainLayout = main.Q<VisualElement>("MainLayout");
            var servicesSectionLayout = mainLayout.Q<VisualElement>("ServicesSectionLayout");
            var noServicesSectionLayout = mainLayout.Q<VisualElement>("NoServicesAvailableLayout");
            var addServiceSectionLayout = mainLayout.Q<VisualElement>("AddServiceSectionLayout");
            var noServicesToAddSectionLayout = mainLayout.Q<VisualElement>("NoServicesToAddLayout");

            servicesSectionLayout.EnableInClassList(hidden, true);
            addServiceSectionLayout.EnableInClassList(hidden, true);

            var root = (Root) serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(root);
            var (servicesFooter, servicesList) = SetupSectionLayout(root, servicesSectionLayout, _services, _disposables);
            var (addSectionFooter, addSectionList) = SetupSectionLayout(root, addServiceSectionLayout, _available, _disposables);

            _services.ItemAddedSubscribe((_, _) =>
            {
                servicesList.RefreshItems();

            }).AddTo(_disposables);
            _services.ItemRemovedSubscribe((_, asset) =>
            {
                AssetDatabase.RemoveObjectFromAsset(asset);

                RefreshAddSection(_available, asset);

            }).AddTo(_disposables);
            _services.CountChangedSubscribe(amount =>
            {
                servicesSectionLayout.EnableInClassList(hidden, amount == 0);
                noServicesSectionLayout.EnableInClassList(hidden, amount >= 1);
                servicesFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);

            }).AddTo(_disposables);

            _available.ItemAddedSubscribe((_, _) =>
            {
                addSectionList.RefreshItems();

            }).AddTo(_disposables);
            _available.ItemRemovedSubscribe((_, type) =>
            {
                var instance = CreateInstance(type);
                instance.name = type.Name;
                AssetDatabase.AddObjectToAsset(instance, assetPath);

                RefreshServices(assetPath, _services, income: type);

            }).AddTo(_disposables);
            _available.CountChangedSubscribe(amount =>
            {
                addServiceSectionLayout.EnableInClassList(hidden, amount == 0);
                noServicesToAddSectionLayout.EnableInClassList(hidden, amount >= 1);
                addSectionFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);

            }).AddTo(_disposables);

            PopulateServices(assetPath, _services);
            PopulateAvailable(AllServiceTypes, _services, _available);

            return main;
        }

        private static void RefreshServices(string assetPath, ObservableList<ServiceAsset> services, Type income)
        {
            var candidate = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Single(asset => asset.GetType() == income);
            services.Add((ServiceAsset) candidate);
        }

        private static void RefreshAddSection(ObservableList<Type> available, ServiceAsset income)
        {
            available.Add(income.GetType());
        }

        private static void PopulateServices(string assetPath, ObservableList<ServiceAsset> services)
        {
            var gathered = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(static candidate => candidate && candidate is ServiceAsset)
                .Cast<ServiceAsset>();

            foreach (var service in gathered.Except(services))
            {
                services.Add(service);
            }
        }

        private static void PopulateAvailable(IEnumerable<Type> allServiceTypes, IEnumerable<ServiceAsset> services, ObservableList<Type> available)
        {
            var gathered = allServiceTypes.Except(services.Select(static service => service.GetType()));
            foreach (var type in gathered.Except(available))
            {
                available.Add(type);
            }
        }

        private static (VisualElement Footer, ListView) SetupSectionLayout<TElement>
        (
            Root root,
            VisualElement sectionLayout,
            ObservableList<TElement> collection,
            DisposableList disposables
        ) {
            var header = sectionLayout.Q<VisualElement>("Header");
            var headerActionButton = header.Q<Button>("ActionButton");
            var footer = sectionLayout.Q<VisualElement>("Footer");
            var footerActionButton = footer.Q<Button>("ActionButton");
            var container = sectionLayout.Q<ListView>("Container");

            var selectedToggles = new List<TElement>(collection.Count);
            var viewToggles = new ObservableList<Toggle>(collection.Count);

            headerActionButton.SetEnabled(false);
            footerActionButton.SetEnabled(false);

            container.itemsSource = collection;
            container.bindItem = (visualElement, index) =>
            {
                var toggle = visualElement.Q<Toggle>();
                var candidate = collection[index];
                toggle.label = candidate switch
                {
                    ServiceAsset serviceAsset => serviceAsset.name,
                    Type type => type.Name,
                    _ => toggle.label
                };

                toggle.RegisterCallback<ChangeEvent<bool>>(ProcessToggle);
                new Subscription(() => toggle.UnregisterCallback<ChangeEvent<bool>>(ProcessToggle))
                    .AddTo(disposables);

                return;

                void ProcessToggle(ChangeEvent<bool> income)
                {
                    if (income.newValue is false)
                    {
                        selectedToggles.Remove(candidate);
                        viewToggles.Remove(toggle);
                    }
                    else
                    {
                        selectedToggles.Add(candidate);
                        viewToggles.Add(toggle);
                    }
                }
            };

            viewToggles.CountChangedSubscribe(amount =>
            {
                headerActionButton.SetEnabled(amount >= 1);
                footerActionButton.SetEnabled(amount >= 1);

            }).AddTo(disposables);
            viewToggles.AddTo(disposables);

            headerActionButton.clicked += ActionOnSelected;
            new Subscription(() => headerActionButton.clicked -= ActionOnSelected)
                .AddTo(disposables);
            footerActionButton.clicked += ActionOnSelected;
            new Subscription(() => footerActionButton.clicked -= ActionOnSelected)
                .AddTo(disposables);

            return (footer, container);

            void ActionOnSelected()
            {
                if (selectedToggles.Count == 0) return;

                Undo.RegisterCompleteObjectUndo(root, $"Modify {typeof(TElement).Name} Collection");

                foreach (var selected in selectedToggles)
                {
                    collection.Remove(selected);
                }

                foreach (var toggle in viewToggles)
                {
                    toggle.value = false;
                }

                selectedToggles.Clear();
                viewToggles.Clear();

                root.OnValidate();

                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();

                container.RefreshItems();
            }
        }
    }
}
