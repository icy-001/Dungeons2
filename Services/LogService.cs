using System.Collections.ObjectModel;
using System.IO;

namespace Dungeons2.Services
{
    public class LogService
    {
        public static LogService Instance { get; } = new();

        public ObservableCollection<(DateTimeOffset, string)> Logs { get; } = [];

        public event Action<(DateTimeOffset, string)>? OnLogReceived;

        private LogService() { }

        public void Log(string message)
        {
            Logs.Add((DateTimeOffset.Now, message));
            OnLogReceived?.Invoke((DateTimeOffset.Now, message));
        }

        public void LogError(string message)
        {
            Logs.Add((DateTimeOffset.Now, message));
            // Logging a failed save must not fail for the same permission problem.
            try { File.AppendAllText("errors.log", $"[{DateTimeOffset.Now:G}] {message}\n"); }
            catch (Exception e) when (FileSafety.IsFileError(e)) { System.Diagnostics.Debug.WriteLine(e); }
            OnLogReceived?.Invoke((DateTimeOffset.Now, message));
        }
    }
}
