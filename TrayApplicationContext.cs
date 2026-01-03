using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClashXW.Models;
using ClashXW.Native;
using ClashXW.Services;

namespace ClashXW
{
    internal class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly MessageWindow _messageWindow;

        private ClashApiService? _apiService;
        private ClashProcessService? _clashProcessService;
        private Win32MenuBuilder? _menuBuilder;
        private readonly string? _executablePath;
        private string _currentConfigPath;

        // Cache for menu state
        private ClashConfig? _cachedConfigs;
        private ProxiesResponse? _cachedProxies;

        public TrayApplicationContext()
        {
            ConfigManager.EnsureDefaultConfigExists();

            _executablePath = Path.Combine(AppContext.BaseDirectory, "ClashAssets", "clash.exe");
            _currentConfigPath = ConfigManager.GetCurrentConfigPath();

            // Initialize services
            _clashProcessService = new ClashProcessService(_executablePath);
            _menuBuilder = new Win32MenuBuilder();

            // Create a message window for menu handling
            _messageWindow = new MessageWindow();

            // Create notify icon
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "ClashXW",
                Visible = true
            };
            _notifyIcon.MouseUp += OnNotifyIconMouseUp;

            StartClashCore();
            InitializeApiService();
        }

        private System.Drawing.Icon LoadIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ClashXW.icon.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return new System.Drawing.Icon(stream);
            }

            // Fallback to file
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
            }

            return SystemIcons.Application;
        }

        private void StartClashCore()
        {
            if (_clashProcessService == null) return;

            try
            {
                _clashProcessService.Start(_currentConfigPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start Clash process:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitThread();
            }
        }

        private void InitializeApiService()
        {
            var apiDetails = ConfigManager.ReadApiDetails(_currentConfigPath);
            if (apiDetails != null)
            {
                _apiService = new ClashApiService(apiDetails.BaseUrl, apiDetails.Secret);
            }
            else
            {
                _apiService = null;
                MessageBox.Show($"Failed to read API details from {_currentConfigPath}. API features will be disabled.",
                    "Config Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

            // Fetch fresh data before showing menu
            await RefreshCachedDataAsync();

            ShowContextMenu();
        }

        private async Task RefreshCachedDataAsync()
        {
            if (_apiService == null) return;

            try
            {
                var configsTask = _apiService.GetConfigsAsync();
                var proxiesTask = _apiService.GetProxiesAsync();

                await Task.WhenAll(configsTask, proxiesTask);

                _cachedConfigs = await configsTask;
                _cachedProxies = await proxiesTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch API data: {ex.Message}");
            }
        }

        private void ShowContextMenu()
        {
            if (_menuBuilder == null) return;

            using var menu = _menuBuilder.BuildMenu(
                _cachedConfigs,
                _cachedProxies,
                _currentConfigPath,
                onModeSelected: OnModeSelected,
                onProxyNodeSelected: OnProxyNodeSelected,
                onTestGroupLatency: OnTestGroupLatency,
                onSystemProxyToggle: OnSystemProxyToggle,
                onTunModeToggle: OnTunModeToggle,
                onOpenDashboard: OnOpenDashboard,
                onTestLatency: OnTestLatency,
                onConfigSelected: OnConfigSelected,
                onReloadConfig: OnReloadConfig,
                onEditConfig: OnEditConfig,
                onOpenConfigFolder: OnOpenConfigFolder,
                onExit: OnExit
            );

            var commandId = menu.Show(_messageWindow.Handle);
            if (commandId.HasValue)
            {
                menu.ExecuteCommand(commandId.Value);
            }
        }

        private async void OnModeSelected(string mode)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.UpdateModeAsync(mode);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set mode: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnProxyNodeSelected(string groupName, string nodeName)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.SelectProxyNodeAsync(groupName, nodeName);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set proxy node: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnTestGroupLatency(string groupName)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.TestGroupLatencyAsync(groupName);
                ShowBalloonTip("Success", $"Latency test completed for {groupName}", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to test latency for {groupName}: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnSystemProxyToggle(bool enable)
        {
            if (_apiService == null) return;

            try
            {
                var configs = await _apiService.GetConfigsAsync();
                if (configs == null) return;

                var proxyAddress = GetProxyAddress(configs);
                if (proxyAddress == null)
                {
                    ShowBalloonTip("Error", "Proxy port not configured in Clash.", ToolTipIcon.Error);
                    return;
                }

                if (enable)
                {
                    SystemProxyManager.SetProxy(proxyAddress);
                }
                else
                {
                    SystemProxyManager.DisableProxy();
                }
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to toggle system proxy: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnTunModeToggle(bool enable)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.UpdateTunModeAsync(enable);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to set TUN mode: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnOpenDashboard()
        {
            var apiDetails = ConfigManager.ReadApiDetails(_currentConfigPath);
            if (apiDetails == null || string.IsNullOrEmpty(apiDetails.DashboardUrl)) return;

            try
            {
                Process.Start(new ProcessStartInfo(apiDetails.DashboardUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to open dashboard: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnTestLatency()
        {
            if (_apiService == null || _cachedProxies?.Proxies == null) return;

            try
            {
                var latencyTasks = new System.Collections.Generic.List<Task>();

                foreach (var proxy in _cachedProxies.Proxies.Values)
                {
                    if (proxy.Type.Equals("Selector", StringComparison.OrdinalIgnoreCase))
                    {
                        latencyTasks.Add(_apiService.TestGroupLatencyAsync(proxy.Name));
                    }
                    else if (proxy.All == null || proxy.All.Count == 0)
                    {
                        latencyTasks.Add(_apiService.TestProxyLatencyAsync(proxy.Name));
                    }
                }

                await Task.WhenAll(latencyTasks);
                ShowBalloonTip("Success", "Latency tests completed", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to run latency tests: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnConfigSelected(string newPath)
        {
            if (_apiService == null) return;

            try
            {
                await _apiService.ReloadConfigAsync(newPath);
                _currentConfigPath = newPath;
                ConfigManager.SetCurrentConfigPath(newPath);
                InitializeApiService();
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to switch configuration: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private async void OnReloadConfig()
        {
            if (_apiService == null || string.IsNullOrEmpty(_currentConfigPath)) return;

            try
            {
                await _apiService.ReloadConfigAsync(_currentConfigPath);
                ShowBalloonTip("Success", "Configuration reloaded", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to reload configuration: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnEditConfig()
        {
            if (string.IsNullOrEmpty(_currentConfigPath) || !File.Exists(_currentConfigPath))
            {
                ShowBalloonTip("Error", $"Config file not found at: {_currentConfigPath}", ToolTipIcon.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("notepad.exe", _currentConfigPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to open config file: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnOpenConfigFolder()
        {
            if (string.IsNullOrEmpty(_currentConfigPath)) return;

            var configFolder = Path.GetDirectoryName(_currentConfigPath);
            if (configFolder == null || !Directory.Exists(configFolder)) return;

            try
            {
                Process.Start(new ProcessStartInfo(configFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowBalloonTip("Error", $"Failed to open config folder: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnExit()
        {
            // Check if system proxy was enabled and disable it
            if (_cachedConfigs != null)
            {
                var proxyAddress = GetProxyAddress(_cachedConfigs);
                if (proxyAddress != null && SystemProxyManager.IsProxyEnabled(proxyAddress))
                {
                    SystemProxyManager.DisableProxy();
                }
            }

            _clashProcessService?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _messageWindow.DestroyHandle();
            ExitThread();
        }

        private string? GetProxyAddress(ClashConfig configs)
        {
            var mixedPort = configs.MixedPort;
            if (mixedPort > 0) return $"127.0.0.1:{mixedPort}";

            var socksPort = configs.SocksPort;
            if (socksPort > 0) return $"socks=127.0.0.1:{socksPort}";

            return null;
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, icon);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Dispose();
                _messageWindow.DestroyHandle();
                _clashProcessService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A message-only window used for Win32 menu operations.
    /// </summary>
    internal class MessageWindow : NativeWindow
    {
        private const int HWND_MESSAGE = -3;

        public MessageWindow()
        {
            CreateHandle(new CreateParams
            {
                Parent = new IntPtr(HWND_MESSAGE)
            });
        }
    }
}
