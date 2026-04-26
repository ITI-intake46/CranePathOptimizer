namespace CraneOptimizer.Application.Services
{
    public enum HighlightMode 
    { 
        Normal, 
        Highlight, 
        Focus, 
        Isolation 
    }

    public interface IHighlightService
    {
        void RequestMode(HighlightMode mode);
    }
}
