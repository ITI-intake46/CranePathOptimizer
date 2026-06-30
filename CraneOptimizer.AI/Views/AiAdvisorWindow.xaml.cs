using System.Windows;
using System.Windows.Input;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.ViewModels;

namespace CraneOptimizer.AI.Views
{
    public partial class AiAdvisorWindow : Window
    {
        private readonly AiAdvisorViewModel _viewModel;
        private readonly IAiCopilotSettingsService _settingsService;
        private bool _autoChecked;

        public AiAdvisorWindow(AiAdvisorViewModel viewModel, IAiCopilotSettingsService settingsService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settingsService = settingsService;
            DataContext = viewModel;
            Loaded += OnLoaded;
            SourceInitialized += (_, _) => FitInitialSizeToCurrentWorkArea();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            FitInitialSizeToCurrentWorkArea();

            if (_autoChecked || !_settingsService.Current.AutoCheckOllamaOnStartup)
                return;

            _autoChecked = true;
            await _viewModel.CheckOllamaAsync().ConfigureAwait(true);
        }

        private void QuestionComposer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                return;

            if (_viewModel.IsBusy)
                return;

            if (_viewModel.ToggleOllamaCommand.CanExecute(null))
            {
                e.Handled = true;
                _viewModel.ToggleOllamaCommand.Execute(null);
            }
        }

        private void FitInitialSizeToCurrentWorkArea()
        {
            if (WindowState == WindowState.Maximized)
                return;

            const double margin = 18d;
            var workArea = SystemParameters.WorkArea;
            var owner = Owner;
            var targetLeft = workArea.Left;
            var targetTop = workArea.Top;
            var targetWidth = workArea.Width;
            var targetHeight = workArea.Height;

            if (owner is { ActualWidth: > 900d, ActualHeight: > 650d, WindowState: WindowState.Normal })
            {
                targetLeft = owner.Left;
                targetTop = owner.Top;
                targetWidth = owner.ActualWidth;
                targetHeight = owner.ActualHeight;
            }

            var maxInitialWidth = Math.Max(900d, targetWidth - margin);
            var maxInitialHeight = Math.Max(600d, targetHeight - margin);

            MinWidth = Math.Min(MinWidth, maxInitialWidth);
            MinHeight = Math.Min(MinHeight, maxInitialHeight);
            Width = Math.Min(Width, maxInitialWidth);
            Height = Math.Min(Height, maxInitialHeight);

            Left = targetLeft + Math.Max(0d, (targetWidth - Width) / 2d);
            Top = targetTop + Math.Max(0d, (targetHeight - Height) / 2d);
        }
    }
}
