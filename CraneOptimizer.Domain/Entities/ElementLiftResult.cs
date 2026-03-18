using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Immutable result DTO produced by <see cref="LiftAnalysisEngine"/> for one IFC element.
    /// </summary>
    public sealed class ElementLiftResult
    {
        /// <summary>The element that was analysed.</summary>
        public IfcElementModel Element { get; }

        /// <summary>Outcome of the 4-step check.</summary>
        public LiftStatus Status { get; }

        /// <summary>Horizontal (XY) distance in metres from crane base to element CG.</summary>
        public double RadiusM { get; }

        /// <summary>Crane rated capacity at this radius and boom length (tonnes).
        /// Null when the point is outside the load-chart envelope.</summary>
        public double? RatedCapacityTonne { get; }

        /// <summary>Element weight in tonnes (Weight [kg] / 1000).</summary>
        public double WeightTonne => (Element.Weight ?? 0) / 1000.0;

        /// <summary>Utilisation ratio: WeightTonne / RatedCapacityTonne. Null when not rated.</summary>
        public double? Utilisation =>
            RatedCapacityTonne.HasValue && RatedCapacityTonne.Value > 0
                ? WeightTonne / RatedCapacityTonne.Value
                : null;

        public ElementLiftResult(
            IfcElementModel element,
            LiftStatus status,
            double radiusM,
            double? ratedCapacityTonne = null)
        {
            Element            = element;
            Status             = status;
            RadiusM            = radiusM;
            RatedCapacityTonne = ratedCapacityTonne;
        }
    }
}
