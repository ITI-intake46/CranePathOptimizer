namespace CraneOptimizer.AI.Security
{
    public interface IAiSensitiveDataRedactor
    {
        string Redact(string? value);
    }
}
