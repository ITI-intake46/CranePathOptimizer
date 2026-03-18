using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WPF_IFC_test1.ZoneModule.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  CursorPickStrip
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A compact floating button strip that appears 20 px below-right of the
    /// cursor when a shape drawing tool is active.  Each button represents one
    /// drawing sub-method (e.g. "W×H", "2 pts", "Side" for rectangles).
    ///
    /// ── How it works ───────────────────────────────────────────────────────
    ///
    ///   The strip is a UserControl placed on the overlay Canvas.  Its position
    ///   is updated every MouseMove via UpdatePosition().
    ///
    ///   ShapeInputController calls:
    ///
    ///     _pickStrip.Show(new[] { ("W×H","1"), ("2 pts","2"), ("Side","3") });
    ///
    ///   When the user clicks a button, MethodSelected fires with the command
    ///   string (e.g. "1").  ShapeInputController wires this to SelectMethod().
    ///
    ///   The strip auto-hides after 3 s of cursor stillness, or immediately
    ///   when Hide() is called (e.g. after method selection).
    ///
    /// ── Placement in overlay Canvas (Session 6 XAML) ─────────────────────
    ///
    ///   The strip is added to the same Canvas-based overlay layer.
    ///   In Session 6 MainWindow.xaml add inside the overlay Grid:
    ///
    ///     <zoneUI:CursorPickStrip x:Name="PickStrip"
    ///                             Canvas.Left="0" Canvas.Top="0"
    ///                             Visibility="Collapsed"/>
    ///
    ///   Then in constructor:
    ///     _shapeCtrl.AttachPickStrip(PickStrip);
    ///
    /// </summary>
    public partial class CursorPickStrip : UserControl
    {
        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user clicks a method button.
        /// The string is the command value passed to Show() (e.g. "1", "2", "3").
        /// </summary>
        public event Action<string> MethodSelected;

        // ── State ─────────────────────────────────────────────────────────────

        private DispatcherTimer _hideTimer;
        private Point _lastCursorPos;
        private bool _visible = false;

        // Offset from cursor to strip top-left corner (spec: 20 px below-right)
        private const double OFFSET_X = 20;
        private const double OFFSET_Y = 20;

        // ── Constructor ───────────────────────────────────────────────────────

        public CursorPickStrip()
        {
            InitializeComponent();

            // Auto-hide timer: 3 seconds after last activity
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromSeconds(3);
            _hideTimer.Tick += (s, e) => { _hideTimer.Stop(); Hide(); };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows the strip with the given method buttons.
        /// Each tuple: (label text, command value).
        /// E.g. ("W×H", "1"), ("2 pts", "2"), ("Side", "3")
        /// </summary>
        public void Show((string Label, string Command)[] methods)
        {
            ButtonPanel.Children.Clear();

            foreach (var (label, command) in methods)
            {
                var btn = CreateMethodButton(label, command);
                ButtonPanel.Children.Add(btn);
            }

            Visibility = Visibility.Visible;
            _visible = true;

            ResetHideTimer();
        }

        /// <summary>
        /// Hides the strip immediately.
        /// </summary>
        public void Hide()
        {
            _hideTimer.Stop();
            Visibility = Visibility.Collapsed;
            _visible = false;
        }

        /// <summary>
        /// Updates the strip position to follow the cursor.
        /// Call this from the overlay's MouseMove handler with the current screen point.
        /// </summary>
        public void UpdatePosition(Point cursorScreen)
        {
            if (!_visible) return;

            _lastCursorPos = cursorScreen;

            // Position: 20 px below-right of cursor
            Canvas.SetLeft(this, cursorScreen.X + OFFSET_X);
            Canvas.SetTop(this, cursorScreen.Y + OFFSET_Y);

            // Reset hide timer on cursor movement
            ResetHideTimer();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private Button CreateMethodButton(string label, string command)
        {
            var btn = new Button
            {
                Content = label,
                Width = 36,
                Height = 22,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(2, 0, 2, 0),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = $"Method {command}",
                Tag = command,           // stores the command value
                Template = BuildButtonTemplate()
            };

            btn.Click += (s, e) =>
            {
                string cmd = (string)((Button)s).Tag;
                Hide();
                MethodSelected?.Invoke(cmd);
            };

            btn.MouseEnter += (s, e) =>
            {
                var b = (Button)s;
                b.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                b.Foreground = Brushes.White;
                ResetHideTimer(); // don't hide while user is interacting
            };

            btn.MouseLeave += (s, e) =>
            {
                var b = (Button)s;
                b.Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));
                b.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            };

            return btn;
        }

        private static ControlTemplate BuildButtonTemplate()
        {
            // Minimal flat button template — avoids default Windows chrome
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush")
                { RelativeSource = new System.Windows.Data.RelativeSource(RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderThicknessProperty,
                new System.Windows.Data.Binding("BorderThickness")
                { RelativeSource = new System.Windows.Data.RelativeSource(RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            borderFactory.SetBinding(Border.PaddingProperty,
                new System.Windows.Data.Binding("Padding")
                { RelativeSource = new System.Windows.Data.RelativeSource(RelativeSourceMode.TemplatedParent) });

            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(cpFactory);
            template.VisualTree = borderFactory;
            return template;
        }

        private void ResetHideTimer()
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}