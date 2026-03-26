using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClashXW.Models;
using ClashXW.Native;

namespace ClashXW.Services
{
    internal static class DashboardWindowPlacementManager
    {
        public static void Restore(Form form)
        {
            try
            {
                var savedPlacement = ConfigManager.GetDashboardPlacement();
                if (savedPlacement == null)
                {
                    return;
                }

                var placement = ToNativePlacement(savedPlacement);
                if (!TryNormalizePlacement(ref placement))
                {
                    return;
                }

                if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
                {
                    placement.showCmd = NativeMethods.SW_SHOWNORMAL;
                }

                if (!NativeMethods.SetWindowPlacement(form.Handle, ref placement))
                {
                    Logger.Warn("Failed to restore dashboard placement.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to restore dashboard placement: {ex.Message}");
            }
        }

        public static void Save(Form form)
        {
            try
            {
                var placement = CreatePlacement();
                if (!NativeMethods.GetWindowPlacement(form.Handle, ref placement))
                {
                    return;
                }

                if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
                {
                    placement.showCmd = NativeMethods.SW_SHOWNORMAL;
                }

                ConfigManager.SetDashboardPlacement(ToState(placement));
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to save dashboard placement: {ex.Message}");
            }
        }

        private static NativeMethods.WINDOWPLACEMENT CreatePlacement()
        {
            return new NativeMethods.WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()
            };
        }

        private static NativeMethods.WINDOWPLACEMENT ToNativePlacement(WindowPlacementState state)
        {
            return new NativeMethods.WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(),
                flags = state.Flags,
                showCmd = state.ShowCmd,
                ptMinPosition = new NativeMethods.POINT
                {
                    X = state.MinX,
                    Y = state.MinY
                },
                ptMaxPosition = new NativeMethods.POINT
                {
                    X = state.MaxX,
                    Y = state.MaxY
                },
                rcNormalPosition = new NativeMethods.RECT
                {
                    Left = state.NormalLeft,
                    Top = state.NormalTop,
                    Right = state.NormalRight,
                    Bottom = state.NormalBottom
                }
            };
        }

        private static WindowPlacementState ToState(NativeMethods.WINDOWPLACEMENT placement)
        {
            return new WindowPlacementState
            {
                Flags = placement.flags,
                ShowCmd = placement.showCmd,
                NormalLeft = placement.rcNormalPosition.Left,
                NormalTop = placement.rcNormalPosition.Top,
                NormalRight = placement.rcNormalPosition.Right,
                NormalBottom = placement.rcNormalPosition.Bottom,
                MinX = placement.ptMinPosition.X,
                MinY = placement.ptMinPosition.Y,
                MaxX = placement.ptMaxPosition.X,
                MaxY = placement.ptMaxPosition.Y
            };
        }

        private static bool TryNormalizePlacement(ref NativeMethods.WINDOWPLACEMENT placement)
        {
            var left = placement.rcNormalPosition.Left;
            var top = placement.rcNormalPosition.Top;
            var width = placement.rcNormalPosition.Right - left;
            var height = placement.rcNormalPosition.Bottom - top;

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var bounds = new Rectangle(left, top, width, height);
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }

            var primaryBounds = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
            width = Math.Min(width, primaryBounds.Width);
            height = Math.Min(height, primaryBounds.Height);
            left = primaryBounds.Left + (primaryBounds.Width - width) / 2;
            top = primaryBounds.Top + (primaryBounds.Height - height) / 2;

            placement.rcNormalPosition.Left = left;
            placement.rcNormalPosition.Top = top;
            placement.rcNormalPosition.Right = left + width;
            placement.rcNormalPosition.Bottom = top + height;
            return true;
        }
    }
}
