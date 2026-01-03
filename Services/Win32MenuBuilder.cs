using System;
using System.Collections.Generic;
using System.Linq;
using ClashXW.Models;
using ClashXW.Native;

namespace ClashXW.Services
{
    internal class Win32MenuBuilder
    {
        public Win32Menu BuildMenu(
            ClashConfig? configs,
            ProxiesResponse? proxies,
            string currentConfigPath,
            Action<string> onModeSelected,
            Action<string, string> onProxyNodeSelected,
            Action<string> onTestGroupLatency,
            Action<bool> onSystemProxyToggle,
            Action<bool> onTunModeToggle,
            Action onOpenDashboard,
            Action onTestLatency,
            Action<string> onConfigSelected,
            Action onReloadConfig,
            Action onEditConfig,
            Action onOpenConfigFolder,
            Action onExit)
        {
            var menu = new Win32Menu(startId: 1000);

            // Mode submenu
            BuildModeSubMenu(menu, configs, onModeSelected);

            menu.AddSeparator();

            // Proxy groups (dynamic)
            if (proxies?.Proxies != null)
            {
                BuildProxyGroupMenus(menu, proxies, onProxyNodeSelected, onTestGroupLatency);
                menu.AddSeparator();
            }

            // System Proxy toggle
            var systemProxyEnabled = IsSystemProxyEnabled(configs);
            menu.AddItem($"Set System Proxy\tCtrl+S", () => onSystemProxyToggle(!systemProxyEnabled), systemProxyEnabled);

            // TUN Mode toggle
            var tunEnabled = configs?.Tun?.Enable ?? false;
            menu.AddItem($"TUN Mode\tCtrl+E", () => onTunModeToggle(!tunEnabled), tunEnabled);

            menu.AddSeparator();

            // Actions
            menu.AddItem("Open Dashboard\tCtrl+D", onOpenDashboard);
            menu.AddItem("Test Latency", onTestLatency);

            // Configuration submenu
            BuildConfigSubMenu(menu, currentConfigPath, onConfigSelected, onReloadConfig, onEditConfig, onOpenConfigFolder);

            menu.AddSeparator();

            // Exit
            menu.AddItem("Exit", onExit);

            return menu;
        }

        private void BuildModeSubMenu(Win32Menu menu, ClashConfig? configs, Action<string> onModeSelected)
        {
            var currentMode = configs?.Mode?.ToLowerInvariant() ?? "rule";
            var modeDisplay = currentMode.Length > 0
                ? char.ToUpper(currentMode[0]) + currentMode.Substring(1)
                : "Rule";

            var modeMenu = menu.AddSubMenu($"Mode ({modeDisplay})");

            modeMenu.AddItem("Rule\tAlt+R", () => onModeSelected("rule"),
                currentMode.Equals("rule", StringComparison.OrdinalIgnoreCase));
            modeMenu.AddItem("Direct\tAlt+D", () => onModeSelected("direct"),
                currentMode.Equals("direct", StringComparison.OrdinalIgnoreCase));
            modeMenu.AddItem("Global\tAlt+G", () => onModeSelected("global"),
                currentMode.Equals("global", StringComparison.OrdinalIgnoreCase));
        }

        private void BuildProxyGroupMenus(Win32Menu menu, ProxiesResponse proxies, Action<string, string> onProxyNodeSelected, Action<string> onTestGroupLatency)
        {
            var orderedGroups = proxies.Proxies.TryGetValue("GLOBAL", out var globalGroup) && globalGroup.All != null
                ? globalGroup.All
                    .Select(groupName => proxies.Proxies.TryGetValue(groupName, out var pg) ? pg : null)
                    .Where(pg => pg != null)
                    .Cast<ProxyNode>()
                    .ToList()
                : proxies.Proxies.Values.ToList();

            var selectorGroups = orderedGroups
                .Where(p => p.Type.Equals("Selector", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var group in selectorGroups)
            {
                BuildProxyGroupSubMenu(menu, group, proxies.Proxies, onProxyNodeSelected, onTestGroupLatency);
            }
        }

        private void BuildProxyGroupSubMenu(Win32Menu menu, ProxyNode group, Dictionary<string, ProxyNode> allProxies,
            Action<string, string> onProxyNodeSelected, Action<string> onTestGroupLatency)
        {
            var groupLatency = GetLatestLatency(group);
            var headerText = groupLatency.HasValue
                ? $"{group.Name} ({group.Now})\t{groupLatency}ms"
                : $"{group.Name} ({group.Now})";

            var groupMenu = menu.AddSubMenu(headerText);

            // Test Latency button
            groupMenu.AddItem("Test Latency", () => onTestGroupLatency(group.Name));
            groupMenu.AddSeparator();

            // Proxy nodes
            foreach (var nodeName in group.All ?? new List<string>())
            {
                var isSelected = nodeName.Equals(group.Now, StringComparison.OrdinalIgnoreCase);
                var nodeLatency = GetNodeLatency(nodeName, allProxies);
                var nodeText = nodeLatency.HasValue ? $"{nodeName}\t{nodeLatency}ms" : nodeName;

                groupMenu.AddItem(nodeText, () => onProxyNodeSelected(group.Name, nodeName), isSelected);
            }
        }

        private void BuildConfigSubMenu(Win32Menu menu, string currentConfigPath, Action<string> onConfigSelected,
            Action onReloadConfig, Action onEditConfig, Action onOpenConfigFolder)
        {
            var configMenu = menu.AddSubMenu("Configuration");

            // Config files
            var availableConfigs = ConfigManager.GetAvailableConfigs();
            foreach (var configPath in availableConfigs)
            {
                var fileName = System.IO.Path.GetFileName(configPath);
                var isCurrentConfig = configPath.Equals(currentConfigPath, StringComparison.OrdinalIgnoreCase);
                configMenu.AddItem(fileName, () => onConfigSelected(configPath), isCurrentConfig);
            }

            configMenu.AddSeparator();

            // Config actions
            configMenu.AddItem("Reload Config\tCtrl+R", onReloadConfig);
            configMenu.AddItem("Edit Config", onEditConfig);
            configMenu.AddItem("Open Config Folder\tCtrl+O", onOpenConfigFolder);
        }

        private bool IsSystemProxyEnabled(ClashConfig? configs)
        {
            if (configs == null) return false;

            var proxyAddress = GetProxyAddress(configs);
            if (proxyAddress == null) return false;

            return SystemProxyManager.IsProxyEnabled(proxyAddress);
        }

        private string? GetProxyAddress(ClashConfig configs)
        {
            var mixedPort = configs.MixedPort;
            if (mixedPort > 0) return $"127.0.0.1:{mixedPort}";

            var socksPort = configs.SocksPort;
            if (socksPort > 0) return $"socks=127.0.0.1:{socksPort}";

            return null;
        }

        private int? GetLatestLatency(ProxyNode proxy)
        {
            return proxy.History?.LastOrDefault()?.Delay;
        }

        private int? GetNodeLatency(string nodeName, Dictionary<string, ProxyNode> allProxies)
        {
            if (allProxies.TryGetValue(nodeName, out var node))
            {
                return GetLatestLatency(node);
            }
            return null;
        }
    }
}
