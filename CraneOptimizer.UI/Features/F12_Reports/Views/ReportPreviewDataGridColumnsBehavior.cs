using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;

namespace CraneOptimizer.UI.Features.F12_Reports.Views;

public static class ReportPreviewDataGridColumnsBehavior
{
    private static readonly DependencyProperty InternalScrollViewerProperty =
        DependencyProperty.RegisterAttached(
            "InternalScrollViewer",
            typeof(ScrollViewer),
            typeof(ReportPreviewDataGridColumnsBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColumnsSourceProperty =
        DependencyProperty.RegisterAttached(
            "ColumnsSource",
            typeof(IEnumerable),
            typeof(ReportPreviewDataGridColumnsBehavior),
            new PropertyMetadata(null, OnColumnsSourceChanged));

    public static readonly DependencyProperty ExternalHorizontalScrollBarProperty =
        DependencyProperty.RegisterAttached(
            "ExternalHorizontalScrollBar",
            typeof(ScrollBar),
            typeof(ReportPreviewDataGridColumnsBehavior),
            new PropertyMetadata(null, OnExternalHorizontalScrollBarChanged));

    public static void SetColumnsSource(DependencyObject element, IEnumerable? value) =>
        element.SetValue(ColumnsSourceProperty, value);

    public static IEnumerable? GetColumnsSource(DependencyObject element) =>
        (IEnumerable?)element.GetValue(ColumnsSourceProperty);

    public static void SetExternalHorizontalScrollBar(DependencyObject element, ScrollBar? value) =>
        element.SetValue(ExternalHorizontalScrollBarProperty, value);

    public static ScrollBar? GetExternalHorizontalScrollBar(DependencyObject element) =>
        (ScrollBar?)element.GetValue(ExternalHorizontalScrollBarProperty);

    private static void OnColumnsSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not DataGrid dataGrid)
            return;

        dataGrid.Columns.Clear();

        if (args.NewValue is not IEnumerable columns)
            return;

        foreach (var item in columns)
        {
            if (item is not ReportPreviewColumnViewModel column)
                continue;

            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding($"[{column.Key}]"),
                Width = new DataGridLength(column.Width, DataGridLengthUnitType.Pixel),
                MinWidth = column.MinWidth,
                ElementStyle = CreateTextElementStyle()
            });
        }

        dataGrid.Dispatcher.BeginInvoke(
            new Action(() => UpdateExternalScrollBar(dataGrid)),
            DispatcherPriority.Loaded);
    }

    private static void OnExternalHorizontalScrollBarChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not DataGrid dataGrid)
            return;

        dataGrid.Loaded -= DataGrid_Loaded;
        dataGrid.Loaded += DataGrid_Loaded;
        dataGrid.LayoutUpdated -= DataGrid_LayoutUpdated;
        dataGrid.LayoutUpdated += DataGrid_LayoutUpdated;

        if (args.OldValue is ScrollBar oldScrollBar)
            oldScrollBar.ValueChanged -= ExternalScrollBar_ValueChanged;

        if (args.NewValue is ScrollBar newScrollBar)
        {
            newScrollBar.ValueChanged -= ExternalScrollBar_ValueChanged;
            newScrollBar.ValueChanged += ExternalScrollBar_ValueChanged;
            newScrollBar.Tag = dataGrid;
        }

        dataGrid.Dispatcher.BeginInvoke(
            new Action(() => UpdateExternalScrollBar(dataGrid)),
            DispatcherPriority.Loaded);
    }

    private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
            UpdateExternalScrollBar(dataGrid);
    }

    private static void DataGrid_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is DataGrid dataGrid)
            UpdateExternalScrollBar(dataGrid);
    }

    private static void ExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not ScrollBar { Tag: DataGrid dataGrid })
            return;

        var scrollViewer = GetInternalScrollViewer(dataGrid) ?? FindDescendant<ScrollViewer>(dataGrid);
        scrollViewer?.ScrollToHorizontalOffset(e.NewValue);
    }

    private static void InternalScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        var dataGrid = FindAncestor<DataGrid>(scrollViewer);
        if (dataGrid == null)
            return;

        UpdateExternalScrollBar(dataGrid);
    }

    private static void UpdateExternalScrollBar(DataGrid dataGrid)
    {
        var externalScrollBar = GetExternalHorizontalScrollBar(dataGrid);
        if (externalScrollBar == null)
            return;

        var scrollViewer = GetInternalScrollViewer(dataGrid) ?? FindDescendant<ScrollViewer>(dataGrid);
        if (scrollViewer == null)
            return;

        if (!ReferenceEquals(GetInternalScrollViewer(dataGrid), scrollViewer))
        {
            scrollViewer.ScrollChanged -= InternalScrollViewer_ScrollChanged;
            scrollViewer.ScrollChanged += InternalScrollViewer_ScrollChanged;
            dataGrid.SetValue(InternalScrollViewerProperty, scrollViewer);
        }

        var maximumOffset = Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
        externalScrollBar.Minimum = 0;
        externalScrollBar.Maximum = maximumOffset;
        externalScrollBar.ViewportSize = Math.Max(0, scrollViewer.ViewportWidth);
        externalScrollBar.LargeChange = Math.Max(16, scrollViewer.ViewportWidth * 0.85);
        externalScrollBar.SmallChange = 32;
        externalScrollBar.IsEnabled = maximumOffset > 0.5;

        if (Math.Abs(externalScrollBar.Value - scrollViewer.HorizontalOffset) > 0.5)
            externalScrollBar.Value = Math.Min(maximumOffset, scrollViewer.HorizontalOffset);
    }

    private static Style CreateTextElementStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0)));
        return style;
    }

    private static ScrollViewer? GetInternalScrollViewer(DependencyObject element) =>
        (ScrollViewer?)element.GetValue(InternalScrollViewerProperty);

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var descendant = FindDescendant<T>(child);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current != null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
