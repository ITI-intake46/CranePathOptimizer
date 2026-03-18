using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace WPF_IFC_test1.Utilities
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Supported length units.</summary>
    public enum LengthUnit
    {
        Metres,
        Feet,
        Centimetres,
        Millimetres,
        Inches
    }

    /// <summary>Supported area units.</summary>
    public enum AreaUnit
    {
        SquareMetres,
        SquareFeet,
        SquareCentimetres
    }

    /// <summary>How a formatted value is displayed.</summary>
    public enum DisplayFormat
    {
        /// <summary>12.34 m  (value then symbol) — default</summary>
        ValueThenSymbol,
        /// <summary>12.34  (no symbol)</summary>
        ValueOnly,
        /// <summary>m 12.34  (symbol then value)</summary>
        SymbolThenValue
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UNIT SYSTEM — static singleton
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Global unit-conversion service for the entire WPF_IFC_test1 application.
    ///
    /// ALL code that displays or accepts a length/area value must go through
    /// this class. Subscribe to <see cref="Changed"/> to refresh UI when the
    /// user changes settings in <see cref="Dialogs.UnitsSettingsDialog"/>.
    ///
    /// Usage:
    ///   UnitSystem.Format(10.5)           → "10.50 m"  (default)
    ///   UnitSystem.Parse("33 ft")         → 10.0584   (always returns metres)
    ///   UnitSystem.FormatArea(250.0)       → "250.00 m²"
    /// </summary>
    public static class UnitSystem
    {
        // ── State ─────────────────────────────────────────────────────────────

        private static LengthUnit _currentUnit = LengthUnit.Metres;
        private static AreaUnit _currentArea = AreaUnit.SquareMetres;
        private static int _precision = 2;
        private static DisplayFormat _displayFormat = DisplayFormat.ValueThenSymbol;

        // ── Public read-only properties ───────────────────────────────────────

        public static LengthUnit CurrentUnit => _currentUnit;
        public static AreaUnit CurrentArea => _currentArea;
        public static int Precision => _precision;
        public static DisplayFormat Format_Style => _displayFormat;

        // ── Persistence ───────────────────────────────────────────────────────

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPF_IFC_SitePlanner",
            "units.json");

        /// <summary>Static constructor — loads persisted settings on first use.</summary>
        static UnitSystem() => LoadSettings();

        /// <summary>
        /// Saves the current unit settings to disk so they survive app restarts.
        /// Called automatically by <see cref="ApplySettings"/>.
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                var data = new
                {
                    Unit = _currentUnit.ToString(),
                    Area = _currentArea.ToString(),
                    Precision = _precision,
                    Format = _displayFormat.ToString()
                };
                File.WriteAllText(_settingsPath,
                    JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* non-critical — silently ignore disk errors */ }
        }

        /// <summary>
        /// Loads persisted settings from disk. Called once by the static constructor.
        /// Falls back to defaults if the file is missing or corrupt.
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                using var doc = JsonDocument.Parse(File.ReadAllText(_settingsPath));
                var root = doc.RootElement;

                if (root.TryGetProperty("Unit", out var u) &&
                    Enum.TryParse<LengthUnit>(u.GetString(), out var unit))
                    _currentUnit = unit;

                if (root.TryGetProperty("Area", out var a) &&
                    Enum.TryParse<AreaUnit>(a.GetString(), out var area))
                    _currentArea = area;

                if (root.TryGetProperty("Precision", out var p))
                    _precision = Math.Clamp(p.GetInt32(), 0, 6);

                if (root.TryGetProperty("Format", out var f) &&
                    Enum.TryParse<DisplayFormat>(f.GetString(), out var fmt))
                    _displayFormat = fmt;
            }
            catch { /* corrupt file — use defaults */ }
        }

        // ── Event ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after <see cref="ApplySettings"/> is called.
        /// Subscribe from any UI element that displays a length value so it
        /// can refresh its bindings when the user changes units.
        ///
        /// Example:
        ///   UnitSystem.Changed += RefreshLabels;
        /// </summary>
        public static event Action Changed;

        // ── Apply from dialog ─────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="Dialogs.UnitsSettingsDialog"/> when the user
        /// clicks OK / Save. Stores the new settings, persists to disk, and
        /// fires <see cref="Changed"/>.
        /// </summary>
        public static void ApplySettings(
            LengthUnit unit,
            AreaUnit area,
            int precision,
            DisplayFormat displayFormat)
        {
            _currentUnit = unit;
            _currentArea = area;
            _precision = Math.Clamp(precision, 0, 6);
            _displayFormat = displayFormat;

            SaveSettings();
            Changed?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FORMAT  —  metres  →  display string
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a raw value in METRES to a display string in the current unit.
        /// Always call this everywhere a length is shown to the user.
        /// </summary>
        /// <param name="metres">Value in metres (internal storage unit).</param>
        /// <returns>Formatted string e.g. "12.34 m" or "40.49 ft".</returns>
        public static string Format(double metres)
        {
            double converted = ConvertFromMetres(metres, _currentUnit);
            string symbol = GetSymbol(_currentUnit);
            return Compose(converted, symbol, _precision, _displayFormat);
        }

        /// <summary>
        /// Formats a length with an explicit override unit (ignores current setting).
        /// Useful for tooltips that always show metres alongside the display unit.
        /// </summary>
        public static string FormatAs(double metres, LengthUnit unit, int? precision = null)
        {
            double converted = ConvertFromMetres(metres, unit);
            string symbol = GetSymbol(unit);
            int prec = precision ?? _precision;
            return Compose(converted, symbol, prec, _displayFormat);
        }

        /// <summary>
        /// Converts a raw value in SQUARE METRES to a display string in the current area unit.
        /// </summary>
        public static string FormatArea(double squareMetres)
        {
            double converted = ConvertAreaFromSqMetres(squareMetres, _currentArea);
            string symbol = GetAreaSymbol(_currentArea);
            return Compose(converted, symbol, _precision, _displayFormat);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PARSE  —  user string  →  metres
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses a user-typed string and returns a value in METRES.
        /// Supports optional unit suffix. If no suffix, the current unit is assumed.
        ///
        /// Examples:
        ///   "10"      → 10.0  (if current unit is metres)
        ///   "10 m"    → 10.0
        ///   "10m"     → 10.0
        ///   "33 ft"   → 10.0584
        ///   "1000 mm" → 1.0
        ///   "40in"    → 1.016
        ///   "5.5cm"   → 0.055
        /// </summary>
        /// <param name="input">Raw string from a TextBox or CommandLine.</param>
        /// <param name="metres">Parsed value in metres, or 0 on failure.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParse(string input, out double metres)
        {
            metres = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToLowerInvariant();

            // Try to split numeric part from unit suffix
            LengthUnit suffix = _currentUnit; // default to current unit
            string numStr = input;

            if (input.EndsWith("mm")) { suffix = LengthUnit.Millimetres; numStr = input[..^2]; }
            else if (input.EndsWith("cm")) { suffix = LengthUnit.Centimetres; numStr = input[..^2]; }
            else if (input.EndsWith("ft")) { suffix = LengthUnit.Feet; numStr = input[..^2]; }
            else if (input.EndsWith("in")) { suffix = LengthUnit.Inches; numStr = input[..^2]; }
            else if (input.EndsWith("m")) { suffix = LengthUnit.Metres; numStr = input[..^1]; }

            numStr = numStr.Trim();

            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return false;

            metres = ConvertToMetres(value, suffix);
            return true;
        }

        /// <summary>
        /// Parses and returns metres directly, throwing on failure.
        /// Use <see cref="TryParse"/> in interactive UI — this is for trusted input.
        /// </summary>
        public static double Parse(string input)
        {
            if (!TryParse(input, out double metres))
                throw new FormatException($"Cannot parse '{input}' as a length value.");
            return metres;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONVERSION HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Converts a value in metres to the target unit.</summary>
        public static double ConvertFromMetres(double metres, LengthUnit to)
        {
            return to switch
            {
                LengthUnit.Metres => metres,
                LengthUnit.Centimetres => metres * 100.0,
                LengthUnit.Millimetres => metres * 1000.0,
                LengthUnit.Feet => metres * 3.28084,
                LengthUnit.Inches => metres * 39.3701,
                _ => metres
            };
        }

        /// <summary>Converts a value in the source unit to metres.</summary>
        public static double ConvertToMetres(double value, LengthUnit from)
        {
            return from switch
            {
                LengthUnit.Metres => value,
                LengthUnit.Centimetres => value / 100.0,
                LengthUnit.Millimetres => value / 1000.0,
                LengthUnit.Feet => value / 3.28084,
                LengthUnit.Inches => value / 39.3701,
                _ => value
            };
        }

        /// <summary>Converts a value in square metres to the target area unit.</summary>
        public static double ConvertAreaFromSqMetres(double sqMetres, AreaUnit to)
        {
            return to switch
            {
                AreaUnit.SquareMetres => sqMetres,
                AreaUnit.SquareFeet => sqMetres * 10.7639,
                AreaUnit.SquareCentimetres => sqMetres * 10000.0,
                _ => sqMetres
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SYMBOL HELPERS
        // ══════════════════════════════════════════════════════════════════════

        public static string GetSymbol(LengthUnit unit) => unit switch
        {
            LengthUnit.Metres => "m",
            LengthUnit.Centimetres => "cm",
            LengthUnit.Millimetres => "mm",
            LengthUnit.Feet => "ft",
            LengthUnit.Inches => "in",
            _ => "m"
        };

        public static string GetAreaSymbol(AreaUnit unit) => unit switch
        {
            AreaUnit.SquareMetres => "m²",
            AreaUnit.SquareFeet => "ft²",
            AreaUnit.SquareCentimetres => "cm²",
            _ => "m²"
        };

        /// <summary>Returns the display name used in the Units Settings dialog.</summary>
        public static string GetDisplayName(LengthUnit unit) => unit switch
        {
            LengthUnit.Metres => "Metres (m)",
            LengthUnit.Centimetres => "Centimetres (cm)",
            LengthUnit.Millimetres => "Millimetres (mm)",
            LengthUnit.Feet => "Feet (ft)",
            LengthUnit.Inches => "Inches (in)",
            _ => "Metres (m)"
        };

        public static string GetDisplayName(AreaUnit unit) => unit switch
        {
            AreaUnit.SquareMetres => "Square Metres (m²)",
            AreaUnit.SquareFeet => "Square Feet (ft²)",
            AreaUnit.SquareCentimetres => "Square Centimetres (cm²)",
            _ => "Square Metres (m²)"
        };

        public static string GetDisplayName(DisplayFormat fmt) => fmt switch
        {
            DisplayFormat.ValueThenSymbol => "Value then symbol  (e.g. 12.34 m)",
            DisplayFormat.ValueOnly => "Value only  (e.g. 12.34)",
            DisplayFormat.SymbolThenValue => "Symbol then value  (e.g. m 12.34)",
            _ => "Value then symbol  (e.g. 12.34 m)"
        };

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static string Compose(double value, string symbol, int precision, DisplayFormat fmt)
        {
            string num = value.ToString("F" + precision, CultureInfo.InvariantCulture);
            return fmt switch
            {
                DisplayFormat.ValueOnly => num,
                DisplayFormat.SymbolThenValue => $"{symbol} {num}",
                _ => $"{num} {symbol}"   // ValueThenSymbol (default)
            };
        }
    }
}