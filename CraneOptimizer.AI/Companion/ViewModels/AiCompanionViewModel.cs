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
        private readonly AiMissionControlService _missionControlService = new();
        private readonly AiShowcaseService _showcaseService = new();
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
        private string _missionDiagnosisTitle = "AI Diagnosis: waiting for project context";
        private string _missionDiagnosisStatus = "Waiting";
        private string _missionRootCause = "Open or create a project so AI Mission Control can analyze crane-planning evidence.";
        private string _missionEvidence = "No live workflow evidence has been evaluated yet.";
        private string _missionRecommendedAction = "Open or create a project from HOME.";
        private string _missionConfidence = "Medium";
        private string _missionRisk = "AI guidance is advisory until deterministic workflow evidence is available.";
        private string _recoveryPlanPreview = "AI recovery plan will appear after live context is evaluated.";
        private string _whatIfAdvisorPreview = "AI what-if advisor will appear after live context is evaluated.";
        private string _reportNarrativePreview = "AI report language will appear after live context is evaluated.";
        private string _showcaseHeroHeadline = "AI Studio: live crane-planning intelligence";
        private string _showcaseHeroSubheadline = "Open or create a project so the AI makeover can bind to live evidence.";
        private string _showcasePowerScoreLabel = "AI visibility: warming up";
        private string _showcaseAgentStackSummary = "Readiness Sentinel · BIM Intelligence · What-If Advisor · Report Narrative Generator";
        private string _showcaseNarrative = "AI capabilities will appear after live workflow context is evaluated.";
        private string _showcaseBrief = "AI showcase brief will appear after live workflow context is evaluated.";
        private double _workflowProgressValue;
        private double _composerHeight = 34d;
        private string _selectedCommandCenterTab = "Chat";
        private string _pinnedReadinessBody = "AI Mission Control will summarize the current project once live context is evaluated.";
        private string _currentModelFingerprint = string.Empty;
        private long _currentModelRevision;
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
            MissionControlCards = new ObservableCollection<AiMissionControlInsight>();
            AiShowcaseCapabilities = new ObservableCollection<AiShowcaseCapability>();
            AiShowcasePrompts = new ObservableCollection<AiShowcasePrompt>();
            AiDecisionTrace = new ObservableCollection<string>();

            ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
            CollapseCommand = new RelayCommand(() => IsExpanded = false);
            ToggleQuietModeCommand = new RelayCommand(() => IsQuietMode = !IsQuietMode);
            OpenFullStudioCommand = new RelayCommand(OpenFullStudio);
            PrepareLocalOllamaQuestionCommand = new RelayCommand(PrepareLocalOllamaQuestion);
            AnalyzeHereCommand = new RelayCommand(AnalyzeHere);
            AskFromHereCommand = new RelayCommand(AskFromHere, () => !Advisor.IsBusy);
            AskToggleCommand = new RelayCommand(ToggleAskOrStop, () => Advisor.IsBusy ? Advisor.CanCancel : true);
            CancelAskCommand = new RelayCommand(() => Advisor.CancelOllamaCommand.Execute(null), () => Advisor.CanCancel);
            SetQuestionCommand = new RelayCommand<string>(SetQuestion);
            SelectCommandCenterTabCommand = new RelayCommand<string>(SelectCommandCenterTab);
            CopyAnswerCommand = new RelayCommand(CopyAnswer, () => Advisor.HasAiAnswer);
            CopyContextCommand = new RelayCommand(CopyContext, () => !string.IsNullOrWhiteSpace(ContextSummary));
            ConfirmActionCommand = new RelayCommand<AiActionProposalViewModel>(ConfirmAction);
            ExecutePrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, () => PrimaryActionProposal != null);
            CopyRecoveryPlanCommand = new RelayCommand(CopyRecoveryPlan, () => !string.IsNullOrWhiteSpace(RecoveryPlanPreview));
            CopyWhatIfAdvisorCommand = new RelayCommand(CopyWhatIfAdvisor, () => !string.IsNullOrWhiteSpace(WhatIfAdvisorPreview));
            CopyReportNarrativeCommand = new RelayCommand(CopyReportNarrative, () => !string.IsNullOrWhiteSpace(ReportNarrativePreview));
            CopyQaEvidencePackCommand = new RelayCommand(CopyQaEvidencePack, () => !string.IsNullOrWhiteSpace(ContextSummary));
            CopyAiShowcaseBriefCommand = new RelayCommand(CopyAiShowcaseBrief, () => !string.IsNullOrWhiteSpace(ShowcaseBrief));

            IsQuietMode = false;
            SubscribeToContextChanges();
            RefreshContext();
        }

        public event EventHandler? ComposerFocusRequested;

        public AiAdvisorViewModel Advisor => _advisor;
        public ObservableCollection<WorkflowReadinessItem> WorkflowChecklist { get; }
        public ObservableCollection<StructuredRecommendationCard> StructuredCards { get; }
        public ObservableCollection<AiActionProposalViewModel> ActionProposals { get; }
        public ObservableCollection<AiCompanionChatMessageViewModel> ConversationMessages { get; }
        public ObservableCollection<AiMissionControlInsight> MissionControlCards { get; }
        public ObservableCollection<AiShowcaseCapability> AiShowcaseCapabilities { get; }
        public ObservableCollection<AiShowcasePrompt> AiShowcasePrompts { get; }
        public ObservableCollection<string> AiDecisionTrace { get; }

        public ICommand ToggleExpandedCommand { get; }
        public ICommand CollapseCommand { get; }
        public ICommand ToggleQuietModeCommand { get; }
        public ICommand OpenFullStudioCommand { get; }
        public ICommand OpenFullCopilotCommand => OpenFullStudioCommand;
        public ICommand PrepareLocalOllamaQuestionCommand { get; }
        public ICommand AnalyzeHereCommand { get; }
        public ICommand AskFromHereCommand { get; }
        public ICommand AskToggleCommand { get; }
        public ICommand CancelAskCommand { get; }
        public ICommand SetQuestionCommand { get; }
        public ICommand SelectCommandCenterTabCommand { get; }
        public ICommand CopyAnswerCommand { get; }
        public ICommand CopyContextCommand { get; }
        public ICommand ConfirmActionCommand { get; }
        public ICommand ExecutePrimaryActionCommand { get; }
        public ICommand CopyRecoveryPlanCommand { get; }
        public ICommand CopyWhatIfAdvisorCommand { get; }
        public ICommand CopyReportNarrativeCommand { get; }
        public ICommand CopyQaEvidencePackCommand { get; }
        public ICommand CopyAiShowcaseBriefCommand { get; }

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
                OnPropertyChanged(nameof(IsProjectReadinessAgentQuestion));
                OnPropertyChanged(nameof(AskToggleLabel));
                OnPropertyChanged(nameof(AskToggleToolTip));
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


        public string MissionDiagnosisTitle
        {
            get => _missionDiagnosisTitle;
            private set
            {
                if (_missionDiagnosisTitle == value) return;
                _missionDiagnosisTitle = value;
                OnPropertyChanged();
            }
        }

        public string MissionDiagnosisStatus
        {
            get => _missionDiagnosisStatus;
            private set
            {
                if (_missionDiagnosisStatus == value) return;
                _missionDiagnosisStatus = value;
                OnPropertyChanged();
            }
        }

        public string MissionRootCause
        {
            get => _missionRootCause;
            private set
            {
                if (_missionRootCause == value) return;
                _missionRootCause = value;
                OnPropertyChanged();
            }
        }

        public string MissionEvidence
        {
            get => _missionEvidence;
            private set
            {
                if (_missionEvidence == value) return;
                _missionEvidence = value;
                OnPropertyChanged();
            }
        }

        public string MissionRecommendedAction
        {
            get => _missionRecommendedAction;
            private set
            {
                if (_missionRecommendedAction == value) return;
                _missionRecommendedAction = value;
                OnPropertyChanged();
            }
        }

        public string MissionConfidence
        {
            get => _missionConfidence;
            private set
            {
                if (_missionConfidence == value) return;
                _missionConfidence = value;
                OnPropertyChanged();
            }
        }

        public string MissionRisk
        {
            get => _missionRisk;
            private set
            {
                if (_missionRisk == value) return;
                _missionRisk = value;
                OnPropertyChanged();
            }
        }

        public string RecoveryPlanPreview
        {
            get => _recoveryPlanPreview;
            private set
            {
                if (_recoveryPlanPreview == value) return;
                _recoveryPlanPreview = value;
                OnPropertyChanged();
            }
        }

        public string WhatIfAdvisorPreview
        {
            get => _whatIfAdvisorPreview;
            private set
            {
                if (_whatIfAdvisorPreview == value) return;
                _whatIfAdvisorPreview = value;
                OnPropertyChanged();
            }
        }

        public string ReportNarrativePreview
        {
            get => _reportNarrativePreview;
            private set
            {
                if (_reportNarrativePreview == value) return;
                _reportNarrativePreview = value;
                OnPropertyChanged();
            }
        }

        public string ShowcaseHeroHeadline
        {
            get => _showcaseHeroHeadline;
            private set
            {
                if (_showcaseHeroHeadline == value) return;
                _showcaseHeroHeadline = value;
                OnPropertyChanged();
            }
        }

        public string ShowcaseHeroSubheadline
        {
            get => _showcaseHeroSubheadline;
            private set
            {
                if (_showcaseHeroSubheadline == value) return;
                _showcaseHeroSubheadline = value;
                OnPropertyChanged();
            }
        }

        public string ShowcasePowerScoreLabel
        {
            get => _showcasePowerScoreLabel;
            private set
            {
                if (_showcasePowerScoreLabel == value) return;
                _showcasePowerScoreLabel = value;
                OnPropertyChanged();
            }
        }

        public string ShowcaseAgentStackSummary
        {
            get => _showcaseAgentStackSummary;
            private set
            {
                if (_showcaseAgentStackSummary == value) return;
                _showcaseAgentStackSummary = value;
                OnPropertyChanged();
            }
        }

        public string ShowcaseNarrative
        {
            get => _showcaseNarrative;
            private set
            {
                if (_showcaseNarrative == value) return;
                _showcaseNarrative = value;
                OnPropertyChanged();
            }
        }

        public string ShowcaseBrief
        {
            get => _showcaseBrief;
            private set
            {
                if (_showcaseBrief == value) return;
                _showcaseBrief = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SelectedCommandCenterTab
        {
            get => _selectedCommandCenterTab;
            private set
            {
                var normalized = NormalizeCommandCenterTab(value);
                if (_selectedCommandCenterTab == normalized) return;
                _selectedCommandCenterTab = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChatTabVisibility));
                OnPropertyChanged(nameof(MissionTabVisibility));
                OnPropertyChanged(nameof(BimTabVisibility));
                OnPropertyChanged(nameof(OptimizeTabVisibility));
                OnPropertyChanged(nameof(ReportTabVisibility));
                OnPropertyChanged(nameof(AiStudioTabVisibility));
                OnPropertyChanged(nameof(IsChatSectionSelected));
                OnPropertyChanged(nameof(IsMissionSectionSelected));
                OnPropertyChanged(nameof(IsBimSectionSelected));
                OnPropertyChanged(nameof(IsOptimizeSectionSelected));
                OnPropertyChanged(nameof(IsReportSectionSelected));
                OnPropertyChanged(nameof(IsAiStudioSectionSelected));
                OnPropertyChanged(nameof(CommandCenterSelectedTabLabel));
            }
        }

        public string CommandCenterTitle => "AI Assist";
        public string CommandCenterSubtitle => "Slim section rail · grounded current-model answers";
        public string CommandCenterSelectedTabLabel => SelectedCommandCenterTab.Equals("AI Studio", StringComparison.OrdinalIgnoreCase) ? "Studio" : SelectedCommandCenterTab;
        public bool IsChatSectionSelected => IsCommandCenterTab("Chat");
        public bool IsMissionSectionSelected => IsCommandCenterTab("Mission");
        public bool IsBimSectionSelected => IsCommandCenterTab("BIM");
        public bool IsOptimizeSectionSelected => IsCommandCenterTab("Optimize");
        public bool IsReportSectionSelected => IsCommandCenterTab("Report");
        public bool IsAiStudioSectionSelected => IsCommandCenterTab("AI Studio");
        public string CommandCenterStateLabel => $"{WorkflowStage} · {MissionDiagnosisStatus} · {WorkflowProgressLabel}";
        public string CommandCenterModelLabel => string.IsNullOrWhiteSpace(_currentModelFingerprint)
            ? $"Model: {ProjectInsight} · {DataInsight}"
            : $"Model: {ProjectInsight} · rev {_currentModelRevision:N0} · {DataInsight}";
        public string CommandCenterNextLabel => $"Next: {NextAction}";
        public Visibility ChatTabVisibility => IsCommandCenterTab("Chat") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MissionTabVisibility => IsCommandCenterTab("Mission") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BimTabVisibility => IsCommandCenterTab("BIM") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OptimizeTabVisibility => IsCommandCenterTab("Optimize") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ReportTabVisibility => IsCommandCenterTab("Report") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AiStudioTabVisibility => IsCommandCenterTab("AI Studio") ? Visibility.Visible : Visibility.Collapsed;

        public string ActiveTabLabel => $"Screen: {_uiContext.ActiveTabName}";
        public string ModelSessionLabel => string.IsNullOrWhiteSpace(_currentModelFingerprint)
            ? "Model context: not evaluated"
            : $"Model context rev {_currentModelRevision:N0}";
        public AiActionProposalViewModel? PrimaryActionProposal => ActionProposals.FirstOrDefault(proposal => proposal.ActionType.Equals("NavigateTab", StringComparison.OrdinalIgnoreCase));
        public string PrimaryActionLabel => PrimaryActionProposal?.ConfirmLabel ?? "Open recommended tab";
        public Visibility PrimaryActionVisibility => PrimaryActionProposal == null ? Visibility.Collapsed : Visibility.Visible;

        public bool HasLatestAnswer => Advisor.HasAiAnswer;
        public Visibility LatestAnswerVisibility => HasLatestAnswer ? Visibility.Visible : Visibility.Collapsed;

        public string PinnedReadinessBody
        {
            get => _pinnedReadinessBody;
            private set
            {
                if (_pinnedReadinessBody == value) return;
                _pinnedReadinessBody = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinnedReadinessVisibility));
            }
        }

        public Visibility PinnedReadinessVisibility => string.IsNullOrWhiteSpace(PinnedReadinessBody) ? Visibility.Collapsed : Visibility.Visible;

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

        public bool IsProjectReadinessAgentQuestion => !Advisor.IsBusy && Advisor.CanRunProjectReadinessAgent(QuestionText);
        public string AskToggleLabel => Advisor.IsBusy ? "Stop" : IsProjectReadinessAgentQuestion ? "Run Agent" : "Send";
        public string AskToggleToolTip => Advisor.IsBusy
            ? "Stop the current local Ollama response."
            : IsProjectReadinessAgentQuestion
                ? "Run the governed Project Readiness Agent with typed read-only project tools."
                : "Send the visible question with private live project context. Enter also sends.";

        public string CollapsedTitle => $"AI Assist · {WorkflowStage}";
        public string CollapsedSubText => IsQuietMode ? "Quiet mode enabled" : CurrentHint;
        public string QuietModeLabel => IsQuietMode ? "Quiet" : "Quiet mode";
        public double PanelWidth => IsExpanded ? 460d : 320d;
        public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CollapsedVisibility => IsExpanded ? Visibility.Collapsed : Visibility.Visible;

        private void NotifyCommandCenterSummaryChanged()
        {
            OnPropertyChanged(nameof(CommandCenterStateLabel));
            OnPropertyChanged(nameof(CommandCenterModelLabel));
            OnPropertyChanged(nameof(CommandCenterNextLabel));
            OnPropertyChanged(nameof(ModelSessionLabel));
        }

        public void RefreshContext()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();
            UpdateConversationFreshness(liveContext);

            Advisor.SetActiveProjectContext(liveContext.ProjectKey, liveContext.ProjectDisplayName);
            var snapshot = _workflowReadinessService.Evaluate(liveContext, _uiContext);
            WorkflowStage = snapshot.CurrentStage;
            WorkflowProgressLabel = $"{snapshot.CompleteCount}/{snapshot.TotalCount} complete · {snapshot.MissingCriticalCount} critical missing";
            WorkflowProgressValue = Math.Max(0d, Math.Min(100d, snapshot.CompletionRatio * 100d));

            var mission = _missionControlService.Build(liveContext, snapshot, _uiContext.ActiveTabName);
            MissionDiagnosisTitle = mission.DiagnosisTitle;
            MissionDiagnosisStatus = mission.DiagnosisStatus;
            MissionRootCause = mission.RootCause;
            MissionEvidence = mission.Evidence;
            MissionRecommendedAction = mission.RecommendedAction;
            MissionConfidence = mission.Confidence;
            MissionRisk = mission.Risk;
            RecoveryPlanPreview = mission.RecoveryPlan;
            WhatIfAdvisorPreview = mission.WhatIfAdvisor;
            ReportNarrativePreview = mission.ReportNarrative;
            MissionControlCards.Clear();
            foreach (var insight in mission.Insights)
                MissionControlCards.Add(insight);

            var showcase = _showcaseService.Build(liveContext, snapshot, mission, _uiContext.ActiveTabName);
            ShowcaseHeroHeadline = showcase.HeroHeadline;
            ShowcaseHeroSubheadline = showcase.HeroSubheadline;
            ShowcasePowerScoreLabel = showcase.PowerScoreLabel;
            ShowcaseAgentStackSummary = showcase.AgentStackSummary;
            ShowcaseNarrative = showcase.Narrative;
            ShowcaseBrief = showcase.PresentationBrief;

            AiShowcaseCapabilities.Clear();
            foreach (var capability in showcase.Capabilities)
                AiShowcaseCapabilities.Add(capability);

            AiShowcasePrompts.Clear();
            foreach (var prompt in showcase.DemoPrompts)
                AiShowcasePrompts.Add(prompt);

            AiDecisionTrace.Clear();
            foreach (var traceItem in showcase.DecisionTrace)
                AiDecisionTrace.Add(traceItem);

            var firstCard = _workflowReadinessService.BuildStructuredCards(snapshot).FirstOrDefault();
            if (firstCard != null)
            {
                CurrentHint = firstCard.Recommendation;
                NextAction = firstCard.NextClick;
            }
            else
            {
                CurrentHint = snapshot.Summary;
                NextAction = "Review AI Assist context or open AI Studio.";
            }

            WorkflowChecklist.Clear();
            foreach (var item in snapshot.Items)
                WorkflowChecklist.Add(item);

            StructuredCards.Clear();
            foreach (var card in _workflowReadinessService.BuildStructuredCards(snapshot))
                StructuredCards.Add(card);

            RefreshActionProposals(snapshot, liveContext);

            var elementCount = liveContext.ElementCount;
            var zoneCount = liveContext.ZoneCount;
            var craneCount = liveContext.CraneCount;
            var hasProject = liveContext.HasProject;
            var hasResult = liveContext.OptimizationResult.HasResult;

            ProjectInsight = hasProject ? $"Project · {liveContext.CurrentProjectName}" : "Project · not opened";
            DataInsight = liveContext.ElementTableTotalCount > 0
                ? $"{elementCount} BIM objects · {liveContext.ElementTableVisibleCount}/{liveContext.ElementTableTotalCount} table items visible · {zoneCount} zones · {BuildCraneInsight(craneCount)}"
                : $"{elementCount} BIM objects · {zoneCount} zones · {BuildCraneInsight(craneCount)}";
            ResultInsight = BuildResultInsight(liveContext.OptimizationResult);
            RiskInsight = snapshot.MissingCriticalCount == 0
                ? "No critical readiness gaps"
                : $"{snapshot.MissingCriticalCount} critical readiness gap(s)";

            ContextSummary = BuildContextSummary(liveContext, snapshot);
            RefreshAssistantIntroMessage();
            OnPropertyChanged(nameof(ActiveTabLabel));
            OnPropertyChanged(nameof(CollapsedTitle));
            OnPropertyChanged(nameof(CollapsedSubText));
            NotifyCommandCenterSummaryChanged();
        }


        private void UpdateConversationFreshness(AiWorkflowContextSnapshot liveContext)
        {
            var nextFingerprint = liveContext.ModelFingerprint ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_currentModelFingerprint)
                && !string.Equals(_currentModelFingerprint, nextFingerprint, StringComparison.Ordinal))
            {
                foreach (var message in ConversationMessages)
                    message.MarkStaleAgainst(nextFingerprint);
            }

            _currentModelFingerprint = nextFingerprint;
            _currentModelRevision = liveContext.ModelRevision;
            OnPropertyChanged(nameof(ModelSessionLabel));
        }

        private static string BuildModelContextLabel(AiWorkflowContextSnapshot context)
        {
            if (!string.IsNullOrWhiteSpace(context.ModelDisplayName))
                return context.ModelDisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(context.CurrentProjectName))
                return context.CurrentProjectName.Trim();

            return context.ElementCount > 0 ? "Imported IFC model" : "No IFC model loaded";
        }

        private void RefreshAssistantIntroMessage()
        {
            var body = new StringBuilder();
            body.AppendLine($"{MissionDiagnosisTitle}: {MissionRootCause}");
            body.AppendLine();
            body.AppendLine(CurrentHint);
            body.AppendLine();
            var liveContext = _workflowContextProvider.GetSnapshot();
            var state = AiWorkflowStateClassifier.Classify(liveContext);
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                body.AppendLine("Optimizer note: GA is blocked because deterministic readiness has zero trusted GA-ready lift units. Review trusted weight/lift-unit evidence before running optimization.");
                body.AppendLine();
            }
            body.AppendLine($"Next: {NextAction}");
            body.AppendLine();
            body.Append("Use the fixed question box below to chat. Open a focused section to review mission, BIM, optimization, report, or AI Studio tools.");

            PinnedReadinessBody = body.ToString().Trim();
            RemoveReadinessIntroMessages();
        }

        private void RemoveReadinessIntroMessages()
        {
            for (var i = ConversationMessages.Count - 1; i >= 0; i--)
            {
                if (ConversationMessages[i].IsReadinessIntro)
                    ConversationMessages.RemoveAt(i);
            }
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

        private void RefreshActionProposals(WorkflowReadinessSnapshot snapshot, AiWorkflowContextSnapshot liveContext)
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

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Generate BIM exporter request",
                Description = "Copy a ready-to-send request for explicit trusted mass/weight, assembly marks, identifiers, and COG evidence.",
                ActionType = "CopyBimExporterChecklist",
                Target = "Clipboard",
                Risk = "Low — exports advisory text only; does not change optimizer eligibility.",
                ConfirmLabel = "Copy BIM request",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Create AI recovery checklist",
                Description = "Copy a step-by-step recovery plan with owner, evidence, verification, and safety boundary.",
                ActionType = "CopyRecoveryPlan",
                Target = "Clipboard",
                Risk = "Low — exports text only for review.",
                ConfirmLabel = "Copy recovery",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Summarize issue for PM / QA",
                Description = "Copy a concise deterministic issue summary and evidence pack for stakeholder review.",
                ActionType = "CopyQaEvidencePack",
                Target = "Clipboard",
                Risk = "Low — exports current evidence and AI advisory text only.",
                ConfirmLabel = "Copy QA pack",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Copy AI makeover showcase brief",
                Description = "Copy the live AI Studio story: capability stack, demo prompts, decision trace, and safety boundary for presentations.",
                ActionType = "CopyAiShowcaseBrief",
                Target = "Clipboard",
                Risk = "Low — exports text only for demonstration and review.",
                ConfirmLabel = "Copy AI brief",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Ask AI what-if advisor",
                Description = "Copy the current what-if assessment: whether to fix data, zones, crane model, load chart, or diagnostics first.",
                ActionType = "CopyWhatIfAdvisor",
                Target = "Clipboard",
                Risk = "Low — ranks investigation levers only; it does not perform optimization changes.",
                ConfirmLabel = "Copy what-if",
                ConfirmCommand = ConfirmActionCommand
            });

            ActionProposals.Add(new AiActionProposalViewModel
            {
                Title = "Draft report caveat",
                Description = "Copy AI-assisted report language generated from deterministic readiness/result evidence.",
                ActionType = "CopyReportNarrative",
                Target = "Clipboard",
                Risk = "Low — exports draft text only; reviewer approval is required before issue.",
                ConfirmLabel = "Copy report text",
                ConfirmCommand = ConfirmActionCommand
            });

            foreach (var hint in liveContext.ScreenContexts
                         .SelectMany(context => context.ActionHints)
                         .Where(hint => IsSupportedConfirmableAction(hint.ActionType))
                         .GroupBy(hint => $"{hint.ActionType}|{hint.Target}", StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First())
                         .Take(6))
            {
                if (ActionProposals.Any(action => action.ActionType.Equals(hint.ActionType, StringComparison.OrdinalIgnoreCase)
                                                 && action.Target.Equals(hint.Target, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ActionProposals.Add(new AiActionProposalViewModel
                {
                    Title = hint.Label,
                    Description = hint.Reason,
                    ActionType = hint.ActionType,
                    Target = hint.Target,
                    Risk = string.IsNullOrWhiteSpace(hint.Risk) ? "Low — user-confirmed helper action only." : hint.Risk,
                    ConfirmLabel = BuildConfirmLabel(hint),
                    ConfirmCommand = ConfirmActionCommand
                });
            }

            OnPropertyChanged(nameof(PrimaryActionProposal));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionVisibility));
            CommandManager.InvalidateRequerySuggested();
        }


        private static bool IsSupportedConfirmableAction(string actionType) =>
            actionType.Equals("NavigateTab", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyContext", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyReportNarrative", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyBimExporterChecklist", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyIssueSummary", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyRecoveryPlan", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyWhatIfAdvisor", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyQaEvidencePack", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("CopyAiShowcaseBrief", StringComparison.OrdinalIgnoreCase)
            || actionType.Equals("SetQuestion", StringComparison.OrdinalIgnoreCase);

        private static string BuildConfirmLabel(AiContextActionHintSnapshot hint)
        {
            if (hint.ActionType.Equals("NavigateTab", StringComparison.OrdinalIgnoreCase))
                return $"Open {hint.Target}";

            if (hint.ActionType.Equals("CopyReportNarrative", StringComparison.OrdinalIgnoreCase))
                return "Copy narrative";

            if (hint.ActionType.Equals("CopyBimExporterChecklist", StringComparison.OrdinalIgnoreCase))
                return "Copy checklist";

            if (hint.ActionType.Equals("CopyIssueSummary", StringComparison.OrdinalIgnoreCase))
                return "Copy summary";

            if (hint.ActionType.Equals("CopyRecoveryPlan", StringComparison.OrdinalIgnoreCase))
                return "Copy recovery";

            if (hint.ActionType.Equals("CopyWhatIfAdvisor", StringComparison.OrdinalIgnoreCase))
                return "Copy what-if";

            if (hint.ActionType.Equals("CopyQaEvidencePack", StringComparison.OrdinalIgnoreCase))
                return "Copy QA pack";

            if (hint.ActionType.Equals("CopyAiShowcaseBrief", StringComparison.OrdinalIgnoreCase))
                return "Copy AI brief";

            if (hint.ActionType.Equals("SetQuestion", StringComparison.OrdinalIgnoreCase))
                return "Use question";

            return "Confirm";
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
                return;
            }

            if (proposal.ActionType.Equals("CopyReportNarrative", StringComparison.OrdinalIgnoreCase))
            {
                CopyReportNarrative();
                return;
            }

            if (proposal.ActionType.Equals("CopyBimExporterChecklist", StringComparison.OrdinalIgnoreCase))
            {
                CopyBimExporterChecklist();
                return;
            }

            if (proposal.ActionType.Equals("CopyIssueSummary", StringComparison.OrdinalIgnoreCase))
            {
                CopyIssueSummary();
                return;
            }

            if (proposal.ActionType.Equals("CopyRecoveryPlan", StringComparison.OrdinalIgnoreCase))
            {
                CopyRecoveryPlan();
                return;
            }

            if (proposal.ActionType.Equals("CopyWhatIfAdvisor", StringComparison.OrdinalIgnoreCase))
            {
                CopyWhatIfAdvisor();
                return;
            }

            if (proposal.ActionType.Equals("CopyQaEvidencePack", StringComparison.OrdinalIgnoreCase))
            {
                CopyQaEvidencePack();
                return;
            }

            if (proposal.ActionType.Equals("CopyAiShowcaseBrief", StringComparison.OrdinalIgnoreCase))
            {
                CopyAiShowcaseBrief();
                return;
            }

            if (proposal.ActionType.Equals("SetQuestion", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(proposal.Target))
                    SetQuestion(proposal.Target);
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
            builder.AppendLine($"Model context: {BuildModelContextLabel(context)}");
            builder.AppendLine($"Model revision: {context.ModelRevision}");
            builder.AppendLine($"Model fingerprint: {context.ModelFingerprint}");
            builder.AppendLine($"Total BIM objects: {context.ElementCount}");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"Element table scope: {context.ElementTableVisibleCount} visible of {context.ElementTableTotalCount}");
            builder.AppendLine($"Elements: {context.ElementCount}");
            builder.AppendLine($"Zones: {context.ZoneCount} total; {context.AllowedZoneCount} allowed/green; {context.ForbiddenZoneCount} forbidden");
            if (context.AllowedZoneArea > 0 || context.ForbiddenZoneArea > 0)
                builder.AppendLine($"Zone stored areas: allowed {context.AllowedZoneArea:0.##} m²; forbidden {context.ForbiddenZoneArea:0.##} m²");
            builder.AppendLine($"Cranes in scene: {context.CraneCount}");
            builder.AppendLine($"Active load-chart evidence sources: {context.LoadChartEvidenceCount}; has load chart evidence: {context.HasLoadChartEvidence}");
            if (context.BuildingArea > 0 || context.BuildingHeight > 0)
                builder.AppendLine($"Building/model metrics: area {context.BuildingArea:0.##} {context.BuildingAreaUnit}; height {context.BuildingHeight:0.##} {context.BuildingLengthUnit}");

            if (result.HasResult)
            {
                builder.AppendLine($"Latest coverage: {result.CoverageRatio:P2}");
                builder.AppendLine($"Lifted elements: {result.LiftedElementCount}/{result.TotalElementCount}");
                builder.AppendLine($"Setup positions: {result.PositionCount}");
                if (!string.IsNullOrWhiteSpace(result.DiagnosticsPath))
                    builder.AppendLine($"Diagnostics path: {result.DiagnosticsPath}");
            }
            else
            {
                builder.AppendLine("Latest optimization: none in current session");
            }

            if (context.ScreenContexts.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Screen evidence:");
                foreach (var screen in context.ScreenContexts)
                {
                    builder.AppendLine($"- {screen.ScreenName}: {screen.Stage} — {screen.Summary}");
                    foreach (var metric in screen.Metrics.Take(4))
                        builder.AppendLine($"  • {metric.Name}: {metric.Value}{(string.IsNullOrWhiteSpace(metric.Detail) ? string.Empty : " — " + metric.Detail)}");
                    foreach (var issue in screen.Issues
                                 .Where(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                                                 || issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                                 .Take(3))
                    {
                        builder.AppendLine($"  ! {issue.Severity} {issue.Code}: {issue.Evidence} Recommendation: {issue.Recommendation}");
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine("AI Mission Control");
            builder.AppendLine($"- Diagnosis: {MissionDiagnosisTitle}");
            builder.AppendLine($"- Root cause: {MissionRootCause}");
            builder.AppendLine($"- Evidence: {MissionEvidence}");
            builder.AppendLine($"- Recommended action: {MissionRecommendedAction}");
            builder.AppendLine($"- Risk: {MissionRisk}");
            builder.AppendLine($"- Confidence: {MissionConfidence}");
            builder.AppendLine();
            builder.AppendLine("AI Studio Showcase");
            builder.AppendLine($"- Headline: {ShowcaseHeroHeadline}");
            builder.AppendLine($"- Agent stack: {ShowcaseAgentStackSummary}");
            builder.AppendLine($"- Visibility: {ShowcasePowerScoreLabel}");
            builder.AppendLine("- Demo prompts: " + string.Join(" | ", AiShowcasePrompts.Take(4).Select(prompt => prompt.Label)));

            var workflowState = AiWorkflowStateClassifier.Classify(context, snapshot);
            if (workflowState.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked
                || workflowState.Kind == AiWorkflowStateKind.NoIfcModel)
            {
                builder.AppendLine();
                builder.AppendLine(AiWorkflowEvidenceTextBuilder.BuildWorkflowBlockerSummary(context));
            }

            return builder.ToString().Trim();
        }

        private void SelectCommandCenterTab(string? requestedTab)
        {
            SelectedCommandCenterTab = requestedTab ?? "Chat";
        }

        private static string NormalizeCommandCenterTab(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Equals("Mission", StringComparison.OrdinalIgnoreCase)) return "Mission";
            if (text.Equals("BIM", StringComparison.OrdinalIgnoreCase)) return "BIM";
            if (text.Equals("Optimize", StringComparison.OrdinalIgnoreCase)) return "Optimize";
            if (text.Equals("Report", StringComparison.OrdinalIgnoreCase)) return "Report";
            if (text.Equals("AI Studio", StringComparison.OrdinalIgnoreCase) || text.Equals("AI", StringComparison.OrdinalIgnoreCase) || text.Equals("Studio", StringComparison.OrdinalIgnoreCase)) return "AI Studio";
            return "Chat";
        }

        private bool IsCommandCenterTab(string tab) => string.Equals(SelectedCommandCenterTab, tab, StringComparison.OrdinalIgnoreCase);

        private void FocusChatForSubmittedPrompt(bool requestComposerFocus = true)
        {
            // A submitted or staged prompt is a conversation event first. Intent routing may still
            // choose the evidence source for the answer, but it must not steal the
            // right-docked panel away from CHAT after the user presses Send/Enter.
            IsExpanded = true;
            SelectedCommandCenterTab = "Chat";
            if (requestComposerFocus)
                RequestComposerFocus();
        }

        private void RequestComposerFocus()
        {
            ComposerFocusRequested?.Invoke(this, EventArgs.Empty);
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

            var visibleQuestion = string.IsNullOrWhiteSpace(QuestionText)
                ? "What should I do next from this screen?"
                : QuestionText.Trim();

            var activeScreenAtSend = string.IsNullOrWhiteSpace(_uiContext.ActiveTabName) ? "HOME" : _uiContext.ActiveTabName.Trim();
            var activePanelAtSend = string.IsNullOrWhiteSpace(_uiContext.ActivePanelName) ? activeScreenAtSend : _uiContext.ActivePanelName.Trim();
            var uiContextAtSend = new CapturedUiInteractionContext(activeScreenAtSend, activePanelAtSend, _uiContext.LastUpdatedAt);
            var liveContext = _workflowContextProvider.GetSnapshot();
            var nextActionAtSend = NextAction;

            ConversationMessages.Add(AiCompanionChatMessageViewModel.User(visibleQuestion, liveContext));
            QuestionText = string.Empty;
            FocusChatForSubmittedPrompt();

            var readiness = _workflowReadinessService.Evaluate(liveContext, uiContextAtSend);

            if (Advisor.CanRunProjectReadinessAgent(visibleQuestion))
            {
                await Advisor.RunProjectReadinessAgentAsync(
                    liveContext,
                    visibleQuestion,
                    activeScreenAtSend,
                    nextActionAtSend).ConfigureAwait(true);
                ConversationMessages.Add(AiCompanionChatMessageViewModel.Assistant(Advisor.AiAnswer, liveContext));
                FocusChatForSubmittedPrompt();
                return;
            }

            if (AiIntentOrchestrator.TryBuildDeterministicAnswer(
                    liveContext,
                    readiness,
                    visibleQuestion,
                    activeScreenAtSend,
                    Advisor.ModelName,
                    Advisor.ModelCapabilitySummary,
                    Advisor.BenchmarkSummary,
                    nextActionAtSend,
                    out var routedAnswer,
                    out var routedSource,
                    out _))
            {
                Advisor.RecordDeterministicCompanionAnswer(visibleQuestion, routedAnswer, routedSource);
                ConversationMessages.Add(AiCompanionChatMessageViewModel.Assistant(routedAnswer, liveContext));
                FocusChatForSubmittedPrompt();
                return;
            }

            if (AiBimProjectQuestionService.TryBuildAnswer(liveContext, visibleQuestion, activeScreenAtSend, out var bimAnswer, out var bimSource))
            {
                Advisor.RecordDeterministicCompanionAnswer(visibleQuestion, bimAnswer, bimSource);
                ConversationMessages.Add(AiCompanionChatMessageViewModel.Assistant(bimAnswer, liveContext));
                FocusChatForSubmittedPrompt();
                return;
            }

            if (AiWorkflowEvidenceTextBuilder.IsIfcMetadataQuestion(visibleQuestion))
            {
                var metadataAnswer = AiWorkflowEvidenceTextBuilder.BuildIfcMetadataAssistantAnswer(
                    liveContext,
                    visibleQuestion,
                    activeScreenAtSend);

                Advisor.RecordDeterministicCompanionAnswer(
                    visibleQuestion,
                    metadataAnswer,
                    "Deterministic IFC metadata guidance produced locally; Ollama was not required for this evidence-routing answer.");

                ConversationMessages.Add(AiCompanionChatMessageViewModel.Assistant(metadataAnswer, liveContext));
                FocusChatForSubmittedPrompt();
                return;
            }

            if (AiIntentOrchestrator.ShouldUseStrictWorkflowBlockerRouting(liveContext, visibleQuestion, activeScreenAtSend))
            {
                var deterministicAnswer = AiWorkflowEvidenceTextBuilder.BuildNoGaCandidateAssistantAnswer(
                    liveContext,
                    visibleQuestion,
                    nextActionAtSend,
                    activeScreenAtSend);

                Advisor.RecordDeterministicCompanionAnswer(
                    visibleQuestion,
                    deterministicAnswer,
                    "Deterministic blocker guidance produced locally; Ollama was not required for this safety-critical next-step answer.");

                ConversationMessages.Add(AiCompanionChatMessageViewModel.Assistant(deterministicAnswer, liveContext));
                FocusChatForSubmittedPrompt();
                return;
            }

            _liveAssistantMessage = AiCompanionChatMessageViewModel.Assistant("Generating locally… live response will appear here as Ollama streams.", liveContext);
            ConversationMessages.Add(_liveAssistantMessage);
            FocusChatForSubmittedPrompt();

            try
            {
                await Advisor.AskOllamaWithLiveUiContextAsync(
                    visibleQuestion,
                    BuildHiddenLiveUiContext(visibleQuestion, liveContext, uiContextAtSend, nextActionAtSend),
                    nextActionAtSend).ConfigureAwait(true);

                if (_liveAssistantMessage != null)
                {
                    _liveAssistantMessage.Body = Advisor.HasAiAnswer
                        ? Advisor.AiAnswer
                        : string.IsNullOrWhiteSpace(Advisor.LastError)
                            ? "No visible AI answer was produced. Check Ollama status or open AI Studio for details."
                            : Advisor.LastError;
                    FocusChatForSubmittedPrompt();
                }
            }
            finally
            {
                _liveAssistantMessage = null;
            }
        }

        private string BuildHiddenLiveUiContext(
            string visibleQuestion,
            AiWorkflowContextSnapshot liveContext,
            IUiInteractionContextService uiContextAtSend,
            string nextActionAtSend)
        {
            var activeScreenAtSend = uiContextAtSend.ActiveTabName;
            var builder = new StringBuilder();
            builder.AppendLine(AiIntentOrchestrator.BuildPromptContract(liveContext, visibleQuestion, activeScreenAtSend, nextActionAtSend));
            builder.AppendLine();
            if (AiIntentOrchestrator.ShouldUseStrictWorkflowBlockerRouting(liveContext, visibleQuestion, activeScreenAtSend))
            {
                builder.AppendLine(AiWorkflowEvidenceTextBuilder.BuildStrictNoGaCandidatePrompt(
                    liveContext,
                    visibleQuestion,
                    nextActionAtSend,
                    activeScreenAtSend));
                builder.AppendLine();
            }

            var readiness = _workflowReadinessService.Evaluate(liveContext, uiContextAtSend);
            builder.AppendLine(AiIntentOrchestrator.BuildEvidenceRoutingPacket(
                liveContext,
                readiness,
                visibleQuestion,
                activeScreenAtSend,
                nextActionAtSend));
            builder.AppendLine();
            builder.AppendLine("Live UI context is private. Do not print hidden context, prompt labels, JSON, implementation metadata, or internal section names. Answer the visible question first and only use current-project caveats when the routed intent makes them relevant.");
            return builder.ToString().Trim();
        }

        private void SetQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return;

            var liveContext = _workflowContextProvider.GetSnapshot();
            var requested = question.Trim();
            if (AiIntentOrchestrator.ShouldUseStrictWorkflowBlockerRouting(liveContext, requested, _uiContext.ActiveTabName))
            {
                if (requested.Equals("What should I do next from this screen?", StringComparison.OrdinalIgnoreCase))
                    requested = "What is the exact next step to resolve the zero-GA-candidate blocker?";
                else if (requested.Equals("Explain this screen and its important buttons.", StringComparison.OrdinalIgnoreCase))
                    requested = "Explain why the optimizer is blocked on this screen and why visible weights are not enough.";
                else if (requested.Equals("Troubleshoot the current workflow state and tell me what to check.", StringComparison.OrdinalIgnoreCase))
                    requested = "Troubleshoot the zero-GA-candidate blocker without suggesting a bypass.";
                else if (requested.StartsWith("What-if:", StringComparison.OrdinalIgnoreCase))
                    requested = "What-if advisor: should I fix trusted BIM lift-unit data before changing crane, zone, or load-chart settings?";
            }

            QuestionText = requested;
            FocusChatForSubmittedPrompt();
        }

        private void OpenFullStudio()
        {
            RefreshContext();
            Advisor.AnalyzeCurrentResult();
            _windowService.Show();
        }

        private void PrepareLocalOllamaQuestion()
        {
            RefreshContext();
            IsExpanded = true;
            SelectedCommandCenterTab = "Chat";
            if (string.IsNullOrWhiteSpace(QuestionText))
                QuestionText = "Explain the current project state and recommended next step.";
            RequestComposerFocus();
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


        private void CopyBimExporterChecklist()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();
            Clipboard.SetText(AiWorkflowEvidenceTextBuilder.BuildBimExporterChecklist(liveContext));
        }

        private void CopyIssueSummary()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();
            Clipboard.SetText(AiWorkflowEvidenceTextBuilder.BuildOptimizerIssueSummary(liveContext));
        }

        private void CopyReportNarrative()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();
            var snapshot = _workflowReadinessService.Evaluate(liveContext, _uiContext);
            Clipboard.SetText(BuildReportNarrativeDraft(liveContext, snapshot));
        }

        private static string BuildReportNarrativeDraft(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot snapshot)
        {
            return AiWorkflowEvidenceTextBuilder.BuildAiReportNarrativeDraft(context, snapshot);
        }

        private void CopyRecoveryPlan()
        {
            RefreshContext();
            if (!string.IsNullOrWhiteSpace(RecoveryPlanPreview))
                Clipboard.SetText(RecoveryPlanPreview);
        }

        private void CopyWhatIfAdvisor()
        {
            RefreshContext();
            if (!string.IsNullOrWhiteSpace(WhatIfAdvisorPreview))
                Clipboard.SetText(WhatIfAdvisorPreview);
        }

        private void CopyQaEvidencePack()
        {
            var liveContext = _workflowContextProvider.GetSnapshot();
            var snapshot = _workflowReadinessService.Evaluate(liveContext, _uiContext);
            Clipboard.SetText(AiWorkflowEvidenceTextBuilder.BuildQaEvidencePack(liveContext, snapshot, _uiContext.ActiveTabName));
        }

        private void CopyAiShowcaseBrief()
        {
            RefreshContext();
            if (!string.IsNullOrWhiteSpace(ShowcaseBrief))
                Clipboard.SetText(ShowcaseBrief);
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

        private sealed class CapturedUiInteractionContext : IUiInteractionContextService
        {
            public CapturedUiInteractionContext(string activeTabName, string activePanelName, DateTimeOffset lastUpdatedAt)
            {
                ActiveTabName = string.IsNullOrWhiteSpace(activeTabName) ? "HOME" : activeTabName.Trim();
                ActivePanelName = string.IsNullOrWhiteSpace(activePanelName) ? ActiveTabName : activePanelName.Trim();
                LastUpdatedAt = lastUpdatedAt;
            }

            public event EventHandler? ContextChanged { add { } remove { } }
            public event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested { add { } remove { } }

            public string ActiveTabName { get; }
            public string ActivePanelName { get; }
            public DateTimeOffset LastUpdatedAt { get; }

            public void SetActiveTab(string tabName, string panelName)
            {
            }

            public void RequestTabNavigation(string tabName, string reason)
            {
            }
        }

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
        private bool _isStale;
        private string _staleContextLabel = string.Empty;

        private AiCompanionChatMessageViewModel(string sender, string body, bool isReadinessIntro, AiWorkflowContextSnapshot? context)
        {
            Sender = sender;
            _body = body;
            IsReadinessIntro = isReadinessIntro;
            if (context != null)
            {
                ContextFingerprint = context.ModelFingerprint ?? string.Empty;
                ContextRevision = context.ModelRevision;
                ContextElementCount = context.ElementCount;
                ContextLabel = BuildContextLabel(context);
                _staleContextLabel = $"Captured context at send time · {ContextLabel}";
            }
        }

        public string Sender { get; }
        public bool IsReadinessIntro { get; }
        public string ContextFingerprint { get; } = string.Empty;
        public long ContextRevision { get; }
        public int ContextElementCount { get; }
        public string ContextLabel { get; } = string.Empty;

        public bool IsStale
        {
            get => _isStale;
            private set
            {
                if (_isStale == value) return;
                _isStale = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StaleVisibility));
            }
        }

        public string StaleContextLabel
        {
            get => _staleContextLabel;
            private set
            {
                if (_staleContextLabel == value) return;
                _staleContextLabel = value;
                OnPropertyChanged();
            }
        }

        public Visibility StaleVisibility => IsStale ? Visibility.Visible : Visibility.Collapsed;

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

        public void MarkStaleAgainst(string currentFingerprint)
        {
            if (IsReadinessIntro || string.IsNullOrWhiteSpace(ContextFingerprint))
                return;

            var isCurrent = string.Equals(ContextFingerprint, currentFingerprint, StringComparison.Ordinal);
            IsStale = !isCurrent;
            if (IsStale)
                StaleContextLabel = $"Stale captured context · {ContextLabel}";
        }

        private static string BuildContextLabel(AiWorkflowContextSnapshot context)
        {
            var label = !string.IsNullOrWhiteSpace(context.ModelDisplayName)
                ? context.ModelDisplayName.Trim()
                : !string.IsNullOrWhiteSpace(context.CurrentProjectName)
                    ? context.CurrentProjectName.Trim()
                    : context.ElementCount > 0
                        ? "Imported IFC model"
                        : "No IFC model";

            return $"{label} · rev {context.ModelRevision:N0} · {context.ElementCount:N0} BIM object(s)";
        }

        private static string CleanPreview(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var lines = value.Replace("**", string.Empty).Replace("__", string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var i = 0; i < lines.Length; i++)
            {
                var leadingLength = lines[i].Length - lines[i].TrimStart().Length;
                var leading = leadingLength > 0 ? lines[i][..leadingLength] : string.Empty;
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("### ", StringComparison.Ordinal))
                    lines[i] = leading + trimmed[4..];
                else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                    lines[i] = leading + trimmed[3..];
                else if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                    lines[i] = leading + trimmed[2..];
                else if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    lines[i] = leading + "• " + trimmed[2..];
                else if (trimmed.StartsWith("* ", StringComparison.Ordinal))
                    lines[i] = leading + "• " + trimmed[2..];
            }

            return string.Join(Environment.NewLine, lines).Trim();
        }

        public static AiCompanionChatMessageViewModel ReadinessIntro(string body)
            => new("AI", body, true, null);

        public static AiCompanionChatMessageViewModel User(string body, AiWorkflowContextSnapshot? context = null)
            => new("You", body, false, context);

        public static AiCompanionChatMessageViewModel Assistant(string body, AiWorkflowContextSnapshot? context = null)
            => new("AI", body, false, context);
    }
}