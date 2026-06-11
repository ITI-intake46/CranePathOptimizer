using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.DTOs;
using CraneOptimizer.UI.Features.F3_CraneSetup.Mappers;
using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels
{
    public class CraneManagerViewModel : ViewModelBase
    {
        // ── Active parameters (bound to the right-panel sliders) ──────────
        private readonly ISceneService _scene;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Dictionary<int, Crane> _availableCraneLookup = new();
        private bool _suppressDatabaseSelection;
        private MobileCraneParameters? _activeParams;
        private CranePickerDto? _selectedDatabaseCrane;

        public ObservableCollection<CranePickerDto> AvailableCranes { get; } = new();

        public CranePickerDto? SelectedDatabaseCrane
        {
            get => _selectedDatabaseCrane;
            set
            {
                if (_selectedDatabaseCrane == value)
                    return;

                _selectedDatabaseCrane = value;
                OnPropertyChanged();

                if (!_suppressDatabaseSelection && value != null)
                    ApplyDatabaseCrane(value);
            }
        }
        public MobileCraneParameters? ActiveParams
        {
            get => _activeParams;
            private set { _activeParams = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActiveCrane)); }
        }

        public bool HasActiveCrane => _activeParams != null;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand RemoveSelectedCraneCommand  { get; }
        public ICommand RemoveAllCranesCommand      { get; }
        public ICommand ToggleSwingRadiusCommand    { get; }
        public ICommand ToggleOutriggerPadsCommand  { get; }

        // ── External actions wired by MainViewModel ────────────────────────
        public Action? RequestRemoveSelected  { get; set; }
        public Action? RequestRemoveAll       { get; set; }

        public CraneManagerViewModel(ISceneService scene, IServiceScopeFactory scopeFactory)
        {
            _scene = scene;
            _scopeFactory = scopeFactory;

            RemoveSelectedCraneCommand = new RelayCommand(
                () => RequestRemoveSelected?.Invoke(),
                () => HasActiveCrane);

            RemoveAllCranesCommand = new RelayCommand(
                () => RequestRemoveAll?.Invoke());

            ToggleSwingRadiusCommand = new RelayCommand(() =>
            {
                if (ActiveParams != null)
                    ActiveParams.ShowSwingRadius = !ActiveParams.ShowSwingRadius;
            }, () => HasActiveCrane);

            ToggleOutriggerPadsCommand = new RelayCommand(() =>
            {
                if (ActiveParams != null)
                    ActiveParams.ShowOutriggerPads = !ActiveParams.ShowOutriggerPads;
            }, () => HasActiveCrane);

            _ = ReloadCranesAsync();
        }

        /// <summary>
        /// Called by MainViewModel after a crane is placed or selected —
        /// pushes the live parameters into the panel so sliders reflect the
        /// correct crane immediately.
        /// </summary>
        public void UpdateActiveParams(MobileCraneParameters? p)
        {
            if (ActiveParams != null)
                ActiveParams.PropertyChanged -= OnActiveParamsChanged;

            ActiveParams = p;

            if (ActiveParams != null)
                ActiveParams.PropertyChanged += OnActiveParamsChanged;

            SyncSelectedDatabaseCrane();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        public async Task ReloadCranesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICraneRepository>();
            var cranes = await repo.GetAllWithSpecsAsync();

            _availableCraneLookup.Clear();
            AvailableCranes.Clear();
            foreach (var crane in cranes)
            {
                _availableCraneLookup[crane.Id] = crane;
                AvailableCranes.Add(crane.ToCranePickerDto());
            }

            RefreshPlacedCranes();
            SyncSelectedDatabaseCrane();
        }

        private void RefreshPlacedCranes()
        {
            foreach (SceneCrane placedCrane in _scene.GetCranes())
            {
                if (!_availableCraneLookup.TryGetValue(placedCrane.CraneDefinition.Id, out Crane? refreshedCrane))
                    continue;

                ApplyCraneDefinition(placedCrane, refreshedCrane);
                _scene.UpdateCrane(placedCrane);
            }
        }

        private void SyncSelectedDatabaseCrane()
        {
            var selectedSceneCrane = _scene.SelectedCrane;
            var databaseCrane = selectedSceneCrane == null
                ? null
                : AvailableCranes.FirstOrDefault(crane => crane.Id == selectedSceneCrane.CraneDefinition.Id);

            _suppressDatabaseSelection = true;
            SelectedDatabaseCrane = databaseCrane;
            _suppressDatabaseSelection = false;
        }

        private void ApplyDatabaseCrane(CranePickerDto databaseCrane)
        {
            if (!_availableCraneLookup.TryGetValue(databaseCrane.Id, out var craneEntity))
                return;

            var selected = _scene.SelectedCrane;
            if (selected == null)
                return;

            ApplyCraneDefinition(selected, craneEntity);
            _scene.UpdateCrane(selected);
        }

        internal static void ApplyCraneDefinition(SceneCrane sceneCrane, Crane crane)
        {
            int? activeSpecificationId = sceneCrane.ActiveSpecification?.Id;

            sceneCrane.CraneDefinition = crane;
            sceneCrane.ActiveSpecification = activeSpecificationId.HasValue
                ? crane.Specifications?.FirstOrDefault(specification => specification.Id == activeSpecificationId.Value)
                    ?? ChooseActiveSpecification(crane)
                : ChooseActiveSpecification(crane);
            sceneCrane.PivotOffset = new Domain.Common.DomainPoint3D(
                0,
                0,
                Math.Max(0.2, ToMeters(crane.VehicleHeight, 1.5) * 0.8));

            CraneSpecification? specification = sceneCrane.ActiveSpecification;
            if (specification == null)
                return;

            double minBoomLength = Math.Max(0d, specification.MinBoomLength);
            double maxBoomLength = Math.Max(minBoomLength, specification.MaxBoomLength);
            double requestedBoomLength = sceneCrane.BoomLength > 0d
                ? sceneCrane.BoomLength
                : maxBoomLength * 0.6d;

            sceneCrane.BoomLength = Math.Clamp(requestedBoomLength, minBoomLength, maxBoomLength);
        }

        private static CraneSpecification? ChooseActiveSpecification(Crane crane)
        {
            return crane.Specifications?
                .OrderByDescending(spec => spec.MaxRadius)
                .FirstOrDefault();
        }

        private void OnActiveParamsChanged(object? sender, PropertyChangedEventArgs e)
        {
            var selected = _scene.SelectedCrane;
            var p = ActiveParams;
            if (selected == null || p == null)
                return;

            if (e.PropertyName is nameof(MobileCraneParameters.PositionX)
                or nameof(MobileCraneParameters.PositionY)
                or nameof(MobileCraneParameters.PositionZ))
            {
                if (NearlyEqual(selected.Position.X, p.PositionX)
                    && NearlyEqual(selected.Position.Y, p.PositionY)
                    && NearlyEqual(selected.Position.Z, p.PositionZ))
                    return;

                selected.Position = new Domain.Common.DomainPoint3D(
                    p.PositionX,
                    p.PositionY,
                    p.PositionZ);
                _scene.UpdateCrane(selected);
                return;
            }

            if (e.PropertyName == nameof(MobileCraneParameters.BodyAngle))
            {
                if (NearlyEqual(selected.Rotation, p.BodyAngle))
                    return;

                selected.Rotation = p.BodyAngle;
                _scene.UpdateCrane(selected);
                return;
            }

            if (e.PropertyName == nameof(MobileCraneParameters.SlewAngle))
            {
                if (NearlyEqual(selected.SlewAngle, p.SlewAngle))
                    return;

                selected.SlewAngle = p.SlewAngle;
                _scene.UpdateCrane(selected);
                return;
            }

            if (e.PropertyName == nameof(MobileCraneParameters.BoomLength))
            {
                if (NearlyEqual(selected.BoomLength, p.BoomLength))
                    return;

                selected.BoomLength = p.BoomLength;
                _scene.UpdateCrane(selected);
                return;
            }

            if (e.PropertyName == nameof(MobileCraneParameters.BoomAngle))
            {
                if (NearlyEqual(selected.BoomAngle, p.BoomAngle))
                    return;

                selected.BoomAngle = p.BoomAngle;
                _scene.UpdateCrane(selected);
            }
        }

        private static bool NearlyEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.0001;
        }

        private static double ToMeters(double value, double fallback)
        {
            if (value <= 0)
                return fallback;

            return value;
        }
    }

    // ── Kept for backward compatibility with CraneManagerView list ────────
    public class CraneItemViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string _type = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }
    }
}
