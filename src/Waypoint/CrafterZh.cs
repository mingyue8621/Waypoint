using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Waypoint;

/// <summary>
/// The Chinese words the item crafter shows: base names, modifier names, the words a rare's name is made of, and the
/// game's wording of every stat, all from the client's own Chinese data (<c>data/*.datc64</c> and
/// <c>metadata/statdescriptions/*.txt</c>) rather than a translation of our own.
///
/// The tables the Gateway prints in its <c>crafter</c> mode are English (the server's data has no translations). Each
/// row here is keyed the way the Gateway names it: a base by its <c>Metadata/…</c> Id, a modifier by its mod Id, a name
/// word by list and hash, a description by the stats it covers. A missing key falls back to the English text, so a table
/// the client has not translated, or a base this build never saw, still reads.
///
/// <c>Data\crafter-zh.json.gz</c> is generated from the client by the server's <c>zhpatcher --crafter-zh</c>; the
/// translation only ever touches what the window shows, never the item the Gateway rolls or hands to the game.
/// </summary>
sealed class CrafterZh
{
    public static CrafterZh Current { get; } = Load();

    readonly Dictionary<string, string> _bases;
    readonly Dictionary<string, string> _mods;
    readonly Dictionary<string, string> _words;
    readonly Dictionary<string, string> _classes;
    readonly Dictionary<string, StatDescription> _descs;

    /// <summary>How many bases, modifiers, words and descriptions carry Chinese; for the log line.</summary>
    public int Bases => _bases.Count;
    public int Mods => _mods.Count;
    public int Words => _words.Count;
    public int Descs => _descs.Count;

    CrafterZh(Dictionary<string, string> bases, Dictionary<string, string> mods, Dictionary<string, string> words,
        Dictionary<string, string> classes, Dictionary<string, StatDescription> descs)
    {
        _bases = bases;
        _mods = mods;
        _words = words;
        _classes = classes;
        _descs = descs;
    }

    /// <summary>An empty table: every lookup falls back to the English text.</summary>
    public static CrafterZh Empty { get; } = new([], [], [], [], []);

    public string Base(string id, string english) => _bases.GetValueOrDefault(id) ?? english;
    public string Mod(string id, string english) => _mods.GetValueOrDefault(id) ?? english;
    public string Word(int list, uint key, string english) => _words.GetValueOrDefault($"{list}/{key}") ?? english;
    public string Class(string id, string english) => _classes.GetValueOrDefault(id) ?? english;

    /// <summary>The description of these stats in Chinese, or null when the client has no Chinese words for them.</summary>
    public StatDescription? Description(IReadOnlyList<string> stats) => _descs.GetValueOrDefault(Key(stats));

    /// <summary>Two descriptions cover the same stats: the order the Gateway lists them in is the client's order.</summary>
    static string Key(IReadOnlyList<string> stats) => string.Join('\u001f', stats);

    static CrafterZh Load()
    {
        try
        {
            using var packed = Assembly.GetExecutingAssembly().GetManifestResourceStream("Waypoint.crafter-zh.json.gz");
            if (packed is null) return Empty;
            using var stream = new GZipStream(packed, CompressionMode.Decompress);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var bases = Text(root, "bases");
            var mods = Text(root, "mods");
            var words = Text(root, "words");
            var classes = Text(root, "classes");

            var descs = new Dictionary<string, StatDescription>(StringComparer.Ordinal);
            if (root.TryGetProperty("descs", out var array))
            {
                foreach (var entry in array.EnumerateArray())
                {
                    var stats = entry.GetProperty("stats").EnumerateArray().Select(s => s.GetString() ?? "").ToList();
                    var variants = new List<DescriptionVariant>();
                    foreach (var variant in entry.GetProperty("variants").EnumerateArray())
                    {
                        var when = variant.GetProperty("when").EnumerateArray().Select(w => w.GetString() ?? "").ToList();
                        var handlers = new List<(string Name, int Stat)>();
                        foreach (var handler in variant.GetProperty("handlers").EnumerateArray())
                            handlers.Add((handler[0].GetString() ?? "", handler[1].GetInt32()));
                        variants.Add(new DescriptionVariant(when, variant.GetProperty("text").GetString() ?? "", handlers));
                    }
                    if (stats.Count > 0 && variants.Count > 0) descs.TryAdd(Key(stats), new StatDescription(stats, variants));
                }
            }
            return new CrafterZh(bases, mods, words, classes, descs);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or InvalidOperationException
                                       or KeyNotFoundException or FormatException)
        {
            // A damaged table only costs the Chinese wording; the window keeps working in English.
            return Empty;
        }
    }

    static Dictionary<string, string> Text(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(name, out var table)) return map;
        foreach (var entry in table.EnumerateObject())
            if (entry.Value.GetString() is { Length: > 0 } text) map[entry.Name] = text;
        return map;
    }
}
