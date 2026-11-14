using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LocalExile.ClientTool;

/// <summary>
/// The pages behind every click on the launcher. Their text comes from the install — a mod's own manifest, a module's own
/// settings — so what a player reads here is what the server they are joining actually has; the launcher writes only the
/// explanations of how this copy is laid out.
/// </summary>
public static class LauncherPages
{
    static readonly Brush Body = Frozen("#D6D3CB");
    static readonly Brush Faint = Frozen("#8E8A82");
    static readonly Brush Strong = Frozen("#F0DFB8");
    static readonly Brush Accent = Frozen("#D8B45A");
    static readonly Brush Edge = Frozen("#2A2E39");
    static readonly Brush CardFill = Frozen("#151821");
    static readonly Brush On = Frozen("#6FBF73");

    static Brush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    // ---- the announcement list's notes -------------------------------------------------------------------------------

    /// <summary>One version of the change log, its own section only.</summary>
    public static void Version(Window owner, ReleaseNote note, int position, int total) =>
        ContentWindow.Show(owner, note.Heading, note.Body,
            total > 0 ? $"第 {position} / {total} 版 · 完整更新日志见「更多」" : "");

    /// <summary>The whole change log, as written.</summary>
    public static void ChangeLog(Window owner, string? path, int versions)
    {
        string markdown;
        try { markdown = path is not null && File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { markdown = ""; }
        if (markdown.Length == 0)
        {
            ContentWindow.Show(owner, "更新日志",
                "没找到 `更新说明.md`。\n\n把它放在登录器同一个文件夹里（和 `流放之路登录器.exe` 并排），这里就会显示服务端的每一版更新。", "");
            return;
        }
        ContentWindow.Show(owner, $"更新日志 · 共 {versions} 个版本", markdown, $"读取自 {UpdateNotes.FileName}");
    }

    // ---- the banner's tabs -------------------------------------------------------------------------------------------

    /// <summary>What this server turns on: every mod it has, with the state its own choices file gives it.</summary>
    public static void Season(Window owner, IReadOnlyList<ModFact> mods)
    {
        if (mods.Count == 0)
        {
            ContentWindow.Show(owner, "赛季特色", NoModsText, "");
            return;
        }
        int on = mods.Count(m => m.Enabled);
        ContentWindow.Show(owner, $"赛季特色 · 已启用 {on} / {mods.Count} 个模组", body =>
        {
            Lead(body, "这个服当前装了这些玩法改动，勾选表示已启用。");
            foreach (var mod in mods) ModCard(body, mod);
        }, "");
    }

    /// <summary>Only what changes drops: the quantity and rarity mods, plus fast campaign's experience.</summary>
    public static void Drops(Window owner, IReadOnlyList<ModFact> mods)
    {
        var shown = mods.Where(m => m.Id is "itemquantity" or "itemrarity" or "fastcampaign").ToList();
        ContentWindow.Show(owner, "掉落调整", body =>
        {
            Lead(body, "影响掉落与收益的玩法改动。每条后面是它当前是否启用：");
            if (shown.Count == 0) { Para(body, NoModsText); }
            foreach (var mod in shown) ModCard(body, mod);
        }, "");
    }

    /// <summary>The bot module, with the settings its own declarations carry.</summary>
    public static void Bots(Window owner, IReadOnlyList<ModuleFact> modules)
    {
        var bots = modules.FirstOrDefault(m => m.Id.Equals("PlayerBots", StringComparison.OrdinalIgnoreCase));
        var settings = InstallFacts.ModuleSettings(bots?.Folder);
        ContentWindow.Show(owner, "机器人队友", body =>
        {
            if (bots is null)
            {
                Para(body, "这个服没有开「玩家机器人」这个玩法。");
                return;
            }
            Lead(body, bots.Description.Length > 0 ? bots.Description : "在城镇里生成玩家机器人，陪人一起刷图。");
            if (settings.Count > 0)
            {
                Section(body, "可调的项目");
                foreach (var (label, value, description) in settings.Take(14))
                {
                    Card(body, label, description, value.Length > 0 ? $"默认 {value}" : "");
                }
            }
            Note(body, "这个服的联赛配置里目前没有勾选 PlayerBots，所以暂时不会生成机器人。");
        }, "");
    }

    /// <summary>The change log tab: the same file, opened at the top.</summary>
    public static void Notes(Window owner, string? path, int versions) => ChangeLog(owner, path, versions);

    public static void Faq(Window owner)
    {
        ContentWindow.Show(owner, "常见问题", body =>
        {
            Qa(body, "怎么注册账号？",
                "点主界面的「注册账号」，账号建在服务器上。服务器没开的时候会失败：等底栏那颗点变绿再试。");
            Qa(body, "点「开始游戏」提示服务器还没有开启？",
                "底栏那颗点变成绿色，才表示服务器已经就绪，这时再点「开始游戏」。还是灰的就说明服务器还没开：稍等一会儿再试，或者问一下开服的人。");
            Qa(body, "提示「连不上服务器」？",
                "服务器没开，或者网络不通。稍后重试；一直连不上就问一下开服的人。");
            Qa(body, "每次更新改了什么，在哪看？",
                "主界面公告栏就是本服的更新说明，点任意一条看那一版的详情，点「更多」看全部历史。");
        }, "");
    }

    public static void Community(Window owner, string server, bool local)
    {
        ContentWindow.Show(owner, "社区与目录", body =>
        {
            Lead(body, local
                ? "这是一个跑在本机的离线服务端：服务端、客户端、存档都在同一台机器上，不删档，随时下线随时接着玩。"
                : $"这是一个远程服务端：进入游戏后连接 {server}，登录在游戏窗口里完成。");
            Section(body, "登录器旁边的文件");
            Bullet(body, "Game.patched.exe", "游戏本体，点「开始游戏」拉起来的就是它。");
            Bullet(body, UpdateNotes.FileName, "本服的更新说明，就是这个窗口公告栏读取的那份文件。");
            Section(body, "这份说明怎么更新");
            Para(body, "每次发版时把仓库里的 `更新说明.md` 复制到登录器旁边即可，登录器每次启动都会重新读取。");
        }, "");
    }

    public static void Tools(Window owner)
    {
        ContentWindow.Show(owner, "精选攻略与常用工具", body =>
        {
            Section(body, "开始游戏之前");
            Bullet(body, "服务器状态", "主界面底栏那颗圆点：绿色表示服务器已经就绪，灰色表示还没开。灰的时候点「开始游戏」不会进去。");
            Bullet(body, "注册账号", "第一次玩先点「注册账号」，账号建在服务器上，注册好就能直接登录。");
            Bullet(body, "更新公告", "主界面右侧公告栏是这个服的更新说明，点任意一条看那一版改了什么，点「更多」看全部历史。");
            Section(body, "游戏内常用");
            Para(body, "聊天框里输入 `/help` 可以看到这个服开放的命令。");
            Para(body, "城镇里的传送点是换图的主要方式：点一下打开地图，选要去的地方。");
        }, "");
    }

    const string NoModsText = "没有读到这个服的玩法改动列表。";

    // ---- drawing -----------------------------------------------------------------------------------------------------

    static void Lead(StackPanel body, string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = Body, Margin = new Thickness(0, 0, 0, 14) };
        block.Text = text;
        body.Children.Add(block);
    }

    static void Section(StackPanel body, string title)
    {
        var block = new TextBlock { Text = title, FontSize = 14.5, FontWeight = FontWeights.SemiBold, Foreground = Strong, Margin = new Thickness(0, 12, 0, 8) };
        body.Children.Add(block);
    }

    static void Para(StackPanel body, string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = Body, Margin = new Thickness(0, 0, 0, 8) };
        body.Children.Add(block);
    }

    static void Note(StackPanel body, string text)
    {
        var border = new Border { BorderBrush = Accent, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 4, 0, 4), Margin = new Thickness(0, 10, 0, 8) };
        border.Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 20, Foreground = Faint };
        body.Children.Add(border);
    }

    static void Bullet(StackPanel body, string title, string text)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var dot = new TextBlock { Text = "•", Foreground = Faint, FontSize = 13, Margin = new Thickness(0, 0, 10, 0) };
        row.Children.Add(dot);
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = Body };
        block.Inlines.Add(new System.Windows.Documents.Run(title) { Foreground = Strong, FontWeight = FontWeights.SemiBold });
        block.Inlines.Add(new System.Windows.Documents.Run(" — " + text) { Foreground = Body });
        Grid.SetColumn(block, 1);
        row.Children.Add(block);
        body.Children.Add(row);
    }

    static void Steps(StackPanel body, params string[] steps)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var number = new TextBlock { Text = $"{i + 1}.", Foreground = Accent, FontSize = 13, Margin = new Thickness(0, 0, 10, 0) };
            row.Children.Add(number);
            var block = new TextBlock { Text = steps[i], TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21, Foreground = Body };
            Grid.SetColumn(block, 1);
            row.Children.Add(block);
            body.Children.Add(row);
        }
    }

    static void Qa(StackPanel body, string question, string answer)
    {
        Section(body, question);
        Para(body, answer);
    }

    static void ModCard(StackPanel body, ModFact mod)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        var title = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Strong, TextWrapping = TextWrapping.Wrap };
        title.Inlines.Add(new System.Windows.Documents.Run(mod.Name));
        if (mod.Version.Length > 0) title.Inlines.Add(new System.Windows.Documents.Run($"  v{mod.Version}") { Foreground = Faint, FontSize = 11.5 });
        text.Children.Add(title);
        if (mod.Description.Length > 0)
            text.Children.Add(new TextBlock { Text = mod.Description, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 19, Foreground = Faint, Margin = new Thickness(0, 4, 0, 0) });
        grid.Children.Add(text);

        var state = new TextBlock
        {
            Text = mod.Enabled ? "已启用" : "已停用",
            Foreground = mod.Enabled ? On : Faint,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(14, 1, 0, 0),
        };
        Grid.SetColumn(state, 1);
        grid.Children.Add(state);

        var card = new Border
        {
            Background = CardFill, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 0, 8), Child = grid,
        };
        body.Children.Add(card);
    }

    static void Card(StackPanel body, string title, string description, string badge)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Strong, TextWrapping = TextWrapping.Wrap });
        if (description.Length > 0)
            text.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 19, Foreground = Faint, Margin = new Thickness(0, 4, 0, 0) });
        grid.Children.Add(text);

        if (badge.Length > 0)
        {
            var value = new TextBlock { Text = badge, Foreground = Accent, FontSize = 12, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 1, 0, 0) };
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
        }

        body.Children.Add(new Border
        {
            Background = CardFill, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 7), Child = grid,
        });
    }
}
