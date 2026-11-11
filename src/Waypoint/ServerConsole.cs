using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Waypoint;

/// <summary>
/// The Gateway's console channel (<c>data/console.json</c>, 127.0.0.1): a chat command run on the running Gateway itself, on
/// the character in game, so neither the game client nor Frida is needed (<c>Agents/docs/dev/console-channel.md</c> in the
/// server). The Gateway answers with the lines it replied, which the console section prints; the player's own chat panel
/// shows them too.
/// </summary>
static class ServerConsole
{
    internal const string Marker = "data/console.json";

    /// <summary>The channel's port when a Gateway is listening there: null when the running Gateway has no channel (one
    /// built before it), none runs, or a marker stayed behind (a stopped Gateway).</summary>
    public static async Task<int?> PortAsync()
    {
        int port;
        try
        {
            if (!File.Exists(Marker)) return null;
            port = JsonNode.Parse(File.ReadAllText(Marker))?["port"]?.GetValue<int>() ?? 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
        if (port is <= 0 or > 65535) return null;
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromMilliseconds(600));
            return port;
        }
        catch (Exception e) when (e is SocketException or TimeoutException or IOException or ObjectDisposedException) { return null; }
    }

    /// <summary>One request, one answer: {"op":"run","line":"/item …","character":"…"} or {"op":"who"}.</summary>
    public static async Task<JsonObject> RequestAsync(int port, JsonObject request, TimeSpan timeout)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(timeout);
        await using var stream = tcp.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        await writer.WriteLineAsync(request.ToJsonString());
        string? line = await reader.ReadLineAsync().WaitAsync(timeout);
        if (line is null) throw new InvalidOperationException(L.Text("the console channel closed without an answer"));
        return JsonNode.Parse(line)?.AsObject() ?? throw new InvalidOperationException(L.Text("not an answer: {0}", line));
    }

    /// <summary>The lines a command answered, or its error (the channel's code: no_player, not_a_command …).</summary>
    public static (bool Ok, IReadOnlyList<string> Lines) Replies(JsonObject reply)
    {
        if (reply["ok"]?.GetValue<bool>() == true)
            return (true, [.. (reply["lines"] as JsonArray ?? []).Select(l => l?.GetValue<string>() ?? "")]);
        return (false, [reply["error"]?.GetValue<string>() ?? reply.ToJsonString()]);
    }
}
