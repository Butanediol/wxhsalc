using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClashXW.Services
{
    /// <summary>
    /// Helper class for enabling Windows 10/11 dark mode.
    /// Uses undocumented uxtheme APIs for native UI dark mode (menus, title bars),
    /// and the registry for reliably detecting the current system theme.
    /// </summary>
    public static class DarkModeHelper
    {
        private enum PreferredAppMode
        {
            Default,
            AllowDark,
            ForceDark,
            ForceLight,
            Max
        }

        private enum WINDOWCOMPOSITIONATTRIB
        {
            WCA_USEDARKMODECOLORS = 26
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public WINDOWCOMPOSITIONATTRIB Attrib;
            public IntPtr pvData;
            public int cbData;
        }

        private delegate bool AllowDarkModeForWindowDelegate(IntPtr hWnd, bool allow);
        private delegate bool AllowDarkModeForAppDelegate(bool allow);
        private delegate void RefreshImmersiveColorPolicyStateDelegate();
        private delegate PreferredAppMode SetPreferredAppModeDelegate(PreferredAppMode appMode);
        private delegate bool SetWindowCompositionAttributeDelegate(IntPtr hWnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

        private static AllowDarkModeForWindowDelegate? _allowDarkModeForWindow;
        private static AllowDarkModeForAppDelegate? _allowDarkModeForApp;
        private static RefreshImmersiveColorPolicyStateDelegate? _refreshImmersiveColorPolicyState;
        private static SetPreferredAppModeDelegate? _setPreferredAppMode;
        private static SetWindowCompositionAttributeDelegate? _setWindowCompositionAttribute;

        private static uint _buildNumber;

        public static bool IsDarkModeSupported { get; private set; }
        public static bool IsDarkModeEnabled { get; private set; }

        [DllImport("ntdll.dll")]
        private static extern void RtlGetNtVersionNumbers(out uint major, out uint minor, out uint build);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr lpProcName);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct HIGHCONTRAST
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr lpszDefaultScheme;
        }

        private const uint SPI_GETHIGHCONTRAST = 0x0042;
        private const uint HCF_HIGHCONTRASTON = 0x00000001;
        private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        /// <summary>
        /// Initialize dark mode support. Must be called before any windows are created.
        /// </summary>
        public static void Initialize()
        {
            RtlGetNtVersionNumbers(out uint major, out uint minor, out uint build);
            _buildNumber = build & ~0xF0000000u;
            Logger.Info($"DarkModeHelper.Initialize: Windows version {major}.{minor} build {_buildNumber}");

            // Dark mode is supported on Windows 10 1809 (build 17763) and later
            if (major != 10 || minor != 0 || _buildNumber < 17763)
            {
                Logger.Warn($"DarkModeHelper: Dark mode not supported (version check failed: {major}.{minor}.{_buildNumber})");
                return;
            }

            var hUxtheme = LoadLibraryEx("uxtheme.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
            if (hUxtheme == IntPtr.Zero)
            {
                Logger.Warn("DarkModeHelper: Failed to load uxtheme.dll");
                return;
            }

            // Load functions by ordinal
            var pRefreshImmersiveColorPolicyState = GetProcAddress(hUxtheme, new IntPtr(104));
            var pAllowDarkModeForWindow = GetProcAddress(hUxtheme, new IntPtr(133));
            var pOrd135 = GetProcAddress(hUxtheme, new IntPtr(135));

            Logger.Info($"DarkModeHelper: Ordinal pointers - 104:{pRefreshImmersiveColorPolicyState != IntPtr.Zero}, 133:{pAllowDarkModeForWindow != IntPtr.Zero}, 135:{pOrd135 != IntPtr.Zero}");

            if (pRefreshImmersiveColorPolicyState == IntPtr.Zero ||
                pAllowDarkModeForWindow == IntPtr.Zero ||
                pOrd135 == IntPtr.Zero)
            {
                Logger.Warn("DarkModeHelper: One or more ordinal functions not found");
                return;
            }

            _refreshImmersiveColorPolicyState = Marshal.GetDelegateForFunctionPointer<RefreshImmersiveColorPolicyStateDelegate>(pRefreshImmersiveColorPolicyState);
            _allowDarkModeForWindow = Marshal.GetDelegateForFunctionPointer<AllowDarkModeForWindowDelegate>(pAllowDarkModeForWindow);

            // Ordinal 135 changed between 1809 and 1903
            if (_buildNumber < 18362)
                _allowDarkModeForApp = Marshal.GetDelegateForFunctionPointer<AllowDarkModeForAppDelegate>(pOrd135);
            else
                _setPreferredAppMode = Marshal.GetDelegateForFunctionPointer<SetPreferredAppModeDelegate>(pOrd135);

            // Load SetWindowCompositionAttribute from user32.dll
            var hUser32 = LoadLibraryEx("user32.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
            if (hUser32 != IntPtr.Zero)
            {
                var pSetWindowCompositionAttribute = GetProcAddress(hUser32, "SetWindowCompositionAttribute");
                if (pSetWindowCompositionAttribute != IntPtr.Zero)
                    _setWindowCompositionAttribute = Marshal.GetDelegateForFunctionPointer<SetWindowCompositionAttributeDelegate>(pSetWindowCompositionAttribute);
            }

            IsDarkModeSupported = true;

            // Enable dark mode for native UI elements (menus, title bars)
            AllowDarkModeForApp(true);
            _refreshImmersiveColorPolicyState();

            // Detect current theme via registry
            var registryDark = IsSystemDarkModeFromRegistry();
            var isHighContrast = IsHighContrast();
            IsDarkModeEnabled = registryDark && !isHighContrast;
            Logger.Info($"DarkModeHelper: RegistryDarkMode={registryDark}, IsHighContrast={isHighContrast}, IsDarkModeEnabled={IsDarkModeEnabled}");
        }

        /// <summary>
        /// Allow or disallow dark mode for the entire application.
        /// </summary>
        public static void AllowDarkModeForApp(bool allow)
        {
            if (_allowDarkModeForApp != null)
                _allowDarkModeForApp(allow);
            else if (_setPreferredAppMode != null)
                _setPreferredAppMode(allow ? PreferredAppMode.AllowDark : PreferredAppMode.Default);
        }

        /// <summary>
        /// Allow or disallow dark mode for a specific window.
        /// </summary>
        public static bool AllowDarkModeForWindow(IntPtr hWnd, bool allow)
        {
            if (IsDarkModeSupported && _allowDarkModeForWindow != null)
                return _allowDarkModeForWindow(hWnd, allow);
            return false;
        }

        /// <summary>
        /// Refresh the title bar theme color for a window.
        /// </summary>
        public static void RefreshTitleBarThemeColor(IntPtr hWnd)
        {
            if (!IsDarkModeSupported)
                return;

            bool dark = IsDarkModeEnabled;

            if (_buildNumber < 18362)
            {
                SetProp(hWnd, "UseImmersiveDarkModeColors", new IntPtr(dark ? 1 : 0));
            }
            else if (_setWindowCompositionAttribute != null)
            {
                var darkValue = dark ? 1 : 0;
                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attrib = WINDOWCOMPOSITIONATTRIB.WCA_USEDARKMODECOLORS,
                    pvData = Marshal.AllocHGlobal(sizeof(int)),
                    cbData = sizeof(int)
                };
                Marshal.WriteInt32(data.pvData, darkValue);
                _setWindowCompositionAttribute(hWnd, ref data);
                Marshal.FreeHGlobal(data.pvData);
            }
        }

        /// <summary>
        /// Check if the system is in high contrast mode.
        /// </summary>
        public static bool IsHighContrast()
        {
            var highContrast = new HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>() };
            if (SystemParametersInfo(SPI_GETHIGHCONTRAST, highContrast.cbSize, ref highContrast, 0))
                return (highContrast.dwFlags & HCF_HIGHCONTRASTON) != 0;
            return false;
        }

        /// <summary>
        /// Refresh the dark mode state. Call this when handling WM_SETTINGCHANGE.
        /// </summary>
        public static void RefreshDarkModeState()
        {
            if (!IsDarkModeSupported)
                return;

            _refreshImmersiveColorPolicyState?.Invoke();
            var isHighContrast = IsHighContrast();
            var registryDark = IsSystemDarkModeFromRegistry();
            var oldState = IsDarkModeEnabled;
            IsDarkModeEnabled = registryDark && !isHighContrast;
            Logger.Info($"DarkModeHelper.RefreshDarkModeState: RegistryDarkMode={registryDark}, IsHighContrast={isHighContrast}, IsDarkModeEnabled={oldState}->{IsDarkModeEnabled}");
        }

        /// <summary>
        /// Read dark mode setting directly from the registry.
        /// Returns true if system apps are set to dark mode.
        /// </summary>
        public static bool IsSystemDarkModeFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var value = key.GetValue("SystemUsesLightTheme");
                    if (value is int intValue)
                        return intValue == 0; // 0 = dark mode, 1 = light mode
                }
            }
            catch (Exception ex)
            {
                Logger.Error("DarkModeHelper: Failed to read registry dark mode", ex);
            }
            return false;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);
    }
}
