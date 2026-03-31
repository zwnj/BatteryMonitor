using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using BatteryMonitor3.Helpers;
using BatteryMonitor3.Models;
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
        private DateTime _lastExplicitOpenTime = DateTime.MinValue;
        private DateTime _lastShortcutToggleTime = DateTime.MinValue;

        private bool _isStickyMode = false;
        private bool _isExplicitMode = false;
        private bool _isCloseAnimating = false;
        private bool _savedStickyMode = false;
        private bool _savedStaysOpen = false;
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
            if (_isCloseAnimating) return;
            if (DateTime.Now - _lastShortcutToggleTime < ShortcutToggleCooldown) return;

            _lastShortcutToggleTime = DateTime.Now;

            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                if (popup.IsOpen)
                {
                    // Pinned (固定モード) の場合はショートカットで閉じない
                    if (_isPinnedDelegate()) return;

                    ClosePopupWithAnimation(popup);
                }
                else
                {
                    OpenExplicitPopup(popup);
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
            _isExplicitMode = true;
            if (_notifyIcon?.TrayPopupResolved is Popup popup)
            {
                if (popup.IsOpen)
                {
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
                    // 開いていないので通常通り表示
                    OpenExplicitPopup(popup);
                }
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            
            // 待機中にコンテキストメニューが開かれたら中断
            if (_notifyIcon?.ContextMenu?.IsOpen == true) return;
            if (DateTime.Now - _lastExplicitOpenTime < ExplicitOpenGracePeriod) return;
            
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
            _isStickyMode = false; // いかなる理由でもポップアップが閉じたらモードをリセット
            _isExplicitMode = false;
            _isCloseAnimating = false;

            if (sender is Popup popup && popup.Child is PopupView view)
            {
                view.ResetVisualState();
            }
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

        private void OpenExplicitPopup(Popup popup)
        {
            _showDelayTimer?.Stop();
            _isStickyMode = true;
            _isExplicitMode = true;
            _lastExplicitOpenTime = DateTime.Now;

            if (popup.Child is PopupView view)
            {
                view.PrepareForOpen();
            }

            ApplyPopupPosition(popup);

            // まずは安定して表示を成立させ、フォーカス取得後に自動クローズへ戻す。
            popup.StaysOpen = true;
            _notifyIcon.ShowTrayPopup();

            if (popup.Child is not UIElement child)
            {
                popup.StaysOpen = false;
                return;
            }

            child.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
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

                Logger.Info($"明示表示: Foreground={foreResult}, Focus={focusResult}");
            }));
        }

        private void ClosePopupWithAnimation(Popup popup)
        {
            popup.StaysOpen = false;

            if (popup.Child is PopupView view)
            {
                _isCloseAnimating = true;
                view.AnimateClose(() =>
                {
                    _isCloseAnimating = false;
                    _notifyIcon.CloseTrayPopup();
                });
                return;
            }

            _isCloseAnimating = false;
            _notifyIcon.CloseTrayPopup();
        }

        private void ApplyPopupPosition(Popup popup)
        {
            popup.Placement = PlacementMode.Absolute;

            var settings = AppSettings.Load();
            if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
            {
                popup.HorizontalOffset = settings.WindowLeft;
                popup.VerticalOffset = settings.WindowTop;
                return;
            }

            if (!GetCursorPos(out Win32Point pt))
            {
                return;
            }

            if (popup.Child is not UIElement child)
            {
                popup.HorizontalOffset = pt.X;
                popup.VerticalOffset = pt.Y;
                return;
            }

            var source = PresentationSource.FromVisual(child);
            if (source?.CompositionTarget == null)
            {
                popup.HorizontalOffset = pt.X;
                popup.VerticalOffset = pt.Y;
                return;
            }

            var logicalPos = source.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
            popup.HorizontalOffset = logicalPos.X;
            popup.VerticalOffset = logicalPos.Y;
        }
    }
}
