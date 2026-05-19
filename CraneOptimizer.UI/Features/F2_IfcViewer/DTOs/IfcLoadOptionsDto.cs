namespace CraneOptimizer.UI.Features.F2_IfcViewer.DTOs
{
    /// <summary>
    /// Simple options chosen by the user before loading an IFC file.
    /// </summary>
    public sealed record IfcLoadOptionsDto
    {
        public bool HideGrids { get; init; }
        public bool HideLevels { get; init; }
        public bool HideAnnotations { get; init; }
        public bool HideSite { get; init; }
        public bool HideSpaces { get; init; }
        public bool HideOpenings { get; init; }
        public bool HideVirtualElements { get; init; }
    }
}
