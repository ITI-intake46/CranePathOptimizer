using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.UI.Features.F13_AiAdvisor.Services;
using CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Views
{
    public partial class AiAdvisorTabView : UserControl
    {
        public AiAdvisorTabView()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this) && App.ServiceProvider != null)
                DataContext = App.ServiceProvider.GetRequiredService<AiAdvisorViewModel>();
        }

        private void OpenAdvisor_Click(object sender, RoutedEventArgs e)
        {
            App.ServiceProvider
                .GetRequiredService<IAiAdvisorWindowService>()
                .Show();
        }
    }
}
