using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using UiCranePlacementResult = CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.CranePlacementResult;
using UiElementLiftResult = CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs.ElementLiftResult;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

namespace CraneOptimizer.UI.Features.F12_Reports.ViewModels;

public sealed class ReportBuilderViewModel : ViewModelBase
{
    private readonly ISceneService _scene;
    private readonly IProjectWorkspaceService _workspace;
    private readonly CranePlacementViewModel _cranePlacement;
    private readonly ElementManagementViewModel _elementManagement;
    private readonly GASettings _gaSettings;
    private readonly IReportBuilder _reportBuilder;
    private readonly IReportPdfExporter _pdfExporter;
    private readonly IReportExcelExporter _excelExporter;
    private readonly IReportTemplateStore _templateStore;
    private readonly IReportFileDialogService _dialogs;
    private readonly IReportPreviewDialogService _previewDialogs;
    private readonly IGADiagnosticsReaderService _gaDiagnosticsReader;
    private readonly List<ReportColumnOptionViewModel> _allowedColumns = new();
    private readonly List<ReportTemplateDto> _userTemplates = new();
    private const string Placeholder = "\u2014";
    private const string NoGaAnalysisWarning = "No GA analysis is available for this project. Run GA optimization first to populate lift result columns.";

    private ReportFamily _family;
    private string _selectedReportName = string.Empty;
    private string _itemsIncluded = string.Empty;
    private string _projectName = string.Empty;
    private string? _companyName;
    private string? _companyLogoPath;
    private string? _createdBy;
    private string? _issuedTo;
    private string? _revision;
    private string? _generalNotes;
    private string _generatedOnText = "Auto on export";
    private string _availableColumnSearch = string.Empty;
    private string _statusMessage = string.Empty;
    private string _previewSummary = "Preview has not been generated yet.";
    private string _buildReadinessMessage = string.Empty;
    private string _layoutWarningMessage = string.Empty;
    private string _gaWarningText = string.Empty;
    private string _textAlignment = "Left";
    private int _globalTextSize = 10;
    private bool _isBusy;
    private bool _hasPreview;
    private ReportDocumentDto? _currentPreview;
    private ReportColumnOptionViewModel? _selectedAvailableColumn;
    private ReportColumnOptionViewModel? _selectedColumn;
    private AsyncRelayCommand _previewCommand = null!;
    private AsyncRelayCommand _exportPdfCommand = null!;
    private AsyncRelayCommand _exportExcelCommand = null!;
    private AsyncRelayCommand _saveTemplateCommand = null!;
    private AsyncRelayCommand _deleteTemplateCommand = null!;

    public ReportBuilderViewModel(
        ISceneService scene,
        IProjectWorkspaceService workspace,
        CranePlacementViewModel cranePlacement,
        ElementManagementViewModel elementManagement,
        GASettings gaSettings,
        IReportBuilder reportBuilder,
        IReportPdfExporter pdfExporter,
        IReportExcelExporter excelExporter,
        IReportTemplateStore templateStore,
        IReportFileDialogService dialogs,
        IReportPreviewDialogService previewDialogs,
        IGADiagnosticsReaderService gaDiagnosticsReader)
    {
        _scene = scene;
        _workspace = workspace;
        _cranePlacement = cranePlacement;
        _elementManagement = elementManagement;
        _gaSettings = gaSettings;
        _reportBuilder = reportBuilder;
        _pdfExporter = pdfExporter;
        _excelExporter = excelExporter;
        _templateStore = templateStore;
        _dialogs = dialogs;
        _previewDialogs = previewDialogs;
        _gaDiagnosticsReader = gaDiagnosticsReader;

        BrowseLogoCommand = new RelayCommand(BrowseLogo);
        _previewCommand = new AsyncRelayCommand(PreviewAsync, () => !IsBusy && CanBuildReport);
        _exportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsBusy);
        _exportExcelCommand = new AsyncRelayCommand(ExportExcelAsync, () => !IsBusy);
        _saveTemplateCommand = new AsyncRelayCommand(SaveTemplateAsync, () => !IsBusy && SelectedColumns.Count > 0);
        _deleteTemplateCommand = new AsyncRelayCommand(DeleteTemplateAsync, () => !IsBusy && IsSelectedTemplateUserTemplate);
        ResetCommand = new RelayCommand(() => ResetToSelectedTemplate(resetItemsIncluded: true));
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke());
        AddColumnCommand = new RelayCommand(AddSelectedAvailableColumn);
        RemoveColumnCommand = new RelayCommand(RemoveSelectedColumn);
        AddAllColumnsCommand = new RelayCommand(AddAllAvailableColumns);
        RemoveAllColumnsCommand = new RelayCommand(RemoveAllSelectedColumns);
        MoveColumnUpCommand = new RelayCommand(MoveSelectedColumnUp);
        MoveColumnDownCommand = new RelayCommand(MoveSelectedColumnDown);
        SelectedColumns.CollectionChanged += SelectedColumns_CollectionChanged;
    }

    public event Action? RequestClose;

    public ObservableCollection<string> ReportNames { get; } = new();
    public ObservableCollection<string> ItemsIncludedOptions { get; } = new();
    public ObservableCollection<ReportColumnOptionViewModel> AvailableColumns { get; } = new();
    public ObservableCollection<ReportColumnOptionViewModel> SelectedColumns { get; } = new();
    public IReadOnlyList<string> AlignmentOptions { get; } = ["Left", "Center", "Right"];
    public IReadOnlyList<int> TextSizeOptions { get; } = [6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];

    public string SelectedReportName
    {
        get => _selectedReportName;
        set
        {
            if (_selectedReportName == value)
                return;

            _selectedReportName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelectedTemplateUserTemplate));
            ResetToSelectedTemplate(resetItemsIncluded: true);
            ClearPreview();
            RefreshCommandState();
        }
    }

    public string ItemsIncluded
    {
        get => _itemsIncluded;
        set
        {
            if (_itemsIncluded == value)
                return;

            _itemsIncluded = value;
            OnPropertyChanged();
            ClearPreview();
            RefreshCommandState();
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            _projectName = value;
            OnPropertyChanged();
        }
    }

    public string GeneratedOnText
    {
        get => _generatedOnText;
        private set
        {
            _generatedOnText = value;
            OnPropertyChanged();
        }
    }

    public string AvailableColumnSearch
    {
        get => _availableColumnSearch;
        set
        {
            if (_availableColumnSearch == value)
                return;

            _availableColumnSearch = value;
            OnPropertyChanged();
            RefreshAvailableColumns();
        }
    }

    public string? CompanyName
    {
        get => _companyName;
        set
        {
            _companyName = value;
            OnPropertyChanged();
        }
    }

    public string? CompanyLogoPath
    {
        get => _companyLogoPath;
        set
        {
            _companyLogoPath = value;
            OnPropertyChanged();
        }
    }

    public string? CreatedBy
    {
        get => _createdBy;
        set
        {
            _createdBy = value;
            OnPropertyChanged();
        }
    }

    public string? IssuedTo
    {
        get => _issuedTo;
        set
        {
            _issuedTo = value;
            OnPropertyChanged();
        }
    }

    public string? Revision
    {
        get => _revision;
        set
        {
            _revision = value;
            OnPropertyChanged();
        }
    }

    public string? GeneralNotes
    {
        get => _generalNotes;
        set
        {
            _generalNotes = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        set
        {
            _previewSummary = value;
            OnPropertyChanged();
        }
    }

    public string BuildReadinessMessage
    {
        get => _buildReadinessMessage;
        set
        {
            _buildReadinessMessage = value;
            OnPropertyChanged();
        }
    }

    public string LayoutWarningMessage
    {
        get => _layoutWarningMessage;
        private set
        {
            _layoutWarningMessage = value;
            OnPropertyChanged();
        }
    }

    public string GaWarningText
    {
        get => _gaWarningText;
        private set
        {
            if (_gaWarningText == value)
                return;

            _gaWarningText = value;
            OnPropertyChanged();
        }
    }

    public string TextAlignment
    {
        get => _textAlignment;
        set
        {
            if (_textAlignment == value)
                return;

            _textAlignment = value;
            OnPropertyChanged();
            ClearPreview();
        }
    }

    public int GlobalTextSize
    {
        get => _globalTextSize;
        set
        {
            var normalized = NormalizeTextSizeOption(value);
            if (_globalTextSize == normalized)
                return;

            _globalTextSize = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextSizePt));
            ClearPreview();
        }
    }

    public double TextSizePt => GlobalTextSize;

    public string TextSizeValidationMessage => string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            RefreshCommandState();
        }
    }

    public bool HasPreview
    {
        get => _hasPreview;
        set
        {
            _hasPreview = value;
            OnPropertyChanged();
        }
    }

    public ReportColumnOptionViewModel? SelectedAvailableColumn
    {
        get => _selectedAvailableColumn;
        set
        {
            _selectedAvailableColumn = value;
            OnPropertyChanged();
        }
    }

    public ReportColumnOptionViewModel? SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            _selectedColumn = value;
            OnPropertyChanged();
        }
    }

    public bool CanBuildReport =>
        !string.IsNullOrWhiteSpace(SelectedReportName)
        && !string.IsNullOrWhiteSpace(ItemsIncluded)
        && SelectedColumns.Count > 0;

    public bool IsSelectedTemplateUserTemplate =>
        _userTemplates.Any(template =>
            template.TemplateName.Equals(SelectedReportName, StringComparison.OrdinalIgnoreCase));

    public ICommand BrowseLogoCommand { get; }
    public ICommand PreviewCommand => _previewCommand;
    public ICommand ExportPdfCommand => _exportPdfCommand;
    public ICommand ExportExcelCommand => _exportExcelCommand;
    public ICommand SaveTemplateCommand => _saveTemplateCommand;
    public ICommand DeleteTemplateCommand => _deleteTemplateCommand;
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddColumnCommand { get; }
    public ICommand RemoveColumnCommand { get; }
    public ICommand AddAllColumnsCommand { get; }
    public ICommand RemoveAllColumnsCommand { get; }
    public ICommand MoveColumnUpCommand { get; }
    public ICommand MoveColumnDownCommand { get; }

    public void Initialize(ReportFamily family, string templateName, string itemsIncluded)
    {
        _family = family;
        ProjectName = _workspace.CurrentSession?.ProjectName ?? "Untitled Project";
        GeneratedOnText = "Auto on export";
        LoadOptions(family);

        _selectedReportName = templateName;
        OnPropertyChanged(nameof(SelectedReportName));

        ItemsIncluded = itemsIncluded;
        ResetToSelectedTemplate(resetItemsIncluded: false);
        UpdateGaWarning();
        ClearPreview();
    }

    private void LoadOptions(ReportFamily family)
    {
        ReportNames.Clear();
        _userTemplates.Clear();

        foreach (var template in ReportTemplateCatalog.GetBuiltInTemplates(family))
            ReportNames.Add(template.Name);

        foreach (var template in _templateStore.LoadAsync(family).GetAwaiter().GetResult()
                     .OrderBy(template => template.TemplateName))
        {
            if (ReportNames.Any(name => name.Equals(template.TemplateName, StringComparison.OrdinalIgnoreCase)))
                continue;

            _userTemplates.Add(template);
            ReportNames.Add(template.TemplateName);
        }

        _allowedColumns.Clear();
        var allowedDefinitions = ReportTemplateCatalog.GetAvailableColumns(family)
            .Concat(_userTemplates.SelectMany(template => template.Columns))
            .GroupBy(column => column.Key)
            .Select(group => group.OrderBy(column => column.Order).First())
            .OrderBy(column => column.Order)
            .ThenBy(column => column.Header)
            .ToList();

        foreach (var column in allowedDefinitions)
            _allowedColumns.Add(new ReportColumnOptionViewModel(column));

        ItemsIncludedOptions.Clear();
        var items = family == ReportFamily.ElementList
            ? new[]
            {
                "All Elements",
                "Rendered Elements Only",
                "GA-Ready Elements",
                "Needs Review Elements",
                "Selected Elements Only",
                "Current Filter Result"
            }
            : new[]
            {
                "Optimization Result",
                "Liftable Items Only",
                "Failed Items Only",
                "Selected Setup Only"
            };

        foreach (var item in items)
            ItemsIncludedOptions.Add(item);

        OnPropertyChanged(nameof(IsSelectedTemplateUserTemplate));
        RefreshCommandState();
    }

    private void ResetToSelectedTemplate(bool resetItemsIncluded = false)
    {
        if (string.IsNullOrWhiteSpace(SelectedReportName))
            return;

        var userTemplate = _userTemplates.FirstOrDefault(template =>
            template.TemplateName.Equals(SelectedReportName, StringComparison.OrdinalIgnoreCase));
        var templateColumns = userTemplate?.Columns
            ?? ReportTemplateCatalog.GetBuiltInTemplate(_family, SelectedReportName).Columns;
        var defaultItemsIncluded = userTemplate?.ItemsIncluded
            ?? ReportTemplateCatalog.GetBuiltInTemplate(_family, SelectedReportName).DefaultItemsIncluded;
        TextAlignment = string.IsNullOrWhiteSpace(userTemplate?.TextAlignment)
            ? "Left"
            : userTemplate!.TextAlignment!;
        GlobalTextSize = NormalizeTextSizeOption(userTemplate?.TextSizePt ?? 10);

        SelectedColumns.Clear();
        foreach (var column in templateColumns.Where(column => column.IsSelected).OrderBy(column => column.Order))
            SelectedColumns.Add(new ReportColumnOptionViewModel(column));

        SelectedColumn = SelectedColumns.FirstOrDefault();

        if (resetItemsIncluded || string.IsNullOrWhiteSpace(ItemsIncluded))
            ItemsIncluded = defaultItemsIncluded;

        NormalizeSelectedColumnOrder();
        RefreshAvailableColumns();
        ClearPreview();
        RefreshCommandState();
    }

    private void BrowseLogo()
    {
        var path = _dialogs.BrowseLogo();
        if (!string.IsNullOrWhiteSpace(path))
            CompanyLogoPath = path;
    }

    private void AddSelectedAvailableColumn()
    {
        if (SelectedAvailableColumn == null)
            return;

        AddColumn(SelectedAvailableColumn);
    }

    private void RemoveSelectedColumn()
    {
        if (SelectedColumn == null)
            return;

        var removedIndex = SelectedColumns.IndexOf(SelectedColumn);
        SelectedColumns.Remove(SelectedColumn);
        SelectedColumn = SelectedColumns.Count == 0
            ? null
            : SelectedColumns[Math.Min(Math.Max(removedIndex, 0), SelectedColumns.Count - 1)];
        NormalizeSelectedColumnOrder();
        RefreshAvailableColumns();
        ClearPreview();
        RefreshCommandState();
    }

    private void AddAllAvailableColumns()
    {
        foreach (var column in AvailableColumns.ToList())
            AddColumn(column, clearPreview: false);

        NormalizeSelectedColumnOrder();
        RefreshAvailableColumns();
        ClearPreview();
        RefreshCommandState();
    }

    private void RemoveAllSelectedColumns()
    {
        SelectedColumns.Clear();
        SelectedColumn = null;
        RefreshAvailableColumns();
        ClearPreview();
        RefreshCommandState();
    }

    private void MoveSelectedColumnUp()
    {
        if (SelectedColumn == null)
            return;

        var index = SelectedColumns.IndexOf(SelectedColumn);
        if (index <= 0)
            return;

        SelectedColumns.Move(index, index - 1);
        NormalizeSelectedColumnOrder();
        ClearPreview();
    }

    private void MoveSelectedColumnDown()
    {
        if (SelectedColumn == null)
            return;

        var index = SelectedColumns.IndexOf(SelectedColumn);
        if (index < 0 || index >= SelectedColumns.Count - 1)
            return;

        SelectedColumns.Move(index, index + 1);
        NormalizeSelectedColumnOrder();
        ClearPreview();
    }

    private void AddColumn(ReportColumnOptionViewModel column, bool clearPreview = true)
    {
        var existing = SelectedColumns.FirstOrDefault(selected =>
            selected.Key.Equals(column.Key, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedColumn = existing;
            return;
        }

        var clone = column.Clone();
        clone.IsSelected = true;
        clone.Order = SelectedColumns.Count;
        SelectedColumns.Add(clone);
        SelectedColumn = clone;

        NormalizeSelectedColumnOrder();
        RefreshAvailableColumns();

        if (clearPreview)
        {
            ClearPreview();
            RefreshCommandState();
        }
    }

    private async Task PreviewAsync()
    {
        ReportDocumentDto? previewReport = null;
        await RunBusyAsync(async () =>
        {
            if (!ValidateCanBuildReport())
                return;

            var previewGeneratedOn = DateTime.Now;
            _currentPreview = await BuildCurrentReportAsync(previewGeneratedOn, updateGeneratedOnLabel: false, CancellationToken.None);
            previewReport = _currentPreview;
            PreviewSummary = BuildPreviewSummary(_currentPreview);
            HasPreview = true;
            var fit = ReportLayoutFitEstimator.EstimateForReport(_currentPreview);
            LayoutWarningMessage = fit.Message;
            StatusMessage = fit.Severity == ReportLayoutFitSeverity.Ok ? "Preview opened." : fit.Message;
        });

        if (previewReport != null)
        {
            var fit = ReportLayoutFitEstimator.EstimateForReport(previewReport);
            _previewDialogs.ShowPreview(previewReport, fit, ExportPdfAsync, ExportExcelAsync);
        }
    }

    private async Task ExportPdfAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!ValidateCanBuildReport())
                return;

            var exportDate = DateTime.Now;
            var report = await BuildCurrentReportAsync(exportDate, updateGeneratedOnLabel: true, CancellationToken.None);
            var fit = ReportLayoutFitEstimator.EstimateForReport(report);

            if (fit.Severity != ReportLayoutFitSeverity.Ok
                && !_dialogs.Confirm($"{fit.Message}\n\nContinue export anyway?", "A4 Layout Warning"))
            {
                return;
            }

            var reportsFolder = GetReportsFolder();
            var path = _dialogs.SavePdf(BuildSuggestedFileName(".pdf", reportsFolder), reportsFolder);
            if (string.IsNullOrWhiteSpace(path))
                return;

            await _pdfExporter.ExportAsync(report, path);
            StatusMessage = $"PDF exported: {path}";
            _dialogs.ShowInfo("PDF export completed.", "Report Export");
        });
    }

    private async Task ExportExcelAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!ValidateCanBuildReport())
                return;

            var exportDate = DateTime.Now;
            var report = await BuildCurrentReportAsync(exportDate, updateGeneratedOnLabel: true, CancellationToken.None);

            var reportsFolder = GetReportsFolder();
            var path = _dialogs.SaveExcel(BuildSuggestedFileName(".xlsx", reportsFolder), reportsFolder);
            if (string.IsNullOrWhiteSpace(path))
                return;

            await _excelExporter.ExportAsync(report, path);
            StatusMessage = $"Excel exported: {path}";
            _dialogs.ShowInfo("Excel export completed.", "Report Export");
        });
    }

    private async Task SaveTemplateAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!ValidateCanBuildReport())
                return;

            var existingNames = ReportNames.ToList();
            var defaultName = CreateUniqueTemplateName(SelectedReportName, existingNames);
            var name = _dialogs.PromptForTemplateName(defaultName, existingNames);
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (ReportNames.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _dialogs.ShowInfo("A template with this name already exists. Please choose another name.", "Report Template");
                return;
            }

            await _templateStore.SaveAsync(new ReportTemplateDto
            {
                TemplateName = name,
                InternalFamily = _family,
                ItemsIncluded = ItemsIncluded,
                Columns = SelectedColumns.Select(column => column.ToDefinition()).ToList(),
                Filters = "Current dialog filter settings",
                Sorting = "Current dialog sort settings",
                Grouping = "Current dialog grouping settings",
                Formatting = BuildTemplateFormattingText(),
                TextAlignment = TextAlignment,
                TextSizePt = TextSizePt
            });

            LoadOptions(_family);
            _selectedReportName = name;
            OnPropertyChanged(nameof(SelectedReportName));
            OnPropertyChanged(nameof(IsSelectedTemplateUserTemplate));
            ResetToSelectedTemplate(resetItemsIncluded: false);
            StatusMessage = $"Template saved: {name}";
        });
    }

    private async Task DeleteTemplateAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!IsSelectedTemplateUserTemplate)
            {
                StatusMessage = "Only user-created templates can be deleted.";
                return;
            }

            var templateName = SelectedReportName;
            if (!_dialogs.Confirm($"Delete template '{templateName}'?", "Delete Report Template"))
                return;

            await _templateStore.DeleteAsync(_family, templateName);
            LoadOptions(_family);

            var fallback = ReportTemplateCatalog.GetBuiltInTemplates(_family).First().Name;
            _selectedReportName = fallback;
            OnPropertyChanged(nameof(SelectedReportName));
            OnPropertyChanged(nameof(IsSelectedTemplateUserTemplate));
            ResetToSelectedTemplate(resetItemsIncluded: true);
            StatusMessage = $"Template deleted: {templateName}";
        });
    }

    private Task<ReportDocumentDto> BuildCurrentReportAsync(
        DateTime generatedOn,
        bool updateGeneratedOnLabel,
        CancellationToken cancellationToken)
    {
        var usesGaData = UsesGaData();
        var gaContext = usesGaData
            ? ResolveGaReportContext()
            : GaReportContext.NoRun;
        var selectedColumns = SelectedColumns
            .Select(column =>
            {
                var definition = column.ToDefinition();
                return new ReportColumnDefinition
                {
                    Key = definition.Key,
                    Header = definition.Header,
                    ValueKind = definition.ValueKind,
                    IsSelected = definition.IsSelected,
                    Order = definition.Order,
                    Format = definition.Format,
                    Alignment = TextAlignment,
                    TextSize = TextSizePt.ToString("0.##", CultureInfo.InvariantCulture),
                    Category = definition.Category,
                    Unit = definition.Unit
                };
            })
            .ToList();

        var request = new ReportBuildRequestDto
        {
            Family = _family,
            TemplateName = SelectedReportName,
            ItemsIncluded = ItemsIncluded,
            Metadata = BuildMetadata(generatedOn, updateGeneratedOnLabel, gaContext),
            SelectedColumns = selectedColumns,
            SourceElements = ResolveSourceElements(),
            SelectedElements = _scene.GetSelectedElements(),
            LiftingResults = BuildLiftingSourceRows(gaContext),
            CraneSetups = BuildCraneSetupRows(gaContext)
        };

        return _reportBuilder.BuildAsync(request, cancellationToken);
    }

    private ReportMetadataDto BuildMetadata(DateTime generatedOn, bool updateGeneratedOnLabel, GaReportContext gaContext)
    {
        if (updateGeneratedOnLabel)
            GeneratedOnText = generatedOn.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

        var usesGaData = UsesGaData();
        var hasGaRun = !usesGaData || gaContext.HasGaRun;
        var gaWarning = usesGaData && !gaContext.HasGaRun
            ? NoGaAnalysisWarning
            : null;

        return new ReportMetadataDto
        {
            ReportName = SelectedReportName,
            ItemsIncluded = ItemsIncluded,
            ProjectName = ProjectName,
            ReportDate = generatedOn,
            CompanyName = CompanyName,
            CompanyLogoPath = CompanyLogoPath,
            CreatedBy = CreatedBy,
            IssuedTo = IssuedTo,
            Revision = Revision,
            HasGaRun = hasGaRun,
            GaRunStatusText = usesGaData
                ? gaContext.Run?.HealthLabel ?? (gaContext.HasGaRun ? "GA run available" : "No GA run")
                : null,
            GaWarningText = gaWarning,
            CraneInfo = BuildCraneInfo(gaContext),
            LiftingAccessories = _family == ReportFamily.LiftingResult && gaContext.HasGaRun
                ? new LiftingAccessoriesInfoDto { AdditionalWeightTons = _gaSettings.AdditionalWt }
                : null,
            GeneralNotes = GeneralNotes
        };
    }

    private bool ValidateCanBuildReport()
    {
        if (!CanBuildReport)
        {
            var message = string.IsNullOrWhiteSpace(SelectedReportName)
                ? "Choose a report template first."
                : string.IsNullOrWhiteSpace(ItemsIncluded)
                    ? "Choose which items are included first."
                    : "Select at least one column first.";

            BuildReadinessMessage = message;
            StatusMessage = message;
            _dialogs.ShowInfo(message, "Report Builder");
            return false;
        }

        BuildReadinessMessage = string.Empty;
        UpdateGaWarning();
        return true;
    }

    private void ClearPreview()
    {
        _currentPreview = null;
        PreviewSummary = "Preview has not been generated yet.";
        HasPreview = false;
        BuildReadinessMessage = CanBuildReport
            ? string.Empty
            : "Choose a report template, items included, and at least one selected column.";
        UpdateGaWarning();
        UpdateLayoutWarning();
    }

    private void RefreshAvailableColumns()
    {
        var selectedKeys = SelectedColumns.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var search = AvailableColumnSearch.Trim();
        var filtered = _allowedColumns
            .Where(column => !selectedKeys.Contains(column.Key))
            .Where(column => string.IsNullOrWhiteSpace(search)
                || column.Header.Contains(search, StringComparison.OrdinalIgnoreCase)
                || column.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(column => column.Category)
            .ThenBy(column => column.Header)
            .ToList();

        AvailableColumns.Clear();
        foreach (var column in filtered)
        {
            var clone = column.Clone();
            clone.IsAlreadySelected = false;
            AvailableColumns.Add(clone);
        }

        SelectedAvailableColumn = AvailableColumns.FirstOrDefault();
    }

    private void SelectedColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (ReportColumnOptionViewModel column in e.OldItems)
                column.PropertyChanged -= SelectedColumn_PropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (ReportColumnOptionViewModel column in e.NewItems)
                column.PropertyChanged += SelectedColumn_PropertyChanged;
        }

        UpdateLayoutWarning();
    }

    private void SelectedColumn_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReportColumnOptionViewModel.Unit))
        {
            ClearPreview();
            UpdateLayoutWarning();
        }
    }

    private void UpdateLayoutWarning()
    {
        if (SelectedColumns.Count == 0)
        {
            LayoutWarningMessage = string.Empty;
            return;
        }

        var fit = ReportLayoutFitEstimator.EstimateForBuilder(SelectedColumns, TextSizePt);
        LayoutWarningMessage = fit.Message;
    }

    private void NormalizeSelectedColumnOrder()
    {
        for (var index = 0; index < SelectedColumns.Count; index++)
        {
            SelectedColumns[index].Order = index;
            SelectedColumns[index].IsSelected = true;
        }

        OnPropertyChanged(nameof(CanBuildReport));
    }

    private void RefreshCommandState()
    {
        OnPropertyChanged(nameof(CanBuildReport));
        _previewCommand.RaiseCanExecuteChanged();
        _exportPdfCommand.RaiseCanExecuteChanged();
        _exportExcelCommand.RaiseCanExecuteChanged();
        _saveTemplateCommand.RaiseCanExecuteChanged();
        _deleteTemplateCommand.RaiseCanExecuteChanged();
    }

    private string BuildTemplateFormattingText()
    {
        var settings = SelectedColumns.Select(column =>
            $"{column.Key}: unit='{column.Unit}'");

        return $"textAlignment='{TextAlignment}', textSizePt='{TextSizePt:0.##}'{Environment.NewLine}"
            + string.Join(Environment.NewLine, settings);
    }

    private CraneReportInfoDto? BuildCraneInfo(GaReportContext gaContext)
    {
        if (_family != ReportFamily.LiftingResult)
            return null;

        return new CraneReportInfoDto
        {
            CraneName = gaContext.HasGaRun ? CleanText(gaContext.CraneNameOrId) : Placeholder,
            CapacityFactor = 0.90
        };
    }

    private IReadOnlyList<PrecastElement> ResolveSourceElements()
    {
        if (_family != ReportFamily.ElementList)
            return _scene.GetElements();

        if (ItemsIncluded == "Current Filter Result")
            return _elementManagement.FilteredElements.ToList();

        return _scene.GetElements();
    }

    private IReadOnlyList<LiftingResultSourceRowDto> BuildLiftingSourceRows(GaReportContext gaContext)
    {
        var result = _cranePlacement.Result;
        if (result == null)
            return gaContext.Diagnostics.Select(row => ToLiftingSourceRow(row, gaContext.Run)).ToList();

        var rows = new List<LiftingResultSourceRowDto>();
        for (int setupIndex = 0; setupIndex < result.Positions.Count; setupIndex++)
        {
            var position = result.Positions[setupIndex];
            rows.AddRange(position.ElementResults.Select(lift => ToLiftingSourceRow(lift, setupIndex, result, gaContext.Run)));
        }

        if (ItemsIncluded == "Selected Setup Only" && _cranePlacement.SelectedPosition != null)
        {
            var selectedIndex = result.Positions.IndexOf(_cranePlacement.SelectedPosition) + 1;
            if (selectedIndex > 0)
                rows = rows.Where(row => row.AssignedSetupNo == selectedIndex).ToList();
        }

        return rows;
    }

    private IReadOnlyList<CraneSetupReportRowDto> BuildCraneSetupRows(GaReportContext gaContext)
    {
        var result = _cranePlacement.Result;
        if (result == null || !gaContext.HasGaRun)
            return Array.Empty<CraneSetupReportRowDto>();

        var total = Math.Max(1, result.TotalElementCount);
        return result.Positions
            .Select((position, index) => new CraneSetupReportRowDto
            {
                SetupNo = index + 1,
                CraneX = position.X,
                CraneY = position.Y,
                CraneZ = position.Z,
                LiftedItemsCount = position.LiftableCount,
                LiftedItemsPercentage = position.LiftableCount * 100.0 / total
            })
            .ToList();
    }

    private GaReportContext ResolveGaReportContext()
    {
        var history = _gaDiagnosticsReader.ReadRunHistory(_workspace.CurrentSession?.ProjectFolderPath);
        var latestRun = history.LastOrDefault(IsValidRunHistory);
        var diagnostics = latestRun == null
            ? Array.Empty<GAElementDiagnosticDto>()
            : _gaDiagnosticsReader.ReadDetailedDiagnostics(latestRun.DetailedDiagnosticsCsvPath);

        if (_cranePlacement.Result is { Positions.Count: > 0 } currentResult)
            return new GaReportContext(true, latestRun, diagnostics, ResolveCraneName(latestRun, diagnostics));

        if (latestRun != null)
            return new GaReportContext(true, latestRun, diagnostics, ResolveCraneName(latestRun, diagnostics));

        return new GaReportContext(false, null, Array.Empty<GAElementDiagnosticDto>(), Placeholder);
    }

    private static bool IsValidRunHistory(GARunHistoryDto run) =>
        !string.IsNullOrWhiteSpace(run.Timestamp)
        || !string.IsNullOrWhiteSpace(run.DetailedDiagnosticsCsvPath)
        || run.TotalTargetElements > 0;

    private static string ResolveCraneName(GARunHistoryDto? run, IReadOnlyList<GAElementDiagnosticDto> diagnostics)
    {
        var fromRun = CleanText(run?.CraneNameOrId);
        if (fromRun != Placeholder)
            return fromRun;

        var fromDiagnostics = diagnostics.Select(row => CleanText(row.CraneNameOrId)).FirstOrDefault(value => value != Placeholder);
        return fromDiagnostics ?? Placeholder;
    }

    private LiftingResultSourceRowDto ToLiftingSourceRow(
        UiElementLiftResult lift,
        int setupIndex,
        UiCranePlacementResult result,
        GARunHistoryDto? run)
    {
        var values = CreateRunSummaryValues(run, result);
        var assignedSetup = setupIndex + 1;
        var utilization = lift.AvailableCapacity > 0 && lift.RequiredCapacity > 0
            ? lift.RequiredCapacity / lift.AvailableCapacity * 100.0
            : (double?)null;

        values["ElementId"] = CleanText(lift.ElementId);
        values["ElementMark"] = CleanText(lift.ElementMark ?? lift.ElementId);
        values["ElementType"] = CleanText(lift.ElementType);
        values["Weight"] = lift.Weight is > 0 ? lift.Weight.Value : Placeholder;
        values["CogX"] = lift.CogX;
        values["CogY"] = lift.CogY;
        values["CogZ"] = lift.CogZ;
        values["AssignedSetupIndex"] = assignedSetup;
        values["AssignedCraneX"] = lift.CraneBaseX;
        values["AssignedCraneY"] = lift.CraneBaseY;
        values["AssignedCraneZ"] = lift.CraneBaseZ;
        values["BestRadius"] = PositiveOrPlaceholder(lift.Radius);
        values["RequiredRadius"] = PositiveOrPlaceholder(lift.RequiredRadius > 0 ? lift.RequiredRadius : lift.Radius);
        values["SelectedChartRadius"] = PositiveOrPlaceholder(lift.SelectedChartRadius);
        values["RequiredBoomLength"] = PositiveOrPlaceholder(lift.BoomLength);
        values["SelectedChartBoomLength"] = PositiveOrPlaceholder(lift.SelectedChartBoomLength);
        values["RequiredCapacity"] = PositiveOrPlaceholder(lift.RequiredCapacity);
        values["AvailableCapacity"] = PositiveOrPlaceholder(lift.AvailableCapacity);
        values["CapacityUtilizationPercent"] = utilization ?? (object)Placeholder;
        values["CapacityMargin"] = lift.AvailableCapacity > 0 && lift.RequiredCapacity > 0
            ? lift.AvailableCapacity - lift.RequiredCapacity
            : Placeholder;
        values["LiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(lift.Status.ToString());
        values["IsLifted"] = BoolText(lift.Status.ToString().Equals("Liftable", StringComparison.OrdinalIgnoreCase));
        values["ReachStatus"] = CleanText(lift.ReachStatus.ToString());
        values["ChartStatus"] = CleanText(lift.ChartStatus.ToString());
        values["IsExactMatch"] = BoolText(lift.IsExactMatch);
        values["IsSteppedRadius"] = BoolText(lift.IsSteppedRadius);
        values["IsBlankCell"] = BoolText(lift.IsBlankCell);
        values["IsZeroCell"] = BoolText(lift.IsZeroCell);
        values["SafetyMargin"] = lift.SafetyMargin;
        values["LookupMode"] = CleanText(lift.LookupMode);
        values["CraneBaseX"] = lift.CraneBaseX;
        values["CraneBaseY"] = lift.CraneBaseY;
        values["CraneBaseZ"] = lift.CraneBaseZ;
        values["SlewCenterX"] = lift.SlewCenterX;
        values["SlewCenterY"] = lift.SlewCenterY;
        values["SlewCenterZ"] = lift.SlewCenterZ;
        values["BoomHingeX"] = lift.BoomHingeX;
        values["BoomHingeY"] = lift.BoomHingeY;
        values["BoomHingeZ"] = lift.BoomHingeZ;
        values["LoadPointX"] = lift.LoadPointX;
        values["LoadPointY"] = lift.LoadPointY;
        values["LoadPointZ"] = lift.LoadPointZ;
        values["RadiusOriginType"] = CleanText(lift.RadiusOriginType);
        values["BoomOriginType"] = CleanText(lift.BoomOriginType);
        values["CandidateBoomsChecked"] = CleanText(lift.CandidateBoomsChecked);
        values["RejectedCandidates"] = CleanText(lift.RejectedCandidates);
        values["CandidatePointId"] = CleanText(lift.CandidatePointId);
        values["CandidatePointIndex"] = lift.CandidatePointIndex >= 0 ? lift.CandidatePointIndex : Placeholder;
        values["IsFeasibleFromPoint"] = BoolText(lift.IsFeasibleFromPoint);
        values["IsFinalAssignedToPoint"] = BoolText(lift.IsFinalAssignedToPoint);
        values["AssignedPointId"] = CleanText(lift.AssignedPointId);
        values["AssignedPointIndex"] = lift.AssignedPointIndex >= 0 ? lift.AssignedPointIndex : Placeholder;
        values["AssignmentReason"] = CleanText(lift.AssignmentReason);
        values["AssignmentScore"] = lift.AssignmentScore;
        values["DuplicateAssignmentResolved"] = BoolText(lift.DuplicateAssignmentResolved);
        values["FeasiblePointIds"] = CleanText(lift.FeasiblePointIds);
        values["FinalOwnerPointId"] = CleanText(lift.FinalOwnerPointId);
        values["IsFinalAssigned"] = BoolText(lift.IsFinalAssigned);
        values["BestFailedPointId"] = CleanText(lift.BestFailedPointId);
        values["BestFailureReason"] = CleanText(lift.BestFailureReason);
        values["HasBestFailedCandidate"] = BoolText(lift.HasBestFailedCandidate);
        values["BestFailedLiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(lift.BestFailedLiftStatus.ToString());
        values["BestFailedReachStatus"] = CleanText(lift.BestFailedReachStatus.ToString());
        values["BestFailedChartStatus"] = CleanText(lift.BestFailedChartStatus.ToString());
        values["BestFailedSafetyMargin"] = lift.HasBestFailedCandidate ? lift.BestFailedSafetyMargin : Placeholder;
        values["BestFailedRequiredRadius"] = lift.HasBestFailedCandidate ? PositiveOrPlaceholder(lift.BestFailedRequiredRadius) : Placeholder;
        values["BestFailedRequiredBoomLength"] = lift.HasBestFailedCandidate ? PositiveOrPlaceholder(lift.BestFailedRequiredBoomLength) : Placeholder;

        return new LiftingResultSourceRowDto
        {
            Values = values,
            ElementId = lift.ElementId,
            ElementMark = lift.ElementMark,
            CanonicalMark = lift.CanonicalMark,
            MarkSource = lift.MarkSource,
            MarkConfidence = lift.MarkConfidence,
            IsExplicitMark = lift.IsExplicitMark,
            ElementType = lift.ElementType,
            Timestamp = run?.Timestamp,
            ProjectName = run?.ProjectName,
            IfcFileName = run?.IfcFileName,
            CraneNameOrId = ResolveCraneName(run, Array.Empty<GAElementDiagnosticDto>()),
            AssignedSetupNo = assignedSetup,
            Radius = lift.Radius,
            BoomLength = lift.BoomLength,
            RequiredCapacity = lift.RequiredCapacity,
            AvailableCapacity = lift.AvailableCapacity,
            InternalLiftStatus = lift.Status.ToString(),
            IsFinalAssignedToPoint = lift.IsFinalAssignedToPoint,
            IsFinalAssigned = lift.IsFinalAssigned
        };
    }

    private static LiftingResultSourceRowDto ToLiftingSourceRow(GAElementDiagnosticDto row, GARunHistoryDto? run)
    {
        var values = CreateRunSummaryValues(run, null);
        var utilization = row.AvailableCapacity > 0 && row.RequiredCapacity > 0
            ? row.RequiredCapacity / row.AvailableCapacity * 100.0
            : (double?)null;

        values["GARunTimestamp"] = CleanText(row.Timestamp, run?.Timestamp);
        values["ProjectName"] = CleanText(row.ProjectName, run?.ProjectName);
        values["IfcFileName"] = CleanText(row.IfcFileName, run?.IfcFileName);
        values["CraneNameOrId"] = CleanText(row.CraneNameOrId, run?.CraneNameOrId);
        values["ElementId"] = CleanText(row.ElementId);
        values["ElementMark"] = CleanText(row.ElementName, row.ElementId);
        values["ElementType"] = CleanText(row.ElementType);
        values["StoreyOrElevation"] = CleanText(row.StoreyOrElevation);
        values["Weight"] = PositiveOrPlaceholder(row.Weight);
        values["CogX"] = row.CogX;
        values["CogY"] = row.CogY;
        values["CogZ"] = row.CogZ;
        values["AssignedSetupIndex"] = row.AssignedSetupIndex >= 0 ? row.AssignedSetupIndex + 1 : Placeholder;
        values["AssignedCraneX"] = ParseDoubleOrPlaceholder(row.AssignedCraneX);
        values["AssignedCraneY"] = ParseDoubleOrPlaceholder(row.AssignedCraneY);
        values["AssignedCraneZ"] = ParseDoubleOrPlaceholder(row.AssignedCraneZ);
        values["BestRadius"] = PositiveOrPlaceholder(row.BestRadius);
        values["RequiredRadius"] = PositiveOrPlaceholder(row.RequiredRadius);
        values["SelectedChartRadius"] = PositiveOrPlaceholder(row.SelectedChartRadius);
        values["RequiredBoomLength"] = PositiveOrPlaceholder(row.RequiredBoomLength);
        values["SelectedChartBoomLength"] = PositiveOrPlaceholder(row.SelectedChartBoomLength);
        values["RequiredCapacity"] = PositiveOrPlaceholder(row.RequiredCapacity);
        values["AvailableCapacity"] = PositiveOrPlaceholder(row.AvailableCapacity);
        values["CapacityUtilizationPercent"] = utilization ?? (object)Placeholder;
        values["CapacityMargin"] = row.AvailableCapacity > 0 && row.RequiredCapacity > 0
            ? row.AvailableCapacity - row.RequiredCapacity
            : Placeholder;
        values["LiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(row.LiftStatus);
        values["IsLifted"] = BoolText(row.IsLifted);
        values["FailureReason"] = CleanText(row.FailureReason);
        values["FailureCategory"] = CleanText(row.FailureCategory);
        values["SuggestedAction"] = CleanText(row.SuggestedAction);
        values["ReachStatus"] = CleanText(row.ReachStatus);
        values["ChartStatus"] = CleanText(row.ChartStatus);
        values["IsExactMatch"] = BoolText(row.IsExactMatch);
        values["IsSteppedRadius"] = BoolText(row.IsSteppedRadius);
        values["IsBlankCell"] = BoolText(row.IsBlankCell);
        values["IsZeroCell"] = BoolText(row.IsZeroCell);
        values["SafetyMargin"] = row.SafetyMargin;
        values["LookupMode"] = CleanText(row.LookupMode);
        values["CraneBaseX"] = row.CraneBaseX;
        values["CraneBaseY"] = row.CraneBaseY;
        values["CraneBaseZ"] = row.CraneBaseZ;
        values["SlewCenterX"] = row.SlewCenterX;
        values["SlewCenterY"] = row.SlewCenterY;
        values["SlewCenterZ"] = row.SlewCenterZ;
        values["BoomHingeX"] = row.BoomHingeX;
        values["BoomHingeY"] = row.BoomHingeY;
        values["BoomHingeZ"] = row.BoomHingeZ;
        values["LoadPointX"] = row.LoadPointX;
        values["LoadPointY"] = row.LoadPointY;
        values["LoadPointZ"] = row.LoadPointZ;
        values["RadiusOriginType"] = CleanText(row.RadiusOriginType);
        values["BoomOriginType"] = CleanText(row.BoomOriginType);
        values["CandidatePointId"] = CleanText(row.CandidatePointId);
        values["CandidatePointIndex"] = row.CandidatePointIndex >= 0 ? row.CandidatePointIndex : Placeholder;
        values["IsFeasibleFromPoint"] = BoolText(row.IsFeasibleFromPoint);
        values["IsFinalAssignedToPoint"] = BoolText(row.IsFinalAssignedToPoint);
        values["AssignedPointId"] = CleanText(row.AssignedPointId);
        values["AssignedPointIndex"] = row.AssignedPointIndex >= 0 ? row.AssignedPointIndex : Placeholder;
        values["AssignmentReason"] = CleanText(row.AssignmentReason);
        values["AssignmentScore"] = row.AssignmentScore;
        values["DuplicateAssignmentResolved"] = BoolText(row.DuplicateAssignmentResolved);
        values["FeasiblePointIds"] = CleanText(row.FeasiblePointIds);
        values["FinalOwnerPointId"] = CleanText(row.FinalOwnerPointId);
        values["IsFinalAssigned"] = BoolText(row.IsFinalAssigned);
        values["BestFailedPointId"] = CleanText(row.BestFailedPointId);
        values["BestFailureReason"] = CleanText(row.BestFailureReason);
        values["HasBestFailedCandidate"] = BoolText(row.HasBestFailedCandidate);
        values["BestFailedLiftStatus"] = LiftingResultReportDataProvider.MapLiftStatus(row.BestFailedLiftStatus);
        values["BestFailedReachStatus"] = CleanText(row.BestFailedReachStatus);
        values["BestFailedChartStatus"] = CleanText(row.BestFailedChartStatus);
        values["BestFailedSafetyMargin"] = row.HasBestFailedCandidate ? row.BestFailedSafetyMargin : Placeholder;
        values["BestFailedRequiredRadius"] = row.HasBestFailedCandidate ? PositiveOrPlaceholder(row.BestFailedRequiredRadius) : Placeholder;
        values["BestFailedRequiredBoomLength"] = row.HasBestFailedCandidate ? PositiveOrPlaceholder(row.BestFailedRequiredBoomLength) : Placeholder;
        values["CandidateBoomsChecked"] = CleanText(row.CandidateBoomsChecked);
        values["RejectedCandidates"] = CleanText(row.RejectedCandidates);
        values["SelectedVisualColumnIndex"] = row.SelectedVisualColumnIndex ?? (object)Placeholder;
        values["SelectedVisualColumnHeader"] = CleanText(row.SelectedVisualColumnHeader);
        values["SelectedNumberOfLines"] = row.SelectedNumberOfLines ?? (object)Placeholder;
        values["SelectedOperatingConditionLabel"] = CleanText(row.SelectedOperatingConditionLabel);
        values["ConservativeDuplicateFallback"] = BoolText(row.IsConservativeDuplicateFallback);
        values["DuplicateAlternativeCount"] = row.DuplicateAlternativeCount;
        values["DuplicateAlternativeSummary"] = CleanText(row.DuplicateAlternativeSummary);

        return new LiftingResultSourceRowDto
        {
            Values = values,
            ElementId = row.ElementId,
            ElementMark = CleanText(row.ElementName, row.ElementId),
            CanonicalMark = CleanText(row.CanonicalMark),
            MarkSource = CleanText(row.MarkSource),
            MarkConfidence = CleanText(row.MarkConfidence),
            IsExplicitMark = row.IsExplicitMark,
            ElementType = row.ElementType,
            StoreyOrElevation = row.StoreyOrElevation,
            Timestamp = row.Timestamp,
            ProjectName = row.ProjectName,
            IfcFileName = row.IfcFileName,
            CraneNameOrId = row.CraneNameOrId,
            AssignedSetupNo = row.AssignedSetupIndex + 1,
            Radius = row.BestRadius,
            BoomLength = row.RequiredBoomLength,
            RequiredCapacity = row.RequiredCapacity,
            AvailableCapacity = row.AvailableCapacity,
            InternalLiftStatus = row.LiftStatus,
            IsFinalAssignedToPoint = row.IsFinalAssignedToPoint,
            IsFinalAssigned = row.IsFinalAssigned
        };
    }

    private static Dictionary<string, object?> CreateRunSummaryValues(GARunHistoryDto? run, UiCranePlacementResult? result)
    {
        var selected = result?.TotalElementCount ?? run?.SelectedElements;
        var lifted = result?.LiftedElementCount ?? run?.LiftedElements;
        var failed = selected.HasValue && lifted.HasValue ? Math.Max(0, selected.Value - lifted.Value) : run?.FailedElements;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GARunTimestamp"] = CleanText(run?.Timestamp),
            ["GARunHealthLabel"] = CleanText(run?.HealthLabel, result != null ? "GA run available" : null),
            ["ProjectName"] = CleanText(run?.ProjectName),
            ["IfcFileName"] = CleanText(run?.IfcFileName),
            ["CraneNameOrId"] = CleanText(run?.CraneNameOrId),
            ["TotalTargetElements"] = result?.TotalElementCount ?? run?.TotalTargetElements ?? (object)Placeholder,
            ["SelectedElements"] = selected ?? (object)Placeholder,
            ["LiftedElements"] = lifted ?? (object)Placeholder,
            ["FailedElements"] = failed ?? (object)Placeholder,
            ["CoverageRatio"] = result != null ? $"{result.CoverageRatio:0.0}%" : CleanText(run?.CoverageRatio),
            ["FinalCraneSetupCount"] = result?.Positions.Count ?? run?.FinalCraneSetupCount ?? (object)Placeholder,
            ["GreedyIterations"] = run?.GreedyIterations ?? (object)Placeholder,
            ["PopulationSize"] = run?.PopulationSize ?? (object)Placeholder,
            ["ConfiguredGenerations"] = run?.ConfiguredGenerations ?? (object)Placeholder,
            ["ActualGenerations"] = run?.ActualGenerationLoopCount ?? (object)Placeholder,
            ["TotalGARuntime"] = CleanText(run?.TotalGATimeMs),
            ["ElementAssignmentTime"] = CleanText(run?.ElementAssignmentTimeMs),
            ["CapacityLookupCount"] = CleanText(run?.CraneCapacityLookupCount),
            ["CapacityCacheHits"] = CleanText(run?.CraneCapacityCacheHits),
            ["CapacityCacheMisses"] = CleanText(run?.CraneCapacityCacheMisses),
            ["CapacityCacheHitRatio"] = CleanText(run?.CraneCapacityCacheHitRatio)
        };
    }

    private void UpdateGaWarning()
    {
        if (!UsesGaData())
        {
            GaWarningText = string.Empty;
            return;
        }

        var context = ResolveGaReportContext();
        GaWarningText = context.HasGaRun
            ? string.Empty
            : NoGaAnalysisWarning;
    }

    private bool UsesGaData() =>
        _family == ReportFamily.LiftingResult
        || SelectedReportName.Equals(ReportTemplateCatalog.GaReadiness, StringComparison.OrdinalIgnoreCase);

    private static object PositiveOrPlaceholder(double value) => value > 0 ? value : Placeholder;

    private static object ParseDoubleOrPlaceholder(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return Placeholder;
    }

    private static string BoolText(bool value) => value ? "Yes" : "No";

    private static string CleanText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.Trim();
            if (trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return trimmed;
        }

        return Placeholder;
    }

    private static string BuildPreviewSummary(ReportDocumentDto report)
    {
        var warningCount = report.Rows.Count(row =>
            row.Values.TryGetValue("WeightWarning", out var value)
            && value is string warning
            && !string.IsNullOrWhiteSpace(warning)
            && warning != "-");

        var previewRows = Math.Min(50, report.Rows.Count);
        return $"Report Name: {report.Metadata.ReportName} | Items Included: {report.Metadata.ItemsIncluded} | Preview Rows: {previewRows} of {report.Rows.Count} | Warnings: {warningCount}";
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            await operation();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _dialogs.ShowInfo(ex.Message, "Report Builder");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildSuggestedFileName(string extension, string reportsFolder)
    {
        var baseName = $"{ProjectName}_{SelectedReportName}";
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Join("_", baseName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        safe = string.IsNullOrWhiteSpace(safe) ? "Report" : safe.Trim();
        var nextNumber = ResolveNextReportNumber(reportsFolder, safe, extension);
        return $"{safe}_{nextNumber:00}{extension}";
    }

    private static int ResolveNextReportNumber(string reportsFolder, string safeBaseName, string extension)
    {
        if (!Directory.Exists(reportsFolder))
            return 1;

        var prefix = safeBaseName + "_";
        return Directory.EnumerateFiles(reportsFolder, $"{safeBaseName}_*{extension}")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : string.Empty)
            .Select(text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string CreateUniqueTemplateName(string baseName, IReadOnlyCollection<string> existingNames)
    {
        var root = string.IsNullOrWhiteSpace(baseName) ? "My Report Template" : baseName.Trim();
        if (!existingNames.Contains(root, StringComparer.OrdinalIgnoreCase))
            return root;

        for (var index = 1; index < 1000; index++)
        {
            var candidate = $"{root} {index}";
            if (!existingNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                return candidate;
        }

        return $"{root} {DateTime.Now:yyyyMMddHHmmss}";
    }

    private string GetReportsFolder()
    {
        var projectFolder = _workspace.CurrentSession?.ProjectFolderPath;
        if (string.IsNullOrWhiteSpace(projectFolder))
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var reports = Path.Combine(projectFolder, "Reports");
        Directory.CreateDirectory(reports);
        return reports;
    }

    private static int NormalizeTextSizeOption(double value)
    {
        var clamped = Math.Clamp(value, 6, 32);
        var roundedToStep = (int)(Math.Round(clamped / 2.0, MidpointRounding.AwayFromZero) * 2);
        return Math.Clamp(roundedToStep, 6, 32);
    }

    private sealed record GaReportContext(
        bool HasGaRun,
        GARunHistoryDto? Run,
        IReadOnlyList<GAElementDiagnosticDto> Diagnostics,
        string CraneNameOrId)
    {
        public static GaReportContext NoRun { get; } =
            new(false, null, Array.Empty<GAElementDiagnosticDto>(), Placeholder);
    }
}
