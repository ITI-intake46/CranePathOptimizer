using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.ZoneModule.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  CommandMode — what tool is currently active
    // ══════════════════════════════════════════════════════════════════════════

    public enum CommandMode
    {
        Idle,
        Measure,
        Draw,
        Select
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CommandLineBar
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AutoCAD-style command bar that sits below the 3D viewport.
    ///
    /// ── Public API (called by tools) ──────────────────────────────────────
    ///
    ///   cmdLine.SetMode(CommandMode.Measure);           // shows [MEASURE] badge
    ///   cmdLine.SetPrompt("Specify first point:");      // updates hint text
    ///   cmdLine.ShowResult("Distance: 12.34 m");        // shows teal right label
    ///   cmdLine.ClearAll();                             // resets to idle
    ///
    /// ── Raised events ─────────────────────────────────────────────────────
    ///
    ///   CommandEntered  — user typed a command and pressed Enter
    ///   EscapePressed   — user pressed Escape
    ///   ValueEntered    — user typed a numeric value and pressed Enter
    ///                     (used by shape tools for typed dimensions)
    ///
    /// ── Built-in commands ─────────────────────────────────────────────────
    ///
    ///   UN   → fires UnitsCommandEntered (wire to UnitsSettingsDialog.Open())
    ///   M    → fires MeasureCommandEntered
    ///   ESC  → fires EscapePressed
    ///
    /// ── Session 6 wiring (MainWindow) ────────────────────────────────────
    ///
    ///   _cmdLine.CommandEntered        += OnCommandEntered;
    ///   _cmdLine.EscapePressed         += OnEscape;
    ///   _cmdLine.UnitsCommandEntered   += () => UnitsSettingsDialog.Open(this);
    ///   _cmdLine.MeasureCommandEntered += () => _measureTool.Start();
    ///
    /// </summary>
    public partial class CommandLineBar : UserControl
    {
        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the user types any command string and presses Enter.</summary>
        public event Action<string> CommandEntered;

        /// <summary>Fired when the user presses Escape.</summary>
        public event Action EscapePressed;

        /// <summary>Fired when the user types a numeric value and presses Enter.</summary>
        public event Action<double> ValueEntered;

        /// <summary>Fired when the user types 'UN' (or 'UNITS') — wire to UnitsSettingsDialog.Open().</summary>
        public event Action UnitsCommandEntered;

        /// <summary>Fired when the user types 'M' (or 'MEASURE') — wire to MeasureTool.Start().</summary>
        public event Action MeasureCommandEntered;

        // ── State ─────────────────────────────────────────────────────────────

        private CommandMode _currentMode = CommandMode.Idle;
        private readonly ObservableCollection<string> _history = new ObservableCollection<string>();
        private int _historyIndex = -1;
        private readonly List<string> _historyList = new List<string>();

        // ── Constructor ───────────────────────────────────────────────────────

        public CommandLineBar()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the active tool mode — shows/hides the mode badge and updates its label.
        /// Pass <see cref="CommandMode.Idle"/> to hide the badge.
        /// </summary>
        public void SetMode(CommandMode mode)
        {
            _currentMode = mode;

            if (mode == CommandMode.Idle)
            {
                ModeBadge.Visibility = Visibility.Collapsed;
                TxtMode.Text = "";
                TxtResult.Text = "";
            }
            else
            {
                ModeBadge.Visibility = Visibility.Visible;
                TxtMode.Text = mode.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Sets the prompt text shown in italic grey to the left of the input box.
        /// E.g. "Specify first point:" or "Specify second point or [Undo]:"
        /// </summary>
        public void SetPrompt(string text)
        {
            TxtPrompt.Text = text ?? "";
        }

        /// <summary>
        /// Shows a result or status value in the teal right-side label.
        /// E.g. "Distance: 12.34 m"
        /// Also adds it to command history.
        /// </summary>
        public void ShowResult(string text)
        {
            TxtResult.Text = text ?? "";
            if (!string.IsNullOrWhiteSpace(text))
                AddToHistory(text);
        }

        /// <summary>
        /// Appends a message to the history panel without changing the result label.
        /// Useful for multi-step feedback ("Point A set.", "Move to second point.")
        /// </summary>
        public void Log(string message)
        {
            AddToHistory(message);
        }

        /// <summary>
        /// Clears prompt, result, mode badge and input box. Returns to idle state.
        /// </summary>
        public void ClearAll()
        {
            TxtInput.Text = "";
            TxtResult.Text = "";
            SetMode(CommandMode.Idle);
            SetPrompt("Type a command or press ESC");
            _historyIndex = -1;
        }

        /// <summary>
        /// <summary>Returns true if the input TextBox currently has keyboard focus.</summary>
        public bool InputHasFocus() => TxtInput.IsFocused;

        /// Focus the input text box so the user can start typing immediately.
        /// Call this when a tool mode becomes active.
        /// </summary>
        public void FocusInput()
        {
            TxtInput.Focus();
            TxtInput.SelectAll();
        }

        /// <summary>
        /// Shows or hides the history panel above the input row.
        /// </summary>
        public void SetHistoryVisible(bool visible)
        {
            HistoryPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
                HistoryScroll.ScrollToBottom();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  KEYBOARD HANDLING
        // ══════════════════════════════════════════════════════════════════════

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    ProcessInput();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    TxtInput.Text = "";
                    TxtResult.Text = "";
                    EscapePressed?.Invoke();
                    e.Handled = true;
                    break;

                case Key.Up:
                    NavigateHistory(-1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    NavigateHistory(+1);
                    e.Handled = true;
                    break;

                case Key.F1:
                    SetHistoryVisible(HistoryPanel.Visibility != Visibility.Visible);
                    e.Handled = true;
                    break;
            }
        }

        private void ProcessInput()
        {
            string raw = TxtInput.Text.Trim();
            if (string.IsNullOrEmpty(raw)) return;

            AddToHistory($"> {raw}");
            TxtInput.Text = "";
            _historyIndex = -1;

            // ── Check for numeric value first ───────────────────────────────
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double numVal))
            {
                // Also try to parse with UnitSystem (handles "10m", "33ft", etc.)
                ValueEntered?.Invoke(numVal);
                return;
            }

            // Try UnitSystem parse for values like "10.5m" or "33ft"
            if (UnitSystem.TryParse(raw, out double metresVal))
            {
                ValueEntered?.Invoke(metresVal);
                return;
            }

            // ── Built-in commands ───────────────────────────────────────────
            string cmd = raw.ToUpperInvariant();

            switch (cmd)
            {
                case "UN":
                case "UNITS":
                    AddToHistory("Opening Units Settings...");
                    UnitsCommandEntered?.Invoke();
                    return;

                case "M":
                case "MEASURE":
                case "DIST":
                    AddToHistory("Measure tool activated.");
                    MeasureCommandEntered?.Invoke();
                    return;

                case "ESC":
                case "CANCEL":
                    EscapePressed?.Invoke();
                    return;

                case "?":
                case "HELP":
                    AddToHistory("Commands: UN (units), M (measure), ESC (cancel)");
                    return;
            }

            // ── Forward to external handler ────────────────────────────────
            CommandEntered?.Invoke(raw);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HISTORY NAVIGATION
        // ══════════════════════════════════════════════════════════════════════

        private void NavigateHistory(int direction)
        {
            if (_historyList.Count == 0) return;

            _historyIndex = Math.Clamp(_historyIndex - direction, 0, _historyList.Count - 1);
            TxtInput.Text = _historyList[_historyList.Count - 1 - _historyIndex];
            TxtInput.SelectionStart = TxtInput.Text.Length;
        }

        private void AddToHistory(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;

            // Deduplicate consecutive identical entries
            if (_historyList.Count > 0 && _historyList[^1] == entry) return;

            _historyList.Add(entry);

            // Keep observable collection in sync (display max 50 lines)
            _history.Add(entry);
            while (_history.Count > 50) _history.RemoveAt(0);

            // Auto-scroll history panel
            if (HistoryPanel.Visibility == Visibility.Visible)
                Dispatcher.BeginInvoke(new Action(() => HistoryScroll.ScrollToBottom()),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TEXT BOX EVENTS
        // ══════════════════════════════════════════════════════════════════════

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Live autocomplete hint could go here in future
        }

        private void TxtInput_GotFocus(object sender, RoutedEventArgs e)
        {
            // Subtle highlight when active — handled via XAML CaretBrush
        }

        private void TxtInput_LostFocus(object sender, RoutedEventArgs e)
        {
            // Don't lose input on accidental click elsewhere
        }
    }
}