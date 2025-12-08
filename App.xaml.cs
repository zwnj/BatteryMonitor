using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace BatteryMonitor3
{
    public partial class App : Application
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Win32Point lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        private TaskbarIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private BatteryViewModel? _batteryViewModel;

        private DispatcherTimer? _watchdogTimer;
        private DateTime _lastActivityTime = DateTime.MinValue;
        private DispatcherTimer? _showDelayTimer;

        // --- Mode Flag ---
        private bool _isStickyMode = false;
        private Win32Point _lastHoverPos;
        private DateTime _lastMoveTime;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _mainWindow = new MainWindow();
            _notifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
            if (_notifyIcon == null) return;

            _batteryViewModel = new BatteryViewModel();
            if (_notifyIcon.TrayPopup is FrameworkElement popupContent)
            {
                popupContent.DataContext = _batteryViewModel;
            }
            if (_notifyIcon.TrayPopupResolved is Popup popup)
            {
                popup.Closed += OnPopupClosed;
            }

            _showDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _showDelayTimer.Tick += OnShowTimerTick;

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();

            _notifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;
            _notifyIcon.TrayLeftMouseDown += MyNotifyIcon_TrayLeftMouseDown;

            if (_notifyIcon.ContextMenu?.Items[0] is MenuItem exitItem)
            {
                exitItem.Click += Exit_Click;
            }

            // Data updates
            _batteryViewModel.UpdateData();
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += (s, ev) => _batteryViewModel.UpdateData();
            updateTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _watchdogTimer?.Stop();
            _showDelayTimer?.Stop();
            base.OnExit(e);
        }

        // --- Event Handlers ---

        private void MyNotifyIcon_TrayMouseMove(object? sender, RoutedEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
            _lastMoveTime = DateTime.Now;
            if (GetCursorPos(out Win32Point pt))
            {
                _lastHoverPos = pt;
            }

            if (_notifyIcon?.TrayPopupResolved is Popup popup && !popup.IsOpen && !_isStickyMode)
            {
                if (_showDelayTimer != null && !_showDelayTimer.IsEnabled)
                {
                    _showDelayTimer.Start();
                }
            }
        }

        private void MyNotifyIcon_TrayLeftMouseDown(object? sender, RoutedEventArgs e)
        {
            _showDelayTimer?.Stop();
            _isStickyMode = true;
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                popup.StaysOpen = false; // Let WPF handle auto-close on focus loss
                _notifyIcon.ShowTrayPopup();
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            if ((DateTime.Now - _lastActivityTime).TotalSeconds < 1.5)
            {
                _isStickyMode = false; // This is a hover-show
                if (_notifyIcon?.TrayPopupResolved is Popup popup)
                {
                    popup.StaysOpen = true; // We will control closing with the watchdog
                    _notifyIcon.ShowTrayPopup();
                }
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _isStickyMode = false; // Reset mode when popup closes for any reason
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (_isStickyMode) return; // Don't run watchdog in sticky mode
            if (_notifyIcon?.TrayPopupResolved is not Popup popup || !popup.IsOpen) return;

            bool isMouseOverPopup = popup.IsMouseOver;
            bool isMouseOverIcon = false; 

            // Heuristic: If mouse hasn't moved far from the last "tray move" location, assume we are still hovering.
            // Tray icons are small, so if we are within e.g. 20-30 pixels, likely still there.
            if (GetCursorPos(out Win32Point currentPt))
            {
                double dx = currentPt.X - _lastHoverPos.X;
                double dy = currentPt.Y - _lastHoverPos.Y;
                double dist = Math.Sqrt(dx*dx + dy*dy);
                
                // If the mouse is static (dist very small) OR within a small radius and moved recently?
                // Actually, if simply "static" is the goal:
                // If the mouse hasn't moved much since the last 'TrayMouseMove', we consider it "over icon".
                // BUT if the user moves OUT, the dist increases.
                // The problem: TrayMouseMove only fires when over. So _lastHoverPos is always "over".
                // So if Dist is small, we are likely still over.
                isMouseOverIcon = dist < 20.0; 
                
                // Logic check:
                // 1. User hovers -> TrayMouseMove fires -> _lastHoverPos updated. dist ~ 0.
                // 2. User stops -> TrayMouseMove stops. Cursor static. dist ~ 0. -> isMouseOverIcon = true. Keep open.
                // 3. User moves OUT -> Cursor moves. dist > 20. -> isMouseOverIcon = false. Close.
            }

            // Fallback to time if heuristic fails (e.g. extremely fast move)? 
            // Or just combine:
            if (!isMouseOverIcon)
            {
                 // Give a small grace period of 0.5s from last actual move event?
                 if ((DateTime.Now - _lastMoveTime).TotalSeconds < 0.5) isMouseOverIcon = true;
            }

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }
                _notifyIcon.CloseTrayPopup();
            }
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }
    }
}
