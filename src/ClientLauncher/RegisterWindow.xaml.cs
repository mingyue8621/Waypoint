using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LocalExile.ClientTool;

/// <summary>
/// 注册账号: one account, made on the server the launcher points at (GameServer.Host's dev protocol), so it works the
/// same from the host's machine and from a player's. Shown from **注册账号** beside **进入游戏**.
/// </summary>
public partial class RegisterWindow : Window
{
    readonly ServerTarget _server;
    bool _busy;

    public RegisterWindow(ServerTarget server)
    {
        InitializeComponent();
        _server = server;
        TargetText.Text = $"注册到服务器 {server.Host}:{server.RegisterPort}";
        Loaded += (_, _) => NameBox.Focus();
    }

    void CaptionDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void CloseClicked(object sender, RoutedEventArgs e) => Close();

    void NameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        PassBox.Focus();
    }

    void PassKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        ConfirmBox.Focus();
    }

    void ConfirmKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        RegisterClicked(sender, e);
    }

    /// <summary>The rule the account service itself applies (AccountService.Register): 3-32 letters, digits, _ or -.</summary>
    static bool ValidName(string name) =>
        name.Length is >= 3 and <= 32 && name.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');

    async void RegisterClicked(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string name = NameBox.Text.Trim();
        if (!ValidName(name)) { Tell("账号要 3-32 位，只能用字母、数字、_ 或 -。", bad: true); NameBox.Focus(); return; }
        if (PassBox.Password.Length < 4) { Tell("密码至少 4 位。", bad: true); PassBox.Focus(); return; }
        if (PassBox.Password != ConfirmBox.Password) { Tell("两次输入的密码不一样。", bad: true); ConfirmBox.Focus(); return; }

        _busy = true;
        RegisterButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        Tell($"正在联系服务器 {_server.Host}:{_server.RegisterPort} …", bad: false);
        try
        {
            var reply = await AccountApi.RegisterAsync(_server, name, PassBox.Password);
            Tell(reply.Describe(_server), !reply.Ok);
            if (reply.Ok)
            {
                PassBox.Clear();
                ConfirmBox.Clear();
                CancelButton.Content = "完成";
            }
            else RegisterButton.IsEnabled = true;
        }
        finally
        {
            _busy = false;
            Mouse.OverrideCursor = null;
        }
    }

    void Tell(string message, bool bad)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(bad ? "Bad" : "Good");
    }
}
