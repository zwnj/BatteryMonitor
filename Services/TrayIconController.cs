using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using BatteryMonitor.Helpers;
using BatteryMonitor.Models;
using BatteryMonitor.Views;
using System.Diagnostics;

namespace BatteryMonitor.Services
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
        private Popup? _trayPopup;

        private DispatcherTimer? _watchdogTimer;
        private DispatcherTimer? _showDelayTimer;
        
        private Win32Point _lastHoverPos;
        private DateTime _lastMoveTime;
        private DateTime _lastRightClickTime = DateTime.MinValue;
        private DateTime _lastExplicitOpenTime = DateTime.MinValue;
        private DateTime _lastShortcutToggleTime = DateTime.MinValue;

        private bool _isStickyMode = false;
        private bool _isExplicitMode = false;
        private bool _isCloseAnimating = false;
        private bool _savedStickyMode = false;
        private bool _savedStaysOpen = false;
        private long _popupOpenSequence;
        private static readonly TimeSpan ExplicitOpenGracePeriod = TimeSpan.FromMilliseconds(600);
        private static readonly TimeSpan ShortcutToggleCooldown = TimeSpan.FromMilliseconds(250);

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
                _trayPopup = popup;
                popup.Opened += OnPopupOpened;
                popup.Closed += OnPopupClosed;
            }

            _showDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            _showDelayTimer.Tick += OnShowTimerTick;

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _watchdogTimer.Tick += WatchdogTimer_Tick;

            _notifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;
            _notifyIcon.TrayLeftMouseDown += MyNotifyIcon_TrayLeftMouseDown;
            _notifyIcon.TrayRightMouseDown += MyNotifyIcon_TrayRightMouseDown;
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
                    _isExplicitMode = true;
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
                        if (popup.Child is UIElement child)
                        {
                            if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                            {
                                SetForegroundWindow(source.Handle);
                            }
                            child.Focus();
                        }

                        _isExplicitMode = true;
                        popup.StaysOpen = true;
                    }
                    else
                    {
                        // ホバーモード（マウス移動で表示状態）へ戻る
                        _isExplicitMode = false;
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

        private bool TryActivatePopupWindow(IntPtr hWnd)
        {
            try
            {
                bool foregroundResult = SetForegroundWindow(hWnd);
                SetActiveWindow(hWnd);
                SetFocus(hWnd);
                return foregroundResult;
            }
            catch (Exception ex)
            {
                Logger.Error("TryActivatePopupWindow 失敗", ex);
                return false;
            }
        }

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
                        success = TryActivatePopupWindow(hWnd);
                    }
                    else
                    {
                        Logger.Info($"AttachThreadInput 失敗。ForeThread={foreThread}, AppThread={appThread}");
                    }
                }
                else
                {
                    success = TryActivatePopupWindow(hWnd);
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
            var sw = Stopwatch.StartNew();
            Logger.Info($"ShowTrayPopup entered. closeAnimating={_isCloseAnimating}, pinned={_isPinnedDelegate()}, sticky={_isStickyMode}, explicit={_isExplicitMode}");
            if (_isCloseAnimating)
            {
                Logger.Info("ShowTrayPopup exited: close animation running");
                return;
            }
            if (DateTime.Now - _lastShortcutToggleTime < ShortcutToggleCooldown)
            {
                Logger.Info($"ShowTrayPopup exited by cooldown after {sw.ElapsedMilliseconds}ms");
                return;
            }

            _lastShortcutToggleTime = DateTime.Now;

            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                Logger.Info($"ShowTrayPopup resolved popup. isOpen={popup.IsOpen}, child={popup.Child?.GetType().Name ?? "null"}");
                if (popup.IsOpen)
                {
                    // Pinned (固定モード) の場合はショートカットで閉じない
                    if (_isPinnedDelegate())
                    {
                        Logger.Info($"ShowTrayPopup kept open because pinned after {sw.ElapsedMilliseconds}ms");
                        return;
                    }

                    Logger.Info("ShowTrayPopup closing popup");
                    ClosePopupWithAnimation(popup);
                }
                else
                {
                    Logger.Info("ShowTrayPopup opening popup");
                    OpenExplicitPopup(popup);
                }
            }
            Logger.Info($"ShowTrayPopup exit after {sw.ElapsedMilliseconds}ms");
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
                    Logger.Info("Hover show timer started");
                }
            }
        }

        private void MyNotifyIcon_TrayRightMouseDown(object? sender, RoutedEventArgs e)
        {
            _showDelayTimer?.Stop();
            _lastRightClickTime = DateTime.Now;
        }

        private void MyNotifyIcon_TrayLeftMouseDown(object? sender, RoutedEventArgs e)
        {
            Logger.Info($"TrayLeftMouseDown entered. pinned={_isPinnedDelegate()}, sticky={_isStickyMode}, explicit={_isExplicitMode}");
            if (_isPinnedDelegate()) 
            {
                if (_notifyIcon?.TrayPopupResolved is Popup p && !p.IsOpen)
                {
                    Logger.Info("TrayLeftMouseDown requesting popup show (pinned)");
                    _notifyIcon.ShowTrayPopup();
                }
                else if (_notifyIcon?.TrayPopupResolved is Popup existingPopup)
                {
                     Logger.Info("TrayLeftMouseDown focusing existing pinned popup");
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
            _isExplicitMode = true;
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                if (popup.IsOpen)
                {
                    Logger.Info("TrayLeftMouseDown popup already open");
                    // 既に開いている（ホバー等）場合は、明示表示モードへ切り替えてフォーカスを当てる。
                    if (popup.Child is UIElement child)
                    {
                        if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                        {
                            SetForegroundWindow(source.Handle);
                        }
                        child.Focus();
                    }

                    popup.StaysOpen = true;
                    _lastExplicitOpenTime = DateTime.Now;
                }
                else
                {
                    Logger.Info("TrayLeftMouseDown opening explicit popup");
                    // 開いていないので通常通り表示
                    OpenExplicitPopup(popup);
                }
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            Logger.Info("Hover show timer tick");
            
            // 待機中にコンテキストメニューが開かれたら中断
            if (_notifyIcon?.ContextMenu?.IsOpen == true) { Logger.Info("Hover show timer aborted: context menu open"); return; }
            if (DateTime.Now - _lastExplicitOpenTime < ExplicitOpenGracePeriod) { Logger.Info("Hover show timer aborted: explicit grace period"); return; }
            
            // 猶予期間: 右クリック直後（例: 1.0秒以内）はホバー表示を抑制
            if ((DateTime.Now - _lastRightClickTime).TotalSeconds < 1.0) { Logger.Info("Hover show timer aborted: right-click grace period"); return; }
            
            if (GetCursorPos(out Win32Point currentPt))
            {
                 double dx = currentPt.X - _lastHoverPos.X;
                 double dy = currentPt.Y - _lastHoverPos.Y;
                 double dist = Math.Sqrt(dx*dx + dy*dy);

                  if (dist < 40.0) // 40ピクセルの閾値
                  {
                     Logger.Info($"Hover show timer opening popup, dist={dist:F1}");
                     _isStickyMode = false; // ホバー表示モード
                     _isExplicitMode = false;
                     if (_notifyIcon?.TrayPopupResolved is Popup popup)
                     {
                         if (popup.IsOpen) return;

                         if (popup.Child is PopupView view)
                         {
                             view.PrepareForOpen();
                         }

                         ApplyPopupPosition(popup);
                         popup.StaysOpen = true; // Watchdogで閉じる制御を行う
                         _notifyIcon.ShowTrayPopup();
                     }
                 }
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            Logger.Info("Popup closed");
            _isStickyMode = false; // いかなる理由でもポップアップが閉じたらモードをリセット
            _isExplicitMode = false;
            _isCloseAnimating = false;
            _watchdogTimer?.Stop();

            if (sender is Popup popup && popup.Child is PopupView view)
            {
                view.ResetVisualState();
            }
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            _watchdogTimer?.Start();
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
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

            if (_isExplicitMode)
            {
                if (DateTime.Now - _lastExplicitOpenTime < ExplicitOpenGracePeriod) return;

                bool hasFocus = popup.Child is UIElement child && child.IsKeyboardFocusWithin;
                if (!hasFocus && !isMouseOverPopup && !isMouseOverIcon)
                {
                    Logger.Info("Watchdog closing explicit popup");
                    ClosePopupWithAnimation(popup);
                }
                return;
            }

            if (_isStickyMode) return; // Stickyモード中はホバー用Watchdogを実行しない

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }
                
                // アニメーションして閉じる
                if (popup.Child is PopupView view)
                {
                    Logger.Info("Watchdog closing hover popup with animation");
                    view.AnimateClose(() => _notifyIcon.CloseTrayPopup());
                    return; 
                }

                Logger.Info("Watchdog closing hover popup immediately");
                _notifyIcon.CloseTrayPopup();
            }
        }

        public void Dispose()
        {
            _watchdogTimer?.Stop();
            _showDelayTimer?.Stop();

            if (_trayPopup != null)
            {
                _trayPopup.Opened -= OnPopupOpened;
                _trayPopup.Closed -= OnPopupClosed;
                _trayPopup = null;
            }

            _notifyIcon.TrayMouseMove -= MyNotifyIcon_TrayMouseMove;
            _notifyIcon.TrayLeftMouseDown -= MyNotifyIcon_TrayLeftMouseDown;
            _notifyIcon.TrayRightMouseDown -= MyNotifyIcon_TrayRightMouseDown;

            if (_watchdogTimer != null)
            {
                _watchdogTimer.Tick -= WatchdogTimer_Tick;
            }

            if (_showDelayTimer != null)
            {
                _showDelayTimer.Tick -= OnShowTimerTick;
            }
        }

        private void OpenExplicitPopup(Popup popup)
        {
            var sw = Stopwatch.StartNew();
            var openId = System.Threading.Interlocked.Increment(ref _popupOpenSequence);
            Logger.Info($"OpenExplicitPopup entered. child={popup.Child?.GetType().Name ?? "null"}");
            _showDelayTimer?.Stop();
            _isStickyMode = true;
            _isExplicitMode = true;
            _lastExplicitOpenTime = DateTime.Now;

            if (popup.Child is PopupView view)
            {
                view.OpenTraceId = openId;
                Logger.Info("OpenExplicitPopup PrepareForOpen");
                view.PrepareForOpen();
            }

            Logger.Info("OpenExplicitPopup ApplyPopupPosition");
            ApplyPopupPosition(popup);

            // まずは安定して表示を成立させ、フォーカス取得後に自動クローズへ戻す。
            popup.StaysOpen = true;
            Logger.Info("OpenExplicitPopup ShowTrayPopup");
            _notifyIcon.ShowTrayPopup();
            Logger.Info($"OpenExplicitPopup ShowTrayPopup returned after {sw.ElapsedMilliseconds}ms");

            if (popup.Child is not UIElement child)
            {
                popup.StaysOpen = false;
                Logger.Info($"OpenExplicitPopup exited without UIElement child after {sw.ElapsedMilliseconds}ms");
                return;
            }

            child.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                var focusSw = Stopwatch.StartNew();
                Logger.Info("OpenExplicitPopup focus begin");
                bool foreResult = false;
                if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                {
                    foreResult = TryActivatePopupWindow(source.Handle);
                    if (!foreResult)
                    {
                        foreResult = ForceForegroundWindow(source.Handle);
                    }
                }

                bool focusResult = child.Focus();

                Logger.Info($"明示表示: Foreground={foreResult}, Focus={focusResult}, focusElapsed={focusSw.ElapsedMilliseconds}ms");
            }));
            Logger.Info($"OpenExplicitPopup exit after {sw.ElapsedMilliseconds}ms");
        }

        private void ClosePopupWithAnimation(Popup popup)
        {
            Logger.Info("ClosePopupWithAnimation entered");
            popup.StaysOpen = false;

            if (popup.Child is PopupView view)
            {
                _isCloseAnimating = true;
                view.AnimateClose(() =>
                {
                    Logger.Info("ClosePopupWithAnimation animation completed");
                    _isCloseAnimating = false;
                    _notifyIcon.CloseTrayPopup();
                });
                return;
            }

            _isCloseAnimating = false;
            Logger.Info("ClosePopupWithAnimation closing immediately");
            _notifyIcon.CloseTrayPopup();
        }

        private void ApplyPopupPosition(Popup popup)
        {
            Logger.Info("ApplyPopupPosition entered");
            popup.Placement = PlacementMode.Absolute;

            var (windowLeft, windowTop, hasValue) = AppSettingsStore.LoadWindowPosition();
            if (hasValue)
            {
                popup.HorizontalOffset = windowLeft;
                popup.VerticalOffset = windowTop;
                Logger.Info($"ApplyPopupPosition using saved position left={windowLeft}, top={windowTop}");
                return;
            }

            if (!GetCursorPos(out Win32Point pt))
            {
                Logger.Info("ApplyPopupPosition could not read cursor position");
                return;
            }

            if (popup.Child is not UIElement child)
            {
                popup.HorizontalOffset = pt.X;
                popup.VerticalOffset = pt.Y;
                Logger.Info($"ApplyPopupPosition using raw cursor position x={pt.X}, y={pt.Y}");
                return;
            }

            var source = PresentationSource.FromVisual(child);
            if (source?.CompositionTarget == null)
            {
                popup.HorizontalOffset = pt.X;
                popup.VerticalOffset = pt.Y;
                Logger.Info($"ApplyPopupPosition using raw cursor position (no source) x={pt.X}, y={pt.Y}");
                return;
            }

            var logicalPos = source.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
            popup.HorizontalOffset = logicalPos.X;
            popup.VerticalOffset = logicalPos.Y;
            Logger.Info($"ApplyPopupPosition using logical cursor position x={logicalPos.X}, y={logicalPos.Y}");
        }
    }
}
