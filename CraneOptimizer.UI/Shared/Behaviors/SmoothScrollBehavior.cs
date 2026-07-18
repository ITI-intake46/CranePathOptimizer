using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shared.Behaviors;

public static class SmoothScrollBehavior
{
    private const double BoundaryTolerance = 0.5;
    private const double LineHeight = 16.0;
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(150));
    private static readonly CubicEase EaseOut = CreateEaseOut();
    private static bool _isInitialized;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new FrameworkPropertyMetadata(true));

    private static readonly DependencyProperty AnimatedVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedVerticalOffset",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(ScrollState),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(null));

    [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            Mouse.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel),
            true);

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(OnPreviewMouseDown),
            true);

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler(OnPreviewKeyDown),
            true);

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnUnloaded),
            true);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer routedScrollViewer ||
            e.Handled ||
            e.Delta == 0 ||
            (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            return;
        }

        DependencyObject? originalSource = e.OriginalSource as DependencyObject;
        ScrollViewer? nearestScrollViewer = FindAncestorOrSelf<ScrollViewer>(originalSource);
        if (!ReferenceEquals(routedScrollViewer, nearestScrollViewer) ||
            nearestScrollViewer is null ||
            !GetIsEnabled(nearestScrollViewer) ||
            IsExcludedInteraction(originalSource, nearestScrollViewer))
        {
            return;
        }

        ScrollViewer? scrollViewer = FindScrollTarget(nearestScrollViewer, e.Delta);
        if (scrollViewer is null)
        {
            return;
        }

        double scrollableHeight = scrollViewer.ScrollableHeight;
        double currentOffset = scrollViewer.VerticalOffset;
        double wheelDistance = GetWheelDistance(scrollViewer);
        if (wheelDistance <= 0.0)
        {
            return;
        }

        ScrollState state = GetState(scrollViewer) ?? CreateState(scrollViewer, currentOffset);

        double baseTarget = state.IsAnimating ? state.TargetOffset : currentOffset;
        baseTarget = Math.Clamp(baseTarget, 0.0, scrollableHeight);

        double wheelNotches = e.Delta / 120.0;
        double targetOffset = Math.Clamp(
            baseTarget - (wheelNotches * wheelDistance),
            0.0,
            scrollableHeight);

        AnimateTo(scrollViewer, state, targetOffset);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollTarget(ScrollViewer start, int wheelDelta)
    {
        ScrollViewer? current = start;
        while (current is not null)
        {
            if (!GetIsEnabled(current))
            {
                return null;
            }

            if (CanScroll(current, wheelDelta))
            {
                return current;
            }

            current = FindAncestor<ScrollViewer>(current);
        }

        return null;
    }

    private static bool CanScroll(ScrollViewer scrollViewer, int wheelDelta)
    {
        double scrollableHeight = scrollViewer.ScrollableHeight;
        if (!double.IsFinite(scrollableHeight) || scrollableHeight <= 0.0)
        {
            return false;
        }

        double currentOffset = scrollViewer.VerticalOffset;
        ScrollState? state = GetState(scrollViewer);
        double targetOffset = state is { IsAnimating: true }
            ? state.TargetOffset
            : currentOffset;

        return wheelDelta > 0
            ? currentOffset > BoundaryTolerance || targetOffset > BoundaryTolerance
            : currentOffset < scrollableHeight - BoundaryTolerance ||
              targetOffset < scrollableHeight - BoundaryTolerance;
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StopAnimation(scrollViewer);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StopAnimation(scrollViewer);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        StopAnimation(scrollViewer);
        scrollViewer.ClearValue(StateProperty);
    }

    private static void AnimateTo(
        ScrollViewer scrollViewer,
        ScrollState state,
        double targetOffset)
    {
        double currentOffset = scrollViewer.VerticalOffset;
        state.TargetOffset = targetOffset;
        state.IsAnimating = true;
        state.AnimationVersion++;
        int animationVersion = state.AnimationVersion;

        scrollViewer.SetValue(AnimatedVerticalOffsetProperty, currentOffset);

        DoubleAnimation animation = new(currentOffset, targetOffset, AnimationDuration)
        {
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };

        animation.Completed += (_, _) =>
            CompleteAnimation(scrollViewer, state, targetOffset, animationVersion);

        scrollViewer.BeginAnimation(
            AnimatedVerticalOffsetProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void CompleteAnimation(
        ScrollViewer scrollViewer,
        ScrollState state,
        double targetOffset,
        int animationVersion)
    {
        if (state.AnimationVersion != animationVersion)
        {
            return;
        }

        state.IsAnimating = false;
        state.TargetOffset = targetOffset;
        scrollViewer.ScrollToVerticalOffset(targetOffset);
        scrollViewer.SetValue(AnimatedVerticalOffsetProperty, targetOffset);
        scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
    }

    private static void StopAnimation(ScrollViewer scrollViewer)
    {
        ScrollState? state = GetState(scrollViewer);
        if (state is null || !state.IsAnimating)
        {
            return;
        }

        double currentOffset = scrollViewer.VerticalOffset;
        state.AnimationVersion++;
        state.IsAnimating = false;
        state.TargetOffset = currentOffset;
        scrollViewer.SetValue(AnimatedVerticalOffsetProperty, currentOffset);
        scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
    }

    private static void OnAnimatedVerticalOffsetChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer scrollViewer ||
            e.NewValue is not double offset ||
            !double.IsFinite(offset))
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(
            Math.Clamp(offset, 0.0, Math.Max(0.0, scrollViewer.ScrollableHeight)));
    }

    private static double GetWheelDistance(ScrollViewer scrollViewer)
    {
        int lines = SystemParameters.WheelScrollLines;
        if (lines == 0)
        {
            return 0.0;
        }

        if (lines < 0)
        {
            return Math.Max(LineHeight, scrollViewer.ViewportHeight * 0.85);
        }

        return Math.Clamp(lines * LineHeight, 32.0, 96.0);
    }

    private static bool IsExcludedInteraction(
        DependencyObject? source,
        ScrollViewer scrollViewer)
    {
        DependencyObject? current = source;
        while (current is not null && !ReferenceEquals(current, scrollViewer))
        {
            if (current is RangeBase or ComboBox or ComboBoxItem or Canvas or Viewport3D)
            {
                return true;
            }

            string typeName = current.GetType().Name;
            if (typeName.Contains("UpDown", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Numeric", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("HelixViewport", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = GetParent(current);
        }

        return FindAncestor<ComboBox>(scrollViewer) is not null;
    }

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        DependencyObject? current = GetParent(start);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? start)
        where T : DependencyObject
    {
        DependencyObject? current = start;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject child)
    {
        if (child is Visual or Visual3D)
        {
            DependencyObject? visualParent = VisualTreeHelper.GetParent(child);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        if (child is FrameworkContentElement { Parent: not null } frameworkContentElement)
        {
            return frameworkContentElement.Parent;
        }

        if (child is FrameworkElement { TemplatedParent: not null } frameworkElement)
        {
            return frameworkElement.TemplatedParent;
        }

        return LogicalTreeHelper.GetParent(child);
    }

    private static ScrollState? GetState(ScrollViewer scrollViewer) =>
        (ScrollState?)scrollViewer.GetValue(StateProperty);

    private static ScrollState CreateState(ScrollViewer scrollViewer, double currentOffset)
    {
        ScrollState state = new() { TargetOffset = currentOffset };
        scrollViewer.SetValue(StateProperty, state);
        return state;
    }

    private static CubicEase CreateEaseOut()
    {
        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        ease.Freeze();
        return ease;
    }

    private sealed class ScrollState
    {
        public double TargetOffset { get; set; }
        public bool IsAnimating { get; set; }
        public int AnimationVersion { get; set; }
    }
}
