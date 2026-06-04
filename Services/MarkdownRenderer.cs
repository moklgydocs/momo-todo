using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MokReport.Todo.Services;

public static class MarkdownRenderer
{
    private const string PrimaryHex = "#D4687B";
    private const string TextPrimaryHex = "#2D1F24";
    private const string TextSecondaryHex = "#8E7B80";
    private const string CodeBgHex = "#FDF2F4";
    private const string BorderHex = "#F0E0E4";

    public static System.Windows.Controls.ScrollViewer Render(string? markdown)
    {
        var scroll = new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            MaxHeight = 180
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            scroll.Content = new System.Windows.Controls.TextBlock
            {
                Text = "暂无描述",
                FontSize = 12,
                Foreground = MakeBrush(TextSecondaryHex),
                FontStyle = System.Windows.FontStyles.Italic,
                Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };
            return scroll;
        }

        try
        {
            var document = Markdown.Parse(markdown);
            var panel = new System.Windows.Controls.StackPanel();
            var firstBlock = true;

            foreach (Markdig.Syntax.Block block in document)
            {
                if (!firstBlock)
                    panel.Children.Add(new System.Windows.Shapes.Rectangle { Height = 2, Fill = MakeBrush(BorderHex), Margin = new System.Windows.Thickness(0, 4, 0, 4) });
                firstBlock = false;

                var element = RenderBlock(block);
                if (element != null)
                    panel.Children.Add(element);
            }

            scroll.Content = panel;
        }
        catch
        {
            scroll.Content = new System.Windows.Controls.TextBlock
            {
                Text = markdown,
                FontSize = 12,
                Foreground = MakeBrush(TextPrimaryHex),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };
        }

        return scroll;
    }

    private static System.Windows.FrameworkElement? RenderBlock(Markdig.Syntax.Block block)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock para => RenderParagraph(para),
            CodeBlock code => RenderCodeBlock(code),
            ListBlock list => RenderList(list),
            QuoteBlock quote => RenderQuote(quote),
            ThematicBreakBlock => new System.Windows.Controls.Border { Height = 1, Background = MakeBrush(BorderHex), Margin = new System.Windows.Thickness(0, 4, 0, 4) },
            _ => RenderParagraph(block as LeafBlock)
        };
    }

    private static System.Windows.FrameworkElement RenderHeading(HeadingBlock heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 18.0, 2 => 16.0, 3 => 14.0, 4 => 13.0, _ => 12.0
        };
        var margin = heading.Level switch
        {
            1 => 6.0, 2 => 4.0, _ => 2.0
        };

        var tb = new System.Windows.Controls.TextBlock
        {
            FontSize = fontSize,
            FontWeight = System.Windows.FontWeights.Bold,
            Foreground = MakeBrush(PrimaryHex),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new System.Windows.Thickness(0, margin, 0, 2)
        };
        RenderInlines(tb.Inlines, heading.Inline);
        return tb;
    }

    private static System.Windows.FrameworkElement RenderParagraph(LeafBlock? para)
    {
        if (para == null) return new System.Windows.Controls.TextBlock { Height = 0 };

        var tb = new System.Windows.Controls.TextBlock
        {
            FontSize = 12,
            Foreground = MakeBrush(TextPrimaryHex),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new System.Windows.Thickness(0, 2, 0, 2)
        };
        if (para.Inline != null)
            RenderInlines(tb.Inlines, para.Inline);
        return tb;
    }

    private static System.Windows.FrameworkElement RenderCodeBlock(CodeBlock code)
    {
        var border = new System.Windows.Controls.Border
        {
            Background = MakeBrush(CodeBgHex),
            CornerRadius = new System.Windows.CornerRadius(6),
            Padding = new System.Windows.Thickness(10),
            Margin = new System.Windows.Thickness(0, 4, 0, 4),
            BorderBrush = MakeBrush(BorderHex),
            BorderThickness = new System.Windows.Thickness(0.5)
        };

        var tb = new System.Windows.Controls.TextBlock
        {
            Text = code.Lines.ToString(),
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
            FontSize = 11,
            Foreground = MakeBrush(TextPrimaryHex),
            TextWrapping = System.Windows.TextWrapping.Wrap
        };
        border.Child = tb;
        return border;
    }

    private static System.Windows.FrameworkElement RenderList(ListBlock list)
    {
        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(8, 2, 0, 4) };

        foreach (var item in list)
        {
            if (item is ListItemBlock li)
            {
                var itemPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new System.Windows.Thickness(0, 1, 0, 1)
                };
                var bullet = new System.Windows.Controls.TextBlock
                {
                    Text = list.IsOrdered ? $"{li.Order}." : "•",
                    FontSize = 12,
                    Foreground = MakeBrush(PrimaryHex),
                    Margin = new System.Windows.Thickness(0, 0, 6, 0),
                    Width = 16,
                    TextAlignment = System.Windows.TextAlignment.Right
                };
                itemPanel.Children.Add(bullet);

                var contentStack = new System.Windows.Controls.StackPanel();
                foreach (var sub in li)
                {
                    var subEl = RenderBlock(sub);
                    if (subEl != null)
                        contentStack.Children.Add(subEl);
                }
                itemPanel.Children.Add(contentStack);
                panel.Children.Add(itemPanel);
            }
        }

        return panel;
    }

    private static System.Windows.FrameworkElement RenderQuote(QuoteBlock quote)
    {
        var border = new System.Windows.Controls.Border
        {
            BorderBrush = MakeBrush(PrimaryHex),
            BorderThickness = new System.Windows.Thickness(2, 0, 0, 0),
            Background = MakeBrush(CodeBgHex),
            Padding = new System.Windows.Thickness(10, 6, 8, 6),
            Margin = new System.Windows.Thickness(0, 4, 0, 4),
            CornerRadius = new System.Windows.CornerRadius(0, 4, 4, 0)
        };

        var panel = new System.Windows.Controls.StackPanel();
        foreach (var block in quote)
        {
            var el = RenderBlock(block);
            if (el != null)
                panel.Children.Add(el);
        }
        border.Child = panel;
        return border;
    }

    private static void RenderInlines(System.Windows.Documents.InlineCollection inlines, ContainerInline? container)
    {
        if (container == null) return;

        foreach (var inline in container)
        {
            System.Windows.Documents.Span? span = null;

            if (inline is LiteralInline lit)
            {
                span = new System.Windows.Documents.Span(new System.Windows.Documents.Run(lit.Content.ToString()!));
            }
            else if (inline is EmphasisInline em)
            {
                span = new System.Windows.Documents.Span();
                span.FontStyle = System.Windows.FontStyles.Italic;
                RenderInlines(span.Inlines, em);
            }
            else if (inline is CodeInline code)
            {
                span = new System.Windows.Documents.Span(new System.Windows.Documents.Run(code.Content)
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 11,
                    Background = MakeBrush(CodeBgHex)
                });
            }
            else if (inline is LinkInline link)
            {
                var text = link.Url != null ? $"{link.FirstChild} ({link.Url})" : link.FirstChild?.ToString() ?? "";
                span = new System.Windows.Documents.Span(new System.Windows.Documents.Run(text));
                span.TextDecorations = System.Windows.TextDecorations.Underline;
                span.Foreground = MakeBrush(PrimaryHex);
            }
            else if (inline is LineBreakInline)
            {
                span = new System.Windows.Documents.Span(new System.Windows.Documents.LineBreak());
            }
            else if (inline is ContainerInline ci)
            {
                span = new System.Windows.Documents.Span();
                RenderInlines(span.Inlines, ci);
            }

            if (span != null)
                inlines.Add(span);
        }
    }

    private static System.Windows.Media.Brush MakeBrush(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new System.Windows.Media.SolidColorBrush(color);
        }
        catch { return System.Windows.Media.Brushes.Transparent; }
    }
}
