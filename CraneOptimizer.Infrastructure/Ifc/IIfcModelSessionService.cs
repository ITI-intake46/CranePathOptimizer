namespace CraneOptimizer.Infrastructure.Ifc
{
    public interface IIfcModelSessionService : IDisposable
    {
        IIfcModelSession? Current { get; }
        IIfcModelSession OpenSession(string ifcPath, CancellationToken cancellationToken = default);
        IIfcModelSession? GetActiveSession(string ifcPath);
        void Clear();
    }
}
