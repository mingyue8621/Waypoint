using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalExile.ClientTool;

/// <summary>One version section of 更新说明.md: its version, the date in the heading, the first line of the body as the
/// summary the announcement list shows, and the body as written.</summary>
public sealed record ReleaseNote(string Version, string Date, string Edition, string Summary, string Body)
{
    /// <summary>MM/DD for the announcement list's right-hand column; the date as written when it is not a date.</summary>
    public string Day => DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)
        ? when.ToString("MM/dd", CultureInfo.InvariantCulture) : Date;

    /// <summary>The heading the popup shows: the version, then the date.</summary>
    public string Heading => Date.Length > 0 ? $"{Version}（{Date}{Edition}）" : Version;
}

/// <summary>
/// The server's own change log, <c>更新说明.md</c>, beside this executable (or in a folder above it, as the host's copy is
/// laid out). The announcement panel lists its newest sections and each opens as a popup; the whole file is the change log
/// popup. Nothing about the notes is written into the launcher: a release that ships a new 更新说明.md shows it as it is.
/// </summary>
public static class UpdateNotes
{
    public const string FileName = "更新说明.md";

    /// <summary>Where the change log is: the first folder of <paramref name="folders"/> that holds it, or null.</summary>
    public static string? Locate(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            try
            {
                string path = Path.Combine(folder, FileName);
                if (File.Exists(path)) return path;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException) { }
        }
        return null;
    }

    /// <summary>The notes in a file, newest first as the file has them; empty when it cannot be read.</summary>
    public static IReadOnlyList<ReleaseNote> Load(string? path)
    {
        if (string.IsNullOrEmpty(path)) return [];
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    // A version heading: "## 0.8.58-upstream（2026-11-11，当前版本）". The date and the note after it are optional, and the
    // version may hold spaces ("0.5.1 ~ 0.5.6"), so a heading this launcher does not recognize still becomes a section with
    // the whole heading as its version. A heading that is not a version ("## 更新方式") is not a section at all.
    static readonly Regex VersionHeading = new(
        @"^##\s+(?<version>[^（(]+?)\s*(?:[（(]\s*(?<date>\d{4}-\d{1,2}-\d{1,2})\s*(?<edition>[^）)]*)[）)])?\s*$",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<ReleaseNote> Parse(string text)
    {
        var notes = new List<ReleaseNote>();
        string? version = null, date = "", edition = "";
        var body = new List<string>();
        void Flush()
        {
            // The file ends with sections that are not versions ("## 更新方式", "## 注意事项"); a version starts with a digit.
            if (version is null || !char.IsAsciiDigit(version[0])) return;
            string summary = body.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? "";
            summary = Plain(summary);
            notes.Add(new ReleaseNote(version, date, edition.Trim().TrimStart('，', ',').Trim(), summary, string.Join("\n", body).Trim()));
        }
        foreach (string raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var match = VersionHeading.Match(line);
                Flush();
                body.Clear();
                if (match.Success)
                {
                    version = match.Groups["version"].Value;
                    date = match.Groups["date"].Success ? Normalize(match.Groups["date"].Value) : "";
                    edition = match.Groups["edition"].Success ? match.Groups["edition"].Value : "";
                }
                else
                {
                    version = line[3..].Trim();
                    date = ""; edition = "";
                }
                continue;
            }
            // The file's own title and its preface are not a version.
            if (version is not null) body.Add(line);
        }
        Flush();
        return notes;
    }

    static string Normalize(string date)
    {
        var parts = date.Split('-');
        return parts.Length == 3 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out int month) && int.TryParse(parts[2], out int day)
            ? $"{parts[0]}-{month:00}-{day:00}" : date;
    }

    /// <summary>The text without the markdown that would read as noise in one line of the announcement list.</summary>
    public static string Plain(string markdown) =>
        markdown.Replace("**", "").Replace("`", "").Replace("~~", "").Trim().TrimStart('-', '*', '#', ' ').Trim();
}
