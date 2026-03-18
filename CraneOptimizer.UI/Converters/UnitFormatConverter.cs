using System;
using System.Globalization;
using System.Windows.Data;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.Utilities
{
    // ══════════════════════════════════════════════════════════════════════════
    //  UnitFormatConverter
    //  WPF IValueConverter that converts a raw double in METRES into a
    //  formatted string using the global UnitSystem.
    //
    //  Usage in XAML:
    //    xmlns:util="clr-namespace:WPF_IFC_test1.Utilities"
    //
    //    <UserControl.Resources>
    //        <util:UnitFormatConverter     x:Key="UnitFmt"/>
    //        <util:UnitFormatAreaConverter x:Key="UnitFmtArea"/>
    //    </UserControl.Resources>
    //
    //    <TextBlock Text="{Binding BoomLength, Converter={StaticResource UnitFmt}}"/>
    //    <TextBlock Text="{Binding FootprintArea, Converter={StaticResource UnitFmtArea}}"/>
    //
    //  When UnitSystem.Changed fires, force WPF to re-evaluate by toggling
    //  DataContext in the code-behind:
    //    UnitSystem.Changed += () => { var dc = DataContext; DataContext = null; DataContext = dc; };
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts a <c>double</c> length value in METRES to a formatted string
    /// using <see cref="UnitSystem.Format"/>.
    /// ConvertBack is not supported (sliders bind TwoWay to the raw metre value).
    /// </summary>
    public class UnitFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return UnitSystem.Format(d);
            if (value is float f) return UnitSystem.Format(f);
            if (value is int i) return UnitSystem.Format(i);
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("UnitFormatConverter is one-way only.");
    }

    /// <summary>
    /// Converts a <c>double</c> area value in SQUARE METRES to a formatted
    /// string using <see cref="UnitSystem.FormatArea"/>.
    /// </summary>
    public class UnitFormatAreaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return UnitSystem.FormatArea(d);
            if (value is float f) return UnitSystem.FormatArea(f);
            if (value is int i) return UnitSystem.FormatArea(i);
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("UnitFormatAreaConverter is one-way only.");
    }
}