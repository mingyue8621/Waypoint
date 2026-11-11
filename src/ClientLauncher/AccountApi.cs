using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace LocalExile.ClientTool;

/// <summary>
/// The account service over the network: GameServer.Auth's registration listener (its own port, a JSON line per request)
/// — <c>{"op":"register","name":…,"password":…}</c> answered by <c>{"ok":true,"account":…}</c> or
/// <c>{"ok":false,"error":"NameTaken"}</c>, and <c>{"op":"count"}</c> by <c>{"ok":true,"count":N}</c>
/// (<c>src/GameServer.Auth/RegisterListener.cs</c>). The same shape as the dev host's "register", so an account made here
/// is the one the login screen accepts. Registering this way rather than in a local database is what lets the same window
/// serve the host and a player on another machine.
/// </summary>
static class AccountApi
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static async Task<AccountReply> RegisterAsync(ServerTarget server, string name, string password) =>
        (await SendAsync(server, new JsonObject { ["op"] = "register", ["name"] = name, ["password"] = password }.ToJsonString())).Reply;

    /// <summary>How many accounts the server has, or null when it cannot be read.</summary>
    public static async Task<int?> CountAsync(ServerTarget server)
    {
        var (reply, body) = await SendAsync(server, new JsonObject { ["op"] = "count" }.ToJsonString());
        return reply is { Reachable: true, Ok: true } && body?["count"] is JsonValue value && value.TryGetValue(out int count)
            ? count : null;
    }

    static async Task<(AccountReply Reply, JsonNode? Body)> SendAsync(ServerTarget server, string request)
    {
        try
        {
            using var client = new TcpClient();
            using var stop = new CancellationTokenSource(Timeout);
            await client.ConnectAsync(server.Host, server.RegisterPort, stop.Token);
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(request.AsMemory(), stop.Token);
            string? line = await reader.ReadLineAsync(stop.Token);
            if (string.IsNullOrWhiteSpace(line)) return (new(false, false, null, null), null);
            JsonNode? body = JsonNode.Parse(line);
            bool ok = body?["ok"]?.GetValue<bool>() ?? false;
            return (new(true, ok, body?["account"]?.GetValue<string>(), body?["error"]?.GetValue<string>()), body);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or ArgumentException
            or InvalidOperationException or System.Text.Json.JsonException or FormatException)
        {
            return (new(false, false, null, null), null);
        }
    }
}

/// <summary>What the account service said: unreachable, an account made, or the error with its code.</summary>
sealed record AccountReply(bool Reachable, bool Ok, string? Account, string? Error)
{
    /// <summary>The player's sentence for it.</summary>
    public string Describe(ServerTarget server) => this switch
    {
        { Reachable: false } => $"连不上服务器 {server.Host}:{server.RegisterPort} 的注册服务：确认服务器开着、防火墙放行，" +
            "并且登录服务（Auth）是运行中。",
        { Ok: true } => $"账号 {Account} 已创建，可以进入游戏了。",
        { Error: "NameTaken" } => "这个账号已经有人用了，换一个名字。",
        { Error: "InvalidName" } => "账号不合规则：3-32 位，只能用字母、数字、_ 或 -。",
        { Error: "WeakPassword" } => "密码太短：至少 4 位。",
        { Error: null or "" } => "注册失败：服务器没有说明原因。",
        { Error: var other } => $"注册失败：{other}",
    };
}
