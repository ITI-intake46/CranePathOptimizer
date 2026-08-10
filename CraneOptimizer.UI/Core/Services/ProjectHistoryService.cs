using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class ProjectHistoryService : IProjectHistoryService
    {
        private const int MaxHistoryCount = 100;
        private readonly Stack<IProjectHistoryAction> _undo = new();
        private readonly Stack<IProjectHistoryAction> _redo = new();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string Breadcrumb { get; private set; } = "No recent action";

        public event Action? StateChanged;

        public void Execute(IProjectHistoryAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action.Execute();
            RecordExecuted(action);
        }

        public void RecordExecuted(IProjectHistoryAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            _undo.Push(action);
            TrimHistory(_undo);
            _redo.Clear();
            Breadcrumb = action.Description;
            StateChanged?.Invoke();
        }

        public void Undo()
        {
            if (_undo.Count == 0)
                return;

            IProjectHistoryAction action = _undo.Pop();
            action.Undo();
            _redo.Push(action);
            Breadcrumb = $"Undid {action.Description}";
            StateChanged?.Invoke();
        }

        public void Redo()
        {
            if (_redo.Count == 0)
                return;

            IProjectHistoryAction action = _redo.Pop();
            action.Execute();
            _undo.Push(action);
            Breadcrumb = $"Redid {action.Description}";
            StateChanged?.Invoke();
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Breadcrumb = "No recent action";
            StateChanged?.Invoke();
        }

        private static void TrimHistory(Stack<IProjectHistoryAction> history)
        {
            if (history.Count <= MaxHistoryCount)
                return;

            IProjectHistoryAction[] newestFirst = history.Take(MaxHistoryCount).ToArray();
            history.Clear();
            for (int index = newestFirst.Length - 1; index >= 0; index--)
                history.Push(newestFirst[index]);
        }
    }

    public sealed class DelegateProjectHistoryAction : IProjectHistoryAction
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public DelegateProjectHistoryAction(string description, Action execute, Action undo)
        {
            Description = string.IsNullOrWhiteSpace(description) ? "Project change" : description;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public string Description { get; }

        public void Execute() => _execute();

        public void Undo() => _undo();
    }
}
