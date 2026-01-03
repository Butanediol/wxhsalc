using System;
using System.Runtime.InteropServices;

namespace ClashXW.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    internal static class NativeMethods
    {
        // Menu creation and destruction
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyMenu(IntPtr hMenu);

        // Menu item manipulation
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

        // Show menu and get selection
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        // Cursor position
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        // Window functions
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Menu flags
        internal const uint MF_STRING = 0x00000000;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_GRAYED = 0x00000001;
        internal const uint MF_DISABLED = 0x00000002;
        internal const uint MF_BYPOSITION = 0x00000400;

        // TrackPopupMenuEx flags
        internal const uint TPM_LEFTALIGN = 0x0000;
        internal const uint TPM_RETURNCMD = 0x0100;
        internal const uint TPM_LEFTBUTTON = 0x0000;
        internal const uint TPM_RIGHTBUTTON = 0x0002;
        internal const uint TPM_NONOTIFY = 0x0080;
        internal const uint TPM_BOTTOMALIGN = 0x0020;

        // Messages
        internal const uint WM_NULL = 0x0000;
    }
}
