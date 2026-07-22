using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.AI.Rendering
{
    /// <summary>
    /// WPF-native Markdown preview for AI responses.
    /// The control can either own its vertical scroll bar (full AI Studio window)
    /// or yield scrolling to a parent ScrollViewer (right-docked AI Assist) to avoid
    /// nested two-step scrolling in constrained side panels.
    /// </summary>
    public partial class MarkdownPreviewControl : UserControl
    {
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownPreviewControl),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

        public static readonly DependencyProperty UseInternalScrollProperty = DependencyProperty.Register(
            nameof(UseInternalScroll),
            typeof(bool),
            typeof(MarkdownPreviewControl),
            new PropertyMetadata(true, OnUseInternalScrollChanged));

        public MarkdownPreviewControl()
        {
            InitializeComponent();
            ApplyScrollMode();
            UpdateDocument();
        }

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        public bool UseInternalScroll
        {
            get => (bool)GetValue(UseInternalScrollProperty);
            set => SetValue(UseInternalScrollProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownPreviewControl control)
                control.UpdateDocument();
        }

        private static void OnUseInternalScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownPreviewControl control)
                control.ApplyScrollMode();
        }

        private void ApplyScrollMode()
        {
            PreviewBox.VerticalScrollBarVisibility = UseInternalScroll
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
        }

        private void UpdateDocument()
        {
            PreviewBox.Document = MarkdownToFlowDocumentConverter.CreateDocument(Markdown);
            PreviewBox.ScrollToHome();
        }
    }
}
