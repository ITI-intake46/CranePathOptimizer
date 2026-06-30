using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Core.Base;
using CraneOptimizer.AI.Core.Commands;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.ViewModels;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Companion.ViewModels
{
    /// <summary>
    /// Non-intrusive, in-shell AI companion that follows the user through the workflow.
    /// It does not open popups automatically and never mutates crane-planning data;
    /// it summarizes context, suggests the next safe step, and forwards explicit user prompts
    /// to the existing local Ollama copilot.
    /// </summary>
    public sealed class AiCompanionViewModel : ViewModelBase, IDisposable
    {
        private readonly IAiWorkflowContextProvider _workflowContextProvider;
        private readonly AiAdvisorViewModel _advisor;
        private readonly IAiAdvisorWindowService _windowService;
        private readonly IUiInteractionContextService _uiContext;
        private readonly IAiCopilotSettingsService _settingsService;
        private readonly IWorkflowReadinessService _workflowReadinessService;
        private bool _disposed;
        private bool _isExpanded;
        private bool _isQuietMode;
        private string _questionText = "What should I do next from this screen?";
        private string _workflowStage = "Project setup";
        private string _currentHint = "Start by opening or creating a project, then load IFC data when needed.";
        private string _nextAction = "Open or create a project from HOME.";
        private string _contextSummary = "No active project context yet.";
        private string _workflowProgressLabel = "Workflow readiness: not evaluated.";
        private string _projectInsight = "Project · none";
        private string _dataInsight = "0 elements · 0 zones · 0 cranes";
        private string _resultInsight = "No optimization run";
        private string _riskInsight = "Readiness not evaluated";
        private double _workflowProgressValue;
        private double _composerHeight = 34d;
        private AiCompanionChatMessageViewModel? _liveAssistantMessage;

        public AiCompanionViewModel(
            IAiWorkflowContextProvider workflowContextProvider,
            AiAdvisorViewModel advisor,
            IAiAdvisorWindowService windowService,
            IUiInteractionContextService uiContext,
            IAiCopilotSettingsService settingsService,
            IWorkflowReadinessService workflowReadinessService)
        {
            _workflowContextProvider = workflowContextProvider ?? throw new ArgumentNullException(nameof(workflowContextProvider));
            _advisor = advisor ?? throw new ArgumentNullException(nameof(advisor));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _workflowReadinessService = workflowReadinessService ?? throw new ArgumentNullException(nameof(workflowReadinessService));

            WorkflowChecklist = new ObservableCollection<WorkflowReadinessItem>();
            StructuredCards = new ObservableCollection<StructuredRecommendationCard>();
            ActionProposals = new ObservableCollection<AiActionProposalViewModel>();
            ConversationMessages = new ObservableCollection<AiCompanionChatMessageViewModel>();

            ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
            CollapseCommand = new RelayCommand(() => IsExpanded = false);
            ToggleQuietModeCommand = new RelayCommand(() => IsQuietMode = !IsQuietMode);
            OpenFullCopilotCommand = new RelayCommand(OpenFullCopilot);
            AnalyzeHereCommand = new RelayCommand(AnalyzeHere);
            AskFromHereCommand = new RelayCommand(AskFromHere, () => !Advisor.IsBusy);
            AskToggleCommand = new RelayCommand(ToggleAskOrStop, () => Advisor.IsBusy ? Advisor.CanCancel : true);
            CancelAskCommand = new RelayCommand(() => Advisor.CancelOllamaCommand.Execute(null), () => Advisor.CanCancel);
            SetQuestionCommand = new RelayCommand<string>(SetQuestion);
            CopyAnswerCommand = new RelayCommand(CopyAnswer, () => Advisor.HasAiAnswer);
            CopyContextCommand = new RelayCommand(CopyContext, () => !string.IsNullOrWhiteSpace(ContextSummary));
            ConfirmActionCommand = new RelayCommand<AiActionProposalViewModel>(ConfirmAction);
            ExecutePrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, () => PrimaryActionProposal != null);

            IsQuietMode = false;
            SubscribeToContextChanges();
            RefreshContext();
        }

        public AiAdvisorViewModel Advisor => _advisor;
        public ObservableCollection<WorkflowReadinessItem> WorkflowChecklist { get; }
        public ObservableCollection<StructuredRecommendationCard> StructuredCards { get; }
        public ObservableCollection<AiActionProposalViewModel> ActionProposals { get; }
        public ObservableCollection<AiCompanionChatMessageViewModel> ConversationMessages { get; }

        public ICommand ToggleExpandedCommand { get; }
        public ICommand CollapseCommand { get; }
        public ICommand ToggleQuietModeCommand { get; }
        public ICommand OpenFullCopilotCommand { get; }
        public ICommand AnalyzeHereCommand { get; }
        public ICommand AskFromHereCommand { get; }
        public ICommand AskToggleCommand { get; }
        public ICommand CancelAskCommand { get; }
        public ICommand SetQuestionCommand { get; }
        public ICommand CopyAnswerCommand { get; }
        public ICommand CopyContextCommand { get; }
        public ICommand ConfirmActionCommand { get; }
        public ICommand ExecutePrimaryActionCommand { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandedVisibility));
                OnPropertyChanged(nameof(CollapsedVisibility));
                OnPropertyChanged(nameof(PanelWidth));
                if (value) RefreshContext();
                PersistDockPreference(value);
            }
        }

        public bool IsQuietMode
        {
            get => _isQuietMode;
            set
            {
                if (_isQuietMode == value) return;
                _isQuietMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuietModeLabel));
                OnPropertyChanged(nameof(CollapsedSubText));
                RefreshContext();
            }
        }

        public string QuestionText
        {
            get => _questionText;
            set
            {
                if (_questionText == value) return;
                _questionText = value;
                OnPropertyChanged();
            }
        }

        public string WorkflowStage
        {
            get => _workflowStage;
            private set
            {
                if (_workflowStage == value) return;
                _workflowStage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CollapsedTitle));
            }
        }

        public string CurrentHint
        {
            get => _currentHint;
            private set
            {
                if (_currentHint == value) return;
                _currentHint = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CollapsedSubText));
            }
        }

        public string NextAction
        {
            get => _nextAction;
            private set
            {
                if (_nextAction == value) return;
                _nextAction = value;
                OnPropertyChanged();
            }
        }

        public string ContextSummary
        {
            get => _contextSummary;
            private set
            {
                if (_contextSummary == value) return;
                _contextSummary = value;
                OnPropertyChanged();
            }
        }

        public string WorkflowProgressLabel
        {
            get => _workflowProgressLabel;
            private set
            {
                if (_workflowProgressLabel == value) return;
                _workflowProgressLabel = value;
                OnPropertyChanged();
            }
        }

        public string ProjectInsight
        {
            get => _projectInsight;
            private set
            {
                if (_projectInsight == value) return;
                _projectInsight = value;
                OnPropertyChanged();
            }
        }

        public string DataInsight
        {
            get => _dataInsight;
            private set
            {
                if (_dataInsight == value) return;
                _dataInsight = value;
                OnPropertyChanged();
            }
        }

        public string ResultInsight
        {
            get => _resultInsight;
            private set
            {
                if (_resultInsight == value) return;
                _resultInsight = value;
                OnPropertyChanged();
            }
        }

        public string RiskInsight
        {
            get => _riskInsight;
            private set
            {
                if (_riskInsight == value) return;
                _riskInsight = value;
                OnPropertyChanged();
            }
        }

        public string ActiveTabLabel => $"Screen: {_uiContext.ActiveTabName}";
        public AiActionProposalViewModel? PrimaryActionProposal => ActionProposals.FirstOrDefault(proposal => proposal.ActionType.Equals("NavigateTab", StringComparison.OrdinalIgnoreCase));
        public string PrimaryActionLabel => PrimaryActionProposal?.ConfirmLabel ?? "Open recommended tab";
        public Visibility PrimaryActionVisibility => PrimaryActionProposal == null ? Visibility.Collapsed : Visibility.Visible;

        public bool HasLatestAnswer => Advisor.HasAiAnswer;
        public Visibility LatestAnswerVisibility => HasLatestAnswer ? Visibility.Visible : Visibility.Collapsed;

        public double WorkflowProgressValue
        {
            get => _workflowProgressValue;
            private set
            {
                if (Math.Abs(_workflowProgressValue - value) < 0.01) return;
                _workflowProgressValue = value;
                OnPropertyChanged();
            }
        }



        public double ComposerHeight
        {
            get => _composerHeight;
            set
            {
                var clamped = Math.Max(32d, Math.Min(120d, value));
                if (Math.Abs(_composerHeight - clamped) < 0.5d) return;
                _composerHeight = clamped;
                OnPropertyChanged();
            }
        }

        public string AskToggleLabel => Advisor.IsBusy ? "Stop" : "Send";
        public string AskToggleToolTip => Advisor.IsBusy
            ? "Stop the current local Ollama response."
            : "Send the visible question with private live project context. Enter also sends.";

        public string CollapsedTitle => $"AI Copilot · {WorkflowStage}";
        public string CollapsedSubText => IsQuietMode ? "Quiet mode enabled" : CurrentHint;
        public string QuietModeLabel => IsQuietMode ? "Quiet" : "Quiet mode";
        public double PanelWidth => IsExpanded ? 420d : 300d;
        public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CollapsedVisibility => IsExpanded ? Visibility.Collapsed : Visibility.Visible;

        public void RefreshContext()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();

            Advisor.SetActiveProjectContext(liveContext.ProjectKey, liveContext.ProjectDisplayName);
            var snapshot = _workflowReadinessService.Evaluate(liveContext, _uiContext);
            WorkflowStage = snapshot.CurrentStage;
            WorkflowProgressLabel = $"{snapshot.CompleteCount}/{snapshot.TotalCount} complete · {snapshot.MissingCriticalCount} critical missing";
            WorkflowProgressValue = Math.Max(0d, Math.Min(100d, snapshot.CompletionRatio * 100d));

            var firstCard = _workflowReadinessService.BuildStructuredCards(snapshot).FirstOrDefault();
            if (firstCard != null)
            {
                CurrentHint = firstCard.Recommendation;
                NextAction = firstCard.NextClick;
            }
            else
            {
                CurrentHint = snapshot.Summary;
                NextAction = "Review AI Assist context or open Full Copilot.";
            }

            WorkflowChecklist.Clear();
            foreach (var item in snapshot.Items)
                WorkflowChecklist.Add(item);

            StructuredCards.Clear();
            foreach (var card in _workflowReadinessService.BuildStructuredCards(snapshot))
                StructuredCards.Add(card);

            RefreshActionProposals(snapshot);

            var elementCount = liveContext.ElementCount;
            var zoneCount = liveContext.ZoneCount;
            var craneCount = liveContext.CraneCount;
            var hasProject = liveContext.HasProject;
            var hasResult = liveContext.OptimizationResult.HasResult;

            ProjectInsight = hasProject ? $"Project · {liveContext.CurrentProjectName}" : "Project · not opened";
            DataInsight = $"{elementCount} elements · {zoneCount} zones · {BuildCraneInsight(craneCount)}";
            ResultInsight = BuildResultInsight(liveContext.OptimizationResult);
            RiskInsight = snapshot.MissingCriticalCount == 0
                ? "No critical readiness gaps"
                : $"{snapshot.MissingCriticalCount} critical readiness gap(s)";

            ContextSummary = BuildContextSummary(liveContext, snapshot);
            RefreshAssistantIntroMessage();
            OnPropertyChanged(nameof(ActiveTabLabel));
            OnPropertyChanged(nameof(CollapsedTitle));
            OnPropertyChanged(nameof(CollapsedSubText));
        }


        private void RefreshAssistantIntroMessage()
        {
            var body = new StringBuilder();
            body.AppendLine(CurrentHint);
            body.AppendLine();
            body.AppendLine($"Next: {NextAction}");
            body.AppendLine();
            body.Append("Use the fixed question box below to chat. Scroll this conversation area to review messages, actions, and evidence.");

            if (ConversationMessages.Count > 0 && ConversationMessages[0].IsReadinessIntro)
            {
                ConversationMessages[0].Body = body.ToString();
                ConversationMessages[0].Timestamp = DateTimeOffset.Now;
                return;
            }

            ConversationMessages.Insert(0, AiCompanionChatMessageViewModel.ReadinessIntro(body.ToString()));
        }

        private string GetProjectKey()
        {
            return _workflowContextProvider.GetSnapshot().ProjectKey;
        }

        private string GetProjectDisplayName()
        {
            return _workflowContextProvider.GetSnapshot().ProjectDisplayName;
        }

        private void PersistDockPreference(bool isVisible)
        {
            var key = GetProjectKey();
            var name = GetProjectDisplayName();
            _settingsService.Update(settings =>
            {
                settings.ProjectDockPreferences[key] = new AiDockPreferenceRecord
                {
                    ProjectKey = key,
                    ProjectDisplayName = name,
                    LastDockVisible = isVisible,
                    DockPreference = settings.DockPreference,
                    UpdatedAt = DateTimeOffset.Now
                };
            });
        }

        private void RefreshActionProposals(WorkflowReadinessSnapshot snapshot)
        {
            ActionProposals.Clear();
            if (!_settingsService.Current.EnableConfirmableNavigationActions)
            {
                OnPropertyChanged(nameof(PrimaryActionProposal));
                OnPropertyChanged(nameof(PrimaryActionLabel));
                OnPropertyChanged(nameof(PrimaryActionVisibility));
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            var targetTab = GuessTargetTab(NextAction, snapshot.CurrentStage);
            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = $"Open {targetTab} tab",
                Description = $"Go to {targetTab} to continue: {NextAction}",
                ActionType = "NavigateTab",
                Target = targetTab,
                Risk = "Low — navigation only; no project data is changed.",
                ConfirmLabel = $"Open {targetTab}",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Copy readiness evidence",
                Description = "Copy the current workflow checklist and live context to the clipboard for audit/review.",
                ActionType = "CopyContext",
                Target = "Clipboard",
                Risk = "Low — exports text only.",
                ConfirmLabel = "Copy evidence",
                ConfirmCommand = ConfirmActionCommand
            });

            OnPropertyChanged(nameof(PrimaryActionProposal));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionVisibility));
            CommandManager.InvalidateRequerySuggested();
        }

        private static string GuessTargetTab(string nextAction, string workflowStage)
        {
            var text = (nextAction + " " + workflowStage).ToUpperInvariant();
            if (text.Contains("IFC")) return "IFC MODEL";
            if (text.Contains("DXF")) return "DXF";
            if (text.Contains("ZONE")) return "ZONES";
            if (text.Contains("CRANE") || text.Contains("LOAD CHART")) return "CRANES";
            if (text.Contains("OPTIM") || text.Contains("ANALYZE")) return "ANALYZE";
            if (text.Contains("REPORT")) return "REPORTS";
            return "HOME";
        }

        private void ExecutePrimaryAction()
        {
            ConfirmAction(PrimaryActionProposal);
        }

        private void ConfirmAction(AiActionProposalViewModel? proposal)
        {
            if (proposal == null)
                return;

            if (proposal.ActionType.Equals("NavigateTab", StringComparison.OrdinalIgnoreCase))
            {
                _uiContext.RequestTabNavigation(proposal.Target, proposal.Description);
                return;
            }

            if (proposal.ActionType.Equals("CopyContext", StringComparison.OrdinalIgnoreCase))
            {
                CopyContext();
            }
        }


        private static string BuildCraneInsight(int craneCount)
        {
            return craneCount switch
            {
                0 => "no crane selected",
                1 => "1 crane available",
                _ => $"{craneCount} cranes available"
            };
        }

        private static string BuildResultInsight(AiOptimizationResultSnapshot result)
        {
            if (!result.HasResult)
                return "No optimization result yet";

            return $"{result.CoverageRatio:P0} coverage · {result.LiftedElementCount}/{result.TotalElementCount} lifted";
        }

        private string BuildContextSummary(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot snapshot)
        {
            var result = context.OptimizationResult;
            var builder = new StringBuilder();
            builder.AppendLine($"Project: {(context.HasProject ? context.CurrentProjectName : "none")}");
            builder.AppendLine($"Active screen: {_uiContext.ActiveTabName}");
            builder.AppendLine($"Workflow stage: {snapshot.CurrentStage}");
            builder.AppendLine($"Readiness: {snapshot.CompleteCount}/{snapshot.TotalCount} complete; {snapshot.MissingCriticalCount} critical missing");
            builder.AppendLine($"Elements: {context.ElementCount}");
            builder.AppendLine($"Zones: {context.ZoneCount}");
            builder.AppendLine($"Cranes in scene: {context.CraneCount}");

            if (result.HasResult)
            {
                builder.AppendLine($"Latest coverage: {result.CoverageRatio:P2}");
                builder.AppendLine($"Lifted elements: {result.LiftedElementCount}/{result.TotalElementCount}");
                builder.AppendLine($"Setup positions: {result.PositionCount}");
            }
            else
            {
                builder.AppendLine("Latest optimization: none in current session");
            }

            return builder.ToString().Trim();
        }

        private void ToggleAskOrStop()
        {
            if (Advisor.IsBusy)
            {
                Advisor.CancelOllamaCommand.Execute(null);
                return;
            }

            AskFromHere();
        }

        private void AnalyzeHere()
        {
            RefreshContext();
            Advisor.AnalyzeCurrentResult();
            IsExpanded = true;
        }

        private void AskFromHere()
        {
            _ = AskFromHereAsync();
        }

        private async Task AskFromHereAsync()
        {
            RefreshContext();
            Advisor.AnalyzeCurrentResult();
            IsExpanded = true;

            var visibleQuestion = string.IsNullOrWhiteSpace(QuestionText)
                ? "What should I do next from this screen?"
                : QuestionText.Trim();

            ConversationMessages.Add(AiCompanionChatMessageViewModel.User(visibleQuestion));
            _liveAssistantMessage = AiCompanionChatMessageViewModel.Assistant("Generating locally… live response will appear here as Ollama streams.");
            ConversationMessages.Add(_liveAssistantMessage);

            try
            {
                await Advisor.AskOllamaWithLiveUiContextAsync(
                    visibleQuestion,
                    BuildHiddenLiveUiContext(),
                    NextAction).ConfigureAwait(true);

                if (_liveAssistantMessage != null)
                {
                    _liveAssistantMessage.Body = Advisor.HasAiAnswer
                        ? Advisor.AiAnswer
                        : string.IsNullOrWhiteSpace(Advisor.LastError)
                            ? "No visible AI answer was produced. Check Ollama status or open Full Copilot for details."
                            : Advisor.LastError;
                }
            }
            finally
            {
                _liveAssistantMessage = null;
            }
        }

        private string BuildHiddenLiveUiContext()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Live UI context for this request. Use silently; do not print this section back to the user.");
            builder.AppendLine(ContextSummary);
            builder.AppendLine($"Suggested deterministic next action: {NextAction}");
            builder.AppendLine("Response contract: provide Status, Evidence, Recommendation, Next Click, Risk, and Confidence when useful. Do not perform actions automatically.");
            return builder.ToString().Trim();
        }

        private void SetQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return;
            QuestionText = question.Trim();
            IsExpanded = true;
        }

        private void OpenFullCopilot()
        {
            RefreshContext();
            Advisor.AnalyzeCurrentResult();
            _windowService.Show();
        }

        private void ToggleExpanded()
        {
            if (IsQuietMode && !IsExpanded)
                IsQuietMode = false;

            IsExpanded = !IsExpanded;
        }

        private void CopyAnswer()
        {
            if (Advisor.HasAiAnswer)
                Clipboard.SetText(Advisor.AiAnswer);
        }

        private void CopyContext()
        {
            if (!string.IsNullOrWhiteSpace(ContextSummary))
                Clipboard.SetText(ContextSummary);
        }

        private void SubscribeToContextChanges()
        {
            _workflowContextProvider.ContextChanged += OnWorkflowContextChanged;
            _advisor.PropertyChanged += OnAdvisorPropertyChanged;
            _uiContext.ContextChanged += OnUiContextChanged;
        }

        private void OnAdvisorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(AiAdvisorViewModel.AiAnswer) or nameof(AiAdvisorViewModel.HasAiAnswer))
            {
                OnPropertyChanged(nameof(HasLatestAnswer));
                OnPropertyChanged(nameof(LatestAnswerVisibility));
                if (_liveAssistantMessage != null && !string.IsNullOrWhiteSpace(Advisor.AiAnswer))
                    _liveAssistantMessage.Body = Advisor.AiAnswer;
            }

            if (e.PropertyName is nameof(AiAdvisorViewModel.IsBusy) or nameof(AiAdvisorViewModel.CanCancel))
            {
                OnPropertyChanged(nameof(AskToggleLabel));
                OnPropertyChanged(nameof(AskToggleToolTip));
            }

            if (e.PropertyName is nameof(AiAdvisorViewModel.AiAnswer) or nameof(AiAdvisorViewModel.HasAiAnswer) or nameof(AiAdvisorViewModel.IsBusy) or nameof(AiAdvisorViewModel.CanCancel))
                CommandManager.InvalidateRequerySuggested();
        }

        private void OnUiContextChanged(object? sender, EventArgs e) => RefreshContext();
        private void OnWorkflowContextChanged(object? sender, EventArgs e) => RefreshContext();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _workflowContextProvider.ContextChanged -= OnWorkflowContextChanged;
            _advisor.PropertyChanged -= OnAdvisorPropertyChanged;
            _uiContext.ContextChanged -= OnUiContextChanged;
        }
    }

    public sealed class AiCompanionChatMessageViewModel : ViewModelBase
    {
        private string _body = string.Empty;
        private DateTimeOffset _timestamp = DateTimeOffset.Now;

        private AiCompanionChatMessageViewModel(string sender, string body, bool isReadinessIntro)
        {
            Sender = sender;
            _body = body;
            IsReadinessIntro = isReadinessIntro;
        }

        public string Sender { get; }
        public bool IsReadinessIntro { get; }

        public string Body
        {
            get => _body;
            set
            {
                if (_body == value) return;
                _body = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BodyPreview));
            }
        }

        public string BodyPreview => CleanPreview(Body);

        public DateTimeOffset Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp == value) return;
                _timestamp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimestampLabel));
            }
        }

        public string TimestampLabel => Timestamp.ToLocalTime().ToString("HH:mm");

        private static string CleanPreview(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var lines = value.Replace("**", string.Empty).Replace("__", string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("* ", StringComparison.Ordinal))
                    lines[i] = lines[i][..(lines[i].Length - trimmed.Length)] + "• " + trimmed[2..];
            }

            return string.Join(Environment.NewLine, lines).Trim();
        }

        public static AiCompanionChatMessageViewModel ReadinessIntro(string body)
            => new("AI", body, true);

        public static AiCompanionChatMessageViewModel User(string body)
            => new("You", body, false);

        public static AiCompanionChatMessageViewModel Assistant(string body)
            => new("AI", body, false);
    }
}
