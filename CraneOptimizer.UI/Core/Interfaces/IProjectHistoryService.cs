namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IProjectHistoryAction
    {
        string Description { get; }
        void Execute();
        void Undo();
    }

    public interface IProjectHistoryService
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        string Breadcrumb { get; }

        event Action? StateChanged;

        void Execute(IProjectHistoryAction action);
        void RecordExecuted(IProjectHistoryAction action);
        void Undo();
        void Redo();
        void Clear();
    }
}
