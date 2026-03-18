using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.ZoneModule.Drawing;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class UndoRedoToolbar : UserControl
    {
        // ── Events ────────────────────────────────────────────────────────────
        public event Action UndoRequested;
        public event Action RedoRequested;
        public event Action UndoActionRequested;  // changed from Action<object>
        public event Action RedoActionRequested;  // changed from Action<object>

        // ── Standalone stacks (used when Initialize is NOT called) ────────────
        private readonly Stack<string> _undoStack = new Stack<string>();
        private readonly Stack<string> _redoStack = new Stack<string>();

        // ── Manager reference ─────────────────────────────────────────────────
        private UndoRedoManager _undoMgr;

        public bool CanUndo => _undoMgr != null ? _undoMgr.CanUndo : _undoStack.Count > 0;
        public bool CanRedo => _undoMgr != null ? _undoMgr.CanRedo : _redoStack.Count > 0;

        public UndoRedoToolbar() => InitializeComponent();

        // ── Initialize with manager (called by controller) ────────────────────
        public void Initialize(UndoRedoManager undoMgr)
        {
            _undoMgr = undoMgr;
            undoMgr.StackChanged += () => Dispatcher.Invoke(RefreshFromManager);
            RefreshFromManager();
        }

        // ── Standalone public API ─────────────────────────────────────────────
        public void PushAction(string description)
        {
            if (_undoMgr != null) return;
            _undoStack.Push(description);
            _redoStack.Clear();
            Refresh();
        }

        public void NotifyUndoDone(string description)
        {
            if (_undoMgr != null) return;
            _redoStack.Push(description);
            Refresh();
        }

        public void NotifyRedoDone(string description)
        {
            if (_undoMgr != null) return;
            _undoStack.Push(description);
            Refresh();
        }

        public void BtnUndo_Click_External() => DoUndo();
        public void BtnRedo_Click_External() => DoRedo();

        // ── Button handlers ───────────────────────────────────────────────────
        private void BtnUndo_Click(object sender, RoutedEventArgs e) => DoUndo();
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => DoRedo();

        private void DoUndo()
        {
            if (_undoMgr != null)
            {
                if (!_undoMgr.CanUndo) return;
                UndoRequested?.Invoke();
                UndoActionRequested?.Invoke();
            }
            else
            {
                if (_undoStack.Count == 0) return;
                _redoStack.Push(_undoStack.Pop());
                Refresh();
                UndoRequested?.Invoke();
                UndoActionRequested?.Invoke();
            }
        }

        private void DoRedo()
        {
            if (_undoMgr != null)
            {
                if (!_undoMgr.CanRedo) return;
                RedoRequested?.Invoke();
                RedoActionRequested?.Invoke();
            }
            else
            {
                if (_redoStack.Count == 0) return;
                _undoStack.Push(_redoStack.Pop());
                Refresh();
                RedoRequested?.Invoke();
                RedoActionRequested?.Invoke();
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear all undo/redo history?", "Clear History",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_undoMgr != null)
                    _undoMgr.Clear();
                else
                {
                    _undoStack.Clear();
                    _redoStack.Clear();
                    Refresh();
                }
            }
        }

        // ── UI refresh (manager mode) ─────────────────────────────────────────
        private void RefreshFromManager()
        {
            BtnUndo.IsEnabled = _undoMgr.CanUndo;
            BtnRedo.IsEnabled = _undoMgr.CanRedo;
            BtnClearHistory.IsEnabled = _undoMgr.CanUndo || _undoMgr.CanRedo;

            if (_undoMgr.CanUndo)
            {
                int steps = _undoMgr.UndoCount;
                TxtUndoInfo.Text = $"{_undoMgr.PeekUndoDescription}  " +
                                   $"({steps} step{(steps > 1 ? "s" : "")})";
                TxtUndoInfo.FontStyle = FontStyles.Normal;
                TxtUndoInfo.Foreground = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                TxtUndoInfo.Text = "No actions yet";
                TxtUndoInfo.FontStyle = FontStyles.Italic;
                TxtUndoInfo.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
            }
        }

        // ── UI refresh (standalone mode) ──────────────────────────────────────
        private void Refresh()
        {
            BtnUndo.IsEnabled = _undoStack.Count > 0;
            BtnRedo.IsEnabled = _redoStack.Count > 0;
            BtnClearHistory.IsEnabled = _undoStack.Count > 0 || _redoStack.Count > 0;

            if (_undoStack.Count > 0)
            {
                int steps = _undoStack.Count;
                TxtUndoInfo.Text = $"{_undoStack.Peek()}  ({steps} step{(steps > 1 ? "s" : "")})";
                TxtUndoInfo.FontStyle = FontStyles.Normal;
                TxtUndoInfo.Foreground = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                TxtUndoInfo.Text = "No actions yet";
                TxtUndoInfo.FontStyle = FontStyles.Italic;
                TxtUndoInfo.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
            }
        }
    }
}