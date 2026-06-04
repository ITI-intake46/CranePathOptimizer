using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Core.Controls
{
    public partial class ParameterHelpIcon : UserControl
    {
        public static readonly DependencyProperty ParameterKeyProperty =
            DependencyProperty.Register(nameof(ParameterKey), typeof(string),
                typeof(ParameterHelpIcon), new PropertyMetadata(string.Empty, OnParameterKeyChanged));

        public string ParameterKey
        {
            get => (string)GetValue(ParameterKeyProperty);
            set => SetValue(ParameterKeyProperty, value);
        }

        public ParameterHelpIcon()
        {
            InitializeComponent();
        }

        private static void OnParameterKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParameterHelpIcon control)
            {
                control.UpdateTooltipContent();
            }
        }

        private void UpdateTooltipContent()
        {
            if (GAParameterHelpRegistry.TryGet(ParameterKey, out var metadata))
            {
                TxtDisplayName.Text = metadata.DisplayName;
                TxtDescription.Text = metadata.Description;
                TxtIncrease.Text = metadata.IncreaseEffect;
                TxtDecrease.Text = metadata.DecreaseEffect;
                TxtValid.Text = metadata.ValidRange;
                TxtRecommended.Text = metadata.RecommendedRange;
                TxtPerformance.Text = metadata.PerformanceImpact;
                TxtQuality.Text = metadata.QualityImpact;

                if (!string.IsNullOrWhiteSpace(metadata.SpecialNote))
                {
                    TxtSpecialNote.Text = metadata.SpecialNote;
                    PanelSpecialNote.Visibility = Visibility.Visible;
                }
                else
                {
                    PanelSpecialNote.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
