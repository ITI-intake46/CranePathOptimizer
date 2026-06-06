using System;
using System.Globalization;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class GAFailureBreakdownDto
    {
        public string FailureCategory { get; set; } = string.Empty;
        public int Count { get; set; }
        public string PercentageOfFailedElements { get; set; } = string.Empty;
        public string PercentageOfSelectedElements { get; set; } = string.Empty;

        public double PctFailedValue => double.TryParse(PercentageOfFailedElements.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        public double PctSelectedValue => double.TryParse(PercentageOfSelectedElements.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
    }
}

