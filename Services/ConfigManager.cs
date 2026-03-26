using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using YamlDotNet.Serialization;
using ClashXW.Models;

namespace ClashXW.Services
{
    public static class ConfigManager
    {
        public static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClashXW");
        public static readonly string ConfigDir = Path.Combine(AppDataDir, "Config");
        private static readonly string StateFilePath = Path.Combine(AppDataDir, "state.json");
        private static readonly string DefaultConfigName = "config.yaml";
        private static readonly string DefaultConfigResourceName = "ClashXW.Resources.default-config.yaml";
        private static readonly JsonSerializerOptions StateSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static void EnsureDefaultConfigExists()
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            var defaultConfigPath = Path.Combine(ConfigDir, DefaultConfigName);
            if (!File.Exists(defaultConfigPath))
            {
                File.WriteAllText(defaultConfigPath, GetDefaultConfigTemplate());
            }
        }

        private static string GetDefaultConfigTemplate()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultConfigResourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Embedded resource '{DefaultConfigResourceName}' not found.");
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static string GetCurrentConfigPath()
        {
            var state = LoadState();
            if (!string.IsNullOrWhiteSpace(state.CurrentConfig) && File.Exists(state.CurrentConfig))
            {
                return state.CurrentConfig;
            }

            return Path.Combine(ConfigDir, DefaultConfigName);
        }

        public static void SetCurrentConfigPath(string configPath)
        {
            var state = LoadState();
            state.CurrentConfig = configPath;
            SaveState(state);
        }

        internal static WindowPlacementState? GetDashboardPlacement()
        {
            return LoadState().DashboardPlacement;
        }

        internal static void SetDashboardPlacement(WindowPlacementState placement)
        {
            var state = LoadState();
            state.DashboardPlacement = placement;
            SaveState(state);
        }

        public static List<string> GetAvailableConfigs()
        {
            if (!Directory.Exists(ConfigDir)) return new List<string>();
            return Directory.EnumerateFiles(ConfigDir, "*.yaml")
                .Union(Directory.EnumerateFiles(ConfigDir, "*.yml"))
                .ToList();
        }

        public static ApiDetails? ReadApiDetails(string configPath)
        {
            try
            {
                var yamlContent = File.ReadAllText(configPath);
                var deserializer = new DeserializerBuilder().Build();
                var yamlObject = deserializer.Deserialize<Dictionary<object, object>>(yamlContent);

                var controller = yamlObject?.GetValueOrDefault("external-controller")?.ToString();
                var secret = yamlObject?.GetValueOrDefault("secret")?.ToString();

                if (string.IsNullOrEmpty(controller))
                {
                    return null;
                }

                // Handle ":port" format by prepending localhost
                if (controller.StartsWith(':'))
                {
                    controller = $"127.0.0.1{controller}";
                }

                var baseUrl = $"http://{controller}";
                var dashboardUrl = $"{baseUrl}/ui";

                return new ApiDetails(baseUrl, secret, dashboardUrl);
            }
            catch
            {
                return null; // Failed to read or parse
            }
        }

        private static AppState LoadState()
        {
            if (!File.Exists(StateFilePath))
            {
                return new AppState();
            }

            try
            {
                var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(StateFilePath), StateSerializerOptions);
                return state ?? new AppState();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to read app state from {StateFilePath}: {ex.Message}");
                return new AppState();
            }
        }

        private static void SaveState(AppState state)
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, StateSerializerOptions));
        }

    }
}
