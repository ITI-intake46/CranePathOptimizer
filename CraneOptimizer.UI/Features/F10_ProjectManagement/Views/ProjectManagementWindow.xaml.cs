using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using System.Windows;

namespace CraneOptimizer.UI.F10_ProjectManagement.Views
{
    public partial class ProjectManagementWindow : Window
    {
        public ProjectManagementWindow(ProjectManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (_, _) => await viewModel.InitializeAsync();
        }
    }
}
