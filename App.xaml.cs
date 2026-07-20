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
using BatteryMonitor.Updates;
using BatteryMonitor.ViewModels;

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
        private IApplicationUpdateService? _updateService;
        private ApplicationUpdateWorkflow? _updateWorkflow;
        private readonly UpdateOperationState _updateOperationState = new();
        private DispatcherTimer? _updateTimer;
        private DispatcherTimer? _popupDetailRefreshTimer;
        private DispatcherTimer? _startupUpdateTimer;
        private bool _runtimeResourcesReleased;

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
            _updateService = new VelopackApplicationUpdateService(UpdateRepositoryUrl);
            _updateWorkflow = new ApplicationUpdateWorkflow(
                _updateService,
                PrepareForUpdateRestartAsync);
            _batteryViewModel.VersionText = _updateService.CurrentVersionText;

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
            _ = _batteryViewModel.UpdateData(IsPopupOpen());
            _batteryViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _updateTimer = new DispatcherTimer { Interval = BackgroundUpdateInterval };
            _updateTimer.Tick += (s, ev) =>
            {
                if (_batteryViewModel != null)
                {
                    _ = _batteryViewModel.UpdateData(IsPopupOpen());
                }
            };
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
            ReleaseRuntimeResources();
            base.OnExit(e);
            Logger.Info("Application Exit");
            Logger.Shutdown();
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }

        private async void CheckUpdates_Click(object? sender, RoutedEventArgs e)
        {
            if (_updateWorkflow == null || !_updateOperationState.TryBegin(out long operationId))
            {
                Logger.Info("Manual update check skipped: updater is unavailable or busy");
                return;
            }

            if (sender is MenuItem menuItem)
            {
                menuItem.IsEnabled = false;
            }

            try
            {
                ApplicationUpdateCheckResult result = await _updateWorkflow.CheckAsync();
                if (!result.IsInstalled)
                {
                    ShowUpdateMessage(
                        "インストール版として起動したときにのみ更新できます。",
                        MessageBoxImage.Information);
                    return;
                }

                if (!result.IsUpdateAvailable)
                {
                    Logger.Info("Manual update check: no updates available");
                    ShowUpdateMessage("最新バージョンです。", MessageBoxImage.Information);
                    return;
                }

                string version = result.AvailableVersion ?? "不明";
                Logger.Info($"Manual update check: update available {version}");
                MessageBoxResult approval = MessageBox.Show(
                    $"更新版 {version} が見つかりました。ダウンロードして再起動しますか？",
                    "更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (approval != MessageBoxResult.Yes)
                {
                    Logger.Info("Manual update check: user canceled");
                    return;
                }

                Progress<int> progress = new(value =>
                {
                    if (_updateOperationState.TryReportProgress(operationId, value))
                    {
                        Logger.Info($"Update download progress: {value}%");
                    }
                });
                await _updateWorkflow.DownloadPrepareAndRestartAsync(progress);
            }
            catch (Exception ex)
            {
                Logger.Error("Manual update flow failed", ex);
                ShowUpdateMessage($"更新に失敗しました。\n{ex.Message}", MessageBoxImage.Error);

                if (_runtimeResourcesReleased)
                {
                    Shutdown();
                }
            }
            finally
            {
                _updateOperationState.Complete(operationId);
                if (sender is MenuItem completedMenuItem && !_runtimeResourcesReleased)
                {
                    completedMenuItem.IsEnabled = true;
                }
            }
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

            // 検証用: ポップアップ表示後に詳細更新を1回だけ走らせる
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
            if (_batteryViewModel != null)
            {
                // 検証用: 開いた瞬間の強制詳細更新を避けて、後追いで反映する
                _ = _batteryViewModel.UpdateData(true, false);
            }
        }

        private async void StartupUpdateTimer_Tick(object? sender, EventArgs e)
        {
            _startupUpdateTimer?.Stop();
            if (_updateWorkflow == null || !_updateOperationState.TryBegin(out long operationId))
            {
                Logger.Info("Startup update check skipped: updater is unavailable or busy");
                return;
            }

            try
            {
                ApplicationUpdateCheckResult result = await _updateWorkflow.CheckAsync();
                if (!result.IsInstalled)
                {
                    Logger.Info("Startup update check skipped: app is not installed");
                    return;
                }

                if (!result.IsUpdateAvailable)
                {
                    Logger.Info("Startup update check: no updates available");
                    return;
                }

                Logger.Info($"Startup update check: available version {result.AvailableVersion}");
            }
            catch (Exception ex)
            {
                Logger.Error("Startup update check failed", ex);
            }
            finally
            {
                _updateOperationState.Complete(operationId);
            }
        }

        private async Task PrepareForUpdateRestartAsync()
        {
            Logger.Info("Preparing application resources for update restart");
            _updateTimer?.Stop();
            _popupDetailRefreshTimer?.Stop();
            _startupUpdateTimer?.Stop();

            if (_batteryViewModel != null)
            {
                await _batteryViewModel.StopUpdatesAsync();
            }

            ReleaseRuntimeResources();
        }

        private void ReleaseRuntimeResources()
        {
            if (_runtimeResourcesReleased)
            {
                return;
            }

            _runtimeResourcesReleased = true;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            if (_batteryViewModel != null)
            {
                _batteryViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                popup.Opened -= TrayPopup_Opened;
                popup.Closed -= TrayPopup_Closed;
                popup.IsOpen = false;
            }

            _updateTimer?.Stop();
            _popupDetailRefreshTimer?.Stop();
            _startupUpdateTimer?.Stop();
            _trayIconController?.Dispose();
            _keyboardHookService?.Dispose();
            _notifyIcon?.Dispose();
        }

        private static void ShowUpdateMessage(string message, MessageBoxImage image)
        {
            MessageBox.Show(message, "更新", MessageBoxButton.OK, image);
        }
    }
}
