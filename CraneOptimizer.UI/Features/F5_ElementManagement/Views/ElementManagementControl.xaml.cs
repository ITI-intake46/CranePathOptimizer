using System.Windows.Controls;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Views
{
    public partial class ElementManagementControl : UserControl
    {
        public ElementManagementControl()
        {
            InitializeComponent();

            var selectionService = App.ServiceProvider.GetService(typeof(CraneOptimizer.Application.Services.ISelectionService)) as CraneOptimizer.Application.Services.ISelectionService;
            if (selectionService != null)
            {
                selectionService.SelectionChanged += element =>
                {
                    if (element == null) return;

                    var dispatcher = System.Windows.Application.Current.Dispatcher;

                    if (dispatcher.CheckAccess())
                        ElementsList.ScrollIntoView(element);
                    else
                        dispatcher.InvokeAsync(() => ElementsList.ScrollIntoView(element));
                };
            }
        }
    }
}
