using CraneOptimizer.Application.Reports.Definitions;
using System.Globalization;
using CraneOptimizer.UI.Core.Base;

namespace CraneOptimizer.UI.Features.F12_Reports.ViewModels;

public sealed class ReportColumnOptionViewModel : ViewModelBase
{
    private bool _isSelected;
    private string _header;
    private int _order;
    private string _format;
    private string _alignment;
    private string _textSizeText;
    private string _textSizeValidationMessage = string.Empty;
    private string _unit;
    private bool _isAlreadySelected;
    private bool _showInPdf = true;
    private bool _showInExcel = true;

    public ReportColumnOptionViewModel(ReportColumnDefinition definition)
    {
        Definition = definition;
        _isSelected = definition.IsSelected;
        _header = definition.Header;
        _order = definition.Order;
        _format = string.IsNullOrWhiteSpace(definition.Format) ? DefaultFormat(definition.ValueKind) : definition.Format;
        _alignment = string.IsNullOrWhiteSpace(definition.Alignment) ? DefaultAlignment(definition.ValueKind) : definition.Alignment;
        _textSizeText = ParseTextSize(definition.TextSize).ToString("0.##", CultureInfo.InvariantCulture);
        _showInPdf = definition.ShowInPdf;
        _showInExcel = definition.ShowInExcel;
        UnitOptions = ResolveUnitOptions(definition);
        _unit = ResolveUnit(definition);
    }

    public ReportColumnDefinition Definition { get; }
    public string Key => Definition.Key;
    public ReportColumnValueKind ValueKind => Definition.ValueKind;
    public string Category => string.IsNullOrWhiteSpace(Definition.Category) ? GetCategory(Definition) : Definition.Category;
    public string DisplayHeader => BuildDisplayHeader(Header, Unit);
    public string DisplayName => $"{Order + 1}. {DisplayHeader}";
    public string UnitDisplayText => HasUnit ? Unit : "Not applicable";
    public bool HasUnit => UnitOptions.Count > 0;
    public IReadOnlyList<string> UnitOptions { get; private set; } = Array.Empty<string>();

    public string Header
    {
        get => _header;
        set
        {
            if (_header == value)
                return;

            _header = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayHeader));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int Order
    {
        get => _order;
        set
        {
            if (_order == value)
                return;

            _order = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Format
    {
        get => _format;
        set
        {
            if (_format == value)
                return;

            _format = value;
            OnPropertyChanged();
        }
    }

    public string Alignment
    {
        get => _alignment;
        set
        {
            if (_alignment == value)
                return;

            _alignment = value;
            OnPropertyChanged();
        }
    }

    public string Unit
    {
        get => _unit;
        set
        {
            if (_unit == value)
                return;

            _unit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnitDisplayText));
            OnPropertyChanged(nameof(DisplayHeader));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string TextSizeText
    {
        get => _textSizeText;
        set
        {
            if (_textSizeText == value)
                return;

            if (TryNormalizeTextSize(value, out var normalized, out var validationMessage))
                _textSizeText = normalized;
            else
                _textSizeText = value;

            TextSizeValidationMessage = validationMessage;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextSizePt));
        }
    }

    public double TextSizePt => ParseTextSize(TextSizeText);

    public string TextSizeValidationMessage
    {
        get => _textSizeValidationMessage;
        private set
        {
            if (_textSizeValidationMessage == value)
                return;

            _textSizeValidationMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsAlreadySelected
    {
        get => _isAlreadySelected;
        set
        {
            if (_isAlreadySelected == value)
                return;

            _isAlreadySelected = value;
            OnPropertyChanged();
        }
    }

    public bool ShowInPdf
    {
        get => _showInPdf;
        set
        {
            if (_showInPdf == value)
                return;

            _showInPdf = value;
            OnPropertyChanged();
        }
    }

    public bool ShowInExcel
    {
        get => _showInExcel;
        set
        {
            if (_showInExcel == value)
                return;

            _showInExcel = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public ReportColumnDefinition ToDefinition() => new()
    {
        Key = Definition.Key,
        Header = DisplayHeader,
        ValueKind = Definition.ValueKind,
        EvidenceScope = Definition.EvidenceScope,
        SupportedFamilies = Definition.SupportedFamilies,
        RequiredCapabilities = Definition.RequiredCapabilities,
        ShowInPreview = Definition.ShowInPreview,
        ShowInPdf = ShowInPdf,
        ShowInExcel = ShowInExcel,
        Order = Order,
        IsSelected = IsSelected,
        Format = Format,
        Alignment = Alignment,
        TextSize = TextSizePt.ToString("0.##", CultureInfo.InvariantCulture),
        Category = Category,
        Unit = Unit
    };

    public ReportColumnOptionViewModel Clone() => new(ToDefinition())
    {
        Format = Format,
        Alignment = Alignment,
        TextSizeText = TextSizeText,
        Unit = Unit,
        IsAlreadySelected = IsAlreadySelected,
        ShowInPdf = ShowInPdf,
        ShowInExcel = ShowInExcel
    };

    private static string DefaultFormat(ReportColumnValueKind valueKind) => valueKind switch
    {
        ReportColumnValueKind.Percentage => "Percentage: 0.0%",
        ReportColumnValueKind.Tons => "Tons: 0.00 t",
        ReportColumnValueKind.Meters => "Meters: 0.00 m",
        ReportColumnValueKind.Number => "Number: 0.00",
        _ => "Text"
    };

    private static string DefaultAlignment(ReportColumnValueKind valueKind) => valueKind switch
    {
        ReportColumnValueKind.Number or ReportColumnValueKind.Tons or ReportColumnValueKind.Meters or ReportColumnValueKind.Percentage => "Right",
        ReportColumnValueKind.YesNo or ReportColumnValueKind.Status => "Center",
        _ => "Left"
    };

    private static string GetCategory(ReportColumnDefinition definition)
    {
        if (definition.Key.Contains("Weight", StringComparison.OrdinalIgnoreCase)
            || definition.Key.Contains("Capacity", StringComparison.OrdinalIgnoreCase))
            return "Weight and Capacity";

        if (definition.ValueKind == ReportColumnValueKind.Meters
            || definition.Key.Contains("Length", StringComparison.OrdinalIgnoreCase)
            || definition.Key.Contains("Radius", StringComparison.OrdinalIgnoreCase))
            return "Geometry";

        if (definition.Key.Contains("Status", StringComparison.OrdinalIgnoreCase)
            || definition.Key.Contains("Warning", StringComparison.OrdinalIgnoreCase))
            return "Review / Lift Status";

        return "Identity";
    }

    private static string ResolveUnit(ReportColumnDefinition definition)
    {
        var options = ResolveUnitOptions(definition);
        if (options.Count == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(definition.Unit)
            && options.Contains(definition.Unit, StringComparer.OrdinalIgnoreCase))
        {
            return options.First(unit => unit.Equals(definition.Unit, StringComparison.OrdinalIgnoreCase));
        }

        return options[0];
    }

    private static IReadOnlyList<string> ResolveUnitOptions(ReportColumnDefinition? definition)
    {
        if (definition == null)
            return Array.Empty<string>();

        if (IsLengthColumn(definition))
            return ["m", "mm"];

        if (IsWeightColumn(definition))
            return ["t", "kg"];

        return Array.Empty<string>();
    }

    private static bool IsLengthColumn(ReportColumnDefinition definition) =>
        definition.ValueKind == ReportColumnValueKind.Meters;

    private static bool IsWeightColumn(ReportColumnDefinition definition) =>
        definition.ValueKind == ReportColumnValueKind.Tons;

    private static string BuildDisplayHeader(string header, string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return header;

        var cleanHeader = StripKnownUnitSuffix(header);
        return $"{cleanHeader} ({unit})";
    }

    private static string StripKnownUnitSuffix(string header)
    {
        var suffixes = new[]
        {
            " (m)", " (mm)", " (tons)", " (ton)", " (t)", " (kg)", " (m3)", " (m³)", " (%)"
        };

        foreach (var suffix in suffixes)
        {
            if (header.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return header[..^suffix.Length];
        }

        return header;
    }

    private static double ParseTextSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 10;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out invariantValue))
        {
            return Math.Clamp(invariantValue, 6, 32);
        }

        return value switch
        {
            "Small" => 7,
            "Large" => 12,
            _ => 10
        };
    }

    private static bool TryNormalizeTextSize(
        string? value,
        out string normalized,
        out string validationMessage)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            var clamped = Math.Clamp(parsed, 6, 32);
            normalized = clamped.ToString("0.##", CultureInfo.InvariantCulture);
            validationMessage = parsed == clamped
                ? string.Empty
                : "Text size was clamped to the allowed range of 6 to 32 pt.";
            return true;
        }

        normalized = value ?? string.Empty;
        validationMessage = "Enter a numeric text size from 6 to 32 pt.";
        return false;
    }
}
