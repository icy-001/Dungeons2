using System.Diagnostics;
using System.Drawing;

namespace Dungeons2
{
    public class ProcessWindow(Process process) : IEquatable<ProcessWindow>
    {
        public Process Process { get; } = process;
        public bool HasExited => Process?.HasExited ?? false;
        public IntPtr Handle => Process?.MainWindowHandle ?? IntPtr.Zero;
        public string Title => Process?.MainWindowTitle ?? "Entire screen";
        public Size Size => Interop.User32.GetClientRect(Handle, out var rect) ? new Size(rect.Right - rect.Left, rect.Bottom - rect.Top) : Size.Empty;

        public bool Equals(ProcessWindow? other) => Process?.Id == other?.Process?.Id;

        public override string ToString()
        {
            var size = Size;
            return Process is null ? Title : $"[{size.Width}x{size.Height}] {Title}";
        }

        public override bool Equals(object? obj) => obj is ProcessWindow x && Equals(x);

        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(ProcessWindow? left, ProcessWindow? right) => left?.Equals(right) ?? (right is null);

        public static bool operator !=(ProcessWindow? left, ProcessWindow? right) => !(left == right);
    }
}
