using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BatteryMonitor3.Helpers
{
    public static class WindowBackdrop
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Windows 10 1803+
            ACCENT_ENABLE_HOSTBACKDROP = 5, // Windows 11 Mica?
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        internal enum DwmWindowAttribute : uint
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_SYSTEMBACKDROP_TYPE = 38
        }

        internal enum DwmWindowCornerPreference : int
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        /* ... existing types ... */

        public static void SetRoundedCorners(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            int preference = (int)DwmWindowCornerPreference.DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DwmWindowAttribute.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        internal enum DwmSystemBackdropType : int
        {
            DWMSBT_AUTO = 0,
            DWMSBT_NONE = 1,
            DWMSBT_MAINWINDOW = 2, // Mica
            DWMSBT_TRANSIENTWINDOW = 3, // Acrylic
            DWMSBT_TABBEDWINDOW = 4 // Mica Alt
        }

        public static void ApplyAcrylic(Window window, bool isDark)
        {
            var interopHelper = new WindowInteropHelper(window);
            ApplyAcrylic(interopHelper.Handle, isDark);
        }

        public static void ApplyAcrylic(IntPtr hwnd, bool isDark)
        {
            if (hwnd == IntPtr.Zero) return;

            // 注意: Windows 11 の DWMWA_SYSTEMBACKDROP_TYPE API はシステムのテーマ設定(ライト/ダーク)を強制するためスキップします。
            // システムのテーマに関わらず一貫して「ダーク」なブラーを表示するため、SetWindowCompositionAttribute API を使用します。

            int tintColor = isDark ? unchecked((int)0x99000000) : unchecked((int)0x99FFFFFF); // AABBGGRR - ダーク(黒) vs ライト(白) の色合い

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = tintColor
            };

            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
