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

        [SerializeField] private VisualTreeAsset _mainAsset = default!;

        private DisposableList _disposables = default!;
        private ObservableList<ServiceAsset> _rootServicesProjection = default!;
        private ObservableList<Type> _availableServiceTypes = default!;

        private void Awake()
        {
            var root = (Root) target;
            var assets = root.GetSubAssets();
            var serializedServices = root.Services;

            // -1 to compensate root itself
            if (assets.Count - 1 == serializedServices.Count) return;

            serializedServices.Clear();
            serializedServices.AddRange(assets.OfType<ServiceAsset>());
        }

        private void OnEnable()
        {
            _disposables = new DisposableList();
            _rootServicesProjection = ObservableList<ServiceAsset>.WrapList(((Root) target).Services);
            var presentServiceTypes = _rootServicesProjection.RawList.Select(static service => service.GetType());
            var availableServiceTypes = Root.AllPotentialServicesTypes.Except(presentServiceTypes);
            _availableServiceTypes = new ObservableList<Type>(availableServiceTypes);
        }

        private void OnDisable()
        {
            _disposables.Dispose();
            _rootServicesProjection.Dispose();
            _availableServiceTypes.Dispose();

            _availableServiceTypes.Clear();

            _disposables = default!;
            _rootServicesProjection = default!;
            _availableServiceTypes = default!;
        }

        public override VisualElement CreateInspectorGUI()
        {
            const string hidden = "hidden";

            var main = _mainAsset.CloneTree();
            var mainLayout = main.Q<VisualElement>("MainLayout");
            var registeredServicesSectionLayout = mainLayout.Q<VisualElement>("RegisteredServicesSectionLayout");
            var noRegisteredServicesLayout = mainLayout.Q<VisualElement>("NoRegisteredServicesLayout");
            var availableServiceSectionLayout = mainLayout.Q<VisualElement>("AvailableServiceSectionLayout");
            var noAvailableServicesLayout = mainLayout.Q<VisualElement>("NoAvailableServicesLayout");

            var root = (Root) target;

            var (registeredSectionFooter, registeredSectionList) = SetupSectionLayout(root, registeredServicesSectionLayout, _rootServicesProjection,
                _disposables, getItemName: static asset => asset.name);
            var (availableSectionFooter, availableSectionList) = SetupSectionLayout(root, availableServiceSectionLayout, _availableServiceTypes,
                _disposables, getItemName: static type => type.Name);

            RefreshRegisteredServicesSection();
            RefreshAvailableServicesSection();

            _rootServicesProjection.ItemsAddedSubscribe(items =>
            {
                root.AddServices(items);

            }).AddTo(_disposables);
            _rootServicesProjection.ItemsRemovedSubscribe(items =>
            {
                _availableServiceTypes.Add(items.Select(static service => service.GetType()).ToArray());

                root.RemoveServices(items);

            }).AddTo(_disposables);
            _rootServicesProjection.CountChangedSubscribe(amount =>
            {
                registeredServicesSectionLayout.EnableInClassList(hidden, amount == 0);
                noRegisteredServicesLayout.EnableInClassList(hidden, amount >= 1);
                registeredSectionFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);

                registeredSectionList.RefreshItems();

            }).AddTo(_disposables);

            _availableServiceTypes.ItemsRemovedSubscribe(types =>
            {
                _rootServicesProjection.Add(Root.CreateInstances(types));

            }).AddTo(_disposables);
            _availableServiceTypes.CountChangedSubscribe(amount =>
            {
                availableServiceSectionLayout.EnableInClassList(hidden, amount == 0);
                noAvailableServicesLayout.EnableInClassList(hidden, amount >= 1);
                availableSectionFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);

                availableSectionList.RefreshItems();

            }).AddTo(_disposables);

            return main;

            void RefreshRegisteredServicesSection()
            {
                registeredSectionList.RefreshItems();
                var amount = registeredSectionList.itemsSource.Count;

                registeredServicesSectionLayout.EnableInClassList(hidden, amount == 0);
                noRegisteredServicesLayout.EnableInClassList(hidden, amount >= 1);
                registeredSectionFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);
            }

            void RefreshAvailableServicesSection()
            {
                availableSectionList.RefreshItems();
                var amount = availableSectionList.itemsSource.Count;

                availableServiceSectionLayout.EnableInClassList(hidden, amount == 0);
                noAvailableServicesLayout.EnableInClassList(hidden, amount >= 1);
                availableSectionFooter.EnableInClassList(hidden, amount < FooterAddButtonShowThreshold);
            }
        }

        private static (VisualElement Footer, ListView) SetupSectionLayout<TElement>
        (
            Root root,
            VisualElement sectionLayout,
            ObservableList<TElement> collection,
            DisposableList disposables,
            Func<TElement, string> getItemName
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

            container.itemsSource = collection.ObjectRawList;
            container.bindItem = (visualElement, index) =>
            {
                var toggle = visualElement.Q<Toggle>();
                var candidate = collection[index];
                toggle.label = getItemName(candidate);

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

                foreach (var toggle in viewToggles.RawList)
                {
                    toggle.value = false;
                }

                collection.Remove(selectedToggles);

                selectedToggles.Clear();
                viewToggles.Clear();
            }
        }
    }
}
