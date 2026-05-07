using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F3_CraneSetup.Management;
using CraneOptimizer.UI.Features.F3_CraneSetup.Parameters;
using CraneOptimizer.UI.Core.Models;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    public class CraneVisualBuilder : IVisualBuilder<SceneCrane>
    {
        private readonly CraneManager _manager = new();

        /// <summary>
        /// Raised after any crane is built/updated.
        /// Listeners (e.g. MainViewModel) can call GetParameters() to refresh the right panel.
        /// </summary>
        public event Action<SceneCrane, MobileCraneParameters>? CraneParametersReady;

        public Visual3D Build(SceneCrane item)
        {
            var instance = _manager.GetOrAdd(item);
            instance.Visual.SetValue(FrameworkElement.TagProperty, item);
            DetachFromCurrentParent(instance.Visual);

            if (instance.Parameters is MobileCraneParameters mp)
                CraneParametersReady?.Invoke(item, mp);

            return instance.Visual;
        }

        public Visual3D BuildBatch(IEnumerable<SceneCrane> items)
        {
            _manager.Sync(items);
            var container = new ModelVisual3D();
            foreach (var instance in _manager.Instances)
            {
                DetachFromCurrentParent(instance.Visual);
                container.Children.Add(instance.Visual);

                if (instance.Parameters is MobileCraneParameters mp)
                    CraneParametersReady?.Invoke(instance.Scene, mp);
            }

            return container;
        }

        /// <summary>
        /// Returns live MobileCraneParameters for the given SceneCrane, or null if not yet built.
        /// </summary>
        public MobileCraneParameters? GetParameters(SceneCrane scene)
        {
            var instance = _manager.Instances.FirstOrDefault(i => i.Scene.Id == scene.Id);
            return instance?.Parameters as MobileCraneParameters;
        }

        private static void DetachFromCurrentParent(Visual3D visual)
        {
            if (VisualTreeHelper.GetParent(visual) is ModelVisual3D parent)
                parent.Children.Remove(visual);
        }
    }
}