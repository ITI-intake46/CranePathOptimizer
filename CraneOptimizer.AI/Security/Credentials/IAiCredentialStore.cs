namespace CraneOptimizer.AI.Security.Credentials
{
    public interface IAiCredentialStore
    {
        bool Contains(string targetName);
        string? Read(string targetName);
        void Save(string targetName, string secret);
        void Delete(string targetName);
    }
}
