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
            Lead(body, "这些是服务端当前安装的模组，启用状态直接读服务端的 `Mods\\Binaries\\mod-state.txt`。要开关或改倍率，在「流放之路服务端.exe」的「模组」页操作，改完重启服务端生效。");
            foreach (var mod in mods) ModCard(body, mod);
        }, "数据来源：Mods\\Binaries");
    }

    /// <summary>Only what changes drops: the quantity and rarity mods, plus fast campaign's experience.</summary>
    public static void Drops(Window owner, IReadOnlyList<ModFact> mods)
    {
        var shown = mods.Where(m => m.Id is "itemquantity" or "itemrarity" or "fastcampaign").ToList();
        ContentWindow.Show(owner, "掉落调整", body =>
        {
            Lead(body, "影响掉落与收益的模组。每条后面是它当前是否启用：");
            if (shown.Count == 0) { Para(body, NoModsText); }
            foreach (var mod in shown) ModCard(body, mod);
            Section(body, "倍率怎么改");
            Para(body, "「物品数量」与「物品稀有度」的倍率是可选的下拉框（×2 / ×4 / ×6 / ×8 / ×10），不再是写死的 ×10：");
            Steps(body,
                "打开「流放之路服务端.exe」，进「模组」页。",
                "点开「物品数量」或「物品稀有度」，右侧设置里选一个倍率，点保存。",
                "确认这一行左边的勾选框是勾上的 —— 没勾就不会生效。",
                "服务端在跑的话会自己重载该模组；没在跑就在下次启动时生效。");
        }, "数据来源：Mods\\Binaries\\mod-state.txt 与各模组的 settings.json");
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
                Para(body, "这个服务端没有安装「玩家机器人」模块（`Modules\\Custom\\PlayerBots`）。");
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
            Section(body, "怎么开");
            Steps(body,
                "机器人是「模块」，不是「模组」：它的开关跟着联赛走。",
                "在「流放之路服务端.exe」进「联赛」页，选中你要玩的联赛，把 PlayerBots 勾上并保存。",
                "回到「模块」页，选中 PlayerBots，即可在这里列出的各项上改数值。",
                "改完重启服务端；机器人会按「首个机器人延迟」「新建机器人间隔」陆续出现。");
            Note(body, "当前这个服务端的联赛配置里没有勾选 PlayerBots，所以即使改了设置也不会生成机器人 —— 先按上面第 2 步勾上。");
        }, "数据来源：Modules\\Custom\\PlayerBots");
    }

    /// <summary>The change log tab: the same file, opened at the top.</summary>
    public static void Notes(Window owner, string? path, int versions) => ChangeLog(owner, path, versions);

    public static void Faq(Window owner)
    {
        ContentWindow.Show(owner, "常见问题", body =>
        {
            Qa(body, "怎么注册账号？",
                "点主界面的「注册账号」，账号建在服务器上。服务端没跑的时候会失败：先在「流放之路服务端.exe」把服务端启动起来。");
            Qa(body, "点「开始游戏」之后一直没反应？",
                "登录器在服务端文件夹里时，会先用 LocalExile.exe 把服务端拉起来（第一次约一分钟），状态栏会依次出现「正在准备并启动」「服务端已就绪，正在启动客户端」。登录器单独放在客户端文件夹里时，它不会启动服务端：先在「流放之路服务端.exe」里启动服务端，再点开始游戏。超过两分钟还没进游戏，去看服务端启动器的「日志」页。");
            Qa(body, "提示「连不上服务器」？",
                "补丁服务的 TCP 端口没开：在服务端启动器的「控制」页确认 Patch 在运行；发给别人的客户端还要确认地址和端口转发。");
            Qa(body, "怎么开掉落 ×10、快速通关这些？",
                "在「流放之路服务端.exe」的「模组」页勾选，改完重进游戏才生效。掉落数量/稀有度的倍率可以在那里选 ×2 到 ×10。");
            Qa(body, "模块页为什么大部分改不了？",
                "模块是玩法系统，只有声明了设置项的模块才有东西可改；模块的开与关由「联赛」页决定，不在模块页。");
            Qa(body, "存档在哪？会不会被覆盖？",
                "存档是服务端目录下的 `data\\saves.db`。服务端启动器的「备份」页可以备份和还原；升级前建议先备一份。");
            Qa(body, "每次更新改了什么，在哪看？",
                "主界面公告栏就是服务端的更新说明，点任意一条看那一版的详情，点「更多」看全部历史。");
        }, "");
    }

    public static void Community(Window owner, string server, bool local)
    {
        ContentWindow.Show(owner, "社区与目录", body =>
        {
            Lead(body, local
                ? "这是一个跑在本机的离线服务端：服务端、客户端、存档都在同一台机器上，不删档，随时下线随时接着玩。"
                : $"这是一个远程服务端：进入游戏后连接 {server}，登录在游戏窗口里完成。");
            Section(body, "文件都在哪");
            Bullet(body, "流放之路服务端.exe", "控制、模组、模块、联赛、备份、账号、性能、日志、控制台、工具、设置。");
            Bullet(body, "Game.patched.exe", "游戏客户端本体，就在登录器旁边（登录器放进服务端目录时它在 Client\\ 里），点「开始游戏」拉起来的就是它。");
            Bullet(body, "data\\saves.db", "账号与角色存档。别删；升级前先在启动器里备份。");
            Bullet(body, "logs\\", "服务端与客户端的日志，出问题时先看这里。");
            Bullet(body, UpdateNotes.FileName, "服务端更新说明，就是本窗口公告栏读取的那份文件。");
            Section(body, "这份说明怎么更新");
            Para(body, "服务端每发一版，把仓库里的 `更新说明.md` 复制到登录器旁边即可（登录器在客户端目录里时，就放在客户端目录），登录器每次启动都会重新读取。");
        }, "");
    }

    public static void Tools(Window owner, IReadOnlyList<ModFact> mods)
    {
        var gm = mods.FirstOrDefault(m => m.Id is "gm-commands" or "waypoint-tools");
        ContentWindow.Show(owner, "精选攻略与常用工具", body =>
        {
            Section(body, "服务端启动器里能做的事");
            Bullet(body, "控制", "逐个启停 Patch / Gateway / Auth，看服务是否就绪。");
            Bullet(body, "模组", "开关模组、改模组设置（掉落倍率、快速通关倍率、实时经济参数）。");
            Bullet(body, "模块 / 联赛", "看玩法模块；在联赛里勾选某个联赛要启用哪些模块。");
            Bullet(body, "备份 / 账号", "备份与还原存档；管理服务器上的账号。");
            Bullet(body, "性能 / 服务器耗时 / 日志 / 控制台", "看负载、看每次请求的耗时、看日志、直接发命令。");
            foreach (var mod in mods.Where(m => m.Id is "gm-commands" or "waypoint-tools"))
            {
                Section(body, mod.Name);
                Para(body, mod.Description);
            }
            if (gm is null) Para(body, "这个服务端没有安装「管理员命令」模组。");
            Section(body, "游戏内常用");
            Para(body, "聊天框里输入 `/help` 看可用命令；装了「管理员命令」模组的话，传送、发物品、加经验这些都在里面。");
        }, "");
    }

    const string NoModsText = "没找到模组目录（`Mods\\Binaries`）。请把登录器放在服务端目录旁边。";

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
