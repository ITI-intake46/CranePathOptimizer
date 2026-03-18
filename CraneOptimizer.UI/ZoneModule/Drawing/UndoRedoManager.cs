// FILE: ZoneModule/Drawing/UndoRedoManager.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    /// <summary>A named, timestamped undo/redo action.</summary>
    public class NamedUndoAction
    {
        public string Description { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public UndoAction Action { get; set; }
    }

    /// <summary>
    /// Undo/redo manager with UI-bindable history lists.
    /// R1 FIX: Not inherently thread-safe — all calls must be made from the UI thread.
    /// Max 50 entries kept; dropdown exposes top 20.
    /// </summary>
    public class UndoRedoManager : INotifyPropertyChanged
    {
        private const int MaxHistory = 50;
        private const int DropdownLimit = 20;

        private readonly LinkedList<NamedUndoAction> _undo = new();
        private readonly LinkedList<NamedUndoAction> _redo = new();
        private readonly object _syncRoot = new object(); // R1 FIX: lock guard for cross-thread push

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Fires whenever undo/redo stacks change.</summary>
        public event Action HistoryChanged;

        /// <summary>
        /// Alias for HistoryChanged — used by UndoRedoToolbar.Initialize().
        /// </summary>
        public event Action StackChanged
        {
            add { HistoryChanged += value; }
            remove { HistoryChanged -= value; }
        }

        // ── UI bindings ───────────────────────────────────────────────────────

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Count of items on the undo stack.</summary>
        public int UndoCount => _undo.Count;

        /// <summary>Count of items on the redo stack.</summary>
        public int RedoCount => _redo.Count;

        public string UndoTooltip => CanUndo
            ? $"Undo: {_undo.First!.Value.Description}  (Ctrl+Z)"
            : "Nothing to undo";

        public string RedoTooltip => CanRedo
            ? $"Redo: {_redo.First!.Value.Description}  (Ctrl+Y)"
            : "Nothing to redo";

        /// <summary>Top description on the undo stack (empty string if none).</summary>
        public string PeekUndoDescription =>
            _undo.Count > 0 ? _undo.First!.Value.Description : "";

        /// <summary>Top description on the redo stack (empty string if none).</summary>
        public string PeekRedoDescription =>
            _redo.Count > 0 ? _redo.First!.Value.Description : "";

        /// <summary>Newest-first descriptions for the Undo dropdown (max 20).</summary>
        public IReadOnlyList<string> UndoHistory =>
            _undo.Take(DropdownLimit).Select(e => e.Description).ToList();

        /// <summary>Newest-first descriptions for the Redo dropdown (max 20).</summary>
        public IReadOnlyList<string> RedoHistory =>
            _redo.Take(DropdownLimit).Select(e => e.Description).ToList();

        // ── Mutation ──────────────────────────────────────────────────────────

        /// <summary>Push a new named action onto the undo stack (clears redo).</summary>
        public void Push(string description, UndoAction action)
        {
            _undo.AddFirst(new NamedUndoAction { Description = description, Action = action });
            _redo.Clear();
            while (_undo.Count > MaxHistory) _undo.RemoveLast();
            Notify();
        }



        /// <summary>
        /// Undo the top <paramref name="count"/> actions.
        /// Returns the actions in the order they should be applied (newest first).
        /// </summary>
        public List<UndoAction> Undo(int count = 1)
        {
            var actions = new List<UndoAction>();
            for (int i = 0; i < count && _undo.Count > 0; i++)
            {
                var entry = _undo.First!.Value;
                _undo.RemoveFirst();
                _redo.AddFirst(entry);
                actions.Add(entry.Action);
            }
            Notify();
            return actions;
        }

        /// <summary>
        /// Redo the top <paramref name="count"/> actions.
        /// Returns the actions in the order they should be applied.
        /// </summary>
        public List<UndoAction> Redo(int count = 1)
        {
            var actions = new List<UndoAction>();
            for (int i = 0; i < count && _redo.Count > 0; i++)
            {
                var entry = _redo.First!.Value;
                _redo.RemoveFirst();
                _undo.AddFirst(entry);
                actions.Add(entry.Action);
            }
            Notify();
            return actions;
        }


        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Notify();
        }

        private void Notify()
        {
            HistoryChanged?.Invoke();
            // R7 FIX: fire named events instead of null (avoids full binding refresh)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanUndo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRedo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UndoCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RedoCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UndoHistory)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RedoHistory)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UndoTooltip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RedoTooltip)));
        }
    }
}