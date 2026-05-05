using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Management
{
    internal sealed class CraneManager
    {
        private readonly Dictionary<Guid, CraneInstance> _instances = new();

        public IEnumerable<CraneInstance> Instances => _instances.Values;

        public CraneInstance GetOrAdd(SceneCrane scene)
        {
            if (_instances.TryGetValue(scene.Id, out var existing))
            {
                existing.UpdateFromScene(scene);
                return existing;
            }

            var (visual, parameters) = CraneFactory.Create(scene);
            var instance = new CraneInstance(scene, parameters, visual);
            _instances[scene.Id] = instance;
            return instance;
        }

        public void Sync(IEnumerable<SceneCrane> scenes)
        {
            var seen = new HashSet<Guid>();
            foreach (var scene in scenes)
            {
                seen.Add(scene.Id);
                GetOrAdd(scene);
            }

            var stale = _instances.Keys.Where(id => !seen.Contains(id)).ToList();
            foreach (var id in stale)
            {
                _instances[id].Dispose();
                _instances.Remove(id);
            }
        }
    }
}
