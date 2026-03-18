using System;
using System.Windows;

namespace WPF_IFC_test1
{
    public partial class CranePositionDialog : Window
    {
        public double PositionX { get; private set; }
        public double PositionY { get; private set; }
        public double PositionZ { get; private set; }

        public CranePositionDialog()
        {
            InitializeComponent();

            // Set focus to first textbox
            Loaded += (s, e) => txtX.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse coordinates
                PositionX = double.Parse(txtX.Text);
                PositionY = double.Parse(txtY.Text);
                PositionZ = double.Parse(txtZ.Text);

                // Validate coordinates (optional)
                if (double.IsNaN(PositionX) || double.IsNaN(PositionY) || double.IsNaN(PositionZ))
                {
                    MessageBox.Show("Please enter valid numbers for all coordinates.",
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (FormatException)
            {
                MessageBox.Show("Please enter valid numbers for coordinates.\n\nExample: 10.5 or -20",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing coordinates:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}