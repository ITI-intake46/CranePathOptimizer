using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementManagementPanel : UserControl
    {
        private readonly ISceneService _sceneService;

        public ElementManagementPanel()
        {
            InitializeComponent();
            _sceneService = App.ServiceProvider.GetRequiredService<ISceneService>();
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not PrecastElement element || VM == null)
                return;

            VM.SelectedElementName = string.IsNullOrWhiteSpace(element.ElementID)
                ? "No element selected"
                : element.ElementID;

            _sceneService.SetSelectedElements(new List<PrecastElement> { element });
            e.Handled = true;
        }
    }
}
