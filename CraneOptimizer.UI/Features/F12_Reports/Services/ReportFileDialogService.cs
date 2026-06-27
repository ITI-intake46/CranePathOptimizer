using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public sealed class ReportFileDialogService : IReportFileDialogService
{
    public string? BrowseLogo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Company Logo",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SavePdf(string suggestedFileName, string initialDirectory)
    {
        var dialog = CreateSaveDialog("PDF report (*.pdf)|*.pdf", ".pdf", suggestedFileName, initialDirectory);
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveExcel(string suggestedFileName, string initialDirectory)
    {
        var dialog = CreateSaveDialog("Excel workbook (*.xlsx)|*.xlsx", ".xlsx", suggestedFileName, initialDirectory);
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptForTemplateName(string defaultName, IEnumerable<string> existingNames)
    {
        var existing = existingNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var window = new Window
        {
            Title = "Save Report Template",
            Width = 360,
            Height = 188,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = System.Windows.Application.Current.TryFindResource("FontPrimary") as System.Windows.Media.FontFamily,
            Background = System.Windows.Application.Current.TryFindResource("SurfaceBrush") as System.Windows.Media.Brush,
            Foreground = System.Windows.Application.Current.TryFindResource("TextPrimaryBrush") as System.Windows.Media.Brush
        };

        var textBox = new TextBox
        {
            Text = defaultName,
            Margin = new Thickness(12, 8, 12, 0)
        };

        var validation = new TextBlock
        {
            Margin = new Thickness(12, 6, 12, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Application.Current.TryFindResource("TextSecondaryBrush") as System.Windows.Media.Brush
        };

        var ok = new Button
        {
            Content = "Save",
            Width = 80,
            Margin = new Thickness(0, 12, 8, 0),
            IsDefault = true
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Width = 80,
            Margin = new Thickness(0, 12, 12, 0),
            IsCancel = true
        };

        void ValidateTemplateName()
        {
            var name = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                validation.Text = "Enter a template name.";
                ok.IsEnabled = false;
                return;
            }

            if (existing.Contains(name))
            {
                validation.Text = "A template with this name already exists. Please choose another name.";
                ok.IsEnabled = false;
                return;
            }

            validation.Text = string.Empty;
            ok.IsEnabled = true;
        }

        textBox.TextChanged += (_, _) => ValidateTemplateName();
        ok.Click += (_, _) =>
        {
            ValidateTemplateName();
            if (ok.IsEnabled)
                window.DialogResult = true;
        };

        var panel = new DockPanel();
        var label = new TextBlock
        {
            Text = "Template name",
            Margin = new Thickness(12, 12, 12, 0)
        };
        panel.Children.Add(label);
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(textBox);
        DockPanel.SetDock(textBox, Dock.Top);
        panel.Children.Add(validation);
        DockPanel.SetDock(validation, Dock.Top);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        DockPanel.SetDock(buttons, Dock.Bottom);

        window.Content = panel;
        window.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current.MainWindow;
        window.Loaded += (_, _) =>
        {
            ValidateTemplateName();
            textBox.Focus();
            textBox.SelectAll();
        };

        return window.ShowDialog() == true ? textBox.Text.Trim() : null;
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
    }

    private static SaveFileDialog CreateSaveDialog(
        string filter,
        string defaultExtension,
        string suggestedFileName,
        string initialDirectory)
    {
        return new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            FileName = suggestedFileName,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
    }
}
