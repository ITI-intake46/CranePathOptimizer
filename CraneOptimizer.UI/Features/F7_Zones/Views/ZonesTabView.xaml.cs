using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F7_Zones.Views
{
    /// <summary>
    /// Zone-panel keyboard forwarding. Viewport pointer input has one owner:
    /// IfcViewerControl and its interaction-mode router.
    /// </summary>
    public partial class ZonesTabView : UserControl
    {
        public ZonesTabView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            DataContext =
                App.ServiceProvider.GetRequiredService<DrawingViewModel>();
        }

        private void ZonesTabView_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (DataContext is DrawingViewModel viewModel)
                viewModel.HandleKeyDown(e);
        }
    }
}
