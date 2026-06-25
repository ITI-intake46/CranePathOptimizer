using System.Collections.Generic;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IIfcSourceMappingProfile
    {
        IReadOnlyList<string> AssemblyIdentityAliases { get; }
        IReadOnlyList<string> ElementIdentityAliases { get; }
        IReadOnlyList<string> TypeReferenceAliases { get; }
        IReadOnlyList<string> WeightAliases { get; }
        IReadOnlyList<string> VolumeAliases { get; }
        IReadOnlyList<string> MaterialAliases { get; }
        IReadOnlyList<string> LevelAliases { get; }
    }
}
