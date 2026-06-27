namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class RawNameDiagnosticDetail
    {
        public string GlobalId { get; set; } = string.Empty;
        public string IfcEntity { get; set; } = string.Empty;
        public string RawProductName { get; set; } = string.Empty;
        public string RawProductLongName { get; set; } = string.Empty;
        public string RawObjectType { get; set; } = string.Empty;
        public string RawTag { get; set; } = string.Empty;
        public string RawAssemblyPos { get; set; } = string.Empty;
        public string RawCastUnitPos { get; set; } = string.Empty;
        public string ExtractedElementName { get; set; } = string.Empty;
        public string ElementID { get; set; } = string.Empty;
        public string ElementMark { get; set; } = string.Empty;
        public string AssemblyMark { get; set; } = string.Empty;
        public string ResolvedDisplayMark { get; set; } = string.Empty;
        public string CanonicalDisplayLabel { get; set; } = string.Empty;
        public string PersistedDisplayName { get; set; } = string.Empty;
        public string F5IfcNameResolved { get; set; } = string.Empty;
    }
}
