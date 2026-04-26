using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class GAResultsWindowService : IGAResultsWindowService
    {
        public void Show(CranePlacementResult result)
        {
            var window = new GAResultsWindow
            {
                DataContext = result
            };

            if (System.Windows.Application.Current?.MainWindow != null)
                window.Owner = System.Windows.Application.Current.MainWindow;

            window.ShowDialog();
        }
    }
}
