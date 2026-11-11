using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LocalExile.ClientTool;

/// <summary>
/// The markdown of 更新说明.md, drawn with WPF's own elements: headings, lists, quotes, fenced code and the inline bold and
/// code the file uses. It is deliberately small — the change log is the only markdown this launcher reads, and a full
/// markdown engine would be a dependency a copy handed to a player has to carry.
/// </summary>
public static class MarkdownView
{
    static readonly Brush Body = Frozen("#D6D3CB");
    static readonly Brush Faint = Frozen("#8E8A82");
    static readonly Brush Accent = Frozen("#D8B45A");
    static readonly Brush Head = Frozen("#F0DFB8");
    static readonly Brush Rule = Frozen("#2A2E39");
    static readonly Brush CodeFill = Frozen("#151821");
    static readonly FontFamily Mono = new("Consolas, Cascadia Mono, Courier New");

    static Brush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    /// <summary>Draws <paramref name="markdown"/> into <paramref name="target"/>, replacing what it had.</summary>
    public static void Render(StackPanel target, string markdown)
    {
        target.Children.Clear();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var code = new List<string>();
        var table = new List<string>();
        bool inCode = false;
        // A markdown table is drawn as one monospaced block: the change log's tables are a handful of columns wide, and
        // reading them aligned is what they are for.
        void FlushTable()
        {
            if (table.Count == 0) return;
            AddCode(target, [.. table]);
            table.Clear();
        }
        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode) { AddCode(target, code); code.Clear(); }
                inCode = !inCode;
                continue;
            }
            if (inCode) { code.Add(raw); continue; }
            if (line.TrimStart().StartsWith("|", StringComparison.Ordinal)) { table.Add(line.TrimEnd()); continue; }
            FlushTable();
            if (line.Trim().Length == 0) { target.Children.Add(new Border { Height = 9 }); continue; }

            int depth = (line.Length - line.TrimStart().Length) / 2;
            string text = line.Trim();
            if (text.StartsWith("# ", StringComparison.Ordinal)) { AddHeading(target, text[2..], 20, Head, 4, 10); continue; }
            if (text.StartsWith("## ", StringComparison.Ordinal)) { AddHeading(target, text[3..], 17, Head, 16, 8); continue; }
            if (text.StartsWith("### ", StringComparison.Ordinal)) { AddHeading(target, text[4..], 14.5, Accent, 13, 6); continue; }
            if (text.StartsWith("#### ", StringComparison.Ordinal)) { AddHeading(target, text[5..], 13.5, Body, 11, 5); continue; }
            if (text.StartsWith("> ", StringComparison.Ordinal)) { AddQuote(target, text[2..]); continue; }
            if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
            { AddBullet(target, text[2..], depth, '•'); continue; }
            // A numbered list keeps its own numbers: the change log numbers the two sides of a fix.
            int dot = text.IndexOf(". ", StringComparison.Ordinal);
            if (dot is > 0 and <= 3 && int.TryParse(text[..dot], out _)) { AddBullet(target, text[(dot + 2)..], depth, '\0', text[..(dot + 1)]); continue; }
            if (text is "---" or "***" or "___") { target.Children.Add(new Border { Height = 1, Background = Rule, Margin = new Thickness(0, 10, 0, 10) }); continue; }
            AddParagraph(target, text, Body, new Thickness(0, 0, 0, 5));
        }
        FlushTable();
        if (inCode) AddCode(target, code);
    }

    static void AddHeading(StackPanel target, string text, double size, Brush brush, double top, double bottom)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = brush, Margin = new Thickness(0, top, 0, bottom) };
        foreach (var inline in Inlines(text, brush)) block.Inlines.Add(inline);
        target.Children.Add(block);
    }

    static void AddParagraph(StackPanel target, string text, Brush brush, Thickness margin)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = brush, Margin = margin };
        foreach (var inline in Inlines(text, brush)) block.Inlines.Add(inline);
        target.Children.Add(block);
    }

    static void AddBullet(StackPanel target, string text, int depth, char marker, string number = "")
    {
        var row = new Grid { Margin = new Thickness(8 + Math.Min(depth, 4) * 18, 0, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bullet = new TextBlock
        {
            Text = number.Length > 0 ? number : marker.ToString(),
            FontSize = 13, LineHeight = 21, Foreground = Faint, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Top,
        };
        row.Children.Add(bullet);
        var body = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = Body };
        foreach (var inline in Inlines(text, Body)) body.Inlines.Add(inline);
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        target.Children.Add(row);
    }

    static void AddQuote(StackPanel target, string text)
    {
        var border = new Border { BorderBrush = Accent, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 2, 0, 2), Margin = new Thickness(0, 2, 0, 8) };
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 20, Foreground = Faint };
        foreach (var inline in Inlines(text, Faint)) block.Inlines.Add(inline);
        border.Child = block;
        target.Children.Add(border);
    }

    static void AddCode(StackPanel target, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;
        var block = new TextBlock
        {
            Text = string.Join("\n", lines.Select(l => l.TrimEnd())),
            FontFamily = Mono, FontSize = 12, LineHeight = 19, Foreground = Body, TextWrapping = TextWrapping.NoWrap,
        };
        var frame = new Border
        {
            Background = CodeFill, BorderBrush = Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 2, 0, 9),
        };
        frame.Child = new ScrollViewer { Content = block, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        target.Children.Add(frame);
    }

    // Bold (**left**) and inline code (`right`); everything else is text. The change log uses no other inline markdown.
    static IEnumerable<Inline> Inlines(string text, Brush brush)
    {
        int at = 0;
        while (at < text.Length)
        {
            int bold = text.IndexOf("**", at, StringComparison.Ordinal);
            int code = text.IndexOf('`', at);
            int next = bold >= 0 && (code < 0 || bold < code) ? bold : code;
            if (next < 0) { yield return new Run(text[at..]) { Foreground = brush }; yield break; }
            if (next > at) yield return new Run(text[at..next]) { Foreground = brush };
            if (next == bold)
            {
                int close = text.IndexOf("**", next + 2, StringComparison.Ordinal);
                if (close < 0) { yield return new Run(text[next..]) { Foreground = brush }; yield break; }
                yield return new Bold(new Run(text[(next + 2)..close]) { Foreground = brush });
                at = close + 2;
            }
            else
            {
                int close = text.IndexOf('`', next + 1);
                if (close < 0) { yield return new Run(text[next..]) { Foreground = brush }; yield break; }
                yield return new Run(text[(next + 1)..close]) { Foreground = Accent, FontFamily = Mono, FontSize = 12.5 };
                at = close + 1;
            }
        }
    }
}
