using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.ViewModels
{
    /// <summary>
    /// ViewModel for the OSnap status panel.
    ///
    /// Binds the "OSN ▾" button + popup panel in MainWindow's status bar.
    /// All properties are two-way bindable so that both the Ribbon toggles
    /// and the status-bar popup always stay in sync (fixing the UI sync bug).
    /// </summary>
    public sealed class OsnapPanelViewModel : ViewModelBase
    {
        private readonly IOsnapService         _snapService;
        private readonly OsnapSettingsService  _settingsService;

        // ─── Constructor ───────────────────────────────────────────────────────
        public OsnapPanelViewModel(IOsnapService snapService, OsnapSettingsService settingsService)
        {
            _snapService     = snapService;
            _settingsService = settingsService;

            // Sync initial UI state from the already-loaded settings
            SyncFromSettings();

            ToggleMasterCommand     = new RelayCommand(ToggleMaster);
            ResetToDefaultsCommand  = new RelayCommand(ResetToDefaults);
            SaveSettingsCommand     = new RelayCommand(SaveSettings);

            _snapService.MasterEnabledChanged += enabled =>
            {
                if (_masterEnabled != enabled)
                {
                    _masterEnabled = enabled;
                    OnPropertyChanged(nameof(MasterEnabled));
                    OnPropertyChanged(nameof(OsnButtonLabel));
                    OnPropertyChanged(nameof(IsAnySnapActive));
                }
            };
        }

        // ─── Commands ──────────────────────────────────────────────────────────
        public ICommand ToggleMasterCommand    { get; }
        public ICommand ResetToDefaultsCommand { get; }
        public ICommand SaveSettingsCommand    { get; }

        // ─── Master toggle ─────────────────────────────────────────────────────
        private bool _masterEnabled;
        public bool MasterEnabled
        {
            get => _masterEnabled;
            set
            {
                if (_masterEnabled == value) return;
                _masterEnabled = value;
                _snapService.SetMasterEnabled(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(OsnButtonLabel));
                OnPropertyChanged(nameof(IsAnySnapActive));
            }
        }

        /// <summary>Status-bar button label changes colour/text based on master state.</summary>
        public string OsnButtonLabel => MasterEnabled ? "OSN" : "OSN";

        // ─── Individual snap flags (auto-propagate to service) ────────────────
        private bool _snapEndpoint;
        public bool SnapEndpoint
        {
            get => _snapEndpoint;
            set { _snapEndpoint = value; _snapService.SetSnapEnabled(OsnapType.Endpoint, value); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapMidpoint;
        public bool SnapMidpoint
        {
            get => _snapMidpoint;
            set { _snapMidpoint = value; _snapService.SetSnapEnabled(OsnapType.Midpoint, value); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapIntersection;
        public bool SnapIntersection
        {
            get => _snapIntersection;
            set { _snapIntersection = value; _snapService.SetSnapEnabled(OsnapType.Intersection, value); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapPerpendicular;
        public bool SnapPerpendicular
        {
            get => _snapPerpendicular;
            set { _snapPerpendicular = false; _snapService.SetSnapEnabled(OsnapType.Perpendicular, false); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapNearest;
        public bool SnapNearest
        {
            get => _snapNearest;
            set { _snapNearest = false; _snapService.SetSnapEnabled(OsnapType.Nearest, false); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapGrid;
        public bool SnapGrid
        {
            get => _snapGrid;
            set { _snapGrid = false; _snapService.SetSnapEnabled(OsnapType.Grid, false); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapCenter;
        public bool SnapCenter
        {
            get => _snapCenter;
            set { _snapCenter = value; _snapService.SetSnapEnabled(OsnapType.Center, value); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        private bool _snapQuadrant;
        public bool SnapQuadrant
        {
            get => _snapQuadrant;
            set { _snapQuadrant = value; _snapService.SetSnapEnabled(OsnapType.Quadrant, value); OnPropertyChanged(); OnPropertyChanged(nameof(IsAnySnapActive)); }
        }

        // ─── Source toggles ────────────────────────────────────────────────────
        private bool _snapToDxf;
        public bool SnapToDxf
        {
            get => _snapToDxf;
            set { _snapToDxf = value; _snapService.Settings.SnapToDxf = value; OnPropertyChanged(); }
        }

        private bool _snapToIfc;
        public bool SnapToIfc
        {
            get => _snapToIfc;
            set { _snapToIfc = value; _snapService.Settings.SnapToIfc = value; OnPropertyChanged(); }
        }

        // ─── Derived UI helpers ────────────────────────────────────────────────
        /// <summary>True when master is on AND at least one type is enabled — drives the OSN button highlight.</summary>
        public bool IsAnySnapActive =>
            MasterEnabled &&
            (SnapEndpoint || SnapMidpoint || SnapIntersection ||
             SnapCenter || SnapQuadrant);

        // ─── Tuning ────────────────────────────────────────────────────────────
        private double _snapRadiusPx;
        public double SnapRadiusPx
        {
            get => _snapRadiusPx;
            set { _snapRadiusPx = value; _snapService.Settings.SnapRadiusPx = value; OnPropertyChanged(); }
        }

        private double _gridSpacing;
        public double GridSpacing
        {
            get => _gridSpacing;
            set { _gridSpacing = value; _snapService.Settings.GridSpacing = value; OnPropertyChanged(); }
        }

        // ─── Private helpers ───────────────────────────────────────────────────
        /// <summary>Public helper so MainWindow legacy stubs can call via code-behind if needed.</summary>
        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            switch (type)
            {
                case OsnapType.Endpoint:      SnapEndpoint      = enabled; break;
                case OsnapType.Midpoint:      SnapMidpoint      = enabled; break;
                case OsnapType.Intersection:  SnapIntersection  = enabled; break;
                case OsnapType.Perpendicular: SnapPerpendicular = false; break;
                case OsnapType.Nearest:       SnapNearest       = false; break;
                case OsnapType.Grid:          SnapGrid          = false; break;
                case OsnapType.Center:        SnapCenter        = enabled; break;
                case OsnapType.Quadrant:      SnapQuadrant      = enabled; break;
            }
        }

        private void ToggleMaster() => MasterEnabled = !MasterEnabled;

        private void ResetToDefaults()
        {
            var defaults = new OsnapSettings();
            ApplySettings(defaults);
            _settingsService.Save(_snapService.Settings);
        }

        private void SaveSettings() => _settingsService.Save(_snapService.Settings);

        private void SyncFromSettings()
        {
            var s = _snapService.Settings;
            _masterEnabled      = s.MasterEnabled;
            _snapEndpoint       = s.SnapEndpoint;
            _snapMidpoint       = s.SnapMidpoint;
            _snapIntersection   = s.SnapIntersection;
            _snapPerpendicular  = false;
            s.SnapPerpendicular = false;
            _snapNearest        = false;
            s.SnapNearest       = false;
            _snapGrid           = false;
            s.SnapGrid          = false;
            _snapCenter         = s.SnapCenter;
            _snapQuadrant       = s.SnapQuadrant;
            _snapToDxf          = s.SnapToDxf;
            _snapToIfc          = s.SnapToIfc;
            _snapRadiusPx       = s.SnapRadiusPx;
            _gridSpacing        = s.GridSpacing;
        }

        private void ApplySettings(OsnapSettings s)
        {
            MasterEnabled      = s.MasterEnabled;
            SnapEndpoint       = s.SnapEndpoint;
            SnapMidpoint       = s.SnapMidpoint;
            SnapIntersection   = s.SnapIntersection;
            SnapPerpendicular  = false;
            SnapNearest        = false;
            SnapGrid           = false;
            SnapCenter         = s.SnapCenter;
            SnapQuadrant       = s.SnapQuadrant;
            SnapToDxf          = s.SnapToDxf;
            SnapToIfc          = s.SnapToIfc;
            SnapRadiusPx       = s.SnapRadiusPx;
            GridSpacing        = s.GridSpacing;
        }
    }
}
