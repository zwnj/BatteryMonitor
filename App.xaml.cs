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
        private bool _isPinned = false;

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

            _lastActivityTime = DateTime.Now; // Update activity time on any mouse movement over the tray
            if (!_popupWindow.IsVisible && !_isPinned)
            {
                _showDelayTimer?.Start();
            }
        }

        private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            if (_popupWindow == null) return;
            
            _showDelayTimer?.Stop();
            
            if (_popupWindow.IsVisible && _isPinned)
            {
                // If it's visible and pinned, a click will hide it and un-pin it.
                _popupWindow.Hide();
            }
            else
            {
                // Otherwise, show it and make it pinned.
                _isPinned = true;
                _popupWindow.IsPinned = true;
                _popupWindow.Show();
                _popupWindow.Activate();
            }
        }

        private void OnShowDelayTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            if (_popupWindow != null && !_popupWindow.IsVisible)
            {
                _isPinned = false; 
                _popupWindow.IsPinned = false;
                _popupWindow.Show();
                _popupWindow.Activate();
            }
        }
        
        private void OnPopupWindowVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_popupWindow != null && !_popupWindow.IsVisible)
            {
                // When the window is hidden for any reason, reset the pinned state.
                _isPinned = false;
                _popupWindow.IsPinned = false;
            }
        }

        private void OnStateTimerTick(object? sender, EventArgs e)
        {
            if (_isPinned || _popupWindow == null || !_popupWindow.IsVisible)
            {
                return; // Don't run watchdog in pinned mode or if window is hidden
            }

            bool isMouseOverPopup = _popupWindow.IsMouseOver;
            // Heuristic check: Was the mouse over the tray icon recently?
            // This prevents flicker when the mouse is static over the icon.
            bool isMouseRecentlyOverIcon = (DateTime.Now - _lastActivityTime).TotalSeconds < 1.5;

            if (!isMouseOverPopup && !isMouseRecentlyOverIcon)
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
