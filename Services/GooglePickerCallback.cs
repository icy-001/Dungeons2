using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Dungeons2.Services;

// Bind the socket before opening the browser. Port zero avoids a port-reservation race
// and only the loopback interface is exposed. No HTTP.sys URL ACL or administrator is needed.
internal static class GooglePickerCallback
{
    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string AuthorizationUrl(string clientId, string redirectUri, string state, string verifier)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = GoogleOAuthService.Scope,
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "false",
            ["prompt"] = "consent",
            ["trigger_onepick"] = "true",
            ["allow_multiple"] = "false",
            ["mimetypes"] = "application/vnd.google-apps.spreadsheet",
            ["state"] = state,
            ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256"
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&",
            parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    internal static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            string key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            string value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "";
            if (!result.TryAdd(key, value)) throw new InvalidDataException("Duplicate OAuth callback parameter.");
        }
        return result;
    }

    public static async Task<(Dictionary<string, string> Parameters, string RedirectUri)> ReceiveAsync(
        string clientId, string verifier, CancellationToken cancellationToken, Action<string>? openBrowser = null)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string redirectUri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
        string state = Base64Url(RandomNumberGenerator.GetBytes(32));
        string url = AuthorizationUrl(clientId, redirectUri, state, verifier);
        if (openBrowser is not null) openBrowser(url);
        else Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var stream = client.GetStream();
                // Limit request size, including a malicious/unfinished request from another local process.
                byte[] buffer = new byte[16384];
                int length = 0;
                while (length < buffer.Length)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(length), deadline.Token);
                    if (read == 0) break;
                    length += read;
                    if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n")) break;
                }
                string request = Encoding.ASCII.GetString(buffer, 0, length);
                string[] line = request.Split("\r\n", 2)[0].Split(' ');
                Dictionary<string, string>? parameters = null;
                if (line.Length == 3 && line[0] == "GET" && line[1].StartsWith("/?", StringComparison.Ordinal))
                {
                    parameters = ParseQuery(line[1][2..]);
                    if (!parameters.TryGetValue("state", out var returnedState) || returnedState != state)
                        parameters = null;
                }
                string message = parameters is null ? "Invalid callback. Return to the Google sign-in tab."
                    : "Return to Dungeons2 to see the connection result. You can close this tab.";
                byte[] response = Encoding.UTF8.GetBytes($"HTTP/1.1 {(parameters is null ? "400 Bad Request" : "200 OK")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: {Encoding.UTF8.GetByteCount(message)}\r\n\r\n{message}");
                // A closed browser connection must not discard a valid authorization callback.
                try { await stream.WriteAsync(response, deadline.Token); }
                catch (IOException) { }
                if (parameters is not null) return (parameters, redirectUri);
            }
            catch (Exception e) when (e is IOException or FormatException ||
                e is OperationCanceledException && !cancellationToken.IsCancellationRequested) { }
        }
    }
}
