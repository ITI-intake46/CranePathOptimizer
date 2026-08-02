namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDeleteConfirmationService
    {
        bool ConfirmDelete(string title, string message, string actionText);
    }
}
