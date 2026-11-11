using System.Text.Json;

namespace LocalExile.ClientTool;

/// <summary>
/// Where the game and the account service are: <c>server.json</c> beside the launcher,
/// <code>{ "server": "203.0.113.7", "patchPort": 12995, "registerPort": 20497 }</code>, every field optional. No file
/// means this machine, which is the host's own copy.
/// </summary>
public sealed record ServerTarget(string Host, int PatchPort, int RegisterPort)
{
    /// <summary>The patch server's TCP port (Ports.Patch): what the client's own update screen talks to.</summary>
    public const int DefaultPatchPort = 12995;

    /// <summary>GameServer.Auth's registration listener (Game_AUTH_REGISTER_PORT).</summary>
    public const int DefaultRegisterPort = 20497;

    public bool IsLocal => Host is "127.0.0.1" or "localhost" or "::1" or "0.0.0.0";

    public static ServerTarget Load(int fallbackPatchPort)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "server.json");
        string host = "127.0.0.1";
        int patch = 0, register = 0;
        if (File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.TryGetProperty("server", out var server) && server.ValueKind == JsonValueKind.String
                    && server.GetString()?.Trim() is { Length: > 0 } named) host = named;
                if (root.TryGetProperty("patchPort", out var p) && p.TryGetInt32(out int patchValue)) patch = patchValue;
                if (root.TryGetProperty("registerPort", out var r) && r.TryGetInt32(out int registerValue)) register = registerValue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        }
        return new(host, patch > 0 ? patch : fallbackPatchPort, register > 0 ? register : DefaultRegisterPort);
    }
}
