namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// Lightweight UI context broker used by the embedded copilot.
    /// It keeps the AI companion aware of the user's current ribbon tab without coupling
    /// feature views directly to the main shell implementation.
    /// </summary>
    public interface IUiInteractionContextService
    {
        event EventHandler? ContextChanged;
        event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested;

        string ActiveTabName { get; }
        string ActivePanelName { get; }
        DateTimeOffset LastUpdatedAt { get; }

        void SetActiveTab(string tabName, string panelName);
        void RequestTabNavigation(string tabName, string reason);
    }

    public sealed class AiUiActionRequestedEventArgs : EventArgs
    {
        public AiUiActionRequestedEventArgs(string actionType, string target, string reason)
        {
            ActionType = actionType;
            Target = target;
            Reason = reason;
        }

        public string ActionType { get; }
        public string Target { get; }
        public string Reason { get; }
    }
}
