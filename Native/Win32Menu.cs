using System;
using System.Collections.Generic;

namespace ClashXW.Native
{
    internal class Win32Menu : IDisposable
    {
        private IntPtr _hMenu;
        private readonly List<IntPtr> _subMenus = new();
        private readonly Dictionary<uint, Action> _actions;
        private readonly Win32Menu? _parent;
        private uint _nextId;
        private bool _disposed;
        private bool _isSubMenu;

        public IntPtr Handle => _hMenu;

        public Win32Menu(uint startId = 1000)
        {
            _hMenu = NativeMethods.CreatePopupMenu();
            _nextId = startId;
            _actions = new Dictionary<uint, Action>();
            _parent = null;
            _isSubMenu = false;
        }

        private Win32Menu(IntPtr handle, Win32Menu parent)
        {
            _hMenu = handle;
            _nextId = parent._nextId;
            _actions = parent._actions; // Share actions with parent
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

            var cmd = NativeMethods.TrackPopupMenuEx(
                _hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN,
                pt.X, pt.Y,
                hwnd,
                IntPtr.Zero);

            // Post a null message to force the window to process pending messages
            // This is required for the menu to close properly
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            return cmd > 0 ? (uint)cmd : null;
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
            }
        }
    }
}
