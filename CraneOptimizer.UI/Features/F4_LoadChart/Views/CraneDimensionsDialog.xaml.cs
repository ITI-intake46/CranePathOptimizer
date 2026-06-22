using System;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneDimensionsDialog : Window
    {
        public double VehicleLengthMeters { get; private set; }
        public double VehicleHeightMeters { get; private set; }
        public double VehicleWidthMeters { get; private set; }
        public double? OutriggerWidthMeters { get; private set; }
        public double? OutriggerLengthMeters { get; private set; }
        private readonly UnitSettings _unitSettings;
        private readonly bool _isOnRubber;
        private readonly LoadChartPdfMetadataDto? _metadata;
        private string _metadataEvidenceText = string.Empty;
        private string _metadataEvidenceSummary = string.Empty;

        public CraneDimensionsDialog(
            double vehicleLengthMeters,
            double vehicleHeightMeters,
            double vehicleWidthMeters,
            double? outriggerWidthMeters,
            double? outriggerLengthMeters,
            bool isOnRubber,
            LoadChartPdfMetadataDto? metadata = null)
        {
            InitializeComponent();
            _unitSettings = App.ServiceProvider.GetService(typeof(IUnitSettingsService)) is IUnitSettingsService unitSettingsService
                ? unitSettingsService.Current
                : UnitSettings.Default;
            _isOnRubber = isOnRubber;
            _metadata = metadata;

            VehicleLengthMeters = vehicleLengthMeters;
            VehicleHeightMeters = vehicleHeightMeters;
            VehicleWidthMeters = vehicleWidthMeters;
            OutriggerWidthMeters = outriggerWidthMeters;
            OutriggerLengthMeters = outriggerLengthMeters;

            ApplyUnitLabels();
            TxtVehicleLength.Text = FormatLengthInput(vehicleLengthMeters);
            TxtVehicleHeight.Text = FormatLengthInput(vehicleHeightMeters);
            TxtVehicleWidth.Text = FormatLengthInput(vehicleWidthMeters);
            TxtOutriggerWidth.Text = FormatLengthInput(outriggerWidthMeters);
            TxtOutriggerLength.Text = FormatLengthInput(outriggerLengthMeters);

            OutriggerFieldsPanel.Visibility = _isOnRubber ? Visibility.Collapsed : Visibility.Visible;
            OnRubberNotice.Visibility = _isOnRubber ? Visibility.Visible : Visibility.Collapsed;
            ApplyMetadataFieldHints(metadata);
        }

        private TextBlock? HintVehicleLengthBlock => FindName("HintVehicleLength") as TextBlock;
        private TextBlock? HintVehicleHeightBlock => FindName("HintVehicleHeight") as TextBlock;
        private TextBlock? HintVehicleWidthBlock => FindName("HintVehicleWidth") as TextBlock;
        private TextBlock? HintOutriggerWidthBlock => FindName("HintOutriggerWidth") as TextBlock;
        private TextBlock? HintOutriggerLengthBlock => FindName("HintOutriggerLength") as TextBlock;
        private FrameworkElement? OutriggerMetadataNoticeElement => FindName("OutriggerMetadataNotice") as FrameworkElement;
        private TextBlock? TxtOutriggerMetadataNoticeBlock => FindName("TxtOutriggerMetadataNotice") as TextBlock;

        private void ApplyMetadataFieldHints(LoadChartPdfMetadataDto? metadata)
        {
            UpdateMetadataSummaryPanel(metadata);
            if (metadata == null)
            {
                CollapseHint(HintVehicleLengthBlock);
                CollapseHint(HintVehicleHeightBlock);
                CollapseHint(HintVehicleWidthBlock);
                CollapseHint(HintOutriggerWidthBlock);
                CollapseHint(HintOutriggerLengthBlock);
                var emptyNotice = OutriggerMetadataNoticeElement;
                if (emptyNotice != null)
                    emptyNotice.Visibility = Visibility.Collapsed;
                return;
            }

            SetDimensionHint(
                LblVehicleLength,
                HintVehicleLengthBlock,
                metadata.VehicleLengthMeters,
                metadata.VehicleLengthSource,
                "No high-confidence transport length was found in readable PDF text/layout; enter it manually if the PDF diagram is ambiguous.");
            SetDimensionHint(
                LblVehicleHeight,
                HintVehicleHeightBlock,
                metadata.VehicleHeightMeters,
                metadata.VehicleHeightSource,
                "No high-confidence transport height was found in readable PDF text/layout; enter it manually if the PDF diagram is ambiguous.");
            SetDimensionHint(
                LblVehicleWidth,
                HintVehicleWidthBlock,
                metadata.VehicleWidthMeters,
                metadata.VehicleWidthSource,
                "No high-confidence travel width was found in readable PDF text/layout; enter it manually if the PDF diagram is ambiguous.");

            if (_isOnRubber)
            {
                CollapseHint(HintOutriggerWidthBlock);
                CollapseHint(HintOutriggerLengthBlock);
                var notice = OutriggerMetadataNoticeElement;
                if (notice != null)
                    notice.Visibility = Visibility.Collapsed;
                return;
            }

            SetOutriggerMetadataNotice(metadata);

            SetDimensionHint(
                LblOutriggerWidth,
                HintOutriggerWidthBlock,
                metadata.OutriggerWidthMeters,
                metadata.OutriggerWidthSource,
                "No high-confidence outrigger pad-to-pad span was found in readable PDF text/layout; inspect the PDF support diagram or enter it manually.");
            SetDimensionHint(
                LblOutriggerLength,
                HintOutriggerLengthBlock,
                metadata.OutriggerLengthMeters,
                metadata.OutriggerLengthSource,
                "No high-confidence outrigger front-to-rear span was found in readable PDF text/layout; inspect the PDF support diagram or enter it manually.");
        }

        private static void SetDimensionHint(TextBlock label, TextBlock? target, double? value, string source, string missingReason)
        {
            if (target != null)
                target.Visibility = Visibility.Visible;

            if (value.HasValue && !string.IsNullOrWhiteSpace(source))
            {
                string compact = BuildShortSourceLabel(source);
                label.ToolTip = compact;
                if (target != null)
                {
                    target.Text = compact;
                    target.ToolTip = compact;
                }

                return;
            }

            if (value.HasValue)
            {
                label.ToolTip = "PDF evidence - auto-detected value";
                if (target != null)
                {
                    target.Text = "PDF evidence - auto-detected";
                    target.ToolTip = null;
                }

                return;
            }

            label.ToolTip = "Review manually - " + missingReason;
            if (target != null)
            {
                target.Text = "Review manually - PDF evidence not found";
                target.ToolTip = missingReason;
            }
        }

        private void SetOutriggerMetadataNotice(LoadChartPdfMetadataDto metadata)
        {
            var notice = OutriggerMetadataNoticeElement;
            var text = TxtOutriggerMetadataNoticeBlock;

            if (notice == null || text == null)
                return;

            bool missingWidth = !metadata.OutriggerWidthMeters.HasValue;
            bool missingLength = !metadata.OutriggerLengthMeters.HasValue;

            notice.Visibility = Visibility.Visible;
            if (missingWidth || missingLength)
            {
                text.Text = "PDF evidence incomplete - review outrigger spans before saving.";
                notice.ToolTip = text.Text;
                return;
            }

            text.Text = "PDF metadata applied - high confidence. Outrigger spans were auto-filled from support-diagram evidence.";
            notice.ToolTip = text.Text;
        }

        private static string BuildShortSourceLabel(string source)
        {
            return BuildCleanSourceSummary(source, "source not available");
        }

        private void UpdateMetadataSummaryPanel(LoadChartPdfMetadataDto? metadata)
        {
            if (metadata == null || !metadata.HasAnyValue)
            {
                MetadataSummaryPanel.Visibility = Visibility.Collapsed;
                TxtMetadataSummary.Text = string.Empty;
                _metadataEvidenceText = string.Empty;
                _metadataEvidenceSummary = string.Empty;
                BtnViewMetadataEvidence.IsEnabled = false;
                return;
            }

            _metadataEvidenceSummary = BuildMetadataEvidenceSummary(metadata);
            _metadataEvidenceText = BuildProfessionalMetadataEvidence(metadata);
            TxtMetadataSummary.Text = _metadataEvidenceSummary;
            MetadataSummaryPanel.Visibility = Visibility.Visible;
            BtnViewMetadataEvidence.IsEnabled = true;
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
            return $"{confidence} - {detectedCount} fields auto-filled from PDF evidence.";
        }

        private string BuildProfessionalMetadataEvidence(LoadChartPdfMetadataDto metadata)
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

            AddTextValue(lines, "Model", metadata.Model, metadata.ModelSource, "model not detected");
            AddTextValue(lines, "Manufacturer", metadata.Manufacturer, metadata.ManufacturerSource, "manufacturer not detected");
            AddNumberValue(lines, "Counterweight", metadata.CounterweightTons, "t", metadata.CounterweightSource, "counterweight not detected");
            AddNumberValue(lines, "Transport length", metadata.VehicleLengthMeters, "m", metadata.VehicleLengthSource, "transport length not detected");
            AddNumberValue(lines, "Transport height", metadata.VehicleHeightMeters, "m", metadata.VehicleHeightSource, "transport height not detected");
            AddNumberValue(lines, "Travel width", metadata.VehicleWidthMeters, "m", metadata.VehicleWidthSource, "travel width not detected");
            AddNumberValue(lines, "Outrigger pad-to-pad", metadata.OutriggerWidthMeters, "m", metadata.OutriggerWidthSource, "outrigger pad-to-pad span not detected");
            AddNumberValue(lines, "Outrigger front-to-rear", metadata.OutriggerLengthMeters, "m", metadata.OutriggerLengthSource, "outrigger front-to-rear span not detected");

            return SanitizeEvidenceDisplayText(string.Join(Environment.NewLine, lines));
        }

        private static void AddTextValue(List<string> lines, string label, string value, string source, string missingReason)
        {
            string displayValue = string.IsNullOrWhiteSpace(value) ? "Missing" : SanitizeEvidenceDisplayText(value.Trim());
            lines.Add($"- {label}: {displayValue}");
            lines.Add($"  Evidence: {BuildCleanSourceSummary(source, missingReason)}");
        }

        private static void AddNumberValue(List<string> lines, string label, double? value, string unit, string source, string missingReason)
        {
            string displayValue = value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit
                : "Missing";

            lines.Add($"- {label}: {displayValue}");
            lines.Add($"  Evidence: {BuildCleanSourceSummary(source, missingReason)}");
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

        private void BtnViewMetadataEvidence_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_metadataEvidenceText))
                return;

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
                Text = _metadataEvidenceSummary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            summary.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(summary, 1);
            root.Children.Add(summary);

            var evidenceBox = new TextBox
            {
                Text = _metadataEvidenceText,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = TxtStatus.FontFamily,
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
            copyButton.Click += (_, _) => Clipboard.SetText(_metadataEvidenceText);

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

        private static void CollapseHint(TextBlock? target)
        {
            if (target == null)
                return;

            target.Text = string.Empty;
            target.ToolTip = null;
            target.Visibility = Visibility.Collapsed;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VehicleLengthMeters = ParseRequiredLength(TxtVehicleLength, "Total Length");
                VehicleHeightMeters = ParseRequiredLength(TxtVehicleHeight, "Transport Height");
                VehicleWidthMeters = ParseRequiredLength(TxtVehicleWidth, "Travel Width");

                if (_isOnRubber)
                {
                    OutriggerWidthMeters = null;
                    OutriggerLengthMeters = null;
                }
                else
                {
                    OutriggerWidthMeters = ParseRequiredLength(TxtOutriggerWidth, "Outrigger Span");
                    OutriggerLengthMeters = ParseRequiredLength(TxtOutriggerLength, "Span to Span");
                }

                DialogResult = true;
                Close();
            }
            catch (FormatException ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Invalid Dimension", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyUnitLabels()
        {
            var symbol = UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit);
            LblVehicleLength.Text = $"Total Length * ({symbol})";
            LblVehicleHeight.Text = $"Transport Height * ({symbol})";
            LblVehicleWidth.Text = $"Travel Width * ({symbol})";
            LblOutriggerWidth.Text = $"Outrigger Span (pad to pad) * ({symbol})";
            LblOutriggerLength.Text = $"Span to Span (front to rear) * ({symbol})";
        }

        private double ParseRequiredLength(TextBox textBox, string fieldName)
        {
            string text = textBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                textBox.Focus();
                throw new FormatException($"{fieldName} is required.");
            }

            if (LoadChartCsvService.TryParseUserEnteredNumber(text, out double value))
            {
                double meters = UnitSystem.ToMeters(value, _unitSettings.DisplayLengthUnit);
                if (double.IsFinite(meters) && meters > 0d)
                    return meters;
            }

            textBox.Focus();
            throw new FormatException($"{fieldName} must be a valid value greater than zero.");
        }

        private string FormatLengthInput(double? value)
        {
            return value.HasValue && value.Value > 0
                ? UnitSystem.FromMeters(value.Value, _unitSettings.DisplayLengthUnit)
                    .ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}
