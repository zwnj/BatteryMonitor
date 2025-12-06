using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input; // Mouse操作用
using System.Windows.Threading;

namespace BatteryMonitor3
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private BatteryViewModel? _batteryViewModel;

        // --- 監視用タイマー ---
        // イベント駆動ではなく、常時監視で確実に消します
        private DispatcherTimer? _watchdogTimer;

        // --- 最後のマウス検知時刻 ---
        private DateTime _lastActivityTime = DateTime.MinValue;

        // 表示までの遅延（1秒）
        private DispatcherTimer? _showDelayTimer;

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
                // Popup側のイベントは不要になりました（IsMouseOverプロパティで判定するため）
            }

            // --- 1. 表示用タイマー（遅延実行用） ---
            _showDelayTimer = new DispatcherTimer(DispatcherPriority.Input);
            _showDelayTimer.Interval = TimeSpan.FromSeconds(1); // 1秒ホバーで表示
            _showDelayTimer.Tick += (s, args) =>
            {
                _showDelayTimer.Stop();
                // まだマウスがアイコン付近にある場合のみ表示
                if ((DateTime.Now - _lastActivityTime).TotalSeconds < 1.5)
                {
                    _notifyIcon?.ShowTrayPopup();
                }
            };

            // --- 2. 監視用ウォッチドッグタイマー（常時稼働） ---
            // 0.2秒ごとに「閉じるべきかどうか」をチェックします
            _watchdogTimer = new DispatcherTimer(DispatcherPriority.Background);
            _watchdogTimer.Interval = TimeSpan.FromMilliseconds(200);
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();

            // アイコン上でのマウス移動のみをフック
            _notifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;

            // コンテキストメニュー終了処理
            if (_notifyIcon.ContextMenu?.Items[0] is MenuItem exitItem)
            {
                exitItem.Click += Exit_Click;
            }

            // データ更新
            _batteryViewModel.UpdateData();
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += (s, ev) => _batteryViewModel.UpdateData();
            updateTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _watchdogTimer?.Stop();
            base.OnExit(e);
        }

        // --- イベントハンドラ ---

        private void MyNotifyIcon_TrayMouseMove(object? sender, RoutedEventArgs e)
        {
            // アイコン上でマウスが動いたら時刻を更新
            _lastActivityTime = DateTime.Now;

            // ポップアップが開いていなければ、表示タイマーを開始
            if (_notifyIcon?.TrayPopupResolved is Popup popup && !popup.IsOpen)
            {
                if (_showDelayTimer != null && !_showDelayTimer.IsEnabled)
                {
                    _showDelayTimer.Start();
                }
            }
        }

        // --- 常時監視ロジック（ここが核心です） ---
        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (_notifyIcon?.TrayPopupResolved is not Popup popup) return;

            // ポップアップが開いていないなら何もしない
            if (!popup.IsOpen) return;

            // ポップアップ上にマウスがあるか判定（WPF標準プロパティ使用）
            // ※Popupの子要素上にある場合も IsMouseOver は true になります
            bool isMouseOverPopup = popup.IsMouseOver;

            // アイコン上にマウスがあるか判定（最終検知時刻から推測）
            // 1秒以内に TrayMouseMove があれば「まだアイコン上にいる」とみなす
            bool isMouseOverIcon = (DateTime.Now - _lastActivityTime).TotalSeconds < 1.0;

            // どちらにもマウスがない状態
            if (!isMouseOverIcon)
            {
                // マウスが離れてから指定時間（ここでは即時～バッファ分）経過していたら閉じる
                // TrayMouseMoveが止まってから1秒後という意味になります

                // 強制的に閉じる処理

                // 1. マウスキャプチャを解放（これがゾンビ化を防ぎます）
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }

                // 2. ライブラリ経由で閉じる
                _notifyIcon.CloseTrayPopup();

                // 3. Popup自体も閉じる
                popup.StaysOpen = false;
                popup.IsOpen = false;
            }
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }
    }
}