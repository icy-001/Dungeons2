using System.IO;
using System.Security;

namespace Dungeons2.Services;

internal static class FileSafety
{
    public static bool IsFileError(Exception e) =>
        e is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

    public static bool Try(string operation, Action action)
    {
        try { action(); return true; }
        catch (Exception e) when (IsFileError(e))
        {
            LogService.Instance.LogError($"{operation}: {e.Message}");
            return false;
        }
    }
}
