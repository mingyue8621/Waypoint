using System.Net.NetworkInformation;
using System.Text.Json;

namespace Launcher.Core;

/// <summary>
/// What 流放之路服务端.exe and 流放之路登录器.exe share: which checkout they drive, where that path is kept, and
/// whether the servers listen. Both drive the same adapter as Waypoint (<c>scripts/RuntimeBridge.ps1</c>, unpacked by
/// <see cref="BridgeScripts"/>), so a checkout is all either of them needs to start with; neither carries Waypoint's
/// installation, update or setup screens.
/// </summary>
public static class SoloLaunch
{
    /// <summary>The saved checkout, in a file of its own: Waypoint's settings.json has another shape and must not be rewritten.</summary>
    public sealed record SavedPaths(string Repository = "");

    public static string SettingsPath => Path.Combine(SettingsStore.DefaultDataDirectory, "launchers.json");

    /// <summary>The checkout saved by either of the two launchers, when it still exists.</summary>
    public static string? SavedRepository()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var saved = JsonSerializer.Deserialize<SavedPaths>(File.ReadAllText(SettingsPath), SettingsStore.JsonOptions);
            return string.IsNullOrWhiteSpace(saved?.Repository) ? null : saved.Repository;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public static void SaveRepository(string root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new SavedPaths(root), SettingsStore.JsonOptions));
    }

    /// <summary>
    /// The checkout to drive: the one saved, else this process's working folder, else this executable's folder or a parent
    /// of it (a build inside the checkout). Null when none of them is a LocalExile checkout: the window then asks for one.
    /// </summary>
    public static string? FindRepository(string? saved = null)
    {
        foreach (string? candidate in Candidates(saved))
            if (!string.IsNullOrWhiteSpace(candidate) && RepositoryDiscovery.IsLocalExile(candidate)) return Path.GetFullPath(candidate);
        return null;
    }

    static IEnumerable<string?> Candidates(string? saved)
    {
        yield return saved;
        yield return SavedRepository();
        yield return Environment.CurrentDirectory;
        for (string? dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
             !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir)) yield return dir;
    }

    /// <summary>Whether something listens on that TCP port: the servers' reachability without asking the adapter.</summary>
    public static bool Listening(int port)
    {
        try { return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port); }
        catch (NetworkInformationException) { return false; }
    }

    /// <summary>The port a service uses, overridable the way the servers' own scripts and Waypoint read it.</summary>
    public static int Port(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out int port) && port is > 0 and < 65536 ? port : fallback;

    public static int AuthPort => Port("Game_LOGIN_PORT", 20493);
    public static int GatewayPort => Port("Game_GATEWAY_PORT", 6112);
    public static int PatchPort => Port("Game_PATCH_TCP_PORT", 12995) ;
    public static int DevPort => Port("Game_DEV_PORT", 7000);

    /// <summary>The game client's executable: the patched client the adapter starts.</summary>
    public const string ClientProcess = "Game.patched.exe";

    /// <summary>Whether the game client runs now.</summary>
    public static bool ClientRunning()
    {
        try { return System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ClientProcess)).Length > 0; }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
}
