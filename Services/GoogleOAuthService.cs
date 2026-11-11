using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dungeons2.Services;

internal sealed class GoogleOAuthService(HttpClient http, GoogleTokenStore store)
{
    internal const string Scope = "https://www.googleapis.com/auth/drive.file";
    private GoogleTokens? tokens;
    public GoogleTokens? Tokens => tokens;

    public void Load() => tokens = store.Load();

    private static async Task<(string Id, string Secret)> ReadClientAsync(CancellationToken ct)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "credentials.json");
        if (!File.Exists(path)) throw new InvalidOperationException("Place desktop OAuth credentials.json next to Dungeons2.exe, then try again.");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        if (!json.RootElement.TryGetProperty("installed", out var installed) ||
            !installed.TryGetProperty("client_id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
            throw new InvalidOperationException("credentials.json must contain a Desktop app OAuth client (installed), not a service account or web client.");
        return (id.GetString()!, installed.TryGetProperty("client_secret", out var secret) ? secret.GetString() ?? "" : "");
    }

    public async Task<GoogleTokens> PickAsync(CancellationToken ct)
    {
        var client = await ReadClientAsync(ct);
        string verifier = GooglePickerCallback.Base64Url(RandomNumberGenerator.GetBytes(32));
        var (parameters, redirectUri) = await GooglePickerCallback.ReceiveAsync(client.Id, verifier, ct);
        if (parameters.ContainsKey("error")) throw new InvalidOperationException("Google authorization was cancelled or denied. Your existing connection was not changed.");
        if (!parameters.TryGetValue("picked_file_ids", out var ids) || string.IsNullOrWhiteSpace(ids))
            throw new InvalidOperationException("No spreadsheet was selected. Your existing connection was not changed.");
        string[] selected = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (selected.Length != 1 || !IsFileId(selected[0])) throw new InvalidOperationException("Select exactly one Google spreadsheet.");
        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Google did not return an authorization code. Try connecting again.");
        return await ExchangeAsync(new()
        {
            ["client_id"] = client.Id, ["client_secret"] = client.Secret,
            ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }, client.Id, selected[0], "", null, ct);
    }

    internal static bool IsFileId(string value) => value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    public void Commit(GoogleTokens value)
    {
        store.Save(value);
        tokens = value;
    }

    public async Task<string> AccessTokenAsync(CancellationToken ct, bool forceRefresh = false)
    {
        var current = tokens ?? throw new InvalidOperationException("Connect Google Sheets in Settings first.");
        if (!forceRefresh && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return current.AccessToken;
        var client = await ReadClientAsync(ct);
        if (client.Id != current.ClientId) throw new InvalidOperationException("The OAuth client changed. Select the spreadsheet again.");
        var refreshed = await ExchangeAsync(new()
        {
            ["client_id"] = client.Id, ["client_secret"] = client.Secret,
            ["refresh_token"] = current.RefreshToken, ["grant_type"] = "refresh_token"
        }, client.Id, current.SpreadsheetId, current.SpreadsheetName, current.RefreshToken, ct);
        Commit(refreshed);
        return refreshed.AccessToken;
    }

    private async Task<GoogleTokens> ExchangeAsync(Dictionary<string, string> fields, string clientId,
        string spreadsheetId, string name, string? previousRefreshToken, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(fields);
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Google authorization expired, was revoked, or the OAuth configuration is invalid. Select the spreadsheet again.");
            throw new HttpRequestException("Google token service is unavailable. Try again later.", null, response.StatusCode);
        }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (root.TryGetProperty("scope", out var scopes) && scopes.GetString() != Scope)
            throw new InvalidOperationException("Google returned unexpected permissions. Only drive.file is accepted. Remove the old app grant in your Google account and reconnect.");
        string refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : previousRefreshToken ?? "";
        if (string.IsNullOrWhiteSpace(refresh)) throw new InvalidOperationException("Google did not issue an offline token. Reconnect and grant consent.");
        return new(clientId, root.GetProperty("access_token").GetString()!, refresh,
            DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()), spreadsheetId, name);
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct)
    {
        var previous = tokens;
        // Clear local access even if the network is offline. Never log the revocation request body.
        store.Delete();
        tokens = null;
        if (previous is null) return true;
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = previous.RefreshToken });
            using var response = await http.PostAsync("https://oauth2.googleapis.com/revoke", content, ct);
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { return false; }
    }
}
