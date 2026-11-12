using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LocalExile.ClientTool;

/// <summary>
/// 流放之路登录器.exe: two buttons and nothing else — **进入游戏** starts the client, **注册账号** opens a window that
/// makes an account on the server.
///
/// The server comes from <c>server.json</c> beside this executable (<c>{"server": "203.0.113.7"}</c>, every field
/// optional); without the file it is this machine, which is the host's own copy:
/// <list type="bullet">
///   <item><b>Local</b> (127.0.0.1): when the servers are not up, **进入游戏** starts <c>LocalExile.exe</c> — hidden, so no
///         console window pops up — which prepares the client and the data, starts Patch, Auth and the Gateway, then the
///         client; the window's status line follows it through the patch port and the client's process.</item>
///   <item><b>Remote</b> (an address): the copy handed to players, who have only the client. It probes the server's patch
///         port, then starts <c>Client\Game.patched.exe --patching-server &lt;server&gt;:&lt;port&gt;</c> and touches
///         nothing else — no LocalExile.exe, no data folder.</item>
/// </list>
/// Accounts are made on the server either way (<see cref="AccountApi"/>), so the same window works on both sides.
/// Closing the game is the game's own business, so there is no button for it here.
/// </summary>
public partial class MainWindow : Window
{
    const string ClientFolder = "Client";
    const string ClientExe = "Game.patched.exe";

    readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1.5) };
    readonly ServerTarget _server;
    string? _entry;
    Process? _starting;
    bool _busy;
    // The client's process was seen: the launcher is put away until that process ends.
    bool _watchingGame;
    // What the install says about itself: the change log beside this executable, and the mods and modules of the server.
    IReadOnlyList<ReleaseNote> _notes = [];
    string? _notesPath;
    IReadOnlyList<ModFact> _mods = [];
    IReadOnlyList<ModuleFact> _modules = [];

    public MainWindow()
    {
        InitializeComponent();
        // A build from source carries no version; only a release's number is worth the caption.
        string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        VersionText.Text = version is "0.0.0" ? "" : version;
        _entry = FindEntry();
        _server = ServerTarget.Load(_entry is { } found ? PatchPort(Path.GetDirectoryName(found)!) : DefaultPatchPort());
        _poll.Tick += (_, _) => Poll();
        LoadArtwork();
        LoadFacts();
        ServerLabel.Text = _server.IsLocal ? "本地服务器" : "远程服务器";
        Loaded += (_, _) =>
        {
            if (!_server.IsLocal) StatusText.Text = "进入游戏会连接服务器；登录在游戏窗口里完成。";
        };
    }

    /// <summary>
    /// The key art and the banner, from <c>Art\hero.png</c> and <c>Art\banner.png</c> beside this executable (a .jpg works
    /// too): the window's own gradients show while a file is missing, so a copy without artwork still looks finished.
    /// </summary>
    void LoadArtwork()
    {
        if (FromArt("hero") is { } hero) HeroImage.Source = hero;
        if (FromArt("banner") is { } banner) BannerImage.Source = banner;
    }

    System.Windows.Media.ImageSource? FromArt(string name)
    {
        foreach (string dir in Candidates())
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = Path.Combine(dir, "Art", name + extension);
                if (!File.Exists(path)) continue;
                try
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path);
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException)
                {
                    return null;
                }
            }
        return null;
    }

    /// <summary>The release's own entry point: beside this executable, or in a folder above it (the host's copy only).</summary>
    static string? FindEntry()
    {
        foreach (string dir in Candidates())
        {
            string path = Path.Combine(dir, "LocalExile.exe");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    static IEnumerable<string> Candidates()
    {
        // A launcher kept inside the client folder still reads the server's mods, modules and change log: server.json's
        // "install" names that folder.
        if (ConfiguredInstall() is { } install) yield return install;
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
        for (string? dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
             !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir)) yield return dir;
    }

    /// <summary>The server's folder when this launcher is not in it: <c>install</c> in server.json beside the launcher,
    /// absolute or relative to it. Null when it names nothing that is there.</summary>
    static string? ConfiguredInstall()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "server.json");
        if (!File.Exists(file)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("install", out var field)
                || field.ValueKind != System.Text.Json.JsonValueKind.String) return null;
            string named = field.GetString()?.Trim() ?? "";
            if (named.Length == 0) return null;
            string full = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, named));
            return Directory.Exists(full) ? full : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
            or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The client folder the server itself patches: <c>Client.Folder</c> in Config\localexile.conf beside this
    /// executable, absolute or relative to it. Null when the file names none.</summary>
    static string? ConfiguredClient()
    {
        string conf = Path.Combine(AppContext.BaseDirectory, "Config", "localexile.conf");
        if (!File.Exists(conf)) return null;
        try
        {
            foreach (string line in File.ReadLines(conf))
            {
                string text = line.Trim();
                if (text.Length == 0 || text[0] == '#') continue;
                int equals = text.IndexOf('=');
                if (equals < 0 || !text[..equals].Trim().Equals("Client.Folder", StringComparison.OrdinalIgnoreCase)) continue;
                string value = text[(equals + 1)..].Trim().Trim('"');
                if (value.Length == 0) return null;
                return Path.IsPathRooted(value) ? value : Path.Combine(AppContext.BaseDirectory, value);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    /// <summary>The played client: the folder the server patches first (Client.Folder may keep the client outside the
    /// install), then this launcher's own folder — a player keeps the launcher beside the game it starts, inside the
    /// client folder, where the game is not in a Client\ folder of its own — then a Client\ beside it.</summary>
    string? FindClient()
    {
        if (ConfiguredClient() is { } configured && File.Exists(Path.Combine(configured, ClientExe)))
            return Path.Combine(configured, ClientExe);
        string beside = Path.Combine(AppContext.BaseDirectory, ClientExe);
        if (File.Exists(beside)) return beside;
        foreach (string dir in Candidates())
        {
            string path = Path.Combine(dir, ClientFolder, ClientExe);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    static Process[] ClientProcesses() => Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ClientExe));

    static bool Listening(int port)
    {
        try
        {
            return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners().Any(e => e.Port == port);
        }
        catch (System.Net.NetworkInformation.NetworkInformationException) { return false; }
    }

    /// <summary>Whether a port on the server answers: a launch that cannot reach the patch server would show the official
    /// update screen instead of the game.</summary>
    static async Task<bool> ReachableAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, stop.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException) { return false; }
    }

    // ---- 公告与各个入口 -------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads what this install says about itself once, when the window opens: <c>更新说明.md</c> beside the executable
    /// (or in a folder above it) and the server's own mods and modules. Every page and every announcement line is built
    /// from these, so shipping a new change log or adding a mod needs no change here.
    /// </summary>
    void LoadFacts()
    {
        _notesPath = UpdateNotes.Locate(Candidates());
        _notes = UpdateNotes.Load(_notesPath);
        _mods = InstallFacts.Mods(Candidates());
        _modules = InstallFacts.Modules(Candidates());
        FillNews();
    }

    /// <summary>The newest five versions of the change log as rows; a row opens that version.</summary>
    void FillNews()
    {
        NewsList.Children.Clear();
        var recent = _notes.Take(5).ToList();
        NewsEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewsList.Visibility = recent.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        for (int i = 0; i < recent.Count; i++)
        {
            var note = recent[i];
            int position = i + 1;
            var row = new DockPanel { Height = 26 };
            var date = new TextBlock { Text = note.Day, Style = (Style)FindResource("NewsDate") };
            DockPanel.SetDock(date, Dock.Right);
            row.Children.Add(date);
            var title = new TextBlock
            {
                Text = note.Summary.Length > 0 ? note.Summary : note.Heading,
                Style = (Style)FindResource("NewsTitle"),
                ToolTip = $"{note.Heading}\n{note.Summary}",
            };
            row.Children.Add(title);
            var button = new Button { Style = (Style)FindResource("NewsRow"), Content = row };
            button.Click += (_, _) => LauncherPages.Version(this, note, position, _notes.Count);
            NewsList.Children.Add(button);
        }
    }

    void SeasonClicked(object sender, RoutedEventArgs e) => LauncherPages.Season(this, _mods);

    void CommunityClicked(object sender, RoutedEventArgs e) => LauncherPages.Community(this, _server.Host, _server.IsLocal);

    void ChangeLogClicked(object sender, RoutedEventArgs e) => LauncherPages.ChangeLog(this, _notesPath, _notes.Count);

    void ToolsClicked(object sender, RoutedEventArgs e) => LauncherPages.Tools(this, _mods);

    /// <summary>A banner tab: it lights up while its page is open, then the row goes back to 赛季特色.</summary>
    void BannerTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button tab) return;
        Highlight(tab);
        try
        {
            switch (tab.Tag as string)
            {
                case "season": LauncherPages.Season(this, _mods); break;
                case "drops": LauncherPages.Drops(this, _mods); break;
                case "bots": LauncherPages.Bots(this, _modules); break;
                case "notes": LauncherPages.ChangeLog(this, _notesPath, _notes.Count); break;
                case "faq": LauncherPages.Faq(this); break;
            }
        }
        finally { Highlight(TabSeason); }
    }

    void Highlight(Button tab)
    {
        foreach (var candidate in new[] { TabSeason, TabDrops, TabBots, TabNotes, TabFaq })
            candidate.Style = (Style)FindResource(candidate == tab ? "BannerTabOn" : "BannerTab");
    }

    void CaptionDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void MinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void CloseClicked(object sender, RoutedEventArgs e) => Close();

    void RegisterClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new RegisterWindow(_server) { Owner = this };
        dialog.ShowDialog();
    }

    // ---- the game ----------------------------------------------------------------------------------------------

    async void PlayClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (ClientProcesses().Length > 0) { Tell("游戏已经在运行了。", bad: false); return; }

        string? client = FindClient();
        if (client is null)
        {
            Tell($"没找到 {ClientExe}：把登录器放进客户端文件夹（和 {ClientExe} 同级），或者放进服务端文件夹（客户端在 {ClientFolder}\\ 里）。", bad: true);
            return;
        }
        string clientFolder = Path.GetDirectoryName(client)!;

        _busy = true;
        PlayButton.IsEnabled = false;
        try
        {
            if (!_server.IsLocal)
            {
                // A player's copy: nothing of the servers here, only their address.
                Tell("正在连接服务器 …", bad: false);
                if (!await ReachableAsync(_server.Host, _server.PatchPort))
                {
                    Tell("连不上服务器：服务器可能没开，或者地址不对（防火墙、端口转发）。", bad: true);
                    return;
                }
                Start(client, $"--patching-server {_server.Host}:{_server.PatchPort}", clientFolder,
                    "正在启动客户端…", "客户端已启动，登录在游戏窗口里完成。");
                WatchGame();
                return;
            }

            // The host's own copy. LocalExile.exe is optional: a player keeps the launcher inside the client folder, with no
            // server install above it, and a server that is already up needs nothing from Disk but its ports.
            string? install = _entry is { } besideServer ? Path.GetDirectoryName(besideServer) : null;
            if (Listening(6112))
            {
                int patch = install is null ? _server.PatchPort : PatchPort(install);
                if (!Listening(patch))
                {
                    Tell("补丁服务没有在运行：这样启动客户端，它会去连官方服务器并显示官方更新。" +
                        "请先在服务端启动器里把 Patch 启动起来。", bad: true);
                    return;
                }
                if (await AccountApi.CountAsync(_server) is 0)
                {
                    Tell("服务器上还没有账号：先点「注册账号」建一个，再进入游戏。", bad: true);
                    return;
                }
                Start(client, $"--patching-server 127.0.0.1:{patch}", clientFolder,
                    "正在启动客户端…", "客户端已启动，登录在游戏窗口里完成。");
                WatchGame();
                return;
            }

            // Nothing is up: LocalExile.exe prepares the client and the data, starts the servers, then the client — hidden,
            // so the player never sees a console window (its progress is watched by Poll).
            if (_entry is not { } entry) { Tell(NoServers, bad: true); return; }
            StartServers(entry, install!);
        }
        finally
        {
            _busy = false;
            PlayButton.IsEnabled = true;
        }
    }

    /// <summary>Starts the release's own launcher with no console window of its own: it is a console program, and a black
    /// window is not what a launcher should show a player.</summary>
    void StartServers(string entry, string install)
    {
        try
        {
            var start = new ProcessStartInfo(entry)
            {
                WorkingDirectory = install,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _starting = Process.Start(start);
            Tell("服务端还没启动：正在准备并启动（第一次约一分钟）…", bad: false);
            WatchGame();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Tell($"启动失败：{ex.Message}", bad: true);
            _starting = null;
        }
    }

    /// <summary>
    /// Follows a launch: the servers coming up (their ports), the client's process, and then that process ending. The
    /// launcher puts itself away once the client is really running and comes back, focused, when the game is closed.
    /// A hidden LocalExile.exe has no console to read, so its milestones are the ports and the game's process.
    /// </summary>
    void Poll()
    {
        if (ClientProcesses().Length > 0)
        {
            if (_watchingGame) return;
            _watchingGame = true;
            _starting = null;
            // Nothing left to watch for but the exit: a slower poll is enough while the game runs.
            _poll.Interval = TimeSpan.FromSeconds(2);
            Tell("客户端已启动，登录在游戏窗口里完成；游戏关掉后这个窗口会自己回来。", bad: false);
            WindowState = WindowState.Minimized;
            return;
        }
        if (_watchingGame)
        {
            _watchingGame = false;
            _poll.Stop();
            ComeBack();
            return;
        }
        if (_starting is { HasExited: true })
        {
            int code = _starting.ExitCode;
            _poll.Stop();
            _starting = null;
            Tell($"启动失败：LocalExile 已退出（代码 {code}）。看游戏目录 logs\\ 里的日志。", bad: true);
            return;
        }
        if (Listening(6112)) Tell("服务端已就绪，正在启动客户端…", bad: false);
    }

    /// <summary>Starts watching a launch that was just made: the poll runs until the client appears and then ends.</summary>
    void WatchGame()
    {
        if (_poll.IsEnabled) return;
        _poll.Interval = TimeSpan.FromSeconds(1.5);
        _poll.Start();
    }

    /// <summary>
    /// Back on screen and in front, the way a launcher waiting for its player should be. The game window has just closed,
    /// so nothing else is asking for the focus; a window that was behind it does not always take it on Activate alone.
    /// </summary>
    void ComeBack()
    {
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        Tell("游戏已关闭。", bad: false);
    }

    /// <summary>Starts an executable of the install, in a folder of its own, and says how it went.</summary>
    void Start(string file, string arguments, string workingDirectory, string status, string done)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        Tell(status, bad: false);
        try
        {
            var start = new ProcessStartInfo(file) { WorkingDirectory = workingDirectory, UseShellExecute = true };
            if (arguments.Length > 0) start.Arguments = arguments;
            Process.Start(start)?.Dispose();
            Tell(done, bad: false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Tell($"启动失败：{ex.Message}", bad: true);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>
    /// The patch server's TCP port — what <c>--patching-server</c> wants, and the port the client's own update screen
    /// talks to. It is <b>not</b> the web port (8080): giving the client that one leaves it unable to reach the local
    /// patch server, so it asks the official one and opens the official update screen (live 2026-11-08).
    ///
    /// The release's own launcher takes it from <c>Ports.Patch</c> (12995 unless started with a port base): here it is the
    /// base <c>data\stack.json</c> records when a checkout's servers run on another block, else the environment's
    /// <c>Game_PATCH_TCP_PORT</c>, else that 12995.
    /// </summary>
    static int PatchPort(string install)
    {
        if (RecordedPortBase(install) is int recorded && recorded > 0) return recorded;
        return DefaultPatchPort();
    }

    /// <summary>The port a server listens on unless a checkout's data\stack.json says otherwise.</summary>
    static int DefaultPatchPort() =>
        int.TryParse(Environment.GetEnvironmentVariable("Game_PATCH_TCP_PORT"), out int named) && named > 0 ? named : 12995;

    /// <summary>live.ps1's recorded port block (<c>{"portBase": N}</c> in data\stack.json); 0 for the default ports.</summary>
    static int? RecordedPortBase(string install)
    {
        string path = Path.Combine(install, "data", "stack.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("portBase", out var basePort) && basePort.TryGetInt32(out int value) ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return null; }
    }

    const string NoServers = "服务端没在运行，这个文件夹里也没有 LocalExile.exe：先在服务端启动器里启动服务端，再点「开始游戏」；或者把登录器放回服务端文件夹。";

    void Tell(string message, bool bad)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(bad ? "Bad" : "Muted");
    }
}
