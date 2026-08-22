using System.Runtime.InteropServices;

namespace Dungeons2
{
    internal static partial class Interop
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [LibraryImport("d3d11.dll")]
        public static partial int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        public static partial class User32
        {
            public const int WS_CHILD = 0x40000000;
            public const int WS_POPUP = unchecked((int)0x80000000);
            public const int GWL_STYLE = -16;

            public const int WS_EX_TRANSPARENT = 0x00000020;
            public const int GWL_EXSTYLE = -20;
            public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_NOZORDER = 0x0004;

            [LibraryImport("user32.dll")]
            public static partial IntPtr GetForegroundWindow();

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool GetClientRect(IntPtr hwnd, out RECT lpRect);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

            [LibraryImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [LibraryImport("user32.dll", SetLastError = true)]
            public static partial IntPtr SetParent(IntPtr hwndChild, IntPtr hwndNewParent);

            [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
            public static partial nint GetWindowLongPtr(IntPtr hwnd, int index);

            [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
            public static partial nint SetWindowLongPtr(IntPtr hwnd, int index, nint newStyle);
        }

        public static partial class DwmApi
        {
            public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

            [LibraryImport("dwmapi.dll")]
            public static partial int DwmGetWindowAttribute(
                IntPtr hwnd,
                uint dwAttribute,
                out RECT pvAttribute,
                uint cbAttribute);
        }
    }
}
