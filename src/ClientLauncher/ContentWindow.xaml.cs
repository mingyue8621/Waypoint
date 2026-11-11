using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LocalExile.ClientTool;

/// <summary>
/// The popup every click on the launcher opens: a heading, a scrolling body and one way out. Its body is either markdown
/// (the change log, drawn by <see cref="MarkdownView"/>) or elements the caller builds (the season's modules, read from the
/// install). It is the launcher's own page, not a browser: a copy handed to a player opens nothing outside the window.
/// </summary>
public partial class ContentWindow : Window
{
    public ContentWindow() => InitializeComponent();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        base.OnKeyDown(e);
    }

    ContentWindow Head(string heading)
    {
        HeadingText.Text = heading;
        Title = heading.Length > 0 ? heading : "流放之路";
        return this;
    }

    ContentWindow Foot(string text)
    {
        FootText.Text = text;
        return this;
    }

    /// <summary>Markdown in the body: the change log and the version sections.</summary>
    ContentWindow Markdown(string markdown)
    {
        MarkdownView.Render(Body, markdown);
        Scroller.ScrollToTop();
        return this;
    }

    /// <summary>Elements the caller builds in the body, which it fills itself.</summary>
    ContentWindow Blocks(Action<StackPanel> build)
    {
        Body.Children.Clear();
        build(Body);
        Scroller.ScrollToTop();
        return this;
    }

    public static ContentWindow Show(Window owner, string heading, string markdown, string footer = "")
    {
        var window = new ContentWindow { Owner = owner };
        window.Head(heading).Foot(footer).Markdown(markdown);
        window.ShowDialog();
        return window;
    }

    public static ContentWindow Show(Window owner, string heading, Action<StackPanel> build, string footer = "")
    {
        var window = new ContentWindow { Owner = owner };
        window.Head(heading).Foot(footer).Blocks(build);
        window.ShowDialog();
        return window;
    }

    void CaptionDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
