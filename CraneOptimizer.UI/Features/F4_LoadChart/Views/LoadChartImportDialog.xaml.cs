using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class LoadChartImportDialog : Window
    {
        private string? _loadedFilePath;

        public LoadChartImportDialog()
        {
            InitializeComponent();
        }

        private void Method_Checked(object sender, RoutedEventArgs e)
        {
            if (DropTitle == null) return;
            bool isCsv = RboCsv.IsChecked == true;
            DropTitle.Text     = isCsv ? "Drop CSV here" : "Drop PDF here";
            DropIcon.Text      = isCsv ? "📄" : "📑";
            PdfPagePanel.Visibility = isCsv ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = RboCsv.IsChecked == true
                    ? "CSV files (*.csv)|*.csv"
                    : "PDF files (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() == true)
                LoadFile(dlg.FileName);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
            => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;

        private void DropZone_DragEnter(object sender, DragEventArgs e)
            => DropZone.Opacity = 0.7;

        private void DropZone_DragLeave(object sender, DragEventArgs e)
            => DropZone.Opacity = 1.0;

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Opacity = 1.0;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                LoadFile(files[0]);
        }

        private void LoadFile(string path)
        {
            _loadedFilePath    = path;
            TxtFileName.Text   = Path.GetFileName(path);
            var info = new FileInfo(path);
            TxtFileSize.Text   = $"{info.Length / 1024.0:F1} KB";
            FileInfoPanel.Visibility = Visibility.Visible;
            AxisPanel.Visibility     = Visibility.Visible;
            TxtStatus.Text     = $"File loaded: {Path.GetFileName(path)}";
            TxtGridStats.Text  = "Ready to parse";
            // TODO: parse CSV/PDF into ChartGrid
        }

        private void AxisOrientation_Changed(object sender, RoutedEventArgs e)
        {
            // TODO: re-transpose grid data when axis flips
        }

        private void ReExtract_Click(object sender, RoutedEventArgs e)
        {
            // TODO: re-parse PDF at specified page number
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtModel.Text))
            {
                TxtStatus.Text = "Please enter a model name before saving.";
                return;
            }
            // TODO: persist chart to database
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
