using CraneOptimizer.Domain.Schemas;
using System;

namespace CraneOptimizer.Application.Services
{
    public interface ISelectionService
    {
        PrecastElement? SelectedElement { get; }
        void Select(PrecastElement? element);
        event Action<PrecastElement?> SelectionChanged;
    }
}
