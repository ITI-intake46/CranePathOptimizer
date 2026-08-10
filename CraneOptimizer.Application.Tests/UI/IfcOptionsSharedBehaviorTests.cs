using System.Xml.Linq;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using Xunit;

namespace CraneOptimizer.Application.Tests.UI
{
    public sealed class IfcOptionsSharedBehaviorTests
    {
        [Fact]
        public void LoadAndDisplayModesUseSameOptionDefinitions()
        {
            var options = IfcVisibilityOptionsCatalog.CreateDefaultOptions();

            var load = new IfcOptionsDialogViewModel(
                IfcOptionsMode.Load,
                "model.ifc",
                options,
                IfcVisibilityOptionsCatalog.Definitions);

            var display = new IfcOptionsDialogViewModel(
                IfcOptionsMode.Display,
                "model.ifc",
                options,
                IfcVisibilityOptionsCatalog.Definitions);

            Assert.Same(load.OptionDefinitions, display.OptionDefinitions);
            Assert.Equal("IFC Load Options", load.DialogTitle);
            Assert.Equal("Load", load.PrimaryButtonText);
            Assert.Equal("IFC Display Options", display.DialogTitle);
            Assert.Equal("Apply", display.PrimaryButtonText);
            Assert.IsType<IfcLoadOptionsDto>(load.ToOptions());
            Assert.IsType<IfcLoadOptionsDto>(display.ToOptions());
        }

        [Fact]
        public void DialogCheckBoxesUseExplicitTwoWayPropertyChangedBindings()
        {
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var document = XDocument.Load(GetRepoFile(
                "CraneOptimizer.UI/Features/F2_IfcViewer/Views/IFCLoadOptionsDialog.xaml"));
            var expectedBindings = new Dictionary<string, string>
            {
                ["ChkHideGrids"] = "HideGrids",
                ["ChkHideLevels"] = "HideLevels",
                ["ChkHideAnnotations"] = "HideAnnotations",
                ["ChkHideSite"] = "HideSite",
                ["ChkHideSpaces"] = "HideSpaces",
                ["ChkHideOpenings"] = "HideOpenings",
                ["ChkHideVirtualElements"] = "HideVirtualElements"
            };

            var checkBoxes = document.Descendants(ns + "CheckBox")
                .Where(element => expectedBindings.ContainsKey(element.Attribute(x + "Name")?.Value ?? string.Empty))
                .ToDictionary(element => element.Attribute(x + "Name")!.Value);

            Assert.Equal(expectedBindings.Count, checkBoxes.Count);
            foreach ((string checkBoxName, string propertyName) in expectedBindings)
            {
                string? binding = checkBoxes[checkBoxName].Attribute("IsChecked")?.Value;
                Assert.Equal(
                    $"{{Binding {propertyName}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}",
                    binding);
                Assert.Null(checkBoxes[checkBoxName].Attribute("Click"));
            }
        }

        [Fact]
        public void DisplayModeWorkingOptionsAreEditableAndHideSpacesToggles()
        {
            var applied = AllVisibleOptions();
            var viewModel = CreateDialogViewModel(IfcOptionsMode.Display, applied);

            viewModel.HideSpaces = true;

            Assert.True(viewModel.HideSpaces);
            Assert.True(viewModel.ToOptions().HideSpaces);
            Assert.False(applied.HideSpaces);

            viewModel.HideSpaces = false;

            Assert.False(viewModel.HideSpaces);
            Assert.False(viewModel.ToOptions().HideSpaces);
        }

        [Theory]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideGrids))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideLevels))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideAnnotations))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideSite))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideSpaces))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideOpenings))]
        [InlineData(nameof(IfcOptionsDialogViewModel.HideVirtualElements))]
        public void EveryWorkingOptionCanToggleAndRaisesPropertyChanged(string propertyName)
        {
            var viewModel = CreateDialogViewModel(IfcOptionsMode.Display, AllVisibleOptions());
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            var property = typeof(IfcOptionsDialogViewModel).GetProperty(propertyName);

            Assert.NotNull(property);
            Assert.True(property.CanWrite);

            property.SetValue(viewModel, true);
            Assert.True((bool)property.GetValue(viewModel)!);
            Assert.Equal(propertyName, Assert.Single(changedProperties));

            changedProperties.Clear();
            property.SetValue(viewModel, false);
            Assert.False((bool)property.GetValue(viewModel)!);
            Assert.Equal(propertyName, Assert.Single(changedProperties));
        }

        [Fact]
        public void SelectAllAndClearAllUpdateTheSameWorkingOptions()
        {
            var viewModel = CreateDialogViewModel(IfcOptionsMode.Display, AllVisibleOptions());

            viewModel.SelectAll();

            AssertAllVisibilityOptions(viewModel.ToOptions(), true);

            viewModel.ClearAll();

            AssertAllVisibilityOptions(viewModel.ToOptions(), false);
        }

        [Fact]
        public void ApplySnapshotCommitsEditedWorkingCopyWithoutSharingMutableState()
        {
            var viewModel = CreateDialogViewModel(IfcOptionsMode.Display, AllVisibleOptions());
            viewModel.HideSpaces = true;
            viewModel.HideSite = true;

            var committed = viewModel.ToOptions();
            viewModel.ClearAll();

            Assert.True(committed.HideSpaces);
            Assert.True(committed.HideSite);
            Assert.False(viewModel.HideSpaces);
            Assert.False(viewModel.HideSite);
        }

        [Fact]
        public void DisplayApplyPassesEditedWorkingCopyToSharedVisibilityService()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);
            var viewModel = CreateDialogViewModel(
                IfcOptionsMode.Display,
                service.GetCurrentOptions());
            viewModel.ClearAll();
            viewModel.HideSpaces = true;

            service.ApplyOptions(viewModel.ToOptions());

            Assert.True(service.GetCurrentOptions().HideSpaces);
            Assert.DoesNotContain(filter.VisibleElements, element => element.RawIfcType == "IfcSpace");
            Assert.Contains(filter.VisibleElements, element => element.RawIfcType == "IfcWall");
        }

        [Fact]
        public void CatalogMapsEveryCheckboxToCanonicalIfcCategory()
        {
            var mappings = IfcVisibilityOptionsCatalog.Definitions
                .ToDictionary(definition => definition.Key, definition => definition.IfcTypes.Single());

            Assert.Equal("IfcGrid", mappings[nameof(IfcLoadOptionsDto.HideGrids)]);
            Assert.Equal("IfcBuildingStorey", mappings[nameof(IfcLoadOptionsDto.HideLevels)]);
            Assert.Equal("IfcAnnotation", mappings[nameof(IfcLoadOptionsDto.HideAnnotations)]);
            Assert.Equal("IfcSite", mappings[nameof(IfcLoadOptionsDto.HideSite)]);
            Assert.Equal("IfcSpace", mappings[nameof(IfcLoadOptionsDto.HideSpaces)]);
            Assert.Equal("IfcOpeningElement", mappings[nameof(IfcLoadOptionsDto.HideOpenings)]);
            Assert.Equal("IfcVirtualElement", mappings[nameof(IfcLoadOptionsDto.HideVirtualElements)]);
        }

        [Fact]
        public void DisplayApplyInvokesSharedViewportVisibilityService()
        {
            var filter = CreateFilter();
            var viewport = new FakeViewportInvalidationObserver(filter);
            var service = new IfcVisibilityOptionsService(filter);
            var options = AllVisibleOptions() with
            {
                HideLevels = true
            };
            filter.ResetObservationCounts();

            service.ApplyOptions(options);

            Assert.Equal(1, filter.ApplyLoadOptionsCount);
            Assert.Equal(1, filter.StateChangedCount);
            Assert.Equal(1, viewport.ReRenderElementsCount);
            Assert.DoesNotContain(filter.VisibleElements, element => element.RawIfcType == "IfcBuildingStorey");
        }

        [Fact]
        public void SharedPredicateHidesLevelsByRawIfcTypeAndRestoresThem()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);

            service.ApplyOptions(AllVisibleOptions() with
            {
                HideLevels = true
            });

            Assert.DoesNotContain(filter.VisibleElements, element => element.RawIfcType == "IfcBuildingStorey");

            service.ApplyOptions(AllVisibleOptions() with
            {
                HideLevels = false
            });

            Assert.Contains(filter.VisibleElements, element => element.RawIfcType == "IfcBuildingStorey");
        }

        [Fact]
        public void HideGridsAffectsIfcGridAndLeavesUnrelatedRenderObjects()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);

            service.ApplyOptions(AllVisibleOptions() with
            {
                HideGrids = true
            });

            Assert.DoesNotContain(filter.VisibleElements, element => element.RawIfcType == "IfcGrid");
            Assert.Contains(filter.VisibleElements, element => element.RawIfcType == "IfcBuildingStorey");
            Assert.Contains(filter.VisibleElements, element => element.RawIfcType == "IfcWall");
        }

        [Fact]
        public void InitialLoadAndPostLoadApplyUseSameVisibilityServiceBehavior()
        {
            var loadFilter = CreateFilter();
            var displayFilter = CreateFilter();
            var loadService = new IfcVisibilityOptionsService(loadFilter);
            var displayService = new IfcVisibilityOptionsService(displayFilter);
            var options = IfcVisibilityOptionsCatalog.CreateDefaultOptions() with
            {
                HideSite = true
            };

            loadService.ApplyOptions(options);
            displayService.ApplyOptions(options);

            Assert.Equal(
                loadFilter.VisibleElements.Select(element => element.ElementID).OrderBy(id => id),
                displayFilter.VisibleElements.Select(element => element.ElementID).OrderBy(id => id));
        }

        [Fact]
        public void ReopeningDisplayOptionsReturnsCurrentlyAppliedState()
        {
            var service = new IfcVisibilityOptionsService(CreateFilter());
            var options = IfcVisibilityOptionsCatalog.CreateDefaultOptions() with
            {
                HideGrids = false,
                HideSite = true
            };

            service.ApplyOptions(options);

            Assert.Equal(options, service.GetCurrentOptions());
        }

        [Fact]
        public void PostLoadApplyCanHideAndRestoreCategories()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);

            service.ApplyOptions(IfcVisibilityOptionsCatalog.CreateDefaultOptions() with
            {
                HideGrids = true,
                HideSpaces = false,
                HideOpenings = false,
                HideVirtualElements = false
            });

            Assert.DoesNotContain(filter.VisibleElements, element => element.RawIfcType == "IfcGrid");

            service.ApplyOptions(IfcVisibilityOptionsCatalog.CreateDefaultOptions() with
            {
                HideGrids = false,
                HideSpaces = false,
                HideOpenings = false,
                HideVirtualElements = false
            });

            Assert.Contains(filter.VisibleElements, element => element.RawIfcType == "IfcGrid");
        }

        [Fact]
        public void CancelDoesNotModifyAppliedOptionsOrRenderVisibility()
        {
            var filter = CreateFilter();
            var viewport = new FakeViewportInvalidationObserver(filter);
            var service = new IfcVisibilityOptionsService(filter);
            var applied = AllVisibleOptions() with
            {
                HideGrids = true
            };
            service.ApplyOptions(applied);
            var visibleBeforeDialog = filter.VisibleElements.Select(element => element.ElementID).ToList();
            filter.ResetObservationCounts();
            viewport.ResetObservationCounts();

            var dialogVm = new IfcOptionsDialogViewModel(
                IfcOptionsMode.Display,
                "model.ifc",
                service.GetCurrentOptions(),
                service.OptionDefinitions);
            dialogVm.HideGrids = false;

            Assert.Equal(applied, service.GetCurrentOptions());
            Assert.Equal(visibleBeforeDialog, filter.VisibleElements.Select(element => element.ElementID).ToList());
            Assert.Equal(0, filter.StateChangedCount);
            Assert.Equal(0, viewport.ReRenderElementsCount);
        }

        [Fact]
        public void VisibilityOptionsDoNotRemoveDomainElementsFromFilterCollection()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);

            service.ApplyOptions(IfcVisibilityOptionsCatalog.CreateDefaultOptions());

            Assert.Equal(8, filter.AllElements.Count);
            Assert.True(filter.VisibleElements.Count < filter.AllElements.Count);
        }

        [Fact]
        public void ApplyingDisplayOptionsDoesNotReloadOrReplaceElements()
        {
            var filter = CreateFilter();
            var service = new IfcVisibilityOptionsService(filter);
            filter.ResetObservationCounts();

            service.ApplyOptions(AllVisibleOptions() with
            {
                HideLevels = true
            });

            Assert.Equal(0, filter.SetElementsCallCount);
            Assert.Equal(8, filter.AllElements.Count);
        }

        [Fact]
        public void ApplyingDisplayOptionsInvalidatesViewportWithoutResettingCameraState()
        {
            var filter = CreateFilter();
            var viewport = new FakeViewportInvalidationObserver(filter)
            {
                CameraState = "target=(12,4,8);distance=34;up=Z"
            };
            var service = new IfcVisibilityOptionsService(filter);
            filter.ResetObservationCounts();

            service.ApplyOptions(AllVisibleOptions() with
            {
                HideLevels = true
            });

            Assert.Equal(1, viewport.ReRenderElementsCount);
            Assert.Equal(0, viewport.ZoomExtentsCount);
            Assert.Equal("target=(12,4,8);distance=34;up=Z", viewport.CameraState);
        }

        [Fact]
        public void ApplyingDisplayOptionsTargetsCurrentActiveViewportFilter()
        {
            var currentFilter = CreateFilter();
            var staleFilter = CreateFilter();
            var currentService = new IfcVisibilityOptionsService(currentFilter);
            var staleService = new IfcVisibilityOptionsService(staleFilter);
            currentFilter.ResetObservationCounts();
            staleFilter.ResetObservationCounts();

            currentService.ApplyOptions(AllVisibleOptions() with
            {
                HideGrids = true,
                HideSite = true
            });

            Assert.DoesNotContain(currentFilter.VisibleElements, element => element.RawIfcType == "IfcGrid");
            Assert.Contains(staleFilter.VisibleElements, element => element.RawIfcType == "IfcGrid");
            Assert.Equal(1, currentFilter.ApplyLoadOptionsCount);
            Assert.Equal(0, staleFilter.ApplyLoadOptionsCount);
            Assert.True(currentService.GetCurrentOptions().HideSite);
            Assert.False(staleService.GetCurrentOptions().HideSite);
        }

        [Fact]
        public void HomeAndIfcFilterButtonsUseSameCommandAndEnabledState()
        {
            var homeFilter = GetSingleFilterButton("CraneOptimizer.UI/Features/F10_ProjectManagement/Views/HomeTabView.xaml");
            var ifcFilter = GetSingleFilterButton("CraneOptimizer.UI/Features/F2_IfcViewer/Views/IfcTabView.xaml");

            Assert.Equal(
                homeFilter.Attribute("Command")?.Value,
                ifcFilter.Attribute("Command")?.Value);
            Assert.Equal(
                homeFilter.Attribute("IsEnabled")?.Value,
                ifcFilter.Attribute("IsEnabled")?.Value);
            Assert.Equal("{Binding ElementManagement.ShowFilterDialogCommand}", ifcFilter.Attribute("Command")?.Value);
            Assert.Equal("{Binding IfcViewer.HasIfcModel}", ifcFilter.Attribute("IsEnabled")?.Value);
        }

        [Fact]
        public void IfcTabHasFinalButtonOrderAndNoMergeRibbonEntry()
        {
            var xaml = ReadRepoFile("CraneOptimizer.UI/Features/F2_IfcViewer/Views/IfcTabView.xaml");

            Assert.DoesNotContain("Content=\"Merge IFC\"", xaml);
            Assert.True(xaml.IndexOf("Content=\"Load IFC\"", StringComparison.Ordinal) < xaml.IndexOf("Content=\"Move IFC\"", StringComparison.Ordinal));
            Assert.True(xaml.IndexOf("Content=\"Move IFC\"", StringComparison.Ordinal) < xaml.IndexOf("Content=\"Delete IFC\"", StringComparison.Ordinal));
            Assert.True(xaml.IndexOf("Content=\"Delete IFC\"", StringComparison.Ordinal) < xaml.IndexOf("ShowIfcDisplayOptionsCommand", StringComparison.Ordinal));
            Assert.True(xaml.IndexOf("ShowIfcDisplayOptionsCommand", StringComparison.Ordinal) < xaml.IndexOf("Content=\"Filter\"", StringComparison.Ordinal));
        }

        [Fact]
        public void FilterAndDisplayOptionsBindToLoadedIfcState()
        {
            var ifcXaml = ReadRepoFile("CraneOptimizer.UI/Features/F2_IfcViewer/Views/IfcTabView.xaml");
            var homeXaml = ReadRepoFile("CraneOptimizer.UI/Features/F10_ProjectManagement/Views/HomeTabView.xaml");

            Assert.Contains("Command=\"{Binding IfcViewer.ShowIfcDisplayOptionsCommand}\"", ifcXaml);
            Assert.Contains("IsEnabled=\"{Binding IfcViewer.HasIfcModel}\"", ifcXaml);
            Assert.Contains("IsEnabled=\"{Binding IfcViewer.HasIfcModel}\"", homeXaml);
        }

        private static XElement GetSingleFilterButton(string relativePath)
        {
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var document = XDocument.Load(GetRepoFile(relativePath));
            return Assert.Single(
                document.Descendants(ns + "Button"),
                element => element.Attribute("Content")?.Value == "Filter");
        }

        private static string ReadRepoFile(string relativePath) => File.ReadAllText(GetRepoFile(relativePath));

        private static string GetRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
        }

        private static FakeElementVisibilityFilterService CreateFilter()
        {
            return new FakeElementVisibilityFilterService(new[]
            {
                Element("grid", "IfcGrid"),
                Element("level", "IfcBuildingStorey", "Reference Geometry"),
                Element("annotation", "IfcAnnotation"),
                Element("site", "IfcSite"),
                Element("space", "IfcSpace"),
                Element("opening", "IfcOpeningElement"),
                Element("virtual", "IfcVirtualElement"),
                Element("wall", "IfcWall")
            });
        }

        private static IfcLoadOptionsDto AllVisibleOptions()
        {
            return IfcVisibilityOptionsCatalog.CreateDefaultOptions() with
            {
                HideGrids = false,
                HideLevels = false,
                HideAnnotations = false,
                HideSite = false,
                HideSpaces = false,
                HideOpenings = false,
                HideVirtualElements = false
            };
        }

        private static IfcOptionsDialogViewModel CreateDialogViewModel(
            IfcOptionsMode mode,
            IfcLoadOptionsDto options)
        {
            return new IfcOptionsDialogViewModel(
                mode,
                "model.ifc",
                options,
                IfcVisibilityOptionsCatalog.Definitions);
        }

        private static void AssertAllVisibilityOptions(IfcLoadOptionsDto options, bool expected)
        {
            Assert.Equal(expected, options.HideGrids);
            Assert.Equal(expected, options.HideLevels);
            Assert.Equal(expected, options.HideAnnotations);
            Assert.Equal(expected, options.HideSite);
            Assert.Equal(expected, options.HideSpaces);
            Assert.Equal(expected, options.HideOpenings);
            Assert.Equal(expected, options.HideVirtualElements);
        }

        private static PrecastElement Element(string id, string ifcType)
        {
            return Element(id, ifcType, ifcType);
        }

        private static PrecastElement Element(string id, string rawIfcType, string elementType)
        {
            return new PrecastElement
            {
                ElementID = id,
                RawIfcType = rawIfcType,
                ElementType = elementType
            };
        }

        private sealed class FakeElementVisibilityFilterService : IElementVisibilityFilterService
        {
            private readonly List<PrecastElement> _allElements;
            private readonly HashSet<string> _enabledTypes = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _excludedElementIds = new(StringComparer.Ordinal);
            private readonly List<PrecastElement> _visibleElements = new();
            private IfcLoadOptionsDto? _activeIfcVisibilityOptions;

            public FakeElementVisibilityFilterService(IEnumerable<PrecastElement> elements)
            {
                _allElements = elements.ToList();
                ElementTypes = BuildElementTypes();
                ResetFilters();
            }

            public event Action? StateChanged;
            public IReadOnlyList<PrecastElement> AllElements => _allElements;
            public IReadOnlyList<PrecastElement> VisibleElements => _visibleElements;
            public IReadOnlyList<string> ElementTypes { get; private set; }
            public IReadOnlyList<string> Materials => Array.Empty<string>();
            public IReadOnlyList<string> Storeys => Array.Empty<string>();
            public int ShowingCount => _visibleElements.Count;
            public int TotalCount => _allElements.Count;
            public double AvailableMinWeightKg => 0;
            public double AvailableMaxWeightKg => 0;
            public double MinWeightKg => 0;
            public double MaxWeightKg => 0;
            public string SearchText => string.Empty;
            public int ApplyLoadOptionsCount { get; private set; }
            public int SetElementsCallCount { get; private set; }
            public int StateChangedCount { get; private set; }

            public void ResetObservationCounts()
            {
                ApplyLoadOptionsCount = 0;
                SetElementsCallCount = 0;
                StateChangedCount = 0;
            }

            public void SetElements(IEnumerable<PrecastElement> elements)
            {
                SetElementsCallCount++;
                _allElements.Clear();
                _allElements.AddRange(elements);
                ElementTypes = BuildElementTypes();
                ResetFilters();
            }

            public void SetSearchText(string text) => throw new NotSupportedException();
            public void SetTypeEnabled(string type, bool enabled)
            {
                if (enabled)
                    _enabledTypes.Add(type);
                else
                    _enabledTypes.Remove(type);

                Recalculate();
            }

            public void SetMaterialEnabled(string material, bool enabled) => throw new NotSupportedException();
            public void SetStoreyEnabled(string storey, bool enabled) => throw new NotSupportedException();
            public void SetWeightRange(double minWeightKg, double maxWeightKg) => throw new NotSupportedException();
            public void SetIncludedElementIds(IEnumerable<string>? elementIds) => throw new NotSupportedException();
            public void SetExcludedElementIds(IEnumerable<string>? elementIds)
            {
                _excludedElementIds.Clear();
                foreach (string id in elementIds ?? Array.Empty<string>())
                    _excludedElementIds.Add(id);

                Recalculate();
            }

            public void ResetFilters()
            {
                _enabledTypes.Clear();
                foreach (string type in ElementTypes)
                    _enabledTypes.Add(type);

                _excludedElementIds.Clear();
                Recalculate();
            }

            public void ShowAll() => ResetFilters();
            public void ShowElements(IEnumerable<PrecastElement> elements) => throw new NotSupportedException();
            public void HideElements(IEnumerable<PrecastElement> elements) => throw new NotSupportedException();
            public void IsolateElements(IEnumerable<PrecastElement> elements) => throw new NotSupportedException();
            public void ApplyLoadOptions(IfcLoadOptionsDto options)
            {
                ApplyLoadOptionsCount++;
                _activeIfcVisibilityOptions = options;
                Recalculate();
            }

            public string GetStorey(PrecastElement element) => "Unassigned";
            public bool IsTypeEnabled(string type) => _enabledTypes.Contains(type);
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
            public bool IsElementIncluded(PrecastElement element) => !IsElementHidden(element);
            public bool IsElementHidden(PrecastElement element) => !_visibleElements.Contains(element);
            public bool IsElementExplicitlyHidden(PrecastElement element) => !_visibleElements.Contains(element);

            private IReadOnlyList<string> BuildElementTypes()
            {
                return _allElements.Select(element => element.ElementType ?? string.Empty)
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            private void Recalculate()
            {
                _visibleElements.Clear();
                _visibleElements.AddRange(_allElements.Where(IsVisible));
                StateChangedCount++;
                StateChanged?.Invoke();
            }

            private bool IsVisible(PrecastElement element)
            {
                if (_activeIfcVisibilityOptions != null &&
                    IfcVisibilityOptionsCatalog.IsHiddenByOptions(_activeIfcVisibilityOptions, element))
                    return false;

                if (string.IsNullOrWhiteSpace(element.ElementID))
                    return false;

                return !_excludedElementIds.Contains(element.ElementID) &&
                       _enabledTypes.Contains(element.ElementType ?? string.Empty);
            }
        }

        private sealed class FakeViewportInvalidationObserver
        {
            public FakeViewportInvalidationObserver(IElementVisibilityFilterService filter)
            {
                filter.StateChanged += ReRenderElements;
            }

            public string CameraState { get; set; } = "unchanged-camera";
            public int ReRenderElementsCount { get; private set; }
            public int ZoomExtentsCount { get; private set; }

            public void ResetObservationCounts()
            {
                ReRenderElementsCount = 0;
                ZoomExtentsCount = 0;
            }

            private void ReRenderElements()
            {
                ReRenderElementsCount++;
            }
        }
    }
}
