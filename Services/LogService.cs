using System.Collections.ObjectModel;

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
    }
}
