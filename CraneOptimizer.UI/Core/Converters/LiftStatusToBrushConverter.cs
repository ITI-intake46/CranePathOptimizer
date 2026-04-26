using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CraneOptimizer.UI.Core.Converters
{
    public class LiftStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LiftStatus status)
                return Brushes.Gray;

            return status switch
            {
                LiftStatus.Liftable => Brushes.Green,
                LiftStatus.OutOfRadius => Brushes.Red,
                LiftStatus.Overweight => Brushes.Red,
                LiftStatus.HeightFail => Brushes.Red,
                LiftStatus.SpecialCase => Brushes.Yellow,
                _ => Brushes.Gray
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
