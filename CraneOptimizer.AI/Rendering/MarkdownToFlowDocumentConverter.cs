using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using WpfList = System.Windows.Documents.List;

namespace CraneOptimizer.AI.Rendering
{
    /// <summary>
    /// Lightweight markdown preview renderer for local AI responses.
    /// It intentionally supports the subset used by the copilot prompts: headings,
    /// paragraphs, bullets, numbered lists, bold text, inline code, and fenced code blocks.
    /// Keeping this local avoids adding a web view or external markdown package to the WPF app.
    /// </summary>
    public sealed partial class MarkdownToFlowDocumentConverter : IValueConverter
    {
        private static readonly FontFamily BodyFont = new("Segoe UI");
        private static readonly FontFamily CodeFont = new("Consolas");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return CreateDocument(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public static FlowDocument CreateDocument(string? markdown)
        {
            var document = new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = 13,
                PagePadding = new Thickness(0),
                Foreground = FindBrush("TextPrimaryBrush", Brushes.WhiteSmoke),
                LineHeight = 19
            };

            var text = Normalize(markdown);
            if (string.IsNullOrWhiteSpace(text))
            {
                document.Blocks.Add(CreateParagraph("No AI response yet.", FindBrush("TextSecondaryBrush", Brushes.LightGray)));
                return document;
            }

            WpfList? activeList = null;
            var activeListKind = ListKind.None;
            var inCodeFence = false;
            var codeLines = new List<string>();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                var trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushList();
                    if (inCodeFence)
                    {
                        document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
                        codeLines.Clear();
                        inCodeFence = false;
                    }
                    else
                    {
                        inCodeFence = true;
                    }

                    continue;
                }

                if (inCodeFence)
                {
                    codeLines.Add(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    FlushList();
                    continue;
                }

                if (HorizontalRuleRegex().IsMatch(trimmed))
                {
                    FlushList();
                    document.Blocks.Add(CreateSeparator());
                    continue;
                }

                var heading = HeadingRegex().Match(trimmed);
                if (heading.Success)
                {
                    FlushList();
                    var level = Math.Min(3, heading.Groups[1].Value.Length);
                    document.Blocks.Add(CreateHeading(heading.Groups[2].Value, level));
                    continue;
                }

                if (TryGetMarkdownHeadingFromBoldLine(trimmed, out var boldHeading))
                {
                    FlushList();
                    document.Blocks.Add(CreateHeading(boldHeading, 3));
                    continue;
                }

                var bullet = BulletRegex().Match(trimmed);
                if (bullet.Success)
                {
                    AddListItem(document, ref activeList, ref activeListKind, ListKind.Bullet, bullet.Groups[1].Value);
                    continue;
                }

                var ordered = OrderedListRegex().Match(trimmed);
                if (ordered.Success)
                {
                    AddListItem(document, ref activeList, ref activeListKind, ListKind.Numbered, ordered.Groups[1].Value);
                    continue;
                }

                FlushList();
                document.Blocks.Add(CreateParagraph(trimmed));
            }

            if (inCodeFence && codeLines.Count > 0)
                document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));

            FlushList();
            return document;

            void FlushList()
            {
                activeList = null;
                activeListKind = ListKind.None;
            }
        }

        private static string Normalize(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            return markdown
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\uFFFD', ' ')
                .Replace("<think>", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("</think>", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        private static Paragraph CreateHeading(string markdownText, int level)
        {
            var paragraph = new Paragraph
            {
                Margin = level switch
                {
                    1 => new Thickness(0, 0, 0, 10),
                    2 => new Thickness(0, 12, 0, 6),
                    _ => new Thickness(0, 10, 0, 4)
                },
                FontSize = level switch
                {
                    1 => 20,
                    2 => 16,
                    _ => 14
                },
                FontWeight = FontWeights.SemiBold,
                Foreground = level == 1
                    ? FindBrush("PrimaryBrush", Brushes.Orange)
                    : FindBrush("TextPrimaryBrush", Brushes.WhiteSmoke)
            };

            foreach (var inline in CreateInlines(CleanMarkdownMarkers(markdownText)))
                paragraph.Inlines.Add(inline);

            return paragraph;
        }

        private static Paragraph CreateParagraph(string markdownText, Brush? foreground = null)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = foreground ?? FindBrush("TextPrimaryBrush", Brushes.WhiteSmoke)
            };

            foreach (var inline in CreateInlines(markdownText))
                paragraph.Inlines.Add(inline);

            return paragraph;
        }

        private static Block CreateCodeBlock(string text)
        {
            return new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 4, 0, 10),
                Padding = new Thickness(10),
                FontFamily = CodeFont,
                FontSize = 12,
                LineHeight = 17,
                Background = FindBrush("SurfaceBrush", new SolidColorBrush(Color.FromRgb(38, 38, 38))),
                Foreground = FindBrush("TextSecondaryBrush", Brushes.LightGray)
            };
        }

        private static Block CreateSeparator()
        {
            return new Paragraph(new Run("────────────────────────────────────────"))
            {
                Margin = new Thickness(0, 4, 0, 10),
                Foreground = FindBrush("BorderBrush", Brushes.DimGray)
            };
        }

        private static void AddListItem(
            FlowDocument document,
            ref WpfList? activeList,
            ref ListKind activeListKind,
            ListKind newKind,
            string markdownText)
        {
            if (activeList == null || activeListKind != newKind)
            {
                activeList = new WpfList
                {
                    MarkerStyle = newKind == ListKind.Numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(18, 0, 0, 8),
                    Padding = new Thickness(0)
                };
                document.Blocks.Add(activeList);
                activeListKind = newKind;
            }

            var itemParagraph = CreateParagraph(markdownText);
            itemParagraph.Margin = new Thickness(0, 0, 0, 4);
            activeList.ListItems.Add(new ListItem(itemParagraph));
        }

        private static IEnumerable<Inline> CreateInlines(string markdownText)
        {
            var text = CleanMarkdownMarkers(markdownText);
            var lastIndex = 0;

            foreach (Match match in InlineRegex().Matches(text))
            {
                if (match.Index > lastIndex)
                    yield return CreateRun(text[lastIndex..match.Index]);

                var token = match.Value;
                if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal) && token.Length > 4)
                {
                    yield return new Bold(CreateRun(token[2..^2]));
                }
                else if (token.StartsWith('`') && token.EndsWith('`') && token.Length > 2)
                {
                    yield return new Run(token[1..^1])
                    {
                        FontFamily = CodeFont,
                        FontSize = 12,
                        Background = FindBrush("SurfaceBrush", new SolidColorBrush(Color.FromRgb(38, 38, 38))),
                        Foreground = FindBrush("TextSecondaryBrush", Brushes.LightGray)
                    };
                }
                else
                {
                    yield return CreateRun(token);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
                yield return CreateRun(text[lastIndex..]);
        }

        private static Run CreateRun(string text)
        {
            return new Run(text);
        }

        private static string CleanMarkdownMarkers(string text)
        {
            return text
                .Replace("__", "**", StringComparison.Ordinal)
                .Replace("\\*", "*", StringComparison.Ordinal)
                .Trim();
        }

        private static bool TryGetMarkdownHeadingFromBoldLine(string line, out string heading)
        {
            var match = BoldHeadingRegex().Match(line);
            if (match.Success)
            {
                heading = match.Groups[1].Value.Trim();
                return true;
            }

            heading = string.Empty;
            return false;
        }

        private static Brush FindBrush(string resourceKey, Brush fallback)
        {
            try
            {
                return System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private enum ListKind
        {
            None,
            Bullet,
            Numbered
        }

        [GeneratedRegex("^(#{1,6})\\s+(.+)$")]
        private static partial Regex HeadingRegex();

        [GeneratedRegex("^[-*_]{3,}$")]
        private static partial Regex HorizontalRuleRegex();

        [GeneratedRegex("^[-*+]\\s+(.+)$")]
        private static partial Regex BulletRegex();

        [GeneratedRegex("^\\d+[.)]\\s+(.+)$")]
        private static partial Regex OrderedListRegex();

        [GeneratedRegex("^\\*\\*(.+?)\\*\\*:?$")]
        private static partial Regex BoldHeadingRegex();

        [GeneratedRegex("(\\*\\*.+?\\*\\*|`.+?`)")]
        private static partial Regex InlineRegex();
    }
}
