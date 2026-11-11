using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LocalExile.ClientTool;

/// <summary>流放之路登录器.exe: the game only (MainWindow).</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!e.Args.Any(arg => arg.StartsWith("--selftest", StringComparison.Ordinal))) return;
        // A build check without a pair of eyes (as 流放之路服务端.exe's): draw the window into a PNG and end.
        // --selftest-register also draws the account window.
        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(800);
            if (MainWindow is { } main) Draw(main, "localexile-client-selftest.png");
            if (e.Args.Contains("--selftest-register"))
            {
                var dialog = new RegisterWindow(ServerTarget.Load(ServerTarget.DefaultPatchPort));
                dialog.Show();
                await Task.Delay(600);
                Draw(dialog, "localexile-client-register.png");
                dialog.Close();
            }
            Shutdown(0);
        });
    }

    static void Draw(Window window, string name)
    {
        try
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(Path.GetTempPath(), name));
            encoder.Save(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
    }
}
