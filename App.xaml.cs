using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BatteryMonitor3
{
    public partial class App : System.Windows.Application
    {
        private TaskbarIcon? _notifyIcon;
        private BatteryViewModel? _batteryViewModel;
        private PopupMovedWindow? _popupWindow;

        // Timers and state for hover/sticky logic
        private DispatcherTimer? _showDelayTimer;
        private DispatcherTimer? _stateTimer; // Watchdog
        private DateTime _lastActivityTime = DateTime.MinValue;
        private bool _isStickyMode = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _notifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
            if (_notifyIcon == null) return;

            _batteryViewModel = new BatteryViewModel();
            _popupWindow = new PopupMovedWindow
            {
                DataContext = _batteryViewModel
            };
            _popupWindow.IsVisibleChanged += OnPopupWindowVisibleChanged;


            // --- Timers ---
            _showDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            _showDelayTimer.Tick += OnShowDelayTimerTick;

            _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _stateTimer.Tick += OnStateTimerTick;
            _stateTimer.Start();

            // --- Event Handlers ---
            _notifyIcon.TrayLeftMouseDown += OnTrayLeftMouseDown;
            _notifyIcon.TrayMouseMove += OnTrayMouseMove;

            if (_notifyIcon.ContextMenu?.Items[0] is MenuItem exitItem)
            {
                exitItem.Click += OnExitClick;
            }

            // --- Data Updates ---
            _batteryViewModel.UpdateData();
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += (s, ev) => _batteryViewModel.UpdateData();
            updateTimer.Start();
        }

        private void OnTrayMouseMove(object sender, RoutedEventArgs e)
        {
            if (_popupWindow == null) return;

            _lastActivityTime = DateTime.Now;
            if (!_popupWindow.IsVisible && !_isStickyMode)
            {
                _showDelayTimer?.Start();
            }
        }

        private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            if (_popupWindow == null) return;
            
            _showDelayTimer?.Stop();
            
            if (_popupWindow.IsVisible && _isStickyMode)
            {
                // If it's visible and sticky, a click will hide it and un-stick it.
                _popupWindow.Hide();
                _isStickyMode = false;
            }
            else
            {
                // Otherwise, show it and make it sticky.
                _isStickyMode = true;
                _popupWindow.Show();
                _popupWindow.Activate();
            }
        }

        private void OnShowDelayTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            if ((DateTime.Now - _lastActivityTime).TotalSeconds < 1.0)
            {
                if (_popupWindow != null && !_popupWindow.IsVisible)
                {
                    _isStickyMode = false; // This is a hover-show
                    _popupWindow.Show();
                    _popupWindow.Activate();
                }
            }
        }
        
        private void OnPopupWindowVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_popupWindow != null && !_popupWindow.IsVisible)
            {
                // When the window is hidden for any reason, reset the sticky mode.
                _isStickyMode = false;
            }
        }

        private void OnStateTimerTick(object? sender, EventArgs e)
        {
            if (_isStickyMode || _popupWindow == null || !_popupWindow.IsVisible)
            {
                return; // Don't run watchdog in sticky mode or if window is hidden
            }

            bool isMouseOverPopup = _popupWindow.IsMouseOver;
            bool isMouseOverIcon = (DateTime.Now - _lastActivityTime).TotalSeconds < 0.5;

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                _popupWindow.Hide();
            }
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            _notifyIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _stateTimer?.Stop();
            _showDelayTimer?.Stop();
            base.OnExit(e);
        }
    }
}
