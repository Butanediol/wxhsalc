using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClashXW.Native
{
    internal class Win32Menu : IDisposable
    {
        private IntPtr _hMenu;
        private readonly List<IntPtr> _subMenus = new();
        private readonly Dictionary<uint, Action> _actions;
        private readonly Dictionary<(Keys key, bool ctrl, bool alt), uint> _shortcuts;
        private readonly Win32Menu? _parent;
        private uint _nextId;
        private bool _disposed;
        private bool _isSubMenu;

        // Hook state (static because hook callback must be static-compatible)
        private static IntPtr _hookHandle;
        private static HookProc? _hookProc;
        private static Win32Menu? _activeMenu;
        private static IntPtr _menuWindow;
        private static uint? _shortcutCommandId;

        public IntPtr Handle => _hMenu;

        public Win32Menu(uint startId = 1000)
        {
            _hMenu = NativeMethods.CreatePopupMenu();
            _nextId = startId;
            _actions = new Dictionary<uint, Action>();
            _shortcuts = new Dictionary<(Keys, bool, bool), uint>();
            _parent = null;
            _isSubMenu = false;
        }

        private Win32Menu(IntPtr handle, Win32Menu parent)
        {
            _hMenu = handle;
            _nextId = parent._nextId;
            _actions = parent._actions; // Share actions with parent
            _shortcuts = parent._shortcuts; // Share shortcuts with parent
            _parent = parent;
            _isSubMenu = true;
        }

        public uint AddItem(string text, Action action, bool isChecked = false, bool isEnabled = true)
        {
            var id = _nextId++;
            _actions[id] = action;
            SyncNextIdToParent();

            uint flags = NativeMethods.MF_STRING;
            if (isChecked) flags |= NativeMethods.MF_CHECKED;
            if (!isEnabled) flags |= NativeMethods.MF_GRAYED;

            NativeMethods.AppendMenu(_hMenu, flags, (UIntPtr)id, text);
            return id;
        }

        public uint AddItemWithColumnBreak(string text, Action action, bool isChecked = false, bool isEnabled = true)
        {
            var id = _nextId++;
            _actions[id] = action;
            SyncNextIdToParent();

            uint flags = NativeMethods.MF_STRING | NativeMethods.MF_MENUBARBREAK;
            if (isChecked) flags |= NativeMethods.MF_CHECKED;
            if (!isEnabled) flags |= NativeMethods.MF_GRAYED;

            NativeMethods.AppendMenu(_hMenu, flags, (UIntPtr)id, text);
            return id;
        }

        public uint AddItemWithShortcut(string text, Action action, Keys key, bool ctrl, bool alt, bool isChecked = false, bool isEnabled = true)
        {
            var id = AddItem(text, action, isChecked, isEnabled);
            var root = GetRoot();
            root._shortcuts[(key, ctrl, alt)] = id;
            return id;
        }

        private Win32Menu GetRoot()
        {
            return _parent != null ? _parent.GetRoot() : this;
        }

        public void AddSeparator()
        {
            NativeMethods.AppendMenu(_hMenu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
        }

        public Win32Menu AddSubMenu(string text)
        {
            var subMenuHandle = NativeMethods.CreatePopupMenu();
            _subMenus.Add(subMenuHandle);

            NativeMethods.AppendMenu(_hMenu, NativeMethods.MF_STRING | NativeMethods.MF_POPUP,
                (UIntPtr)subMenuHandle, text);

            return new Win32Menu(subMenuHandle, this);
        }

        private void SyncNextIdToParent()
        {
            if (_parent != null)
            {
                _parent._nextId = _nextId;
                _parent.SyncNextIdToParent();
            }
        }

        public uint? Show(IntPtr hwnd)
        {
            NativeMethods.GetCursorPos(out var pt);

            // Required for menu to close properly when clicking outside
            NativeMethods.SetForegroundWindow(hwnd);

            // Install message filter hook for keyboard shortcuts
            _activeMenu = this;
            _menuWindow = hwnd;
            _shortcutCommandId = null;
            _hookProc = MessageFilterProc;
            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MSGFILTER,
                _hookProc,
                IntPtr.Zero,
                NativeMethods.GetCurrentThreadId());

            try
            {
                var cmd = NativeMethods.TrackPopupMenuEx(
                    _hMenu,
                    NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN,
                    pt.X, pt.Y,
                    hwnd,
                    IntPtr.Zero);

                // Post a null message to force the window to process pending messages
                NativeMethods.PostMessage(hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                // If a shortcut was triggered, return that command instead
                if (_shortcutCommandId.HasValue)
                {
                    return _shortcutCommandId.Value;
                }

                return cmd > 0 ? (uint)cmd : null;
            }
            finally
            {
                // Remove hook
                if (_hookHandle != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }
                _hookProc = null;
                _activeMenu = null;
            }
        }

        private static IntPtr MessageFilterProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == NativeMethods.MSGF_MENU && _activeMenu != null)
            {
                var msg = Marshal.PtrToStructure<NativeMethods.MSG>(lParam);

                if (msg.message == NativeMethods.WM_KEYDOWN || msg.message == NativeMethods.WM_SYSKEYDOWN)
                {
                    var key = (Keys)(int)msg.wParam;
                    var ctrlPressed = (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                    var altPressed = (NativeMethods.GetKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;

                    if (_activeMenu._shortcuts.TryGetValue((key, ctrlPressed, altPressed), out var commandId))
                    {
                        _shortcutCommandId = commandId;
                        // Cancel the menu
                        NativeMethods.SendMessage(_menuWindow, NativeMethods.WM_CANCELMODE, IntPtr.Zero, IntPtr.Zero);
                        return (IntPtr)1; // Handled
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public bool ExecuteCommand(uint commandId)
        {
            if (_actions.TryGetValue(commandId, out var action))
            {
                action();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var subMenu in _subMenus)
            {
                NativeMethods.DestroyMenu(subMenu);
            }
            _subMenus.Clear();

            if (_hMenu != IntPtr.Zero && !_isSubMenu)
            {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }

            // Only clear actions on root menu
            if (!_isSubMenu)
            {
                _actions.Clear();
                _shortcuts.Clear();
            }
        }
    }
}
