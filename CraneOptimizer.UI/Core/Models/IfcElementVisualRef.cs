namespace CraneOptimizer.UI.Core.Models
{
    public sealed record IfcElementVisualRef(Guid ModelInstanceId, string ElementId)
    {
        public string Key => $"{ModelInstanceId:N}:{ElementId}";
    }
}
