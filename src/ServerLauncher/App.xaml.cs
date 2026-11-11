using System.Diagnostics;
using System.Windows;
using Launcher.Core;
using Waypoint;

namespace LocalExile.ServerTool;

/// <summary>
/// 流放之路服务端.exe: Waypoint's sections, for the servers. It needs a <b>source checkout</b> — the folder with
/// <c>GameServer.slnx</c>, where the Control section's Start/Stop/Servers drive <c>scripts\dev\live.ps1</c>. A release
/// install (<c>LocalExile.exe</c> + <c>release.json</c>) has no live.ps1 and nothing to build, so this tool does not run
/// there: its console already starts everything, and the game's own entry point does the rest.
/// </summary>
public partial class App : Application
{
    internal Settings Settings { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // The window's own failures are callbacks of the dispatcher, outside the try below: keep them in a file too.
        DispatcherUnhandledException += (_, args) => WriteCrash(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrash(args.ExceptionObject as Exception);
        try
        {
            Settings = Settings.Load();
            Settings.ApplyPaths();
            // Found, never chosen: this executable's own folder first (put it in the checkout or in a release install and
            // it uses that one), then the folder it was started from, then the one Waypoint last used. Both homes work: the
            // Control section drives live.ps1 in a checkout and LocalExile.exe in an install.
            string? root = Home(AppContext.BaseDirectory) ?? Home(Environment.CurrentDirectory) ?? Home(Settings.ServerPath);
            if (root is null)
            {
                MessageBox.Show(
                    "没有找到服务端目录。\n\n请把 流放之路服务端.exe 放在服务端目录里：源码目录（要有 GameServer.slnx 和" +
                    " scripts\\dev\\live.ps1），或者发布目录（要有 LocalExile.exe 和 release.json）。",
                    "流放之路", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            L.Current.SetLanguage(Settings.Language);
            Theme.Apply(Settings.Appearance);
            Repo.UseLocalProfile = Settings.UseLocalProfile;
            if (Settings.ServerPath != root) { Settings.ServerPath = root; Settings.Save(); }
            Environment.CurrentDirectory = root;
            Repo.IsInstall = LocalExileReleases.IsInstall(root);
            // The tools' own probe (python, node, git) only fills in paths; the window may open while it runs, or a slow
            // PATH costs the player a blank screen at startup.
            _ = Prerequisites.RefreshToolsAsync();
            var window = new MainWindow(Settings);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            // A build check without a pair of eyes: --selftest [section] writes what the window built (and a PNG of it) and ends.
            if (e.Args.Contains("--selftest"))
            {
                // A named section lets the check look at one page: its own loading has a moment to finish first.
                int at = Array.IndexOf(e.Args, "--selftest");
                if (at >= 0 && at + 1 < e.Args.Length && !e.Args[at + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    string wanted = e.Args[at + 1];
                    foreach (object item in window.Sections.Items)
                        if (item is Waypoint.Section section && section.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                        { window.Sections.SelectedItem = item; break; }
                    await Task.Delay(9000);
                }
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "localexile-server-selftest.txt"),
                    $"{DateTime.Now:O}\r\nroot={root}\r\nsize={window.ActualWidth:0}x{window.ActualHeight:0}\r\n" +
                    $"sections={window.Sections.Items.Count}\r\n");
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(Path.GetTempPath(), "localexile-server-selftest.png"));
                    encoder.Save(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
                Shutdown(0);
            }
        }
        catch (Exception ex)
        {
            // A launcher that cannot show its window says why in a file rather than vanishing.
            WriteCrash(ex);
            MessageBox.Show($"流放之路服务端无法启动：\n\n{ex.Message}", "流放之路", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>A folder this tool can run on: a source checkout, or a release install.</summary>
    static string? Home(string? folder) =>
        !string.IsNullOrEmpty(folder) && (Repo.IsRoot(folder) || LocalExileReleases.IsInstall(folder))
            ? Path.GetFullPath(folder) : null;

    /// <summary>What went wrong, in a file: %TEMP%\localexile-server-crash.txt.</summary>
    static void WriteCrash(Exception? exception)
    {
        if (exception is null) return;
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "localexile-server-crash.txt"), $"{DateTime.Now:O}\r\n{exception}"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
