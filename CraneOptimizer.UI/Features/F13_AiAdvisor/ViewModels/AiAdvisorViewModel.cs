using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CraneOptimizer.Application.AI.DTOs;
using CraneOptimizer.Application.AI.Interfaces;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Configuration;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using Microsoft.Win32;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels
{
    public sealed class AiAdvisorViewModel : ViewModelBase
    {
        private static readonly JsonSerializerOptions ContextJsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly IAiOptimizationAdvisorService _advisorService;
        private readonly IAiChatService _chatService;
        private readonly IAiSoftwareContextService _softwareContextService;
        private readonly IAiModelManagementService _modelManagementService;
        private readonly IAiCopilotSettingsService _settingsService;
        private readonly IAiLocalDocumentSearchService _documentSearchService;
        private readonly IAiSoftwareInventoryService _softwareInventoryService;
        private readonly IAiPromptTemplateService _promptTemplateService;
        private readonly IAiReportAppendixService _reportAppendixService;
        private readonly CranePlacementViewModel _cranePlacement;
        private readonly AiSoftwareContextDto _softwareContext;

        private CancellationTokenSource? _activeRequestCts;
        private string _pendingLiveUiContext = string.Empty;
        private string _pendingDeterministicNextAction = string.Empty;
        private string _modelName;
        private string _userQuestion = "Explain the current crane optimization result and recommend the next practical action.";
        private string _advisorSummary = "Run an optimization, then click Analyze Current Result.";
        private string _healthLabel = "Waiting";
        private string _aiAnswer = "No Ollama answer yet. Select or type a model, enter a question, then click Ask Ollama.";
        private string _lastError = string.Empty;
        private string _statusText = "Local Ollama mode. No cloud API is required.";
        private string _contextPreview = string.Empty;
        private string _softwareGuideText = string.Empty;
        private string _ollamaStatusText = "Ollama status not checked.";
        private string _lastDocumentContext = "No local documentation snippets have been attached yet.";
        private string _lastXamlInventory = "XAML-derived software inventory has not been generated yet.";
        private string _selectedPromptTemplateSummary = "Prompt template will be selected after analysis.";
        private string _modelCapabilitySummary = "Run a benchmark to receive model capability recommendations.";
        private string _activeProjectKey = "global";
        private string _activeProjectDisplayName = "Global";
        private string _benchmarkSummary = "Benchmark not run.";
        private bool _isBusy;
        private AiOptimizationContextDto? _lastContext;
        private AiAdvisorResponseDto? _lastAdvisorResponse;

        public AiAdvisorViewModel(
            IAiOptimizationAdvisorService advisorService,
            IAiChatService chatService,
            IAiSoftwareContextService softwareContextService,
            IAiModelManagementService modelManagementService,
            IAiCopilotSettingsService settingsService,
            IAiLocalDocumentSearchService documentSearchService,
            IAiSoftwareInventoryService softwareInventoryService,
            IAiPromptTemplateService promptTemplateService,
            IAiReportAppendixService reportAppendixService,
            CranePlacementViewModel cranePlacement)
        {
            _advisorService = advisorService;
            _chatService = chatService;
            _softwareContextService = softwareContextService;
            _modelManagementService = modelManagementService;
            _settingsService = settingsService;
            _documentSearchService = documentSearchService;
            _softwareInventoryService = softwareInventoryService;
            _promptTemplateService = promptTemplateService;
            _reportAppendixService = reportAppendixService;
            _cranePlacement = cranePlacement;
            _softwareContext = _softwareContextService.GetSoftwareContext();
            _modelName = _settingsService.Current.DefaultModelName;

            Recommendations = new ObservableCollection<AiRecommendationItemViewModel>();
            Evidence = new ObservableCollection<string>();
            InstalledModels = new ObservableCollection<string>();
            ResponseHistory = new ObservableCollection<AiResponseHistoryItemViewModel>();
            ModelBenchmarks = new ObservableCollection<AiModelBenchmarkResultViewModel>();
            ContextCards = new ObservableCollection<AiContextCardViewModel>();
            PromptTemplates = new ObservableCollection<AiPromptTemplate>(_promptTemplateService.GetAllTemplates());
            SoftwareGuideText = BuildSoftwareGuideText(_softwareContext);
            LoadPersistentHistory();

            AnalyzeCurrentResultCommand = new RelayCommand(AnalyzeCurrentResult);
            AskOllamaCommand = new AsyncRelayCommand(AskOllamaAsync, () => !IsBusy);
            CancelOllamaCommand = new RelayCommand(CancelOllama, () => CanCancel);
            ToggleOllamaCommand = new RelayCommand(async () => await ToggleOllamaAsync().ConfigureAwait(true), () => CanUseComposerAction);
            CopyContextCommand = new RelayCommand(CopyContext, () => !string.IsNullOrWhiteSpace(ContextPreview));
            CopyAnswerCommand = new RelayCommand(CopyAnswer, () => HasAiAnswer);
            ExportAnswerCommand = new RelayCommand(ExportAnswer, () => HasAiAnswer);
            ExportReportAppendixCommand = new RelayCommand(ExportReportAppendix, () => HasAiAnswer);
            ClearHistoryCommand = new RelayCommand(ClearHistory, () => ResponseHistory.Count > 0);
            SelectHistoryCommand = new RelayCommand<AiResponseHistoryItemViewModel>(SelectHistory);
            CheckOllamaCommand = new AsyncRelayCommand(CheckOllamaAsync, () => !IsBusy);
            PullModelCommand = new AsyncRelayCommand(PullModelAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(ModelName));
            BenchmarkModelsCommand = new AsyncRelayCommand(BenchmarkModelsAsync, () => !IsBusy);
            SetQuickQuestionCommand = new RelayCommand<string>(SetQuickQuestion);
            SelectModelCommand = new RelayCommand<string>(SelectModel);
        }

        public ObservableCollection<AiRecommendationItemViewModel> Recommendations { get; }
        public ObservableCollection<string> Evidence { get; }
        public ObservableCollection<string> InstalledModels { get; }
        public ObservableCollection<AiResponseHistoryItemViewModel> ResponseHistory { get; }
        public ObservableCollection<AiModelBenchmarkResultViewModel> ModelBenchmarks { get; }
        public ObservableCollection<AiContextCardViewModel> ContextCards { get; }
        public ObservableCollection<AiPromptTemplate> PromptTemplates { get; }

        public ICommand AnalyzeCurrentResultCommand { get; }
        public ICommand AskOllamaCommand { get; }
        public ICommand CancelOllamaCommand { get; }
        public ICommand ToggleOllamaCommand { get; }
        public ICommand CopyContextCommand { get; }
        public ICommand CopyAnswerCommand { get; }
        public ICommand ExportAnswerCommand { get; }
        public ICommand ExportReportAppendixCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand SelectHistoryCommand { get; }
        public ICommand CheckOllamaCommand { get; }
        public ICommand PullModelCommand { get; }
        public ICommand BenchmarkModelsCommand { get; }
        public ICommand SetQuickQuestionCommand { get; }
        public ICommand SelectModelCommand { get; }

        public string ModelName
        {
            get => _modelName;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
                if (_modelName == normalized) return;
                _modelName = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedModelLabel));
                OnPropertyChanged(nameof(CanUseComposerAction));
                _settingsService.Update(settings => settings.DefaultModelName = _modelName);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SelectedModelLabel => string.IsNullOrWhiteSpace(ModelName)
            ? "No Ollama model selected"
            : $"Selected: {ModelName}";

        public string UserQuestion
        {
            get => _userQuestion;
            set
            {
                if (_userQuestion == value) return;
                _userQuestion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsQuestionEmpty));
                OnPropertyChanged(nameof(HasUserQuestion));
                OnPropertyChanged(nameof(CanUseComposerAction));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string AdvisorSummary
        {
            get => _advisorSummary;
            private set
            {
                if (_advisorSummary == value) return;
                _advisorSummary = value;
                OnPropertyChanged();
            }
        }

        public string HealthLabel
        {
            get => _healthLabel;
            private set
            {
                if (_healthLabel == value) return;
                _healthLabel = value;
                OnPropertyChanged();
            }
        }

        public string AiAnswer
        {
            get => _aiAnswer;
            private set
            {
                if (_aiAnswer == value) return;
                _aiAnswer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAiAnswer));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string LastError
        {
            get => _lastError;
            private set
            {
                if (_lastError == value) return;
                _lastError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string ContextPreview
        {
            get => _contextPreview;
            private set
            {
                if (_contextPreview == value) return;
                _contextPreview = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SoftwareGuideText
        {
            get => _softwareGuideText;
            private set
            {
                if (_softwareGuideText == value) return;
                _softwareGuideText = value;
                OnPropertyChanged();
            }
        }

        public string OllamaStatusText
        {
            get => _ollamaStatusText;
            private set
            {
                if (_ollamaStatusText == value) return;
                _ollamaStatusText = value;
                OnPropertyChanged();
            }
        }

        public string LastDocumentContext
        {
            get => _lastDocumentContext;
            private set
            {
                if (_lastDocumentContext == value) return;
                _lastDocumentContext = value;
                OnPropertyChanged();
            }
        }

        public string LastXamlInventory
        {
            get => _lastXamlInventory;
            private set
            {
                if (_lastXamlInventory == value) return;
                _lastXamlInventory = value;
                OnPropertyChanged();
            }
        }

        public string SelectedPromptTemplateSummary
        {
            get => _selectedPromptTemplateSummary;
            private set
            {
                if (_selectedPromptTemplateSummary == value) return;
                _selectedPromptTemplateSummary = value;
                OnPropertyChanged();
            }
        }

        public string ModelCapabilitySummary
        {
            get => _modelCapabilitySummary;
            private set
            {
                if (_modelCapabilitySummary == value) return;
                _modelCapabilitySummary = value;
                OnPropertyChanged();
            }
        }

        public string ActiveProjectLabel => $"Project memory: {_activeProjectDisplayName}";

        public string BenchmarkSummary
        {
            get => _benchmarkSummary;
            private set
            {
                if (_benchmarkSummary == value) return;
                _benchmarkSummary = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanUseComposerAction));
                OnPropertyChanged(nameof(ComposerActionText));
                OnPropertyChanged(nameof(ComposerActionToolTip));
                OnPropertyChanged(nameof(ComposerStateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanCancel => IsBusy && _activeRequestCts is { IsCancellationRequested: false };
        public bool IsQuestionEmpty => string.IsNullOrWhiteSpace(UserQuestion);
        public bool HasUserQuestion => !IsQuestionEmpty;
        public bool CanUseComposerAction => IsBusy || (!string.IsNullOrWhiteSpace(ModelName) && HasUserQuestion);
        public string ComposerActionText => IsBusy ? "Stop" : "Send";
        public string ComposerActionToolTip => IsBusy ? "Stop the current local Ollama response." : "Send this question to local Ollama. Press Enter to send or Shift+Enter for a new line.";
        public string ComposerStateText => IsBusy ? "Generating locally… click Stop to cancel" : "Private project context is attached automatically";
        public bool HasAiAnswer => IsMeaningfulAnswer(AiAnswer);

        public static bool IsMeaningfulAnswer(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();
            return !text.StartsWith("No Ollama answer yet", StringComparison.OrdinalIgnoreCase)
                && !text.StartsWith("Waiting for Ollama response", StringComparison.OrdinalIgnoreCase);
        }
        public bool HasError => !string.IsNullOrWhiteSpace(LastError);
        public bool HasHistory => ResponseHistory.Count > 0;
        public bool HasNoHistory => ResponseHistory.Count == 0;
        public string HistoryCountLabel => ResponseHistory.Count == 0
            ? "No saved answers"
            : $"{ResponseHistory.Count} saved answer(s)";

        public void AnalyzeCurrentResult()
        {
            LastError = string.Empty;
            _lastContext = BuildContextFromCurrentResult();
            _lastAdvisorResponse = _advisorService.Analyze(_lastContext);

            HealthLabel = _lastAdvisorResponse.HealthLabel;
            AdvisorSummary = _lastAdvisorResponse.Summary;

            Recommendations.Clear();
            foreach (var recommendation in _lastAdvisorResponse.Recommendations)
                Recommendations.Add(new AiRecommendationItemViewModel(recommendation));

            Evidence.Clear();
            foreach (var item in _lastAdvisorResponse.Evidence)
                Evidence.Add(item);

            LastDocumentContext = BuildDocumentContext(UserQuestion);
            LastXamlInventory = ShouldUseSoftwareInventory(UserQuestion) && _settingsService.Current.IncludeXamlDerivedSoftwareInventory
                ? _softwareInventoryService.BuildXamlInventorySummary()
                : "XAML-derived software inventory not injected for this workflow request.";
            SelectedPromptTemplateSummary = BuildSelectedTemplateSummary(_lastContext, _lastAdvisorResponse);
            RefreshContextCards(_lastContext, _lastAdvisorResponse);
            ContextPreview = BuildContextJson(_lastContext, _lastAdvisorResponse, _softwareContext, LastDocumentContext + Environment.NewLine + LastXamlInventory, SelectedPromptTemplateSummary);
            StatusText = _lastContext.HasOptimizationResult
                ? "Analysis complete. AI context includes result metrics, deterministic findings, local docs, and the software feature/tab map."
                : "No optimizer result found yet. The AI can still answer software-navigation questions using the embedded feature map and local docs.";
        }

        private async Task ToggleOllamaAsync()
        {
            if (IsBusy)
            {
                CancelOllama();
                return;
            }

            await AskOllamaAsync().ConfigureAwait(true);
        }

        public async Task AskOllamaWithLiveUiContextAsync(string visibleQuestion, string liveUiContext, string deterministicNextAction)
        {
            _pendingLiveUiContext = string.IsNullOrWhiteSpace(liveUiContext) ? string.Empty : liveUiContext.Trim();
            _pendingDeterministicNextAction = string.IsNullOrWhiteSpace(deterministicNextAction) ? string.Empty : deterministicNextAction.Trim();
            UserQuestion = string.IsNullOrWhiteSpace(visibleQuestion)
                ? "What should I do next from this screen?"
                : visibleQuestion.Trim();

            await AskOllamaAsync().ConfigureAwait(true);
        }

        private async Task AskOllamaAsync()
        {
            LastError = string.Empty;

            if (string.IsNullOrWhiteSpace(ModelName))
            {
                LastError = "Select or type an Ollama model first. Click Check Ollama, then choose one of the detected models.";
                StatusText = "No Ollama model selected.";
                return;
            }

            AiAnswer = "Waiting for Ollama response...";

            if (_lastContext == null || _lastAdvisorResponse == null)
                AnalyzeCurrentResult();

            if (_lastContext == null || _lastAdvisorResponse == null)
                return;

            _activeRequestCts?.Dispose();
            _activeRequestCts = new CancellationTokenSource();
            IsBusy = true;
            StatusText = $"Asking local Ollama model '{ModelName}'...";

            try
            {
                LastDocumentContext = BuildDocumentContext(UserQuestion);
                LastXamlInventory = ShouldUseSoftwareInventory(UserQuestion) && _settingsService.Current.IncludeXamlDerivedSoftwareInventory
                    ? _softwareInventoryService.BuildXamlInventorySummary()
                    : "XAML-derived software inventory not injected for this workflow request.";
                SelectedPromptTemplateSummary = BuildSelectedTemplateSummary(_lastContext, _lastAdvisorResponse);

                var request = new AiChatRequestDto
                {
                    Model = ModelName,
                    SystemPrompt = BuildSystemPrompt(),
                    UserPrompt = UserQuestion,
                    ContextJson = BuildPromptContext(_lastContext, _lastAdvisorResponse, _softwareContext, LastDocumentContext + Environment.NewLine + LastXamlInventory, SelectedPromptTemplateSummary, _pendingLiveUiContext, _pendingDeterministicNextAction),
                    Temperature = _settingsService.Current.Temperature,
                    TimeoutSeconds = _settingsService.Current.RequestTimeoutSeconds,
                    PreferStreaming = _settingsService.Current.UseStreamingResponses
                };

                var response = await AskWithOptionalStreamingAsync(request, _activeRequestCts.Token).ConfigureAwait(true);
                if (_activeRequestCts.IsCancellationRequested)
                {
                    AiAnswer = "Ollama request cancelled by the user.";
                    StatusText = "Ollama request cancelled.";
                    return;
                }

                if (response.Success)
                {
                    var normalizedAnswer = NormalizeAiOutput(response.Content);
                    AiAnswer = string.IsNullOrWhiteSpace(normalizedAnswer)
                        ? "Ollama returned a successful response, but no visible text was included. Try another model or ask a shorter question."
                        : normalizedAnswer;
                    StatusText = $"Ollama response received ({AiAnswer.Length:N0} characters).";
                    AddHistory(ModelName, UserQuestion, AiAnswer, "Ollama");
                }
                else
                {
                    LastError = response.ErrorMessage ?? "Ollama request failed.";
                    StatusText = "Ollama request failed.";
                    AiAnswer = BuildOllamaHelpText();
                }
            }
            catch (OperationCanceledException)
            {
                AiAnswer = "Ollama request cancelled by the user.";
                StatusText = "Ollama request cancelled.";
            }
            finally
            {
                IsBusy = false;
                _activeRequestCts?.Dispose();
                _activeRequestCts = null;
                _pendingLiveUiContext = string.Empty;
                _pendingDeterministicNextAction = string.Empty;
            }
        }

        private void CancelOllama()
        {
            _activeRequestCts?.Cancel();
            StatusText = "Cancelling Ollama request...";
        }

        private void CopyContext()
        {
            if (string.IsNullOrWhiteSpace(ContextPreview))
                return;

            Clipboard.SetText(ContextPreview);
            StatusText = "Context copied to clipboard.";
        }

        private void CopyAnswer()
        {
            if (!HasAiAnswer)
                return;

            Clipboard.SetText(AiAnswer);
            StatusText = "AI answer copied to clipboard.";
        }

        private void ExportAnswer()
        {
            if (!HasAiAnswer)
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Export AI Copilot response",
                FileName = $"ai-copilot-response-{DateTime.Now:yyyyMMdd-HHmmss}.md",
                Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            var builder = new StringBuilder();
            builder.AppendLine("# AI Copilot Response");
            builder.AppendLine();
            builder.AppendLine($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            builder.AppendLine($"- Model: {ModelName}");
            builder.AppendLine();
            builder.AppendLine("## Question");
            builder.AppendLine();
            builder.AppendLine(UserQuestion.Trim());
            builder.AppendLine();
            builder.AppendLine("## Response");
            builder.AppendLine();
            builder.AppendLine(AiAnswer.Trim());
            File.WriteAllText(dialog.FileName, builder.ToString());
            StatusText = $"AI response exported to {dialog.FileName}.";
        }

        public async Task CheckOllamaAsync()
        {
            LastError = string.Empty;
            IsBusy = true;
            StatusText = "Checking local Ollama status...";

            try
            {
                var status = await _modelManagementService.GetStatusAsync().ConfigureAwait(true);
                InstalledModels.Clear();
                foreach (var model in status.InstalledModels)
                    InstalledModels.Add(model);

                SelectBestInstalledModel();

                if (status.IsAvailable)
                {
                    var versionText = string.IsNullOrWhiteSpace(status.Version) ? string.Empty : $" v{status.Version}";
                    var modelText = InstalledModels.Count == 0
                        ? "No models installed yet."
                        : $"{InstalledModels.Count} model(s): {string.Join(", ", InstalledModels.Take(6))}{(InstalledModels.Count > 6 ? "..." : string.Empty)}";

                    OllamaStatusText = $"Ollama online at {status.Endpoint}{versionText}. {modelText}";
                    StatusText = "Ollama status check complete.";
                }
                else
                {
                    OllamaStatusText = $"Ollama offline at {status.Endpoint}. {status.ErrorMessage}";
                    StatusText = "Ollama is not available. Deterministic advisor still works.";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PullModelAsync()
        {
            LastError = string.Empty;
            if (string.IsNullOrWhiteSpace(ModelName))
            {
                LastError = "Enter a model name first, for example llama3.2.";
                return;
            }

            IsBusy = true;
            StatusText = $"Pulling local Ollama model '{ModelName}'. This may take several minutes...";

            try
            {
                var response = await _modelManagementService.PullModelAsync(ModelName).ConfigureAwait(true);
                if (response.Success)
                {
                    StatusText = response.Content;
                    await CheckOllamaAsync().ConfigureAwait(true);
                }
                else
                {
                    LastError = response.ErrorMessage ?? "Model pull failed.";
                    StatusText = "Ollama model pull failed.";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BenchmarkModelsAsync()
        {
            LastError = string.Empty;
            ModelBenchmarks.Clear();
            BenchmarkSummary = "Benchmark running...";

            if (InstalledModels.Count == 0)
                await CheckOllamaAsync().ConfigureAwait(true);

            var models = InstalledModels.Count > 0
                ? InstalledModels.Take(_settingsService.Current.BenchmarkModelLimit).ToArray()
                : new[] { ModelName };

            _activeRequestCts?.Dispose();
            _activeRequestCts = new CancellationTokenSource();
            IsBusy = true;

            try
            {
                foreach (var model in models.Where(model => !string.IsNullOrWhiteSpace(model)))
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var response = await _chatService.AskAsync(new AiChatRequestDto
                        {
                            Model = model,
                            SystemPrompt = "You are benchmarking local model availability. Reply in one concise sentence.",
                            UserPrompt = "Reply with: AI Copilot benchmark ready.",
                            ContextJson = "Benchmark context only. No project data is included.",
                            Temperature = 0.0,
                            TimeoutSeconds = Math.Min(_settingsService.Current.RequestTimeoutSeconds, 60),
                            PreferStreaming = false
                        }, _activeRequestCts.Token).ConfigureAwait(true);
                        stopwatch.Stop();

                        ModelBenchmarks.Add(new AiModelBenchmarkResultViewModel
                        {
                            Model = model,
                            Success = response.Success,
                            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                            ResponseCharacters = response.Content?.Length ?? 0,
                            Notes = response.Success ? "OK" : response.ErrorMessage ?? "Failed"
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        ModelBenchmarks.Add(new AiModelBenchmarkResultViewModel
                        {
                            Model = model,
                            Success = false,
                            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                            Notes = "Cancelled"
                        });
                        break;
                    }
                }

                var successful = ModelBenchmarks.Where(result => result.Success).OrderBy(result => result.ElapsedMilliseconds).ToArray();
                BenchmarkSummary = successful.Length == 0
                    ? "No model responded successfully during benchmark."
                    : $"Fastest responding model: {successful[0].Model} ({successful[0].ElapsedMilliseconds:N0} ms).";
                ModelCapabilitySummary = BuildModelCapabilitySummary();
                PersistModelCapabilities();
                StatusText = "Model benchmark complete.";
            }
            finally
            {
                IsBusy = false;
                _activeRequestCts?.Dispose();
                _activeRequestCts = null;
            }
        }

        private void SetQuickQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return;

            UserQuestion = question;
            StatusText = "Quick question loaded. Click Ask Ollama to get a software-aware answer.";
        }

        private void SelectModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return;

            ModelName = modelName.Trim();
            StatusText = $"Selected Ollama model '{ModelName}'. Click Ask Ollama to use it.";
        }

        private void SelectHistory(AiResponseHistoryItemViewModel item)
        {
            if (item == null)
                return;

            ModelName = item.Model;
            UserQuestion = item.Question;
            AiAnswer = item.Answer;
            StatusText = $"Loaded AI response history from {item.CreatedAt:HH:mm}.";
        }

        private void ClearHistory()
        {
            ResponseHistory.Clear();
            PersistHistory();
            NotifyHistoryStateChanged();
            StatusText = "AI response history cleared for this session.";
            CommandManager.InvalidateRequerySuggested();
        }

        private void AddHistory(string model, string question, string answer, string source)
        {
            ResponseHistory.Insert(0, new AiResponseHistoryItemViewModel
            {
                CreatedAt = DateTimeOffset.Now,
                Model = model,
                Question = question,
                Answer = answer,
                Source = source
            });

            while (ResponseHistory.Count > _settingsService.Current.MaxHistoryItems)
                ResponseHistory.RemoveAt(ResponseHistory.Count - 1);

            PersistHistory();
            NotifyHistoryStateChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyHistoryStateChanged()
        {
            OnPropertyChanged(nameof(HistoryCountLabel));
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(HasNoHistory));
        }

        public void SetActiveProjectContext(string projectKey, string projectDisplayName)
        {
            projectKey = string.IsNullOrWhiteSpace(projectKey) ? "global" : projectKey.Trim();
            projectDisplayName = string.IsNullOrWhiteSpace(projectDisplayName) ? "Global" : projectDisplayName.Trim();

            if (string.Equals(_activeProjectKey, projectKey, StringComparison.OrdinalIgnoreCase))
                return;

            _activeProjectKey = projectKey;
            _activeProjectDisplayName = projectDisplayName;
            OnPropertyChanged(nameof(ActiveProjectLabel));
            LoadPersistentHistory();
        }


        private async Task<AiChatResponseDto> AskWithOptionalStreamingAsync(AiChatRequestDto request, CancellationToken cancellationToken)
        {
            if (_settingsService.Current.UseStreamingResponses && _chatService is IAiStreamingChatService streamingService)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                var lastUiUpdate = DateTimeOffset.MinValue;

                return await streamingService.AskStreamingAsync(request, update =>
                {
                    if (string.IsNullOrEmpty(update.Delta) && !update.IsComplete)
                        return;

                    var normalized = NormalizeAiOutput(update.AccumulatedContent);
                    if (string.IsNullOrWhiteSpace(normalized))
                        normalized = "Receiving streaming response...";

                    var shouldUpdate = update.IsComplete || (DateTimeOffset.Now - lastUiUpdate).TotalMilliseconds >= 80;
                    if (!shouldUpdate)
                        return;

                    lastUiUpdate = DateTimeOffset.Now;
                    if (dispatcher == null || dispatcher.CheckAccess())
                    {
                        AiAnswer = normalized;
                    }
                    else
                    {
                        dispatcher.BeginInvoke(new Action(() => AiAnswer = normalized));
                    }
                }, cancellationToken).ConfigureAwait(true);
            }

            return await _chatService.AskAsync(new AiChatRequestDto
            {
                Model = request.Model,
                SystemPrompt = request.SystemPrompt,
                UserPrompt = request.UserPrompt,
                ContextJson = request.ContextJson,
                Temperature = request.Temperature,
                TimeoutSeconds = request.TimeoutSeconds,
                PreferStreaming = false
            }, cancellationToken).ConfigureAwait(true);
        }

        private void ExportReportAppendix()
        {
            if (!HasAiAnswer)
                return;

            if (_lastContext == null || _lastAdvisorResponse == null)
                AnalyzeCurrentResult();

            var dialog = new SaveFileDialog
            {
                Title = "Export AI Copilot report appendix",
                FileName = $"ai-copilot-report-appendix-{DateTime.Now:yyyyMMdd-HHmmss}.md",
                Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            var appendix = _reportAppendixService.BuildAppendixMarkdown(UserQuestion, AiAnswer, ModelName, ContextPreview);
            File.WriteAllText(dialog.FileName, appendix);
            StatusText = $"AI report appendix exported to {dialog.FileName}.";
        }

        private void LoadPersistentHistory()
        {
            ResponseHistory.Clear();

            if (!_settingsService.Current.SaveConversationHistory)
            {
                NotifyHistoryStateChanged();
                return;
            }

            var records = _settingsService.Current.ProjectConversationHistory.TryGetValue(_activeProjectKey, out var projectRecords)
                ? projectRecords
                : _settingsService.Current.ConversationHistory;

            foreach (var record in records
                .OrderByDescending(record => record.CreatedAt)
                .Take(_settingsService.Current.MaxHistoryItems))
            {
                ResponseHistory.Add(new AiResponseHistoryItemViewModel
                {
                    CreatedAt = record.CreatedAt,
                    Model = record.Model,
                    Question = record.Question,
                    Answer = record.Answer,
                    Source = record.Source
                });
            }

            NotifyHistoryStateChanged();
        }

        private void PersistHistory()
        {
            if (!_settingsService.Current.SaveConversationHistory)
                return;

            var records = ResponseHistory.Select(item => new AiConversationRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = item.CreatedAt,
                ProjectKey = _activeProjectKey,
                ProjectDisplayName = _activeProjectDisplayName,
                Model = item.Model,
                Question = item.Question,
                Answer = item.Answer,
                Source = item.Source
            }).ToList();

            _settingsService.Update(settings =>
            {
                settings.ProjectConversationHistory[_activeProjectKey] = records;
                if (string.Equals(_activeProjectKey, "global", StringComparison.OrdinalIgnoreCase))
                    settings.ConversationHistory = records;
            });
        }

        private string BuildSelectedTemplateSummary(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse)
        {
            var template = _promptTemplateService.SelectTemplate(context, advisorResponse);
            return $"{template.Title}: {template.AppliesWhen}";
        }

        private void RefreshContextCards(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse)
        {
            ContextCards.Clear();
            ContextCards.Add(new AiContextCardViewModel
            {
                Title = "Optimization result",
                Value = context.HasOptimizationResult ? $"{context.CoverageRatio:P0} coverage" : "No completed run",
                Detail = context.HasOptimizationResult ? $"{context.LiftedElementCount}/{context.TotalElementCount} elements lifted" : "Run ANALYZE -> Run Optimizer before result analysis."
            });
            ContextCards.Add(new AiContextCardViewModel
            {
                Title = "Advisor health",
                Value = advisorResponse.HealthLabel,
                Detail = advisorResponse.Summary
            });
            ContextCards.Add(new AiContextCardViewModel
            {
                Title = "Prompt template",
                Value = _promptTemplateService.SelectTemplate(context, advisorResponse).Title,
                Detail = SelectedPromptTemplateSummary
            });
            ContextCards.Add(new AiContextCardViewModel
            {
                Title = "Local docs",
                Value = LastDocumentContext.StartsWith("No matching", StringComparison.OrdinalIgnoreCase) ? "No match" : "Attached",
                Detail = "Documentation snippets and XAML-derived UI labels are included when available."
            });
        }

        private string BuildModelCapabilitySummary()
        {
            if (ModelBenchmarks.Count == 0)
                return "No benchmark results are available yet.";

            var successful = ModelBenchmarks.Where(result => result.Success).OrderBy(result => result.ElapsedMilliseconds).ToArray();
            if (successful.Length == 0)
                return "No installed model completed the benchmark. Use Check Ollama, confirm models are available, then benchmark again.";

            var fastest = successful[0];
            var reasoning = successful.FirstOrDefault(result => result.Model.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                || result.Model.Contains("deepseek", StringComparison.OrdinalIgnoreCase));
            var compact = successful.FirstOrDefault(result => result.Model.Contains("llama", StringComparison.OrdinalIgnoreCase)
                || result.Model.Contains("gemma", StringComparison.OrdinalIgnoreCase));

            var builder = new StringBuilder();
            builder.AppendLine($"Fastest benchmark response: {fastest.Model} ({fastest.ElapsedMilliseconds:N0} ms). ");
            if (compact != null)
                builder.AppendLine($"Recommended for everyday workflow help: {compact.Model}.");
            if (reasoning != null)
                builder.AppendLine($"Recommended for deeper diagnostics/report reasoning: {reasoning.Model}.");
            builder.AppendLine("Use smaller/faster models for interactive side-dock help and larger/reasoning models for final report narratives.");
            return builder.ToString().Trim();
        }

        private void PersistModelCapabilities()
        {
            _settingsService.Update(settings =>
            {
                foreach (var result in ModelBenchmarks)
                {
                    settings.ModelCapabilities[result.Model] = new AiModelCapabilityRecord
                    {
                        Model = result.Model,
                        LastBenchmarkSucceeded = result.Success,
                        LastElapsedMilliseconds = result.ElapsedMilliseconds,
                        LastResponseCharacters = result.ResponseCharacters,
                        Recommendation = result.Success ? BuildModelRecommendation(result.Model, result.ElapsedMilliseconds) : result.Notes,
                        UpdatedAt = DateTimeOffset.Now
                    };
                }
            });
        }

        private static string BuildModelRecommendation(string model, long elapsedMilliseconds)
        {
            var speed = elapsedMilliseconds < 5000 ? "fast" : elapsedMilliseconds < 15000 ? "moderate" : "slow";
            if (model.Contains("deepseek", StringComparison.OrdinalIgnoreCase) || model.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                return $"{speed}; best for reasoning-heavy diagnostics and final report explanations.";
            if (model.Contains("gemma", StringComparison.OrdinalIgnoreCase) || model.Contains("llama", StringComparison.OrdinalIgnoreCase))
                return $"{speed}; best for responsive workflow help and screen guidance.";
            return $"{speed}; general local assistant model.";
        }

        private void SelectBestInstalledModel()
        {
            if (InstalledModels.Count == 0)
                return;

            var current = ModelName?.Trim() ?? string.Empty;
            var exact = InstalledModels.FirstOrDefault(model => string.Equals(model, current, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                ModelName = exact;
                return;
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                var latestAlias = InstalledModels.FirstOrDefault(model => string.Equals(model, current + ":latest", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(latestAlias))
                {
                    ModelName = latestAlias;
                    return;
                }

                var versionedAlias = InstalledModels.FirstOrDefault(model => model.StartsWith(current + ":", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(versionedAlias))
                {
                    ModelName = versionedAlias;
                    return;
                }
            }

            var preferred = InstalledModels.FirstOrDefault(model => model.StartsWith("llama3.2", StringComparison.OrdinalIgnoreCase))
                ?? InstalledModels.FirstOrDefault(model => model.StartsWith("qwen", StringComparison.OrdinalIgnoreCase))
                ?? InstalledModels.FirstOrDefault(model => model.StartsWith("gemma", StringComparison.OrdinalIgnoreCase))
                ?? InstalledModels[0];

            ModelName = preferred;
        }

        private AiOptimizationContextDto BuildContextFromCurrentResult()
        {
            CranePlacementResult? result = _cranePlacement.Result;
            if (result == null)
            {
                return new AiOptimizationContextDto
                {
                    HasOptimizationResult = false
                };
            }

            var stats = result.Statistics ?? new LiftStatistics();
            var total = result.TotalElementCount > 0
                ? result.TotalElementCount
                : stats.Liftable + stats.OutOfRadius + stats.Overweight + stats.HeightFail + stats.SpecialCase + stats.NotCharted;

            var lifted = result.LiftedElementCount > 0 ? result.LiftedElementCount : stats.Liftable;

            return new AiOptimizationContextDto
            {
                HasOptimizationResult = true,
                ProjectName = string.Empty,
                CraneName = result.CraneName ?? string.Empty,
                CraneModelOrSpecification = result.CraneModelOrSpecification ?? string.Empty,
                TotalElementCount = total,
                LiftedElementCount = lifted,
                PositionCount = result.Positions.Count,
                CoverageRatio = result.CoverageRatio,
                FitnessScore = result.FitnessScore,
                MaxCraneRadius = result.MaxRadius,
                MaxCraneCapacity = result.MaxCapacity,
                SafetyFactor = result.SafetyFactor,
                DeratingRatio = result.DeratingRatio,
                AdditionalWeight = result.AdditionalWeight,
                WireLength = result.WireLength,
                DiagnosticsPath = result.DebugReportPath,
                FailureCounts = new FailureCountsDto
                {
                    Liftable = stats.Liftable,
                    OutOfRadius = stats.OutOfRadius,
                    Overweight = stats.Overweight,
                    HeightFail = stats.HeightFail,
                    SpecialCase = stats.SpecialCase,
                    NotCharted = stats.NotCharted
                },
                Reachability = result.ReachabilityDiagnostics == null
                    ? null
                    : new ReachabilitySnapshotDto
                    {
                        TotalCandidatePoints = result.ReachabilityDiagnostics.TotalCandidatePoints,
                        ValidCandidatePoints = result.ReachabilityDiagnostics.ValidCandidatePoints,
                        RejectedCandidatePoints = result.ReachabilityDiagnostics.RejectedCandidatePoints,
                        ZeroCoveragePoints = result.ReachabilityDiagnostics.ZeroCoveragePoints,
                        HotSpotCount = result.ReachabilityDiagnostics.HotSpotCount,
                        MaxHotSpotCoverage = result.ReachabilityDiagnostics.MaxHotSpotCoverage,
                        AverageHotSpotCoverage = result.ReachabilityDiagnostics.AverageHotSpotCoverage,
                        ReachabilityMean = result.ReachabilityDiagnostics.ReachabilityMean,
                        ReachabilityStdDev = result.ReachabilityDiagnostics.ReachabilityStdDev
                    }
            };
        }

        private string BuildDocumentContext(string query)
        {
            if (!ShouldUseLocalDocumentation(query))
                return "Local documentation snippets were intentionally not injected for this workflow request. Prioritize live UI state, deterministic advisor findings, and visible software tabs/buttons.";

            var results = _documentSearchService.Search(query, 4);
            if (results.Count == 0)
                return "No matching local documentation snippets were found for the current question.";

            var builder = new StringBuilder();
            builder.AppendLine("LOCAL DOCUMENTATION SEARCH RESULTS — use only if directly relevant to the user's question; do not summarize these snippets unless asked.");
            foreach (var result in results)
            {
                builder.AppendLine($"- {result.RelativePath} (score {result.Score})");
                builder.AppendLine($"  {result.Snippet}");
            }
            return builder.ToString().Trim();
        }

        private static bool ShouldUseLocalDocumentation(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var text = query.ToUpperInvariant();
            return text.Contains("DOCUMENT")
                || text.Contains("DOCS")
                || text.Contains("README")
                || text.Contains("IMPLEMENTATION")
                || text.Contains("ARCHITECTURE")
                || text.Contains("REPORT")
                || text.Contains("DOSSIER")
                || text.Contains("SEARCH")
                || text.Contains("EXPLAIN THE AI COPILOT")
                || text.Contains("WHAT DOES THE DOCUMENTATION")
                || text.Contains("USER GUIDE");
        }

        private static bool ShouldUseSoftwareInventory(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var text = query.ToUpperInvariant();
            return text.Contains("XAML")
                || text.Contains("UI INVENTORY")
                || text.Contains("SOFTWARE INVENTORY")
                || text.Contains("INTERFACE INVENTORY")
                || text.Contains("CONTROL INVENTORY")
                || text.Contains("LIST ALL BUTTONS")
                || text.Contains("LIST EVERY BUTTON")
                || text.Contains("LIST ALL TABS")
                || text.Contains("IMPLEMENTATION")
                || text.Contains("WHAT CAN THIS SOFTWARE");
        }

        private static string BuildPromptContext(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse, AiSoftwareContextDto softwareContext, string documentContext, string promptTemplateSummary, string liveUiContext = "", string deterministicNextAction = "")
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(liveUiContext))
            {
                builder.AppendLine("LIVE UI CONTEXT — highest priority for workflow/navigation questions");
                builder.AppendLine(liveUiContext.Trim());
                if (!string.IsNullOrWhiteSpace(deterministicNextAction))
                    builder.AppendLine($"Deterministic next action: {deterministicNextAction}");
                builder.AppendLine();
            }

            builder.AppendLine("APPLICATION CONTEXT");
            builder.AppendLine($"Name: {softwareContext.Framework.ApplicationName}");
            builder.AppendLine($"Purpose: {softwareContext.Framework.ProductPurpose}");
            builder.AppendLine($"Framework: {softwareContext.Framework.UiFramework}; {softwareContext.Framework.TargetFramework}; {softwareContext.Framework.Architecture}");
            builder.AppendLine($"AI integration: {softwareContext.Framework.AiIntegrationMode}");
            builder.AppendLine();

            builder.AppendLine("VISIBLE SOFTWARE TABS AND ACTIONS");
            foreach (var tab in softwareContext.Tabs)
            {
                builder.AppendLine($"- {tab.TabName}: {tab.Purpose}");
                foreach (var action in tab.Actions)
                {
                    var availability = string.IsNullOrWhiteSpace(action.Availability) ? string.Empty : $" ({action.Availability})";
                    builder.AppendLine($"  - {action.Label}{availability}: {action.Purpose}");
                }
            }
            builder.AppendLine();

            builder.AppendLine("WORKFLOWS");
            foreach (var workflow in softwareContext.Workflows)
            {
                builder.AppendLine($"- {workflow.Name}: {workflow.Goal}");
                foreach (var step in workflow.Steps)
                    builder.AppendLine($"  - {step}");
            }
            builder.AppendLine();

            builder.AppendLine("RESULT-SPECIFIC PROMPT TEMPLATE");
            builder.AppendLine(promptTemplateSummary);
            builder.AppendLine("Use the selected prompt-template guidance when it matches the user's question.");
            builder.AppendLine();

            builder.AppendLine("CURRENT OPTIMIZATION RESULT");
            if (!context.HasOptimizationResult)
            {
                builder.AppendLine("No completed optimization result is currently available. For result-specific questions, tell the user to run ANALYZE -> Run Optimizer first, then return to AI COPILOT.");
            }
            else
            {
                builder.AppendLine($"Crane: {context.CraneName}");
                builder.AppendLine($"Coverage: {context.CoverageRatio:P2} ({context.LiftedElementCount}/{context.TotalElementCount} target elements)");
                builder.AppendLine($"Setup positions: {context.PositionCount}");
                builder.AppendLine($"Fitness score: {context.FitnessScore:N3}");
                builder.AppendLine($"Limits/inputs: max radius {context.MaxCraneRadius:N2} m, max capacity {context.MaxCraneCapacity:N2} t, safety factor {context.SafetyFactor:N2}, derating {context.DeratingRatio:P0}, additional weight {context.AdditionalWeight:N2} t, wire length {context.WireLength:N2} m");
                builder.AppendLine($"Failure counts: out of radius {context.FailureCounts.OutOfRadius}, overweight {context.FailureCounts.Overweight}, height fail {context.FailureCounts.HeightFail}, not charted {context.FailureCounts.NotCharted}, special case {context.FailureCounts.SpecialCase}");
                if (context.Reachability != null)
                {
                    builder.AppendLine($"Candidate points: {context.Reachability.ValidCandidatePoints}/{context.Reachability.TotalCandidatePoints} valid; hot spots {context.Reachability.HotSpotCount}; max hot-spot coverage {context.Reachability.MaxHotSpotCoverage:P2}");
                }
            }
            builder.AppendLine();

            builder.AppendLine("DETERMINISTIC ADVISOR FINDINGS");
            builder.AppendLine($"Health: {advisorResponse.HealthLabel}");
            builder.AppendLine($"Summary: {advisorResponse.Summary}");
            foreach (var recommendation in advisorResponse.Recommendations)
            {
                builder.AppendLine($"- {recommendation.Severity}: {recommendation.Title}");
                builder.AppendLine($"  Detail: {recommendation.Detail}");
                builder.AppendLine($"  Suggested action: {recommendation.SuggestedAction}");
            }
            foreach (var evidence in advisorResponse.Evidence)
                builder.AppendLine($"Evidence: {evidence}");
            builder.AppendLine();

            builder.AppendLine("LOCAL DOCUMENTATION CONTEXT");
            builder.AppendLine(documentContext);
            builder.AppendLine();

            builder.AppendLine("SAFETY GUARDRAILS");
            foreach (var guardrail in softwareContext.SafetyGuardrails)
                builder.AppendLine($"- {guardrail}");

            return builder.ToString();
        }

        private static string BuildContextJson(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse, AiSoftwareContextDto softwareContext, string documentContext, string promptTemplateSummary)
        {
            var payload = new
            {
                OptimizationContext = context,
                DeterministicAdvisor = new
                {
                    advisorResponse.HealthLabel,
                    advisorResponse.Summary,
                    advisorResponse.Recommendations,
                    advisorResponse.Evidence,
                    ActivePromptTemplate = promptTemplateSummary
                },
                SoftwareContext = softwareContext,
                LocalDocumentationContext = documentContext,
                SafetyInstruction = "This AI output is advisory only. Final crane planning decisions must be checked by qualified personnel against project rules, manufacturer data, and applicable safety standards."
            };

            return JsonSerializer.Serialize(payload, ContextJsonOptions);
        }

        private static string BuildSystemPrompt()
        {
            return "You are a local AI copilot embedded in a WPF crane path optimization application. " +
                   "Answer the user's visible question first. Use supplied internal context silently; never describe it as JSON, schema, prompt metadata, XAML inventory, or documentation snippets unless explicitly asked. " +
                   "Prioritize evidence in this order: live UI context, deterministic advisor findings, current optimization result, visible tabs/buttons/workflows, then local documentation only when directly relevant. " +
                   "Do not invent project facts, standards, site measurements, certification claims, or hidden UI commands. " +
                   "When the user asks what to do next, answer with the practical workflow state first; do not summarize documentation, XAML inventory, UI inventory, or implementation context. " +
                   "Give one concise recommended next click, why it matters, and what to verify afterward. " +
                   "When the user asks how to use the software, point to exact visible tab and button names from SoftwareContext, and clearly say when a feature is hidden or marked coming soon. " +
                   "When the user asks about optimization, explain the result in practical engineering language, identify the dominant failure mode, and suggest safe next actions. " +
                   "Use a structured professional format with headings such as Status, Evidence, Recommendation, Next Click, Risk, and Confidence when appropriate. " +
                   "Format the answer as clean Markdown for a professional preview: use short headings, concise paragraphs, bullet lists, and bold labels only where useful. " +
                   "Do not wrap the whole answer in code fences. Do not show raw JSON, prompt metadata, or implementation details unless the user explicitly asks. " +
                   "Never override the application's deterministic calculations. Make it clear that final crane plans require qualified engineering review.";
        }

        private static string BuildSoftwareGuideText(AiSoftwareContextDto context)
        {
            var builder = new StringBuilder();
            builder.AppendLine(context.Framework.ApplicationName);
            builder.AppendLine(context.Framework.ProductPurpose);
            builder.AppendLine();
            builder.AppendLine("Framework");
            builder.AppendLine($"- UI: {context.Framework.UiFramework}");
            builder.AppendLine($"- Target: {context.Framework.TargetFramework}");
            builder.AppendLine($"- Architecture: {context.Framework.Architecture}");
            builder.AppendLine($"- AI mode: {context.Framework.AiIntegrationMode}");
            builder.AppendLine();
            builder.AppendLine("Tabs and buttons");

            foreach (var tab in context.Tabs)
            {
                builder.AppendLine();
                builder.AppendLine($"[{tab.TabName}] {tab.Purpose}");
                foreach (var action in tab.Actions)
                    builder.AppendLine($"  - {action.Label}: {action.Purpose}");
            }

            builder.AppendLine();
            builder.AppendLine("Main workflows");
            foreach (var workflow in context.Workflows)
            {
                builder.AppendLine();
                builder.AppendLine($"{workflow.Name}: {workflow.Goal}");
                foreach (var step in workflow.Steps)
                    builder.AppendLine($"  {step}");
            }

            builder.AppendLine();
            builder.AppendLine("AI guardrails");
            foreach (var guardrail in context.SafetyGuardrails)
                builder.AppendLine($"- {guardrail}");

            return builder.ToString();
        }

        private static string NormalizeAiOutput(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var text = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\uFFFD', ' ')
                .Trim();

            text = RemoveTaggedBlock(text, "<think>", "</think>");
            text = RemoveTaggedBlock(text, "<analysis>", "</analysis>");

            return text.Trim();
        }

        private static string RemoveTaggedBlock(string text, string startTag, string endTag)
        {
            while (true)
            {
                var start = text.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    return text;

                var end = text.IndexOf(endTag, start + startTag.Length, StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                    return text.Remove(start).Trim();

                text = text.Remove(start, end + endTag.Length - start).Trim();
            }
        }

        private static string BuildOllamaHelpText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Local Ollama did not respond.");
            builder.AppendLine();
            builder.AppendLine("Checklist on your Windows machine:");
            builder.AppendLine("1. Start Ollama.");
            builder.AppendLine("2. Confirm it is listening at http://localhost:11434.");
            builder.AppendLine("3. Click Check Ollama in this window to detect installed models.");
            builder.AppendLine("4. Install or run a model, for example: ollama run llama3.2, or use Pull Model from this window.");
            builder.AppendLine("5. Put that exact model name in the Model field, then click Ask Ollama.");
            return builder.ToString();
        }
    }
}
