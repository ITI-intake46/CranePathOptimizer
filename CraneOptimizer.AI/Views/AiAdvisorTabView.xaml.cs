using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CraneOptimizer.AI.Views
{
    public partial class AiAdvisorTabView : UserControl
    {
        public AiAdvisorTabView()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this) && AiServiceLocator.ServiceProvider != null)
                DataContext = AiServiceLocator.ServiceProvider.GetRequiredService<AiAdvisorViewModel>();
        }

        private void OpenAdvisor_Click(object sender, RoutedEventArgs e)
        {
            var serviceProvider = AiServiceLocator.ServiceProvider;
            serviceProvider?.GetRequiredService<IAiAdvisorWindowService>().Show();
        }
    }
}
