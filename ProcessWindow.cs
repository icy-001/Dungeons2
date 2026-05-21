using System;
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
        public Size Size
        {
            get
            {
                if (Interop.User32.GetClientRect(Handle, out var rect))
                    return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
                return Size.Empty;
            }
        }

        public bool Equals(ProcessWindow? other)
        {
            return Process?.Id == other?.Process?.Id;
        }

        public override string ToString()
        {
            var size = Size;
            return Process == null ? Title : $"[{size.Width}x{size.Height}] {Title}";
        }

        public override bool Equals(object? obj)
        {
            return obj is ProcessWindow x && Equals(x);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(ProcessWindow? left, ProcessWindow? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(ProcessWindow? left, ProcessWindow? right)
        {
            return !(left == right);
        }
    }
}
