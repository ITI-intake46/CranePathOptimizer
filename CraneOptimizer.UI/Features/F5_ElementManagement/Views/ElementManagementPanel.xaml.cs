using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementManagementPanel : UserControl
    {
        private readonly ISceneService? _sceneService;

        public ElementManagementPanel()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            _sceneService = App.ServiceProvider.GetRequiredService<ISceneService>();
        }

        private ElementManagementViewModel? VM => DataContext as ElementManagementViewModel;

    }
}
