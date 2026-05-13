using System;
using System.Runtime.InteropServices;

namespace ClashXW.Native
{
    

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        // ShowWindow commands
        internal const int SW_HIDE = 0;
        internal const int SW_SHOWNORMAL = 1;
        internal const int SW_SHOWMINIMIZED = 2;
        internal const int SW_SHOWMAXIMIZED = 3;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

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

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Hooks
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetCurrentThreadId();

        // Keyboard state
        [DllImport("user32.dll")]
        internal static extern short GetKeyState(int nVirtKey);

        // Menu flags
        internal const uint MF_STRING = 0x00000000;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_GRAYED = 0x00000001;
        internal const uint MF_DISABLED = 0x00000002;
        internal const uint MF_BYPOSITION = 0x00000400;

        // Menu break flags for multi-column layout
        internal const uint MF_MENUBREAK = 0x00000040;      // Column break without separator
        internal const uint MF_MENUBARBREAK = 0x00000020;   // Column break with separator

        // TrackPopupMenuEx flags
        internal const uint TPM_LEFTALIGN = 0x0000;
        internal const uint TPM_RETURNCMD = 0x0100;
        internal const uint TPM_LEFTBUTTON = 0x0000;
        internal const uint TPM_RIGHTBUTTON = 0x0002;
        internal const uint TPM_NONOTIFY = 0x0080;
        internal const uint TPM_BOTTOMALIGN = 0x0020;

        // Messages
        internal const uint WM_NULL = 0x0000;
        internal const uint WM_KEYDOWN = 0x0100;
        internal const uint WM_SYSKEYDOWN = 0x0104;
        internal const uint WM_CANCELMODE = 0x001F;

        // Hook types
        internal const int WH_MSGFILTER = -1;

        // Message filter codes
        internal const int MSGF_MENU = 2;

        // Virtual key codes
        internal const int VK_CONTROL = 0x11;
        internal const int VK_MENU = 0x12; // Alt key

        // System color indices (for SetSysColors/GetSysColor)
        internal const int COLOR_MENU = 4;
        internal const int COLOR_MENUBAR = 30;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

        [DllImport("user32.dll")]
        internal static extern int GetSysColor(int nIndex);
    }
}
