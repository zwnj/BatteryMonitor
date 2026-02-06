using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;

using BatteryMonitor3.Helpers;
using BatteryMonitor3.Services;
using BatteryMonitor3.Services.Keyboard;
using BatteryMonitor3.ViewModels;
using BatteryMonitor3.Views;

namespace BatteryMonitor3
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private Views.MainWindow? _mainWindow;
        private BatteryViewModel? _batteryViewModel;
        private TrayIconController? _trayIconController;
        private KeyboardHookService? _keyboardHookService;

        protected override void OnStartup(StartupEventArgs e)
        {
            // グローバル例外ハンドリング
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                Logger.Error("AppDomain Unhandled Exception", ev.ExceptionObject as Exception);
            };
            DispatcherUnhandledException += (s, ev) =>
            {
                Logger.Error("Dispatcher Unhandled Exception", ev.Exception);
            };

            Logger.Info("Application Startup");

            base.OnStartup(e);
            _mainWindow = new Views.MainWindow();
            _notifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
            if (_notifyIcon == null) return;

            _batteryViewModel = new BatteryViewModel();
            _notifyIcon.DataContext = _batteryViewModel;

            if (_notifyIcon.TrayPopup is FrameworkElement popupContent)
            {
                popupContent.DataContext = _batteryViewModel;
            }
            
            // コントローラーの初期化
            _trayIconController = new TrayIconController(_notifyIcon, () => _batteryViewModel?.IsPinned ?? false);

            // キーボードフックの初期化
            try
            {
                _keyboardHookService = new KeyboardHookService();
                _keyboardHookService.TriggerActivated += (s, args) =>
                {
                    // UIスレッドで実行
                    Dispatcher.Invoke(() => _trayIconController?.ShowTrayPopup());
                };
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize KeyboardHookService", ex);
            }

            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // データ更新
            _batteryViewModel.UpdateData();
            _batteryViewModel.PropertyChanged += ViewModel_PropertyChanged;

            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += (s, ev) => _batteryViewModel.UpdateData();
            updateTimer.Start();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BatteryViewModel.IsPinned))
            {
                _trayIconController?.HandlePinStateChange(_batteryViewModel?.IsPinned ?? false);
            }
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _notifyIcon?.Dispose();
            _trayIconController?.Dispose();
            _keyboardHookService?.Dispose();
            base.OnExit(e);
            Logger.Info("Application Exit");
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }

        private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            Logger.Info($"PowerModeChanged: {e.Mode}");
        }
    }
}
