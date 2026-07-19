using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace BatteryMonitor.Helpers
{
    internal static class PopupPlacementHelper
    {
        private const uint MonitorDefaultToNearest = 2;
        private const double DefaultWidth = 320;
        private const double DefaultHeight = 450;

        public static void Apply(Popup popup, double? savedLeft = null, double? savedTop = null)
        {
            popup.Placement = PlacementMode.Absolute;

            Point requested;
            if (savedLeft is double left && savedTop is double top &&
                double.IsFinite(left) && double.IsFinite(top))
            {
                requested = new Point(left, top);
            }
            else if (GetCursorPos(out var cursor))
            {
                requested = FromDevice(popup, new Point(cursor.X, cursor.Y));
            }
            else
            {
                requested = SystemParameters.WorkArea.TopLeft;
            }

            var clamped = ClampToVisibleWorkArea(popup, requested);
            popup.HorizontalOffset = clamped.X;
            popup.VerticalOffset = clamped.Y;
        }

        private static Point ClampToVisibleWorkArea(Popup popup, Point requested)
        {
            var requestedDevice = ToDevice(popup, requested);
            var size = GetPopupSize(popup);
            var sizeDevice = ToDeviceVector(popup, size);
            var monitor = MonitorFromPoint(
                new NativePoint { X = (int)Math.Round(requestedDevice.X), Y = (int)Math.Round(requestedDevice.Y) },
                MonitorDefaultToNearest);

            var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
            {
                double maxX = Math.Max(monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Right - sizeDevice.X);
                double maxY = Math.Max(monitorInfo.WorkArea.Top, monitorInfo.WorkArea.Bottom - sizeDevice.Y);
                var clampedDevice = new Point(
                    Math.Clamp(requestedDevice.X, monitorInfo.WorkArea.Left, maxX),
                    Math.Clamp(requestedDevice.Y, monitorInfo.WorkArea.Top, maxY));
                return FromDevice(popup, clampedDevice);
            }

            var workArea = SystemParameters.WorkArea;
            return new Point(
                Math.Clamp(requested.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - size.Width)),
                Math.Clamp(requested.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - size.Height)));
        }

        private static Size GetPopupSize(Popup popup)
        {
            if (popup.Child is FrameworkElement child)
            {
                double width = child.ActualWidth > 0 ? child.ActualWidth : child.DesiredSize.Width;
                double height = child.ActualHeight > 0 ? child.ActualHeight : child.DesiredSize.Height;
                return new Size(width > 0 ? width : DefaultWidth, height > 0 ? height : DefaultHeight);
            }

            return new Size(DefaultWidth, DefaultHeight);
        }

        private static Point ToDevice(Popup popup, Point point)
        {
            if (popup.Child is Visual child && PresentationSource.FromVisual(child)?.CompositionTarget is { } target)
            {
                return target.TransformToDevice.Transform(point);
            }

            return point;
        }

        private static Point FromDevice(Popup popup, Point point)
        {
            if (popup.Child is Visual child && PresentationSource.FromVisual(child)?.CompositionTarget is { } target)
            {
                return target.TransformFromDevice.Transform(point);
            }

            return point;
        }

        private static Vector ToDeviceVector(Popup popup, Size size)
        {
            var origin = ToDevice(popup, new Point(0, 0));
            var extent = ToDevice(popup, new Point(size.Width, size.Height));
            return extent - origin;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }
    }
}
