using System.Text.Json;

namespace LocalExile.ClientTool;

/// <summary>One mod as the launcher shows it: its manifest, whether <c>mod-state.txt</c> has it on, and the Chinese the
/// server launcher's own catalog carries.</summary>
public sealed record ModFact(string Id, string Name, string Description, string Version, string Author, bool Enabled);

/// <summary>One gameplay module (<c>Modules/**/module.json</c>) with the deepest manifest folder for its id.</summary>
public sealed record ModuleFact(string Id, string Folder, string Name, string Description);

/// <summary>
/// What the install itself says: the mods beside the servers (<c>Mods/Binaries/*/mod.json</c> plus <c>mod-state.txt</c>) and
/// the gameplay modules (<c>Modules/**/module.json</c>). The launcher carries no list of its own, so a mod added, removed or
/// switched on the server shows up here as it is. Names and descriptions are the manifests' English, with the server
/// launcher's reviewed Chinese where it has it; a manifest this file has no Chinese for is shown as written.
/// </summary>
public static class InstallFacts
{
    static readonly string ModsFolder = Path.Combine("Mods", "Binaries");
    static readonly string StateFile = "mod-state.txt";

    public static string? ModsPath(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            try { if (Directory.Exists(Path.Combine(folder, ModsFolder))) return Path.Combine(folder, ModsFolder); }
            catch (Exception ex) when (ex is ArgumentException or IOException) { }
        }
        return null;
    }

    public static string? ModulesPath(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            try { if (Directory.Exists(Path.Combine(folder, "Modules"))) return Path.Combine(folder, "Modules"); }
            catch (Exception ex) when (ex is ArgumentException or IOException) { }
        }
        return null;
    }

    /// <summary>The mods in the install, on or off as its own choices file has them; empty when there is none.</summary>
    public static IReadOnlyList<ModFact> Mods(IEnumerable<string> folders)
    {
        string? root = ModsPath(folders);
        if (root is null) return [];
        var choices = Choices(Path.Combine(root, StateFile));
        var mods = new List<ModFact>();
        try
        {
            foreach (string folder in Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string manifest = Path.Combine(folder, "mod.json");
                if (!File.Exists(manifest)) continue;
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                    var rootElement = document.RootElement;
                    string id = Text(rootElement, "id") is { Length: > 0 } named ? named : Path.GetFileName(folder);
                    string name = Text(rootElement, "name");
                    string description = Text(rootElement, "description");
                    bool enabled = !rootElement.TryGetProperty("enabled", out var flag) || flag.ValueKind != JsonValueKind.False;
                    mods.Add(new ModFact(id, Chinese(name, id), Chinese(description, description),
                        Text(rootElement, "version").Length > 0 ? Text(rootElement, "version") : "0",
                        Text(rootElement, "author"), choices.TryGetValue(id, out bool chosen) ? chosen : enabled));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return mods;
    }

    /// <summary>The gameplay modules, by id; a module keeps its deepest manifest folder, as the server reads it.</summary>
    public static IReadOnlyList<ModuleFact> Modules(IEnumerable<string> folders)
    {
        string? root = ModulesPath(folders);
        if (root is null) return [];
        var modules = new Dictionary<string, ModuleFact>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string manifest in Directory.EnumerateFiles(root, "module.json", SearchOption.AllDirectories).Take(512))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                    string folder = Path.GetDirectoryName(manifest)!;
                    string id = Text(document.RootElement, "id") is { Length: > 0 } named ? named : Path.GetFileName(folder);
                    if (!modules.TryGetValue(id, out var previous) || folder.Length > previous.Folder.Length)
                        modules[id] = new ModuleFact(id, folder, Chinese(Text(document.RootElement, "name"), id), Chinese(Text(document.RootElement, "description"), Text(document.RootElement, "description")));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return [.. modules.Values.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>A module's settings as its declarations give them: label, default, and what it does.</summary>
    public static IReadOnlyList<(string Label, string Value, string Description)> ModuleSettings(string? folder)
    {
        var settings = new List<(string, string, string)>();
        if (folder is null) return settings;
        string path = Path.Combine(folder, "settings.json");
        if (!File.Exists(path)) return settings;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("settings", out var declared) || declared.ValueKind != JsonValueKind.Object) return settings;
            foreach (var setting in declared.EnumerateObject())
            {
                var definition = setting.Value;
                if (definition.ValueKind != JsonValueKind.Object) continue;
                string label = Chinese(Text(definition, "label"), setting.Name);
                string value = definition.TryGetProperty("default", out var given) ? given.ToString() : "";
                string unit = Text(definition, "unit");
                string format = Text(definition, "format");
                if (format == "percent" && double.TryParse(value, out double share)) value = $"{share * 100:0.#}%";
                else if (format == "multiplier") value = $"×{value}";
                else if (unit.Length > 0) value = $"{value} {unit}";
                settings.Add((label, value, Chinese(Text(definition, "description"), Text(definition, "description"))));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return settings;
    }

    /// <summary>The mod choices file: "id on" or "id off" per line.</summary>
    public static Dictionary<string, bool> Choices(string path)
    {
        var choices = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(path)) return choices;
            foreach (string line in File.ReadAllLines(path))
            {
                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words is [var id, "on" or "off"]) choices[id] = words[1] == "on";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return choices;
    }

    static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    /// <summary>The server launcher's Chinese for a manifest's English, which is what its Mods page shows; the English as
    /// written when this launcher has no line for it (a mod newer than this build).</summary>
    public static string Chinese(string english, string fallback)
    {
        if (english.Length == 0) return fallback;
        return Translated.TryGetValue(english, out string? chinese) ? chinese : english;
    }

    static readonly Dictionary<string, string> Translated = new(StringComparer.Ordinal)
    {
        ["Fast Campaign"] = "快速通关",
        ["A faster campaign: 5x kill experience, +50% movement speed, and /fast lists the commands that skip ahead."] =
            "更快的战役：击杀经验 ×5、移动速度 +50%，/fast 列出可跳过进度的命令。",
        ["Global Chat"] = "全球聊天",
        ["Talk to other LocalExile players: global, trade and whisper chat go through the LocalExile global chat server."] =
            "与其他 LocalExile 玩家交流：公共、交易与密语都走 LocalExile 全球聊天服务器。",
        ["GM Commands"] = "管理员命令",
        ["The development and testing chat commands: teleport, items, monsters, experience, quest flags, test hits and protocol probes."] =
            "开发与测试用的聊天命令：传送、物品、怪物、经验、任务旗标、测试命中和协议探针。",
        ["Item Quantity"] = "物品数量",
        ["10 times as many item drops from every kill."] = "每次击杀的掉落数量 ×10（倍率可在服务端启动器的「模组」页调整）。",
        ["Item Rarity"] = "物品稀有度",
        ["10 times the chance of magic, rare and unique equipment."] = "魔法、稀有与传奇装备的概率 ×10（倍率可在服务端启动器的「模组」页调整）。",
        ["Live Economy"] = "实时经济",
        ["Faustus's Currency Exchange as a live market: the official Path of Exile leagues' prices, a spread, depth, and orders that fill with time."] =
            "把浮士德的通货交易所做成实时市场：采用官方 Path of Exile 联赛的价格，含价差、深度与会随时间成交的订单。",
        ["POB"] = "POB 构筑导入",
        ["Preview and import a character build through a configured external PoB converter."] =
            "通过配置好的外部 PoB 转换器预览并导入角色构筑。",
        ["Waypoint Tools"] = "启动器工具",
        ["Lets Waypoint's tools reach the game without the client control bridge: the item crafter gives its items through it."] =
            "让启动器的工具不依赖客户端桥接就能作用于游戏：物品制作器通过它发放物品。",
        ["Ruthless"] = "无情",
        ["Player Bots"] = "玩家机器人",
        ["Atlas"] = "图集",
        ["Item drops"] = "物品掉落",
        ["Movement speed"] = "移动速度",
        ["Kill experience"] = "击杀经验",
        ["Most bots"] = "机器人上限",
        ["External AI control (MCP)"] = "外部 AI 控制（MCP）",
        ["A new bot every"] = "新建机器人间隔",
        ["First bot after"] = "首个机器人延迟",
    };
}
