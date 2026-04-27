using System.Windows;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    /// <summary>
    /// Interaction logic for GAResultsWindow.xaml
    /// </summary>
    public partial class GAResultsWindow : Window
    {
        public static readonly DependencyProperty SelectedPointProperty =
            DependencyProperty.Register(
                nameof(SelectedPoint),
                typeof(CranePositionResult),
                typeof(GAResultsWindow),
                new PropertyMetadata(null));

        public CranePositionResult? SelectedPoint
        {
            get => (CranePositionResult?)GetValue(SelectedPointProperty);
            set => SetValue(SelectedPointProperty, value);
        }

        public GAResultsWindow()
        {
            InitializeComponent();
        }
    }
}
