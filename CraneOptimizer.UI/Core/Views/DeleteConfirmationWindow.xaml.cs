using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Core.Views
{
    public partial class DeleteConfirmationWindow : Window
    {
        public DeleteConfirmationWindow(string title, string message, string actionText)
        {
            InitializeComponent();
            Title = title;
            HeadingText.Text = title;
            MessageText.Text = message;
            DeleteButton.Content = actionText;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CancelButton.Focus();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter)
                return;

            DialogResult = ReferenceEquals(Keyboard.FocusedElement, DeleteButton);
            e.Handled = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
