using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Data;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class LoadChartImportDialog : Window
    {
        private readonly LoadChartCatalogService _catalogService;
        private readonly LoadChartCraneDto? _existingCrane;
        private readonly IUnitSettingsService _unitSettingsService;
        private UnitSettings _unitSettings;
        private ParsedLoadChart? _nominalChart;
        private ParsedLoadChart? _importableEnvelopeChart;
        private List<string> _gridHeaders = new();
        private DataTable? _exactVisualPreviewTable;
        private List<string> _exactVisualPreviewHeaders = new();
        private bool _hasExactVisualPreview;
        private bool _isShowingExactVisualPreview;
        private bool _suppressCapacityUnitRefresh;
        private bool _isApplyingMetadataAutoFill;
        private readonly LoadChartMetadataAutoFillState _metadataAutoFillState = new();
        private LoadChartPdfMetadataDto? _lastDetectedMetadata;
        private string _lastMetadataEvidenceText = string.Empty;
        private string _lastMetadataEvidenceSummary = string.Empty;
        private double _vehicleLengthMeters;
        private double _vehicleHeightMeters;
        private double _vehicleWidthMeters;
        private double? _outriggerWidthMeters;
        private double? _outriggerLengthMeters;

        public LoadChartImportDialog(LoadChartCraneDto? craneToEdit = null)
        {
            InitializeComponent();
            var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<CraneDbContext>>();
            _catalogService = new LoadChartCatalogService(factory);
            _unitSettingsService = App.ServiceProvider.GetRequiredService<IUnitSettingsService>();
            _unitSettings = _unitSettingsService.Current;
            _existingCrane = craneToEdit;
            ApplyUnitLabels();
            WireMetadataAutoFillOwnershipTracking();
            RefreshDimensionsSummary();
            ClearPdfMetadataEvidence();

            if (_existingCrane != null)
                LoadExistingCrane(_existingCrane);
        }

        private void WireMetadataAutoFillOwnershipTracking()
        {
            TxtModel.TextChanged += (_, _) =>
            {
                if (!_isApplyingMetadataAutoFill)
                    _metadataAutoFillState.ClearModelOwnership();
            };

            TxtMfg.TextChanged += (_, _) =>
            {
                if (!_isApplyingMetadataAutoFill)
                    _metadataAutoFillState.ClearManufacturerOwnership();
            };

            TxtCounterweight.TextChanged += (_, _) =>
            {
                if (!_isApplyingMetadataAutoFill)
                    _metadataAutoFillState.ClearCounterweightOwnership();
            };
        }
        private void LoadExistingCrane(LoadChartCraneDto crane)
        {
            TxtHeader.Text = "Edit Crane Load Chart";
            TxtModel.Text = crane.Name;
            TxtMfg.Text = crane.Manufacturer;
            SelectOutriggerConfig(crane.OutriggerConfig);
            TxtCounterweight.Text = FormatMassInput(crane.Counterweight);
            TxtMaxRadius.Text = FormatLengthInput(crane.MaxRadius);
            SetCapacityUnitMode(LoadChartCapacityUnitMode.Ton, suppressRefresh: true);
            CboChartCapacityUnit.IsEnabled = false;
            CboChartCapacityUnit.ToolTip = "Saved catalog capacity values are canonical metric tons. Load a new source file to choose a source unit again.";
            _vehicleLengthMeters = crane.VehicleLengthMeters;
            _vehicleHeightMeters = crane.VehicleHeightMeters;
            _vehicleWidthMeters = crane.VehicleWidthMeters;
            _outriggerWidthMeters = crane.OutriggerWidthMeters;
            _outriggerLengthMeters = crane.OutriggerLengthMeters;
            _metadataAutoFillState.Clear();
            _metadataAutoFillState.ClearOutriggerOwnership();
            ClearOutriggerDimensionsWhenOnRubber();
            RefreshDimensionsSummary();

            var rawGrid = BuildRawGridFromDto(crane);
            var parsedChart = crane.HasExactVisualLoadChart
                ? LoadChartCsvService.ParseExactVisualRawGrid(
                    rawGrid,
                    crane.LoadChartSourceDescription,
                    LoadChartCapacityUnitMode.Ton)
                : LoadChartCsvService.ParseRawGrid(rawGrid, LoadChartCapacityUnitMode.Ton);

            // ExactLoadChartRawGridJson is source/provenance data and may contain the
            // manufacturer's original kg representation. The edit grid is rebuilt from
            // canonical persisted TargetPoint capacities (tons), while the original source
            // grid remains preserved for audit/provenance.
            if (crane.HasExactVisualLoadChart && !string.IsNullOrWhiteSpace(crane.ExactLoadChartRawGridJson))
            {
                parsedChart = CopyChartWith(
                    parsedChart,
                    exactLoadChartRawGridJson: crane.ExactLoadChartRawGridJson);
            }

            SetNominalChart(parsedChart);

            if (crane.HasExactVisualLoadChart)
            {
                _hasExactVisualPreview = true;
                _exactVisualPreviewTable = parsedChart.PreviewTable;
                _exactVisualPreviewHeaders = parsedChart.PreviewHeaders.ToList();
                TxtEnvelopePreviewTitle.Text = "Exact PDF Visual Load Chart";
                TxtEnvelopePreviewDescription.Text = "This saved crane preserves repeated manufacturer PDF visual columns. Save Crane will keep the exact visual chart; optimization uses the conservative minimum capacity across duplicate numeric boom columns.";
                EnvelopePreviewPanel.Visibility = Visibility.Visible;
                BtnShowImportableEnvelope.IsEnabled = false;
                ShowExactVisualPreview();
            }

            TxtStatus.Text = crane.HasExactVisualLoadChart
                ? $"Editing saved exact PDF visual crane '{crane.Name}'."
                : $"Editing saved crane '{crane.Name}'.";
        }

        private static List<List<string>> BuildRawGridFromDto(LoadChartCraneDto crane)
        {
            if (crane.HasExactVisualLoadChart)
            {
                var exactRawGrid = TryReadExactRawGrid(crane.ExactLoadChartRawGridJson);
                if (exactRawGrid.Count > 1 && crane.Points.Any(point => point.VisualColumnIndex.HasValue))
                {
                    var canonicalPoints = crane.Points
                        .Select(point => new CraneOptimizer.Domain.Entities.TargetPoint
                        {
                            Radius = point.Radius,
                            BoomLength = point.BoomLength,
                            Capacity = point.Capacity,
                            VisualColumnIndex = point.VisualColumnIndex,
                            VisualColumnHeader = string.IsNullOrWhiteSpace(point.VisualColumnHeader) ? null : point.VisualColumnHeader,
                            NumberOfLines = point.NumberOfLines,
                            OperatingConditionLabel = string.IsNullOrWhiteSpace(point.OperatingConditionLabel) ? null : point.OperatingConditionLabel
                        })
                        .ToList();

                    return LoadChartCsvService.BuildCanonicalExactEditGrid(exactRawGrid, canonicalPoints);
                }

                return BuildExactRawGridFromDtoPoints(crane);
            }

            var booms = crane.Points
                .Select(point => point.BoomLength)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            var radii = crane.Points
                .Select(point => point.Radius)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)" }
            };
            rawGrid[0].AddRange(booms.Select(boom => boom.ToString("G", CultureInfo.InvariantCulture)));

            foreach (double radius in radii)
            {
                var row = new List<string> { radius.ToString("G", CultureInfo.InvariantCulture) };

                for (int i = 0; i < booms.Count; i++)
                {
                    var point = crane.Points
                        .FirstOrDefault(item => Math.Abs(item.Radius - radius) < 0.0001 && Math.Abs(item.BoomLength - booms[i]) < 0.0001);

                    row.Add(FormatCapacityForRawGrid(point?.Capacity));
                }

                rawGrid.Add(row);
            }

            return rawGrid;
        }

        private static List<List<string>> TryReadExactRawGrid(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<List<string>>();

            try
            {
                return JsonSerializer.Deserialize<List<List<string>>>(json) ?? new List<List<string>>();
            }
            catch
            {
                return new List<List<string>>();
            }
        }

        private static List<List<string>> BuildExactRawGridFromDtoPoints(LoadChartCraneDto crane)
        {
            var orderedColumns = crane.Points
                .Where(point => point.VisualColumnIndex.HasValue)
                .GroupBy(point => point.VisualColumnIndex.Value)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    Index = group.Key,
                    Header = group.Select(point => point.VisualColumnHeader)
                        .FirstOrDefault(header => !string.IsNullOrWhiteSpace(header))
                        ?? group.First().BoomLength.ToString("G", CultureInfo.InvariantCulture),
                    BoomLength = group.First().BoomLength
                })
                .ToList();

            if (orderedColumns.Count == 0)
                return BuildRawGridFromDto(crane with { LoadChartStorageMode = "Standard2D" });

            var rawGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
            };
            rawGrid[0].AddRange(orderedColumns.Select(column => column.Header));

            var radii = crane.Points
                .Select(point => point.Radius)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            foreach (double radius in radii)
            {
                var row = new List<string> { radius.ToString("G", CultureInfo.InvariantCulture) };

                foreach (var column in orderedColumns)
                {
                    var point = crane.Points.FirstOrDefault(item =>
                        item.VisualColumnIndex == column.Index && Math.Abs(item.Radius - radius) < 0.0001);

                    row.Add(FormatCapacityForRawGrid(point?.Capacity));
                }

                rawGrid.Add(row);
            }

            var numberOfLinesRow = new List<string> { "Number of lines" };
            bool hasNumberOfLines = false;
            foreach (var column in orderedColumns)
            {
                int? numberOfLines = crane.Points
                    .Where(point => point.VisualColumnIndex == column.Index)
                    .Select(point => point.NumberOfLines)
                    .FirstOrDefault(value => value.HasValue);

                numberOfLinesRow.Add(numberOfLines?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                hasNumberOfLines |= numberOfLines.HasValue;
            }

            if (hasNumberOfLines)
                rawGrid.Add(numberOfLinesRow);

            return rawGrid;
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Load chart files (*.csv;*.pdf)|*.csv;*.pdf|CSV files (*.csv)|*.csv|PDF files (*.pdf)|*.pdf"
            };

            if (dialog.ShowDialog() == true)
                LoadFile(dialog.FileName);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
            => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;

        private void DropZone_DragEnter(object sender, DragEventArgs e)
            => DropZone.Opacity = 0.7;

        private void DropZone_DragLeave(object sender, DragEventArgs e)
            => DropZone.Opacity = 1.0;

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Opacity = 1.0;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                LoadFile(files[0]);
        }

        private void LoadFile(string path)
        {
            try
            {
                ClearPdfMetadataEvidence();
                var importResult = LoadChartImportService.Import(path, this);

                // A successfully loaded replacement source file has its own source-unit
                // evidence and therefore re-enables source-unit resolution when editing.
                CboChartCapacityUnit.IsEnabled = true;
                CboChartCapacityUnit.ToolTip = null;
                ClearExactVisualPreview();
                _importableEnvelopeChart = importResult.ImportableEnvelopeChart;
                SetCapacityUnitMode(importResult.Chart.RequestedCapacityUnitMode, suppressRefresh: true);
                SetNominalChart(importResult.Chart);
                ApplyExactVisualPreview(importResult);
                string metadataSourceIdentity = BuildMetadataSourceIdentity(path, importResult.SourceDescription);
                PrepareForNewFileMetadataAutofill(metadataSourceIdentity);
                var metadataPrefillNotes = ApplyDetectedCraneMetadata(importResult.DetectedMetadata, Path.GetFileNameWithoutExtension(path), metadataSourceIdentity);
                UpdatePdfMetadataEvidence(importResult.DetectedMetadata, metadataPrefillNotes);

                TxtFileName.Text = Path.GetFileName(path);
                var info = new FileInfo(path);
                TxtFileSize.Text = $"{info.Length / 1024.0:F1} KB";
                FileInfoPanel.Visibility = Visibility.Visible;

                if (string.IsNullOrWhiteSpace(TxtModel.Text))
                {
                    _isApplyingMetadataAutoFill = true;
                    try
                    {
                        TxtModel.Text = Path.GetFileNameWithoutExtension(path);
                        _metadataAutoFillState.RememberModel(TxtModel.Text);
                    }
                    finally
                    {
                        _isApplyingMetadataAutoFill = false;
                    }
                }

                TxtMaxRadius.Text = _nominalChart == null ? string.Empty : FormatLengthInput(_nominalChart.MaxRadius);

                string metadataStatus = metadataPrefillNotes.Count == 0
                    ? string.Empty
                    : " " + string.Join(" ", metadataPrefillNotes);

                TxtStatus.Text = importResult.BuildStatusMessage(Path.GetFileName(path)) + " " + LoadChartCsvService.GetCapacityUnitSummary(importResult.Chart) + metadataStatus;

                if (importResult.HasActionableWarnings())
                {
                    MessageBox.Show(this, importResult.BuildWarningsMessage(), "PDF Import Notes", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Load Chart Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnTranspose_Click(object sender, RoutedEventArgs e)
        {
            if (_hasExactVisualPreview)
            {
                TxtStatus.Text = "Swap Radius / Boom is disabled for exact PDF visual charts so repeated manufacturer columns stay aligned.";
                return;
            }

            if (_nominalChart == null)
            {
                TxtStatus.Text = "Load chart data first.";
                return;
            }

            CommitGridEdits();
            if (ChartGrid.ItemsSource is DataView dataView && dataView.Table != null)
            {
                try
                {
                    _nominalChart = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders, GetSelectedCapacityUnitMode());
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = ex.Message;
                    return;
                }
            }

            if (_nominalChart == null)
                return;

            SetNominalChart(LoadChartCsvService.TransposePreview(_nominalChart.PreviewTable, _gridHeaders, GetSelectedCapacityUnitMode()));
            TxtStatus.Text = "Grid orientation swapped.";
        }

        private void PrepareForNewFileMetadataAutofill(string sourceIdentity)
        {
            bool dimensionsChanged = false;
            bool wasApplying = _isApplyingMetadataAutoFill;
            _isApplyingMetadataAutoFill = true;

            try
            {
                if (_metadataAutoFillState.IsSameSource(sourceIdentity))
                    return;

                ClearTextIfStillAuto(TxtModel, _metadataAutoFillState.ModelText);
                ClearTextIfStillAuto(TxtMfg, _metadataAutoFillState.ManufacturerText);
                ClearTextIfStillAuto(TxtCounterweight, _metadataAutoFillState.CounterweightText);

                if (_metadataAutoFillState.ShouldReplaceVehicleLength(_vehicleLengthMeters) && _vehicleLengthMeters > 0d)
                {
                    _vehicleLengthMeters = 0d;
                    dimensionsChanged = true;
                }

                if (_metadataAutoFillState.ShouldReplaceVehicleHeight(_vehicleHeightMeters) && _vehicleHeightMeters > 0d)
                {
                    _vehicleHeightMeters = 0d;
                    dimensionsChanged = true;
                }

                if (_metadataAutoFillState.ShouldReplaceVehicleWidth(_vehicleWidthMeters) && _vehicleWidthMeters > 0d)
                {
                    _vehicleWidthMeters = 0d;
                    dimensionsChanged = true;
                }

                if (_metadataAutoFillState.ShouldReplaceOutriggerWidth(_outriggerWidthMeters) && _outriggerWidthMeters.HasValue)
                {
                    _outriggerWidthMeters = null;
                    dimensionsChanged = true;
                }

                if (_metadataAutoFillState.ShouldReplaceOutriggerLength(_outriggerLengthMeters) && _outriggerLengthMeters.HasValue)
                {
                    _outriggerLengthMeters = null;
                    dimensionsChanged = true;
                }

                _metadataAutoFillState.Clear();
                _metadataAutoFillState.BeginSource(sourceIdentity);

                if (dimensionsChanged)
                    RefreshDimensionsSummary();
            }
            finally
            {
                _isApplyingMetadataAutoFill = wasApplying;
            }
        }

        private static void ClearTextIfStillAuto(TextBox textBox, string? previousAutoFill)
        {
            if (LoadChartMetadataAutoFillState.TextStillMatchesAutoFill(textBox.Text, previousAutoFill))
                textBox.Clear();
        }

        private static string BuildMetadataSourceIdentity(string path, string sourceDescription)
        {
            string normalizedPath = NormalizeMetadataSourcePath(path);
            string normalizedDescription = string.IsNullOrWhiteSpace(sourceDescription)
                ? string.Empty
                : sourceDescription.Trim();

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return $"{normalizedPath}|{normalizedDescription}";

                using var stream = info.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                string hash = Convert.ToHexString(SHA256.HashData(stream));

                return string.Join("|", new[]
                {
                    normalizedPath,
                    info.Length.ToString(CultureInfo.InvariantCulture),
                    info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                    hash,
                    normalizedDescription
                });
            }
            catch
            {
                return $"{normalizedPath}|{normalizedDescription}";
            }
        }

        private static string NormalizeMetadataSourcePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).Trim();
            }
            catch
            {
                return path?.Trim() ?? string.Empty;
            }
        }

        private List<string> ApplyDetectedCraneMetadata(LoadChartPdfMetadataDto metadata, string fallbackModelName, string sourceIdentity)
        {
            var applied = new List<string>();

            if (metadata == null || !metadata.HasAnyValue)
                return applied;

            bool wasApplying = _isApplyingMetadataAutoFill;
            _isApplyingMetadataAutoFill = true;

            try
            {
                _metadataAutoFillState.BeginSource(sourceIdentity);

                if (!string.IsNullOrWhiteSpace(metadata.Model)
                    && _metadataAutoFillState.ShouldReplaceModel(TxtModel.Text, fallbackModelName))
                {
                    TxtModel.Text = metadata.Model;
                    _metadataAutoFillState.RememberModel(TxtModel.Text);
                    applied.Add($"Detected model '{metadata.Model}'.");
                }

                if (!string.IsNullOrWhiteSpace(metadata.Manufacturer)
                    && _metadataAutoFillState.ShouldReplaceManufacturer(TxtMfg.Text))
                {
                    TxtMfg.Text = metadata.Manufacturer;
                    _metadataAutoFillState.RememberManufacturer(TxtMfg.Text);
                    applied.Add($"Detected manufacturer '{metadata.Manufacturer}'.");
                }

                if (metadata.CounterweightTons.HasValue
                    && _metadataAutoFillState.ShouldReplaceCounterweight(TxtCounterweight.Text))
                {
                    TxtCounterweight.Text = FormatMassInput(metadata.CounterweightTons.Value);
                    _metadataAutoFillState.RememberCounterweight(TxtCounterweight.Text);
                    applied.Add($"Detected counterweight {metadata.CounterweightTons.Value:0.###} t.");
                }

                bool dimensionsChanged = false;

                if (metadata.VehicleLengthMeters.HasValue && _metadataAutoFillState.ShouldReplaceVehicleLength(_vehicleLengthMeters))
                {
                    _vehicleLengthMeters = metadata.VehicleLengthMeters.Value;
                    _metadataAutoFillState.RememberVehicleLength(_vehicleLengthMeters);
                    dimensionsChanged = true;
                }

                if (metadata.VehicleHeightMeters.HasValue && _metadataAutoFillState.ShouldReplaceVehicleHeight(_vehicleHeightMeters))
                {
                    _vehicleHeightMeters = metadata.VehicleHeightMeters.Value;
                    _metadataAutoFillState.RememberVehicleHeight(_vehicleHeightMeters);
                    dimensionsChanged = true;
                }

                if (metadata.VehicleWidthMeters.HasValue && _metadataAutoFillState.ShouldReplaceVehicleWidth(_vehicleWidthMeters))
                {
                    _vehicleWidthMeters = metadata.VehicleWidthMeters.Value;
                    _metadataAutoFillState.RememberVehicleWidth(_vehicleWidthMeters);
                    dimensionsChanged = true;
                }

                if (!IsOnRubberSelected())
                {
                    if (metadata.OutriggerWidthMeters.HasValue && _metadataAutoFillState.ShouldReplaceOutriggerWidth(_outriggerWidthMeters))
                    {
                        _outriggerWidthMeters = metadata.OutriggerWidthMeters.Value;
                        _metadataAutoFillState.RememberOutriggerWidth(_outriggerWidthMeters);
                        dimensionsChanged = true;
                    }

                    if (metadata.OutriggerLengthMeters.HasValue && _metadataAutoFillState.ShouldReplaceOutriggerLength(_outriggerLengthMeters))
                    {
                        _outriggerLengthMeters = metadata.OutriggerLengthMeters.Value;
                        _metadataAutoFillState.RememberOutriggerLength(_outriggerLengthMeters);
                        dimensionsChanged = true;
                    }
                }

                if (dimensionsChanged)
                {
                    ClearOutriggerDimensionsWhenOnRubber();
                    RefreshDimensionsSummary();
                    applied.Add("Detected crane dimensions from the PDF.");
                }
            }
            finally
            {
                _isApplyingMetadataAutoFill = wasApplying;
            }

            if (metadata.Notes.Count > 0)
            {
                foreach (string note in metadata.Notes.Take(6))
                    applied.Add(note);
            }

            return applied.Distinct().ToList();
        }

        private void ClearPdfMetadataEvidence()
        {
            _lastDetectedMetadata = null;
            _lastMetadataEvidenceText = string.Empty;
            _lastMetadataEvidenceSummary = string.Empty;
            MetadataEvidencePanel.Visibility = Visibility.Collapsed;
            TxtMetadataEvidence.Text = string.Empty;
            TxtMetadataEvidenceSummary.Text = string.Empty;
            BtnViewFullMetadataEvidence.IsEnabled = false;
            TxtModel.ToolTip = null;
            TxtMfg.ToolTip = null;
            TxtCounterweight.ToolTip = null;
            TxtDimensionsSummary.ToolTip = null;
        }
        private void UpdatePdfMetadataEvidence(LoadChartPdfMetadataDto? metadata, IReadOnlyList<string> appliedNotes)
        {
            IEnumerable<string> safeAppliedNotes = appliedNotes == null ? Array.Empty<string>() : appliedNotes;
            if (metadata == null)
            {
                ClearPdfMetadataEvidence();
                return;
            }

            IEnumerable<string> metadataNotes = metadata.Notes == null ? Array.Empty<string>() : metadata.Notes;
            if (!metadata.HasAnyValue && !metadataNotes.Any() && !safeAppliedNotes.Any())
            {
                ClearPdfMetadataEvidence();
                return;
            }

            _lastDetectedMetadata = metadata;

            _lastMetadataEvidenceText = BuildProfessionalMetadataEvidence(metadata, safeAppliedNotes);
            _lastMetadataEvidenceSummary = BuildMetadataEvidenceSummary(metadata);
            TxtMetadataEvidenceSummary.Text = _lastMetadataEvidenceSummary;
            TxtMetadataEvidence.Text = _lastMetadataEvidenceText;
            MetadataEvidencePanel.Visibility = Visibility.Visible;
            BtnViewFullMetadataEvidence.IsEnabled = !string.IsNullOrWhiteSpace(_lastMetadataEvidenceText);

            TxtModel.ToolTip = BuildEvidenceToolTip("Model", metadata.ModelSource);
            TxtMfg.ToolTip = BuildEvidenceToolTip("Manufacturer", metadata.ManufacturerSource);
            TxtCounterweight.ToolTip = BuildEvidenceToolTip("Counterweight", metadata.CounterweightSource);
            TxtDimensionsSummary.ToolTip = BuildDimensionsEvidenceToolTip(metadata);
        }

        private double? ConvertLengthForDisplay(double? meters)
        {
            return meters.HasValue
                ? UnitSystem.FromMeters(meters.Value, _unitSettings.DisplayLengthUnit)
                : null;
        }

        private double? ConvertMassForDisplay(double? tons)
        {
            return tons.HasValue
                ? UnitSystem.FromTons(tons.Value, _unitSettings.DisplayMassUnit)
                : null;
        }

        private string BuildProfessionalMetadataEvidence(LoadChartPdfMetadataDto metadata, IEnumerable<string> appliedNotes)
        {
            var lines = new List<string>
            {
                "PDF Metadata Details",
                "",
                "Status",
                "- " + BuildMetadataEvidenceSummary(metadata),
                "",
                "Detected values"
            };

            AddProfessionalTextValue(lines, "Model", metadata.Model, metadata.ModelSource, "model not detected");
            AddProfessionalTextValue(lines, "Manufacturer", metadata.Manufacturer, metadata.ManufacturerSource, "manufacturer not detected");
            AddProfessionalNumberValue(lines, "Counterweight", ConvertMassForDisplay(metadata.CounterweightTons), UnitSystem.MassSymbol(_unitSettings.DisplayMassUnit), metadata.CounterweightSource, "counterweight not detected");
            AddProfessionalNumberValue(lines, "Transport length", ConvertLengthForDisplay(metadata.VehicleLengthMeters), UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit), metadata.VehicleLengthSource, "transport length not detected");
            AddProfessionalNumberValue(lines, "Transport height", ConvertLengthForDisplay(metadata.VehicleHeightMeters), UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit), metadata.VehicleHeightSource, "transport height not detected");
            AddProfessionalNumberValue(lines, "Travel width", ConvertLengthForDisplay(metadata.VehicleWidthMeters), UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit), metadata.VehicleWidthSource, "travel width not detected");
            AddProfessionalNumberValue(lines, "Outrigger pad-to-pad", ConvertLengthForDisplay(metadata.OutriggerWidthMeters), UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit), metadata.OutriggerWidthSource, "outrigger pad-to-pad span not detected");
            AddProfessionalNumberValue(lines, "Outrigger front-to-rear", ConvertLengthForDisplay(metadata.OutriggerLengthMeters), UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit), metadata.OutriggerLengthSource, "outrigger front-to-rear span not detected");

            var reviewNotes = metadata.Notes
                .Concat(appliedNotes ?? Array.Empty<string>())
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Select(SanitizeEvidenceDisplayText)
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            if (reviewNotes.Count > 0)
            {
                lines.Add("");
                lines.Add("Review notes");
                foreach (string note in reviewNotes)
                    lines.Add("- " + note);
            }

            return SanitizeEvidenceDisplayText(string.Join(Environment.NewLine, lines));
        }

        private string BuildMetadataEvidenceSummary(LoadChartPdfMetadataDto metadata)
        {
            int detectedCount = new[]
            {
                !string.IsNullOrWhiteSpace(metadata.Model),
                !string.IsNullOrWhiteSpace(metadata.Manufacturer),
                metadata.CounterweightTons.HasValue,
                metadata.VehicleLengthMeters.HasValue,
                metadata.VehicleHeightMeters.HasValue,
                metadata.VehicleWidthMeters.HasValue,
                metadata.OutriggerWidthMeters.HasValue,
                metadata.OutriggerLengthMeters.HasValue
            }.Count(value => value);

            string confidence = detectedCount >= 6 ? "High confidence" : detectedCount > 0 ? "Review recommended" : "No metadata detected";
            return $"{confidence} - {detectedCount} fields auto-filled.";
        }

        private static void AddProfessionalTextValue(List<string> lines, string label, string value, string source, string missingReason)
        {
            string displayValue = string.IsNullOrWhiteSpace(value) ? "Missing" : SanitizeEvidenceDisplayText(value.Trim());
            lines.Add($"- {label}: {displayValue}");
            lines.Add($"  Evidence: {BuildCleanSourceSummary(source, missingReason)}");
        }

        private static void AddProfessionalNumberValue(List<string> lines, string label, double? value, string unit, string source, string missingReason)
        {
            string displayValue = value.HasValue
                ? FormatEvidenceNumber(value.Value) + " " + unit
                : "Missing";

            if (value.HasValue && SourceLooksInferred(source) && !displayValue.Contains("inferred", StringComparison.OrdinalIgnoreCase))
                displayValue += " (inferred)";

            lines.Add($"- {label}: {displayValue}");
            lines.Add($"  Evidence: {BuildCleanSourceSummary(source, missingReason)}");
        }

        private static string BuildSourceText(string source, string missingReason)
        {
            return BuildCleanSourceSummary(source, missingReason);
        }

        private static string BuildCleanSourceSummary(string source, string missingReason)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "Missing PDF evidence - " + missingReason;

            string clean = SanitizeEvidenceDisplayText(source);
            var parts = new List<string>();

            string confidence = "PDF evidence";
            if (clean.Contains("confidence high", StringComparison.OrdinalIgnoreCase))
                confidence = "High confidence";
            else if (clean.Contains("confidence medium", StringComparison.OrdinalIgnoreCase))
                confidence = "Medium confidence";
            else if (clean.Contains("confidence low", StringComparison.OrdinalIgnoreCase))
                confidence = "Low confidence";

            parts.Add(confidence);

            var pages = Regex.Matches(clean, @"Page\s+(\d+)", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (pages.Count == 1)
                parts.Add("Page " + pages[0]);
            else if (pages.Count > 1)
                parts.Add("Pages " + string.Join(", ", pages));

            parts.Add(GetEvidenceKind(clean));

            string values = ExtractEvidenceValuesSummary(clean);
            if (!string.IsNullOrWhiteSpace(values))
                parts.Add(values);

            return string.Join(" - ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string GetEvidenceKind(string cleanSource)
        {
            if (cleanSource.Contains("transverse", StringComparison.OrdinalIgnoreCase)
                || cleanSource.Contains("pad-to-pad", StringComparison.OrdinalIgnoreCase))
                return "transverse support diagram";

            if (cleanSource.Contains("longitudinal", StringComparison.OrdinalIgnoreCase)
                || cleanSource.Contains("front-to-rear", StringComparison.OrdinalIgnoreCase)
                || cleanSource.Contains("support base", StringComparison.OrdinalIgnoreCase))
                return "front-to-rear support base";

            if (cleanSource.Contains("dimension table", StringComparison.OrdinalIgnoreCase))
                return "dimension table";

            if (cleanSource.Contains("dimension diagram", StringComparison.OrdinalIgnoreCase)
                || cleanSource.Contains("dimensions page", StringComparison.OrdinalIgnoreCase))
                return "dimensions diagram";

            if (cleanSource.Contains("counterweight", StringComparison.OrdinalIgnoreCase)
                || cleanSource.Contains("ballast", StringComparison.OrdinalIgnoreCase))
                return "counterweight evidence";

            return "PDF text evidence";
        }

        private static string ExtractEvidenceValuesSummary(string cleanSource)
        {
            var tokens = new List<string>();

            var valuesMatch = Regex.Match(cleanSource, @"values\s+([^\.]+)", RegexOptions.IgnoreCase);
            if (valuesMatch.Success)
            {
                tokens.AddRange(Regex.Matches(valuesMatch.Groups[1].Value, @"\d+(?:\.\d+)?")
                    .Cast<Match>()
                    .Select(match => match.Value)
                    .Take(8));
            }

            var selectedMatch = Regex.Match(cleanSource, @"selected\s+(\d{3,5})\s*mm", RegexOptions.IgnoreCase);
            if (selectedMatch.Success && !tokens.Contains(selectedMatch.Groups[1].Value))
                tokens.Add(selectedMatch.Groups[1].Value);

            tokens = tokens
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            return tokens.Count > 0
                ? "values " + string.Join(" / ", tokens)
                : string.Empty;
        }

        private static string FormatEvidenceNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string SanitizeEvidenceDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string cleaned = text
                .Replace('\u00A0', ' ')
                .Replace('\uFFFD', ' ')
                .Replace("\u00C2", " ")
                .Replace("\u00C3", " ")
                .Replace("\uFFFD", " ");

            cleaned = Regex.Replace(cleaned, "[^\u0009\u000A\u000D\u0020-\u007E]", " ");
            cleaned = Regex.Replace(cleaned, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", " ");
            cleaned = Regex.Replace(cleaned, @"[^\S\r\n]+", " ");
            cleaned = Regex.Replace(cleaned, @"[ ]+(\r?\n)", "$1");

            return cleaned.Trim();
        }

        private static bool SourceLooksInferred(string source)
        {
            return !string.IsNullOrWhiteSpace(source)
                && (source.Contains("inferred", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("diagram-cluster", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("diagram cluster", StringComparison.OrdinalIgnoreCase));
        }

        private static string? BuildEvidenceToolTip(string label, string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            return label + " - " + BuildCleanSourceSummary(source, "source not available");
        }

        private static string? BuildDimensionsEvidenceToolTip(LoadChartPdfMetadataDto metadata)
        {
            var sources = new[]
            {
                metadata.VehicleLengthSource,
                metadata.VehicleHeightSource,
                metadata.VehicleWidthSource,
                metadata.OutriggerWidthSource,
                metadata.OutriggerLengthSource
            }
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => BuildCleanSourceSummary(source, "source not available"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            return sources.Count == 0
                ? null
                : "PDF dimension evidence" + Environment.NewLine + string.Join(Environment.NewLine, sources);
        }
        private void BtnViewFullMetadataEvidence_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastMetadataEvidenceText))
            {
                TxtStatus.Text = "No PDF metadata details are available for the current import.";
                return;
            }

            var dialog = new Window
            {
                Title = "PDF Metadata Details",
                Owner = this,
                Width = 820,
                Height = 640,
                MinWidth = 620,
                MinHeight = 440,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush");

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "PDF Metadata Details",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var summary = new TextBlock
            {
                Text = _lastMetadataEvidenceSummary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            summary.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(summary, 1);
            root.Children.Add(summary);

            var evidenceBox = new TextBox
            {
                Text = _lastMetadataEvidenceText,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = TxtMetadataEvidence.FontFamily,
                FontSize = 13,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            evidenceBox.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
            evidenceBox.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetRow(evidenceBox, 2);
            root.Children.Add(evidenceBox);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var copyButton = new Button
            {
                Content = "Copy details",
                Width = 120,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            if (TryFindResource("ButtonPrimary") is Style copyStyle)
                copyButton.Style = copyStyle;
            copyButton.Click += (_, _) =>
            {
                Clipboard.SetText(_lastMetadataEvidenceText);
                TxtStatus.Text = "PDF metadata details copied to clipboard.";
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 90,
                Height = 32
            };
            if (TryFindResource("ButtonSecondary") is Style closeStyle)
                closeButton.Style = closeStyle;
            closeButton.Click += (_, _) => dialog.Close();

            footer.Children.Add(copyButton);
            footer.Children.Add(closeButton);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            dialog.Content = root;
            dialog.ShowDialog();
        }

        private void BtnCopyMetadataEvidence_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastMetadataEvidenceSummary))
            {
                TxtStatus.Text = "No PDF metadata summary is available to copy.";
                return;
            }

            Clipboard.SetText(_lastMetadataEvidenceSummary);
            TxtStatus.Text = "PDF metadata summary copied to clipboard.";
        }
        private void BtnToggleDimensions_Click(object sender, RoutedEventArgs e)
        {
            double previousVehicleLengthMeters = _vehicleLengthMeters;
            double previousVehicleHeightMeters = _vehicleHeightMeters;
            double previousVehicleWidthMeters = _vehicleWidthMeters;
            double? previousOutriggerWidthMeters = _outriggerWidthMeters;
            double? previousOutriggerLengthMeters = _outriggerLengthMeters;

            var dialog = new CraneDimensionsDialog(
                _vehicleLengthMeters,
                _vehicleHeightMeters,
                _vehicleWidthMeters,
                _outriggerWidthMeters,
                _outriggerLengthMeters,
                IsOnRubberSelected(),
                _lastDetectedMetadata)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            _metadataAutoFillState.ClearChangedDimensionOwnership(
                previousVehicleLengthMeters,
                previousVehicleHeightMeters,
                previousVehicleWidthMeters,
                previousOutriggerWidthMeters,
                previousOutriggerLengthMeters,
                dialog.VehicleLengthMeters,
                dialog.VehicleHeightMeters,
                dialog.VehicleWidthMeters,
                dialog.OutriggerWidthMeters,
                dialog.OutriggerLengthMeters);

            _vehicleLengthMeters = dialog.VehicleLengthMeters;
            _vehicleHeightMeters = dialog.VehicleHeightMeters;
            _vehicleWidthMeters = dialog.VehicleWidthMeters;
            _outriggerWidthMeters = dialog.OutriggerWidthMeters;
            _outriggerLengthMeters = dialog.OutriggerLengthMeters;
            RefreshDimensionsSummary();
            TxtStatus.Text = "Crane dimensions updated.";
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtModel.Text))
                {
                    ShowRequiredFieldError("Model name is required.", TxtModel);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TxtMfg.Text))
                {
                    ShowRequiredFieldError("Manufacturer is required.", TxtMfg);
                    return;
                }

                if (!TryParseRequiredCounterweight(out double counterweightTons))
                    return;

                if (!HasRequiredDimensions(out string dimensionError))
                {
                    ShowRequiredFieldError(dimensionError, BtnToggleDimensions);
                    return;
                }

                if (ChartGrid.ItemsSource is not DataView dataView || dataView.Table == null)
                {
                    TxtStatus.Text = "Load chart data is required.";
                    return;
                }

                if (_nominalChart == null || !_nominalChart.IsCapacityUnitResolved)
                {
                    string message =
                        "The load-chart capacity unit is unresolved. Select kg or t before saving. " +
                        "Auto is allowed only when the source contains unambiguous explicit unit evidence.";
                    TxtStatus.Text = message;
                    CboChartCapacityUnit.Focus();
                    MessageBox.Show(this, message, "Capacity Unit Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ParsedLoadChart chartForSave;

                if (_nominalChart?.IsExactVisualChart == true)
                {
                    if (_isShowingExactVisualPreview)
                    {
                        if (!CaptureExactVisualEdits())
                            return;
                    }

                    if (!ConfirmExactVisualSaveForCurrentChart())
                        return;

                    chartForSave = CopyChartWith(_nominalChart);
                }
                else
                {
                    CommitGridEdits();
                    chartForSave = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders, GetSelectedCapacityUnitMode());
                    _nominalChart = chartForSave;
                }

                if (!chartForSave.IsCapacityUnitResolved)
                {
                    throw new InvalidOperationException(
                        "The load-chart capacity unit is unresolved. Select kg or t before saving the crane.");
                }

                if (TryParseOptionalNumber(TxtMaxCap.Text, "Imported Chart Max", out double manualMaxCapacity))
                    chartForSave = CopyChartWith(chartForSave, maxCapacity: UnitSystem.ToTons(manualMaxCapacity, _unitSettings.DisplayMassUnit));

                if (TryParseOptionalNumber(TxtMaxRadius.Text, "Max Radius", out double manualMaxRadius))
                    chartForSave = CopyChartWith(chartForSave, maxRadius: UnitSystem.ToMeters(manualMaxRadius, _unitSettings.DisplayLengthUnit));

                var crane = LoadChartCatalogService.BuildCraneDto(
                    TxtModel.Text.Trim(),
                    TxtMfg.Text.Trim(),
                    GetSelectedOutriggerConfig(),
                    counterweightTons,
                    _vehicleLengthMeters,
                    _vehicleHeightMeters,
                    _vehicleWidthMeters,
                    IsOnRubberSelected() ? null : _outriggerWidthMeters,
                    IsOnRubberSelected() ? null : _outriggerLengthMeters,
                    chartForSave,
                    _existingCrane?.Id ?? 0);

                if (_existingCrane == null)
                    await _catalogService.CreateAsync(crane);
                else
                    await _catalogService.UpdateAsync(crane);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private LoadChartCapacityUnitMode GetSelectedCapacityUnitMode()
        {
            if (CboChartCapacityUnit.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString() ?? string.Empty;
                if (Enum.TryParse(tag, ignoreCase: true, out LoadChartCapacityUnitMode mode))
                    return mode;
            }

            return LoadChartCapacityUnitMode.Auto;
        }

        private void SetCapacityUnitMode(LoadChartCapacityUnitMode mode, bool suppressRefresh = false)
        {
            _suppressCapacityUnitRefresh = suppressRefresh;
            try
            {
                foreach (ComboBoxItem item in CboChartCapacityUnit.Items)
                {
                    string tag = item.Tag?.ToString() ?? string.Empty;
                    if (Enum.TryParse(tag, ignoreCase: true, out LoadChartCapacityUnitMode itemMode) && itemMode == mode)
                    {
                        CboChartCapacityUnit.SelectedItem = item;
                        return;
                    }
                }

                CboChartCapacityUnit.SelectedIndex = 0;
            }
            finally
            {
                _suppressCapacityUnitRefresh = false;
            }
        }

        private void CboChartCapacityUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCapacityUnitRefresh || _nominalChart == null)
                return;

            ReparseCurrentChartWithSelectedCapacityUnit();
        }

        private void ReparseCurrentChartWithSelectedCapacityUnit()
        {
            if (_nominalChart == null)
                return;

            try
            {
                var capacityUnitMode = GetSelectedCapacityUnitMode();

                if (_nominalChart.IsExactVisualChart)
                {
                    var reparsed = LoadChartCsvService.ParseExactVisualTable(
                        _nominalChart.PreviewTable,
                        _nominalChart.PreviewHeaders,
                        _nominalChart.ExactLoadChartRawGridJson,
                        _nominalChart.LoadChartSourceDescription,
                        capacityUnitMode);

                    _nominalChart = reparsed;
                    _exactVisualPreviewTable = reparsed.PreviewTable;
                    _exactVisualPreviewHeaders = reparsed.PreviewHeaders.ToList();

                    if (_importableEnvelopeChart != null)
                    {
                        _importableEnvelopeChart = LoadChartCsvService.ParseTable(
                            _importableEnvelopeChart.PreviewTable,
                            _importableEnvelopeChart.PreviewHeaders,
                            capacityUnitMode);
                    }

                    ShowExactVisualPreview();
                }
                else
                {
                    CommitGridEdits();

                    if (ChartGrid.ItemsSource is not DataView dataView || dataView.Table == null)
                        return;

                    var reparsed = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders, capacityUnitMode);
                    SetNominalChart(reparsed);
                }

                TxtStatus.Text = $"{LoadChartCsvService.GetCapacityUnitSummary(_nominalChart)} Saved chart capacities are stored internally in tons.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Capacity Unit Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private string GetSelectedOutriggerConfig()
        {
            return CboOutriggerConfig.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? "100%"
                : "100%";
        }

        private void CboOutriggerConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            _metadataAutoFillState.ClearOutriggerOwnership();
            ClearOutriggerDimensionsWhenOnRubber();
            RefreshDimensionsSummary();
        }

        private bool IsOnRubberSelected()
        {
            return LoadChartCraneValidator.IsOnRubber(GetSelectedOutriggerConfig());
        }

        private void ClearOutriggerDimensionsWhenOnRubber()
        {
            if (!IsOnRubberSelected())
                return;

            _outriggerWidthMeters = null;
            _outriggerLengthMeters = null;
        }

        private bool TryParseRequiredCounterweight(out double counterweightTons)
        {
            counterweightTons = 0d;

            if (string.IsNullOrWhiteSpace(TxtCounterweight.Text))
            {
                ShowRequiredFieldError("Counterweight is required.", TxtCounterweight);
                return false;
            }

            if (!LoadChartCsvService.TryParseUserEnteredNumber(TxtCounterweight.Text, out double displayValue)
                || !double.IsFinite(displayValue)
                || displayValue < 0d)
            {
                ShowRequiredFieldError("Counterweight must be a valid non-negative value.", TxtCounterweight);
                return false;
            }

            counterweightTons = UnitSystem.ToTons(displayValue, _unitSettings.DisplayMassUnit);
            return true;
        }

        private bool HasRequiredDimensions(out string error)
        {
            if (_vehicleLengthMeters <= 0d || _vehicleWidthMeters <= 0d || _vehicleHeightMeters <= 0d)
            {
                error = "Total length, travel width, and transport height are required.";
                return false;
            }

            if (!IsOnRubberSelected()
                && (!_outriggerWidthMeters.HasValue || _outriggerWidthMeters.Value <= 0d
                    || !_outriggerLengthMeters.HasValue || _outriggerLengthMeters.Value <= 0d))
            {
                error = "Both outrigger dimensions are required unless the crane operates On Rubber.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void ShowRequiredFieldError(string message, Control control)
        {
            TxtStatus.Text = message;
            control.Focus();
            MessageBox.Show(this, message, "Required Crane Information", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SelectOutriggerConfig(string? value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? "100%"
                : value.Trim();

            foreach (ComboBoxItem item in CboOutriggerConfig.Items)
            {
                if (string.Equals(item.Content?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    CboOutriggerConfig.SelectedItem = item;
                    return;
                }
            }

            CboOutriggerConfig.SelectedIndex = 0;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateGridStats(ParsedLoadChart parsedChart)
        {
            string capacityText = parsedChart.IsCapacityUnitResolved
                ? $"normalized max {UnitSystem.FormatMass(parsedChart.MaxCapacity, _unitSettings)}"
                : $"raw max {parsedChart.RawMaxCapacity:0.###} | unit unresolved";

            TxtGridStats.Text =
                $"{parsedChart.PreviewTable.Rows.Count} radii | {parsedChart.PreviewTable.Columns.Count - 1} booms | {capacityText}";
        }

        private void RefreshDimensionsSummary()
        {
            var parts = new List<string>();

            if (_vehicleLengthMeters > 0 || _vehicleWidthMeters > 0 || _vehicleHeightMeters > 0)
            {
                parts.Add(
                    $"Transport L {FormatLengthForSummary(_vehicleLengthMeters)}  W {FormatLengthForSummary(_vehicleWidthMeters)}  H {FormatLengthForSummary(_vehicleHeightMeters)}");
            }

            if (IsOnRubberSelected())
            {
                parts.Add("Operating configuration: On Rubber (no outriggers)");
            }
            else if (_outriggerWidthMeters.HasValue || _outriggerLengthMeters.HasValue)
            {
                parts.Add(
                    $"Outriggers W {FormatLengthForSummary(_outriggerWidthMeters, preservePrecision: true)}  B {FormatLengthForSummary(_outriggerLengthMeters, preservePrecision: true)}");
            }

            TxtDimensionsSummary.Text = parts.Count > 0
                ? string.Join(Environment.NewLine, parts)
                : "Required crane dimensions have not been entered.";

            BtnToggleDimensions.Content = _vehicleLengthMeters > 0d && _vehicleWidthMeters > 0d && _vehicleHeightMeters > 0d
                ? "Edit Crane Dimensions"
                : "Enter Required Crane Dimensions";
        }

        private void SetNominalChart(ParsedLoadChart nominalChart)
        {
            _nominalChart = nominalChart;
            ShowChart(nominalChart);
            UpdateCapacityUnitPresentation(nominalChart);
        }

        private void ClearExactVisualPreview()
        {
            _importableEnvelopeChart = null;
            _exactVisualPreviewTable = null;
            _exactVisualPreviewHeaders = new List<string>();
            _hasExactVisualPreview = false;
            _isShowingExactVisualPreview = false;
            EnvelopePreviewPanel.Visibility = Visibility.Collapsed;
            BtnTranspose.IsEnabled = true;
        }

        private void ApplyExactVisualPreview(LoadChartImportResult importResult)
        {
            if (!importResult.HasExactVisualPreview && !importResult.Chart.IsExactVisualChart)
                return;

            if (_nominalChart == null)
                return;

            _exactVisualPreviewTable = _nominalChart.PreviewTable;
            _exactVisualPreviewHeaders = _nominalChart.PreviewHeaders.ToList();
            _hasExactVisualPreview = true;

            TxtEnvelopePreviewTitle.Text = string.IsNullOrWhiteSpace(importResult.ExactVisualPreviewTitle)
                ? "Exact PDF Visual Load Chart"
                : importResult.ExactVisualPreviewTitle;

            TxtEnvelopePreviewDescription.Text = string.IsNullOrWhiteSpace(importResult.ExactVisualPreviewDescription)
                ? "Save Crane will save the exact PDF visual chart. Duplicate numeric boom columns are preserved with visual metadata; optimization uses the conservative minimum capacity across duplicates."
                : importResult.ExactVisualPreviewDescription;

            EnvelopePreviewPanel.Visibility = Visibility.Visible;
            BtnShowImportableEnvelope.IsEnabled = _importableEnvelopeChart != null;
            ShowExactVisualPreview();
        }

        private void BtnShowExactPdfPreview_Click(object sender, RoutedEventArgs e)
        {
            ShowExactVisualPreview();
        }

        private void BtnShowImportableEnvelope_Click(object sender, RoutedEventArgs e)
        {
            ShowImportableEnvelopePreview();
        }

        private void ShowExactVisualPreview()
        {
            if (_nominalChart == null)
                return;

            _gridHeaders = _nominalChart.PreviewHeaders.ToList();
            ConfigureChartGrid(_nominalChart.PreviewTable, _gridHeaders, previewOnly: false);
            ChartGrid.ItemsSource = _nominalChart.PreviewTable.DefaultView;
            UpdateGridStats(_nominalChart);

            _isShowingExactVisualPreview = _nominalChart.IsExactVisualChart;
            BtnTranspose.IsEnabled = !_nominalChart.IsExactVisualChart;
            BtnShowExactPdfPreview.IsEnabled = false;
            BtnShowImportableEnvelope.IsEnabled = _importableEnvelopeChart != null;

            if (_nominalChart.IsExactVisualChart)
            {
                int visualColumns = Math.Max(0, _nominalChart.PreviewTable.Columns.Count - 1);
                string saveState = _nominalChart.IsCapacityUnitResolved ? "saveable" : "capacity unit required";
                TxtGridStats.Text = $"{_nominalChart.PreviewTable.Rows.Count} radii | {visualColumns} exact PDF visual columns | {saveState}";
                TxtPreviewMode.Text = _nominalChart.IsCapacityUnitResolved
                    ? "Viewing exact PDF visual load chart. Save Crane will preserve these repeated manufacturer columns and use conservative duplicate-column capacity lookup."
                    : "Viewing exact PDF visual load chart. Select the capacity unit before Save Crane is allowed.";
                TxtStatus.Text = _nominalChart.IsCapacityUnitResolved
                    ? "Exact PDF visual load chart shown. Save Crane will save this exact visual chart."
                    : LoadChartCsvService.GetCapacityUnitSummary(_nominalChart);
            }

            UpdateCapacityUnitPresentation(_nominalChart);
        }

        private void ShowImportableEnvelopePreview()
        {
            if (_importableEnvelopeChart == null)
                return;

            if (_nominalChart?.IsExactVisualChart == true && _isShowingExactVisualPreview && !CaptureExactVisualEdits())
                return;

            ConfigureChartGrid(_importableEnvelopeChart.PreviewTable, _importableEnvelopeChart.PreviewHeaders, previewOnly: true);
            ChartGrid.ItemsSource = _importableEnvelopeChart.PreviewTable.DefaultView;
            UpdateGridStats(_importableEnvelopeChart);

            _isShowingExactVisualPreview = false;
            BtnTranspose.IsEnabled = false;
            BtnShowExactPdfPreview.IsEnabled = true;
            BtnShowImportableEnvelope.IsEnabled = false;

            TxtPreviewMode.Text = "Viewing reference 2D envelope only. This is not what Save Crane will save for this exact PDF visual import.";
            TxtStatus.Text = _nominalChart?.IsCapacityUnitResolved == true
                ? "Reference envelope shown. Save Crane will still save the exact PDF visual chart."
                : "Reference envelope shown. Select kg or t before Save Crane is allowed.";
        }

        private bool CaptureExactVisualEdits()
        {
            if (_nominalChart?.IsExactVisualChart != true)
                return true;

            if (ChartGrid.ItemsSource is not DataView dataView || dataView.Table == null)
                return true;

            try
            {
                CommitGridEdits();
                _nominalChart = LoadChartCsvService.ParseExactVisualTable(
                    dataView.Table,
                    _gridHeaders,
                    _nominalChart.ExactLoadChartRawGridJson,
                    _nominalChart.LoadChartSourceDescription,
                    GetSelectedCapacityUnitMode());
                _gridHeaders = _nominalChart.PreviewHeaders.ToList();
                return true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Exact PDF Visual Chart Has Invalid Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private bool ConfirmExactVisualSaveForCurrentChart()
        {
            string message =
                "This crane will be saved as an exact PDF visual load chart." +
                Environment.NewLine + Environment.NewLine +
                "Repeated visual boom columns will be preserved as separate saved capacity points with visual column metadata." +
                Environment.NewLine + Environment.NewLine +
                "For optimization, when repeated columns share the same radius and numeric boom length, the app will use the conservative minimum capacity across those duplicate columns." +
                Environment.NewLine + Environment.NewLine +
                "Continue saving this exact visual chart?";

            return MessageBox.Show(
                this,
                message,
                "Confirm Saving Exact PDF Visual Chart",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes) == MessageBoxResult.Yes;
        }

        private static DataTable BuildSafeRawPreviewTable(
            IReadOnlyList<List<string>> rawGrid,
            List<string> previewHeaders)
        {
            var table = new DataTable();

            if (rawGrid.Count == 0)
                return table;

            int columnCount = rawGrid.Max(row => row.Count);
            var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string rawHeader = columnIndex < rawGrid[0].Count
                    ? rawGrid[0][columnIndex]
                    : string.Empty;

                string header = string.IsNullOrWhiteSpace(rawHeader)
                    ? columnIndex == 0 ? "Radius (m)" : $"Column {columnIndex + 1}"
                    : rawHeader.Trim();

                string uniqueHeader = header;
                int duplicateIndex = 2;

                while (!usedHeaders.Add(uniqueHeader))
                {
                    uniqueHeader = $"{header} ({duplicateIndex})";
                    duplicateIndex++;
                }

                previewHeaders.Add(uniqueHeader);
                table.Columns.Add($"C{columnIndex}", typeof(string));
            }

            foreach (var rawRow in rawGrid.Skip(1))
            {
                var row = table.NewRow();

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    row[columnIndex] = FormatRawGridCellForUi(columnIndex, columnIndex < rawRow.Count ? rawRow[columnIndex] : string.Empty);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static string FormatCapacityForRawGrid(double? capacity)
        {
            double value = capacity.GetValueOrDefault();
            return value > 0d
                ? value.ToString("G", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string FormatRawGridCellForUi(int columnIndex, string? value)
        {
            string text = value?.Trim() ?? string.Empty;
            if (columnIndex == 0)
                return text;

            return LoadChartCsvService.TryParseFlexibleNumber(text, out double numericValue) && numericValue <= 0d
                ? string.Empty
                : text;
        }
        private void CommitGridEdits()
        {
            ChartGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ChartGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void ShowChart(ParsedLoadChart chart)
        {
            _gridHeaders = chart.PreviewHeaders.ToList();
            _isShowingExactVisualPreview = false;
            BtnTranspose.IsEnabled = !_hasExactVisualPreview;
            TxtMaxCap.Text = chart.IsCapacityUnitResolved
                ? FormatMassInput(chart.MaxCapacity)
                : string.Empty;

            ConfigureChartGrid(chart.PreviewTable, _gridHeaders);
            ChartGrid.ItemsSource = chart.PreviewTable.DefaultView;
            UpdateGridStats(chart);
        }

        private void UpdateCapacityUnitPresentation(ParsedLoadChart? chart)
        {
            if (chart == null)
            {
                TxtCapacityUnitResolution.Text = "Load a chart to resolve the capacity unit.";
                TxtRawCapacityMax.Text = "Raw maximum: -";
                TxtNormalizedCapacityMax.Text = "Normalized maximum: -";
                BtnSaveCrane.IsEnabled = false;
                return;
            }

            TxtCapacityUnitResolution.Text = LoadChartCsvService.GetCapacityUnitSummary(chart);
            TxtRawCapacityMax.Text = chart.EffectiveCapacityUnit.HasValue
                ? $"Raw maximum: {chart.RawMaxCapacity:0.###} {UnitSystem.MassSymbol(chart.EffectiveCapacityUnit.Value)}"
                : $"Raw maximum: {chart.RawMaxCapacity:0.###} (unit unresolved)";
            TxtNormalizedCapacityMax.Text = chart.IsCapacityUnitResolved
                ? $"Normalized maximum: {chart.MaxCapacity:0.###} t"
                : "Normalized maximum: - (select capacity unit)";
            TxtMaxCap.Text = chart.IsCapacityUnitResolved
                ? FormatMassInput(chart.MaxCapacity)
                : string.Empty;
            BtnSaveCrane.IsEnabled = chart.IsCapacityUnitResolved;
        }

        private static ParsedLoadChart CopyChartWith(
            ParsedLoadChart source,
            DataTable? previewTable = null,
            List<CraneOptimizer.Domain.Entities.TargetPoint>? points = null,
            double? maxCapacity = null,
            double? maxRadius = null,
            string? exactLoadChartRawGridJson = null)
        {
            return new ParsedLoadChart
            {
                PreviewTable = previewTable ?? source.PreviewTable.Copy(),
                PreviewHeaders = source.PreviewHeaders.ToList(),
                Points = points ?? source.Points
                    .Select(point => new CraneOptimizer.Domain.Entities.TargetPoint
                    {
                        Radius = point.Radius,
                        BoomLength = point.BoomLength,
                        Capacity = point.Capacity,
                        VisualColumnIndex = point.VisualColumnIndex,
                        VisualColumnHeader = point.VisualColumnHeader,
                        NumberOfLines = point.NumberOfLines,
                        OperatingConditionLabel = point.OperatingConditionLabel
                    })
                    .ToList(),
                MinRadius = source.MinRadius,
                MaxRadius = maxRadius ?? source.MaxRadius,
                MinBoomLength = source.MinBoomLength,
                MaxBoomLength = source.MaxBoomLength,
                MaxCapacity = maxCapacity ?? source.MaxCapacity,
                RawMaxCapacity = source.RawMaxCapacity,
                EffectiveCapacityUnit = source.EffectiveCapacityUnit,
                CapacityUnitResolutionStatus = source.CapacityUnitResolutionStatus,
                RequestedCapacityUnitMode = source.RequestedCapacityUnitMode,
                LoadChartStorageMode = source.LoadChartStorageMode,
                LoadChartCapacityPolicy = source.LoadChartCapacityPolicy,
                ExactLoadChartRawGridJson = exactLoadChartRawGridJson ?? source.ExactLoadChartRawGridJson,
                LoadChartSourceDescription = source.LoadChartSourceDescription,
                VisualSummary = source.VisualSummary,
                VisualRows = source.VisualRows.ToList()
            };
        }

        private void ApplyUnitLabels()
        {
            LblCounterweight.Text = $"Counterweight * ({UnitSystem.MassSymbol(_unitSettings.DisplayMassUnit)})";
            LblMaxCapacity.Text = $"Imported Chart Max ({UnitSystem.MassSymbol(_unitSettings.DisplayMassUnit)})";
            LblMaxRadius.Text = $"Max Radius ({UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit)})";
        }

        private void ConfigureChartGrid(DataTable table, IReadOnlyList<string> headers, bool previewOnly = false)
        {
            ChartGrid.AutoGenerateColumns = false;
            ChartGrid.Columns.Clear();
            ChartGrid.IsReadOnly = previewOnly;
            ChartGrid.CanUserAddRows = !previewOnly;
            ChartGrid.CanUserDeleteRows = !previewOnly;

            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                string header = columnIndex < headers.Count
                    ? headers[columnIndex]
                    : table.Columns[columnIndex].ColumnName;

                ChartGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding($"[{table.Columns[columnIndex].ColumnName}]")
                    {
                        Mode = previewOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = columnIndex == 0 ? new DataGridLength(88) : new DataGridLength(58),
                    MinWidth = columnIndex == 0 ? 76 : 52
                });
            }
        }

        private static double ParseNumberOrZero(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0d;
            }

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out double value))
                return value;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private static bool TryParseOptionalNumber(string text, string fieldName, out double value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0d;
                return false;
            }

            if (LoadChartCsvService.TryParseUserEnteredNumber(text, out value))
                return true;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private string FormatLengthForSummary(double? value, bool preservePrecision = false)
        {
            if (!value.HasValue || value.Value <= 0)
                return "-";

            double displayValue = UnitSystem.FromMeters(value.Value, _unitSettings.DisplayLengthUnit);
            string format = preservePrecision ? "0.###" : "0.##";
            return displayValue.ToString(format, CultureInfo.InvariantCulture) + " " + UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit);
        }

        private string FormatLength(double? value)
        {
            return value.HasValue && value.Value > 0
                ? UnitSystem.FormatLength(value.Value, _unitSettings, CultureInfo.InvariantCulture)
                : "-";
        }
        private string FormatLengthInput(double value)
        {
            return UnitSystem.FromMeters(value, _unitSettings.DisplayLengthUnit)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }

        private string FormatMassInput(double value)
        {
            return UnitSystem.FromTons(value, _unitSettings.DisplayMassUnit)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
