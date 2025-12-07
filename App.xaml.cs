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

            _notifyIcon.TrayLeftMouseDown += OnTrayLeftMouseDown;

            if (_notifyIcon.ContextMenu?.Items[0] is MenuItem exitItem)
            {
                exitItem.Click += OnExitClick;
            }

            // Start data updates
            _batteryViewModel.UpdateData();
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += (s, ev) => _batteryViewModel.UpdateData();
            updateTimer.Start();
        }

        private void OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            if (_popupWindow == null) return;

            if (_popupWindow.IsVisible)
            {
                _popupWindow.Hide();
            }
            else
            {
                _popupWindow.Show();
                _popupWindow.Activate();
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
            base.OnExit(e);
        }
    }
}
