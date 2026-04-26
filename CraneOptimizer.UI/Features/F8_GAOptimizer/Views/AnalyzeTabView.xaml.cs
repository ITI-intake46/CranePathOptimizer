using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    /// <summary>
    /// Interaction logic for AnalyzeTabView.xaml
    /// </summary>
    public partial class AnalyzeTabView : UserControl
    {
        public AnalyzeTabView()
        {
            InitializeComponent();
        }

        public CranePlacementViewModel ViewModel
        {
            get => (CranePlacementViewModel)DataContext;
            set => DataContext = value;
        }
    }
}
