using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.UI.Interfaces;

namespace CraneOptimizer.UI.ViewModels
{
    /// <summary>
    /// Holds scene state (elements + zones).
    /// Acts as bridge between UI and Application layer.
    /// </summary>
    public class SceneViewModel
    {
        private readonly IRenderingService _render;

        public List<PrecastElement> Elements { get; private set; } = new();
        public List<CranePlacementZone> Zones { get; private set; } = new();

        public SceneViewModel(IRenderingService render)
        {
            _render = render;
        }

        public void SetElements(IEnumerable<PrecastElement> elements)
        {
            Elements = elements.ToList();
            _render.RenderElements(Elements);
        }

        public void AddZone(CranePlacementZone zone)
        {
            Zones.Add(zone);
            _render.RenderZone(zone);
        }
    }
}