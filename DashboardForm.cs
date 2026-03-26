using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ClashXW.Services;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClashXW.Native;

namespace ClashXW
{
    public class DashboardForm : Form
    {
        private readonly WebView2 _webView;
        private readonly string _dashboardUrl;

        public DashboardForm(string dashboardUrl)
        {
            _dashboardUrl = dashboardUrl;

            Text = "Dashboard";
            Size = new Size(920, 580);
            StartPosition = FormStartPosition.CenterScreen;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            _webView.CoreWebView2InitializationCompleted += OnWebViewInitialized;
            Controls.Add(_webView);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Apply dark mode to window title bar
            if (DarkModeHelper.IsDarkModeSupported)
            {
                DarkModeHelper.AllowDarkModeForWindow(Handle, true);
                DarkModeHelper.RefreshTitleBarThemeColor(Handle);
            }
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Use AppData folder for WebView2 user data to avoid permission issues in Program Files
            var userDataFolder = Path.Combine(ConfigManager.AppDataDir, "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
        }

        private void OnWebViewInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _webView.CoreWebView2.Navigate(_dashboardUrl);
            }
        }

        // Save window size and placement when closing dashboard
        // Data saved to state.json file
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                var wp = new NativeMethods.WINDOWPLACEMENT
                {
                    length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT))
                };

                if (!NativeMethods.GetWindowPlacement(Handle, ref wp)) return;

                Directory.CreateDirectory(ConfigManager.AppDataDir);
                var statePath = Path.Combine(ConfigManager.AppDataDir, "state.json");

                JsonNode? root = null;
                if (File.Exists(statePath))
                {
                    try
                    {
                        root = JsonNode.Parse(File.ReadAllText(statePath));
                    }
                    catch
                    {
                        root = null;
                    }
                }

                if (root == null || root.GetType() != typeof(JsonObject))
                {
                    root = new JsonObject();
                }

                var placementObj = new JsonObject
                {
                    ["flags"] = wp.flags,
                    ["showCmd"] = wp.showCmd,
                    ["NormalLeft"] = wp.rcNormalPosition.Left,
                    ["NormalTop"] = wp.rcNormalPosition.Top,
                    ["NormalRight"] = wp.rcNormalPosition.Right,
                    ["NormalBottom"] = wp.rcNormalPosition.Bottom,
                    ["MinX"] = wp.ptMinPosition.X,
                    ["MinY"] = wp.ptMinPosition.Y,
                    ["MaxX"] = wp.ptMaxPosition.X,
                    ["MaxY"] = wp.ptMaxPosition.Y
                };

                ((JsonObject)root)["dashboardPlacement"] = placementObj;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(statePath, root.ToJsonString(options));
            }
            catch
            {
                // Best-effort only
            }
        }
    }
}
