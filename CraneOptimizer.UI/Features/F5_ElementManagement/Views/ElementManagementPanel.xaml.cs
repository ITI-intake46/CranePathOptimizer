using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementManagementPanel : UserControl
    {
        public ElementManagementPanel()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            var vm = App.ServiceProvider.GetRequiredService<ElementManagementViewModel>();
            DataContext = vm;
        }
    }
}
