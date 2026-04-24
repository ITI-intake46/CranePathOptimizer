namespace CraneOptimizer.UI.Features.F2_IfcViewer.Models
{
    /// <summary>
    /// UI-level model that holds the filter flags chosen by the user in
    /// <see cref="Views.IFCLoadOptionsDialog"/>.
    /// <para>
    /// This is a visual-filtering concern only — it lives entirely within the
    /// F2_IfcViewer feature and is never passed to the Application or
    /// Infrastructure layers.
    /// </para>
    /// All flags default to <c>false</c> (nothing hidden).
    /// </summary>
    public class IfcFilterOptions
    {
        public bool HideGrids           { get; set; }
        public bool HideLevels          { get; set; }
        public bool HideAnnotations     { get; set; }
        public bool HideSite            { get; set; }
        public bool HideSpaces          { get; set; }
        public bool HideOpenings        { get; set; }
        public bool HideVirtualElements { get; set; }
    }
}
