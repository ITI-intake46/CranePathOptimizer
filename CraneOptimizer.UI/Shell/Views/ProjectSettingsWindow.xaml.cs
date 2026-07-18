using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CraneOptimizer.UI.Features.F1_IfcViewerControl.Views;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.Shell.Views;

public partial class ProjectSettingsWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly IfcViewerControl _viewer;
    private bool _isInitializing = true;

    public ProjectSettingsWindow(MainViewModel mainViewModel, IfcViewerControl viewer)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _viewer = viewer;
        DataContext = mainViewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshGridControls();
        _isInitializing = false;
    }

    private void UnitSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || UnitSelector.SelectedItem is not string selectedUnit)
            return;

        _mainViewModel.SelectedUnit = selectedUnit;
        RefreshGridControls();
    }

    private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || ShowGridCheckBox.IsChecked is not bool isVisible)
            return;

        _viewer.SetGridVisible(isVisible);
    }

    private void ApplyGridSize_Click(object sender, RoutedEventArgs e)
    {
        if (_viewer.ApplyManualGridSize(GridSizeXTextBox.Text, GridSizeYTextBox.Text))
            RefreshGridControls();
    }

    private void AutoGridSize_Click(object sender, RoutedEventArgs e)
    {
        _viewer.AutoSizeGrid();
        RefreshGridControls();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshGridControls()
    {
        GridSizeXTextBox.Text = _viewer.CurrentGridWidthDisplayText;
        GridSizeYTextBox.Text = _viewer.CurrentGridLengthDisplayText;
        GridUnitXLabel.Text = _viewer.CurrentGridUnitSymbol;
        GridUnitYLabel.Text = _viewer.CurrentGridUnitSymbol;
        ShowGridCheckBox.IsChecked = _viewer.IsGridVisible;
    }
}
