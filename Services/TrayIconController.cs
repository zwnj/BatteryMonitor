using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using BatteryMonitor3.Helpers;
using BatteryMonitor3.Views;

namespace BatteryMonitor3.Services
{
    public class TrayIconController : IDisposable
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Win32Point lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        private readonly TaskbarIcon _notifyIcon;
        private readonly Func<bool> _isPinnedDelegate;

        private DispatcherTimer? _watchdogTimer;
        private DispatcherTimer? _showDelayTimer;
        
        private Win32Point _lastHoverPos;
        private DateTime _lastMoveTime;
        private DateTime _lastRightClickTime = DateTime.MinValue;

        private bool _isStickyMode = false;
        private bool _savedStickyMode = false;
        private bool _savedStaysOpen = false;

        public TrayIconController(TaskbarIcon notifyIcon, Func<bool> isPinnedDelegate)
        {
            _notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
            _isPinnedDelegate = isPinnedDelegate ?? throw new ArgumentNullException(nameof(isPinnedDelegate));

            Initialize();
        }

        private void Initialize()
        {
            if (_notifyIcon.TrayPopupResolved is Popup popup)
            {
                popup.Closed += OnPopupClosed;
            }

            _showDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            _showDelayTimer.Tick += OnShowTimerTick;

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();

            _notifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;
            _notifyIcon.TrayLeftMouseDown += MyNotifyIcon_TrayLeftMouseDown;
            _notifyIcon.TrayRightMouseDown += (s, e) => 
            {
                _showDelayTimer?.Stop();
                _lastRightClickTime = DateTime.Now;
            };
        }

        public void HandlePinStateChange(bool isPinned)
        {
            if (isPinned)
            {
                // 現在の状態を保存
                _savedStickyMode = _isStickyMode;
                if (_notifyIcon?.TrayPopupResolved is Popup popup)
                {
                    _savedStaysOpen = popup.StaysOpen;
                    
                    // 固定モードへ移行
                    _isStickyMode = true;
                    popup.StaysOpen = true;
                }
            }
            else
            {
                // 前の状態を復元
                _isStickyMode = _savedStickyMode;
                if (_notifyIcon?.TrayPopupResolved is Popup popup)
                {
                    if (_isStickyMode)
                    {
                        // Stickyモード（クリック表示状態）へ戻る
                        
                        // StaysOpen=false に切り替える際、即座に閉じないようにするため、
                        // まずフォーカス/アクティブ化を確実に行う
                        if (popup.Child is UIElement child)
                        {
                            if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                            {
                                SetForegroundWindow(source.Handle);
                            }
                            child.Focus();
                        }
                        
                        popup.StaysOpen = false;
                    }
                    else
                    {
                        // ホバーモード（マウス移動で表示状態）へ戻る
                        popup.StaysOpen = true; // Watchdogによって制御される
                    }
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private bool ForceForegroundWindow(IntPtr hWnd)
        {
            uint foreThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            uint appThread = GetCurrentThreadId();
            bool threadsAttached = false;
            bool success = false;

            try
            {
                if (foreThread != appThread)
                {
                    threadsAttached = AttachThreadInput(foreThread, appThread, true);
                    if (threadsAttached)
                    {
                        // 最前面へ持ってくる
                        success = SetForegroundWindow(hWnd);
                        // 念のため SetActiveWindow と SetFocus も試行
                        SetActiveWindow(hWnd);
                        SetFocus(hWnd);
                    }
                    else
                    {
                        Logger.Info($"AttachThreadInput 失敗。ForeThread={foreThread}, AppThread={appThread}");
                    }
                }
                else
                {
                    success = SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                    SetFocus(hWnd);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ForceForegroundWindow 失敗", ex);
            }
            finally
            {
                if (threadsAttached)
                {
                    AttachThreadInput(foreThread, appThread, false);
                }
            }
            return success;
        }

        public void ShowTrayPopup()
        {
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                if (popup.IsOpen)
                {
                    // Pinned (固定モード) の場合はショートカットで閉じない
                    if (_isPinnedDelegate()) return;

                    // Toggle: 既に開いている場合は閉じる
                    popup.StaysOpen = false;
                    _notifyIcon.CloseTrayPopup();
                }
                else
                {
                    // Open: 新規に開く
                    _isStickyMode = true; // クリック扱い
                    popup.StaysOpen = false; // フォーカスが外れたら閉じる挙動
                    _notifyIcon.ShowTrayPopup();
                    
                    // 表示直後にフォーカスをあてる
                    // HACK: グローバルショートカット経由だとバックグラウンドプロセス扱いになるため、
                    // AttachThreadInputを使って無理やり最前面化する
                    if (popup.Child is UIElement child)
                    {
                        // Loadedより少し低い優先度で実行する必要があるか？
                        // 実際にはLoadedで問題ない
                        child.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>  
                        {
                            bool foreResult = false;
                            if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                            {
                                foreResult = ForceForegroundWindow(source.Handle);
                            }
                            
                            bool focusResult = child.Focus();
                            // 手動でマウスキャプチャはしない。外部クリックによる他のウィンドウのアクティブ化を妨げ、
                            // StaysOpen=false のロジックを壊してしまうため。
                            // bool captureResult = Mouse.Capture(child);

                            Logger.Info($"ショートカット起動: Foreground={foreResult}, Focus={focusResult}");
                        }));
                    }
                }
            }
        }

        private void MyNotifyIcon_TrayMouseMove(object? sender, RoutedEventArgs e)
        {
            // コンテキストメニューが開いている場合はホバー処理を開始しない
            if (_notifyIcon?.ContextMenu?.IsOpen == true) return;

            _lastMoveTime = DateTime.Now;
            if (GetCursorPos(out Win32Point pt))
            {
                _lastHoverPos = pt;
            }

            if (_notifyIcon?.TrayPopupResolved is Popup popup && !popup.IsOpen && !_isStickyMode && !_isPinnedDelegate())
            {
                if (_showDelayTimer != null && !_showDelayTimer.IsEnabled)
                {
                    _showDelayTimer.Start();
                }
            }
        }

        private void MyNotifyIcon_TrayLeftMouseDown(object? sender, RoutedEventArgs e)
        {
            if (_isPinnedDelegate()) 
            {
                if (_notifyIcon?.TrayPopupResolved is Popup p && !p.IsOpen)
                {
                    _notifyIcon.ShowTrayPopup();
                }
                else if (_notifyIcon?.TrayPopupResolved is Popup existingPopup)
                {
                     // 既に開いている場合はフォーカスのみ行う
                     if (existingPopup.Child is UIElement child)
                     {
                         if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                         {
                             SetForegroundWindow(source.Handle);
                         }
                         child.Focus();
                     }
                }
                return;
            }

            _showDelayTimer?.Stop();
            _isStickyMode = true;
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                if (popup.IsOpen)
                {
                    // 既に開いている（ホバー等）。
                    // 重要: StaysOpen=false にする前に必ずフォーカスをあてる。
                    // フォーカスがタスクトレイアイコンに残ったまま StaysOpen=false にすると即座に閉じてしまう。
                    if (popup.Child is UIElement child)
                    {
                        if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                        {
                            SetForegroundWindow(source.Handle);
                        }
                        child.Focus();
                    }
                    
                    popup.StaysOpen = false; // フォーカス喪失で自動で閉じるようにWPFに任せる
                }
                else
                {
                    // 開いていないので通常通り表示
                    popup.StaysOpen = false;
                    _notifyIcon.ShowTrayPopup();
                }
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            
            // 待機中にコンテキストメニューが開かれたら中断
            if (_notifyIcon?.ContextMenu?.IsOpen == true) return;

            // 猶予期間: 右クリック直後（例: 1.0秒以内）はホバー表示を抑制
            if ((DateTime.Now - _lastRightClickTime).TotalSeconds < 1.0) return;
            
            if (GetCursorPos(out Win32Point currentPt))
            {
                 double dx = currentPt.X - _lastHoverPos.X;
                 double dy = currentPt.Y - _lastHoverPos.Y;
                 double dist = Math.Sqrt(dx*dx + dy*dy);

                 if (dist < 40.0) // 40ピクセルの閾値
                 {
                     _isStickyMode = false; // ホバー表示モード
                     if (_notifyIcon?.TrayPopupResolved is Popup popup)
                     {
                         popup.StaysOpen = true; // Watchdogで閉じる制御を行う
                         _notifyIcon.ShowTrayPopup();
                     }
                 }
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _isStickyMode = false; // いかなる理由でもポップアップが閉じたらモードをリセット
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (_isStickyMode) return; // Stickyモード中はWatchdogを実行しない
            if (_notifyIcon?.TrayPopupResolved is not Popup popup || !popup.IsOpen) return;

            bool isMouseOverPopup = popup.IsMouseOver;
            bool isMouseOverIcon = false; 

            if (GetCursorPos(out Win32Point currentPt))
            {
                double dx = currentPt.X - _lastHoverPos.X;
                double dy = currentPt.Y - _lastHoverPos.Y;
                double dist = Math.Sqrt(dx*dx + dy*dy);
                
                isMouseOverIcon = dist < 20.0; 
            }

            if (!isMouseOverIcon)
            {
                 // Give a small grace period (0.5s) from the last move event?
                 if ((DateTime.Now - _lastMoveTime).TotalSeconds < 0.5) isMouseOverIcon = true;
            }

            if (_isPinnedDelegate()) return; // Do not close when pinned

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }
                
                // アニメーションして閉じる
                if (popup.Child is PopupView view)
                {
                    view.AnimateClose(() => _notifyIcon.CloseTrayPopup());
                    return; 
                }

                _notifyIcon.CloseTrayPopup();
            }
        }

        public void Dispose()
        {
            _watchdogTimer?.Stop();
            _showDelayTimer?.Stop();
        }
    }
}
