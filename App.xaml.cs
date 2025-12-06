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
        private TaskbarIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private BatteryViewModel? _batteryViewModel;

        private DispatcherTimer? _watchdogTimer;
        private DateTime _lastActivityTime = DateTime.MinValue;
        private DispatcherTimer? _showDelayTimer;

        // --- Mode Flag ---
        private bool _isStickyMode = false;

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
            bool isMouseOverIcon = (DateTime.Now - _lastActivityTime).TotalSeconds < 1.0;

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
