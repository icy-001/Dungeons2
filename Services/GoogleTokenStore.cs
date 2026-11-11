using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dungeons2.Services;

// No tokens are stored in settings.json or in the application's working directory.
internal sealed class GoogleTokenStore(string path)
{
    public GoogleTokens? Load()
    {
        if (!File.Exists(path)) return null;
        byte[] plaintext = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<GoogleTokens>(plaintext); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void Save(GoogleTokens tokens)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(tokens);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void Delete()
    {
        File.Delete(path);
        File.Delete(path + ".tmp");
    }
}

internal sealed record GoogleTokens(string ClientId, string AccessToken, string RefreshToken,
    DateTimeOffset ExpiresAt, string SpreadsheetId, string SpreadsheetName);
