using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;

using BatteryMonitor.Helpers;
using BatteryMonitor.Services;
using BatteryMonitor.Services.Keyboard;
using BatteryMonitor.ViewModels;
using Velopack;

namespace BatteryMonitor
{
    public partial class App : Application
    {
        private static readonly TimeSpan ForegroundUpdateInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan BackgroundUpdateInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan StartupUpdateDelay = TimeSpan.FromSeconds(20);
        private const string UpdateRepositoryUrl = "https://github.com/zwnj/BatteryMonitor";

        private TaskbarIcon? _notifyIcon;
        private BatteryViewModel? _batteryViewModel;
        private TrayIconController? _trayIconController;
        private KeyboardHookService? _keyboardHookService;
        private UpdateService? _updateService;
        private DispatcherTimer? _updateTimer;
        private DispatcherTimer? _popupDetailRefreshTimer;
        private DispatcherTimer? _startupUpdateTimer;

        public App()
        {
            VelopackApp.Build().Run();
            InitializeComponent();
        }

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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _notifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
            if (_notifyIcon == null) return;

            _batteryViewModel = new BatteryViewModel();
            _notifyIcon.DataContext = _batteryViewModel;
            _updateService = new UpdateService(UpdateRepositoryUrl);
            _batteryViewModel.VersionText = _updateService.GetCurrentVersionText();

            if (_notifyIcon.TrayPopup is FrameworkElement popupContent)
            {
                popupContent.DataContext = _batteryViewModel;
            }
            
            // コントローラーの初期化
            _trayIconController = new TrayIconController(_notifyIcon, () => _batteryViewModel?.IsPinned ?? false);

            if (_notifyIcon.TrayPopupResolved is Popup popup)
            {
                popup.Opened += TrayPopup_Opened;
                popup.Closed += TrayPopup_Closed;
            }

            // キーボードフックの初期化
            try
            {
                _keyboardHookService = new KeyboardHookService();
                _keyboardHookService.TriggerActivated += (s, args) =>
                {
                    Logger.Info("Shortcut trigger received");
                    // フック側を待たせないように、UIスレッドへ非同期で渡す
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Logger.Info("Shortcut dispatch started");
                        _trayIconController?.ShowTrayPopup();
                        Logger.Info("Shortcut dispatch finished");
                    }));
                };
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize KeyboardHookService", ex);
            }

            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // データ更新
            _batteryViewModel.UpdateData(IsPopupOpen());
            _batteryViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _updateTimer = new DispatcherTimer { Interval = BackgroundUpdateInterval };
            _updateTimer.Tick += (s, ev) => _batteryViewModel?.UpdateData(IsPopupOpen());
            _updateTimer.Start();

            _popupDetailRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _popupDetailRefreshTimer.Tick += PopupDetailRefreshTimer_Tick;

            _startupUpdateTimer = new DispatcherTimer { Interval = StartupUpdateDelay };
            _startupUpdateTimer.Tick += StartupUpdateTimer_Tick;
            _startupUpdateTimer.Start();
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
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                popup.Opened -= TrayPopup_Opened;
                popup.Closed -= TrayPopup_Closed;
            }
            _updateTimer?.Stop();
            _popupDetailRefreshTimer?.Stop();
            _startupUpdateTimer?.Stop();
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

        private async void CheckUpdates_Click(object? sender, RoutedEventArgs e)
        {
            if (_updateService == null)
            {
                Logger.Info("Manual update check skipped: update service is not initialized");
                return;
            }

            await _updateService.CheckPromptAndApplyAsync();
        }

        private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            Logger.Info($"PowerModeChanged: {e.Mode}");
        }

        private void TrayPopup_Opened(object? sender, EventArgs e)
        {
            if (_updateTimer != null)
            {
                _updateTimer.Interval = ForegroundUpdateInterval;
            }

            _popupDetailRefreshTimer?.Stop();
            _popupDetailRefreshTimer?.Start();
        }

        private void TrayPopup_Closed(object? sender, EventArgs e)
        {
            if (_updateTimer != null)
            {
                _updateTimer.Interval = BackgroundUpdateInterval;
            }

            _popupDetailRefreshTimer?.Stop();
        }

        private bool IsPopupOpen()
        {
            return _notifyIcon?.TrayPopupResolved is Popup popup && popup.IsOpen;
        }

        private void PopupDetailRefreshTimer_Tick(object? sender, EventArgs e)
        {
            _popupDetailRefreshTimer?.Stop();
            _batteryViewModel?.UpdateData(true, true);
        }

        private async void StartupUpdateTimer_Tick(object? sender, EventArgs e)
        {
            _startupUpdateTimer?.Stop();
            if (_updateService == null)
            {
                Logger.Info("Startup update check skipped: update service is not initialized");
                return;
            }

            await _updateService.CheckForUpdatesSilentlyAsync();
        }
    }
}
