using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.AI.Host;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Shared.Layouts
{
    public partial class TopNavigation : UserControl
    {
        private readonly IUiInteractionContextService? _uiContext;

        public TopNavigation()
        {
            InitializeComponent();
            _uiContext = App.ServiceProvider?.GetService<IUiInteractionContextService>();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_uiContext != null)
                _uiContext.ActionRequested += OnAiActionRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_uiContext != null)
                _uiContext.ActionRequested -= OnAiActionRequested;
        }

        private void OnAiActionRequested(object? sender, AiUiActionRequestedEventArgs e)
        {
            if (!string.Equals(e.ActionType, "NavigateTab", StringComparison.OrdinalIgnoreCase))
                return;

            Dispatcher.Invoke(() => SelectTab(e.Target));
        }

        private void SelectTab(string tabName)
        {
            var normalized = (tabName ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Contains("IFC")) TabIFC.IsChecked = true;
            else if (normalized.Contains("DXF")) TabDXF.IsChecked = true;
            else if (normalized.Contains("CRANE")) TabCranes.IsChecked = true;
            else if (normalized.Contains("ZONE")) TabZones.IsChecked = true;
            else if (normalized.Contains("ANALY")) TabAnalyze.IsChecked = true;
            else if (normalized.Contains("REPORT")) TabAnalyze.IsChecked = true;
            else if (IsLegacyAiWorkspaceTarget(normalized)) return;
            else if (normalized.Contains("SETTING")) TabSettings.IsChecked = true;
            else if (normalized.Contains("COORDINATE")) TabCoordinateReference.IsChecked = true;
            else TabHome.IsChecked = true;
        }


        private static bool IsLegacyAiWorkspaceTarget(string normalizedTarget)
        {
            return normalizedTarget.Contains("COPILOT", StringComparison.Ordinal)
                || normalizedTarget.Contains("AI STUDIO", StringComparison.Ordinal)
                || normalizedTarget.Contains("AI ASSIST", StringComparison.Ordinal)
                || normalizedTarget.Contains("AIADVISOR", StringComparison.Ordinal)
                || normalizedTarget.Equals("AI", StringComparison.Ordinal)
                || normalizedTarget.Equals("STUDIO", StringComparison.Ordinal);
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            // Guard: elements may not yet be ready during InitializeComponent
            if (PanelHome == null) return;

            // Hide all panels
            PanelHome.Visibility    = Visibility.Collapsed;
            PanelIFC.Visibility     = Visibility.Collapsed;
            PanelDXF.Visibility     = Visibility.Collapsed;
            PanelCranes.Visibility  = Visibility.Collapsed;
            PanelZones.Visibility   = Visibility.Collapsed;
            PanelAnalyze.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Collapsed;
            PanelCoordinateReference.Visibility = Visibility.Collapsed;

            // Show the selected panel and update the shared UI context used by the AI companion.
            if (sender == TabHome)
            {
                PanelHome.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("HOME", "Home");
            }
            if (sender == TabIFC)
            {
                PanelIFC.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("IFC MODEL", "IfcTabView");
            }
            if (sender == TabDXF)
            {
                PanelDXF.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("DXF", "DXFTabView");
            }
            if (sender == TabCranes)
            {
                PanelCranes.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("CRANES", "CranesTabView");
            }
            if (sender == TabZones)
            {
                PanelZones.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("ZONES", "ZonesTabView");
            }
            if (sender == TabAnalyze)
            {
                PanelAnalyze.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("ANALYSIS", "AnalyzeTabView");
            }
            if (sender == TabSettings)
            {
                PanelSettings.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("SETTINGS", "UserSettingsView");
            }
            if (sender == TabCoordinateReference)
            {
                PanelCoordinateReference.Visibility = Visibility.Visible;
                _uiContext?.SetActiveTab("COORDINATE REFERENCE", "CoordinateReferenceView");
            }
        }
    }
}
