using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF_IFC_test1.Main_Application_UserControl
{
    public partial class MainRibbonControl : UserControl
    {
        // ── Events forwarded up to MainWindow ─────────────────────────────
        public event Action OnNewProject;
        public event Action OnOpenProject;
        public event Action OnSaveProject;
        public event Action OnProjectSettings;
        public event Action OnLoadIFC;
        public event Action OnMergeIFC;
        public event Action OnToggleCOG;
        public event Action OnFilterElements;
        public event Action OnLoadDXF;
        public event Action OnAlignDXF;
        public event Action OnAddTowerCrane;
        public event Action OnAddMobileCrane;
        public event Action OnRemoveCrane;
        public event Action OnRemoveAllCranes;
        public event Action OnEnterPlanView;
        public event Action OnReturnTo3D;
        public event Action OnFitToSite;
        public event Action OnToggleGrid;
        public event Action OnToggleFlatZones;
        public event Action<ZoneTabEventArgs> OnZoneAction;

        // FIX: New measure events forwarded to MainWindow
        public event Action OnStartMeasure;
        public event Action OnCancelMeasure;

        // ── NEW: Load chart events ────────────────────────────────────────────
        public event Action OnImportLoadChart;
        public event Action OnViewLoadCharts;

        // ── Crane catalog, compare and lift plan events ───────────────────────
        public event Action OnOpenCatalog;
        public event Action OnCompareCharts;
        public event Action OnOpenLiftPlan;
        public event Action OnRunOptimizer;

        private UserControl[] _panels;

        public MainRibbonControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _panels = new UserControl[]
            {
                PanelHome, PanelIFC, PanelCrane,
                PanelZone, PanelView, PanelAnalyze
            };

            // ── Wire HOME tab ─────────────────────────────────────────────
            PanelHome.OnNewProject += () => OnNewProject?.Invoke();
            PanelHome.OnOpenProject += () => OnOpenProject?.Invoke();
            PanelHome.OnSaveProject += () => OnSaveProject?.Invoke();
            PanelHome.OnProjectSettings += () => OnProjectSettings?.Invoke();

            // ── Wire IFC tab ──────────────────────────────────────────────
            PanelIFC.OnLoadIFC += () => OnLoadIFC?.Invoke();
            PanelIFC.OnMergeIFC += () => OnMergeIFC?.Invoke();
            PanelIFC.OnToggleCOG += () => OnToggleCOG?.Invoke();
            PanelIFC.OnFilterElements += () => OnFilterElements?.Invoke();
            PanelIFC.OnLoadDXF += () => OnLoadDXF?.Invoke();
            PanelIFC.OnAlignDXF += () => OnAlignDXF?.Invoke();

            // ── Wire CRANE tab ────────────────────────────────────────────
            PanelCrane.OnAddTowerCrane += () => OnAddTowerCrane?.Invoke();
            PanelCrane.OnAddMobileCrane += () => OnAddMobileCrane?.Invoke();
            PanelCrane.OnRemoveCrane += () => OnRemoveCrane?.Invoke();
            PanelCrane.OnRemoveAllCranes += () => OnRemoveAllCranes?.Invoke();

            // ── NEW: wire load chart events from CraneRibbonTab up to MainWindow ──
            PanelCrane.OnImportLoadChart += () => OnImportLoadChart?.Invoke();
            PanelCrane.OnViewLoadCharts += () => OnViewLoadCharts?.Invoke();
            PanelCrane.OnOpenCatalog += () => OnOpenCatalog?.Invoke();
            PanelCrane.OnCompareCharts += () => OnCompareCharts?.Invoke();

            // ── Wire ANALYZE tab ──────────────────────────────────────────────
            PanelAnalyze.OnOpenLiftPlan   += () => OnOpenLiftPlan?.Invoke();
            PanelAnalyze.OnRunOptimizer   += () => OnRunOptimizer?.Invoke();

            // ── Wire ZONE tab ─────────────────────────────────────────────
            PanelZone.OnZoneAction += args => OnZoneAction?.Invoke(args);
            PanelZone.OnEnterPlanView += () => OnEnterPlanView?.Invoke();
            PanelZone.OnReturnTo3D += () => OnReturnTo3D?.Invoke();

            // FIX: Wire measure events from zone tab up to MainWindow
            PanelZone.OnStartMeasure += () => OnStartMeasure?.Invoke();
            PanelZone.OnCancelMeasure += () => OnCancelMeasure?.Invoke();

            // ── Wire VIEW tab ─────────────────────────────────────────────
            PanelView.OnEnterPlanView += () => OnEnterPlanView?.Invoke();
            PanelView.OnReturnTo3D += () => OnReturnTo3D?.Invoke();
            PanelView.OnFitToSite += () => OnFitToSite?.Invoke();
            PanelView.OnToggleGrid += () => OnToggleGrid?.Invoke();
            PanelView.OnToggleFlatZones += () => OnToggleFlatZones?.Invoke();
        }

        // ── Tab switching ──────────────────────────────────────────────────
        private void TabButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_panels == null) return;

            var btns = new[] { BtnHome, BtnIFC, BtnCrane, BtnZone, BtnView, BtnAnalyze };
            var pans = new UserControl[] { PanelHome, PanelIFC, PanelCrane, PanelZone, PanelView, PanelAnalyze };

            for (int i = 0; i < btns.Length; i++)
                pans[i].Visibility = (btns[i] == sender) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Public helpers called by MainWindow ────────────────────────────
        public void SwitchToZoneTab() => BtnZone.IsChecked = true;
        public void SwitchToCraneTab() => BtnCrane.IsChecked = true;
        public void SwitchToIFCTab() => BtnIFC.IsChecked = true;

        public WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs.ZoneRibbonTab ZoneTab => PanelZone;
        public WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs.IFCRibbonTab IFCTab => PanelIFC;

        /// <summary>
        /// FIX: Call this from MainWindow.Window_PreviewKeyDown when Escape is pressed
        /// so that keyboard events captured by the 3D viewport still cancel the active mode.
        /// Example:
        ///   protected override void OnPreviewKeyDown(KeyEventArgs e) {
        ///       if (e.Key == Key.Escape) { Ribbon.ForwardEscape(); e.Handled = true; }
        ///   }
        /// </summary>
        public void ForwardEscape()
        {
            PanelZone.CancelCurrentMode();
        }

        /// <summary>
        /// FIX: Called by MainWindow viewport controller after the user picks two measure points.
        /// Passes the result back to ZoneRibbonTab so it shows the distance number on the button.
        /// </summary>
        public void NotifyMeasureResult(double distanceMetres)
        {
            PanelZone.ShowMeasureResult(distanceMetres);
        }
    }
}