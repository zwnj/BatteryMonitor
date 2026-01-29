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
                // Save current state
                _savedStickyMode = _isStickyMode;
                if (_notifyIcon?.TrayPopupResolved is Popup popup)
                {
                    _savedStaysOpen = popup.StaysOpen;
                    
                    // Enter pinned state
                    _isStickyMode = true;
                    popup.StaysOpen = true;
                }
            }
            else
            {
                // Restore previous state
                _isStickyMode = _savedStickyMode;
                if (_notifyIcon?.TrayPopupResolved is Popup popup)
                {
                    if (_isStickyMode)
                    {
                        // Returning to Sticky Mode
                        
                        // To ensure it doesn't close immediately (since we are setting StaysOpen=false),
                        // we must ensure it has focus/activation FIRST.
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
                        // Returning to Hover Mode
                        popup.StaysOpen = true; // Controlled by Watchdog
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
                        // Bring to foreground
                        success = SetForegroundWindow(hWnd);
                        // Also try SetActiveWindow and SetFocus to be sure
                        SetActiveWindow(hWnd);
                        SetFocus(hWnd);
                    }
                    else
                    {
                        Logger.Info($"AttachThreadInput failed. ForeThread={foreThread}, AppThread={appThread}");
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
                Logger.Error("ForceForegroundWindow failed", ex);
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
                        // Use a slightly lower priority than Loaded to ensure visual tree is ready? 
                        // Actually Loaded is good.
                        child.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => 
                        {
                            bool foreResult = false;
                            if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                            {
                                foreResult = ForceForegroundWindow(source.Handle);
                            }
                            
                            bool focusResult = child.Focus();
                            // Do NOT capture mouse manually; it prevents external clicks from activating other windows,
                            // which breaks StaysOpen=false logic.
                            // bool captureResult = Mouse.Capture(child);

                            Logger.Info($"Shortcut Activation: Foreground={foreResult}, Focus={focusResult}");
                        }));
                    }
                }
            }
        }

        private void MyNotifyIcon_TrayMouseMove(object? sender, RoutedEventArgs e)
        {
            // If ContextMenu is open, do not start hover logic
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
                     // If already open, just focus
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
                    // Already open (e.g. from hover). 
                    // CRITICAL: Ensure focus FIRST before setting StaysOpen=false.
                    // If we set StaysOpen=false while focus is still on the Tray Icon (Taskbar), 
                    // the popup will close immediately.
                    if (popup.Child is UIElement child)
                    {
                        if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                        {
                            SetForegroundWindow(source.Handle);
                        }
                        child.Focus();
                    }
                    
                    popup.StaysOpen = false; // Now let WPF handle auto-close on focus loss
                }
                else
                {
                    // Not open, show it normally
                    popup.StaysOpen = false;
                    _notifyIcon.ShowTrayPopup();
                }
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            
            // If ContextMenu opened while waiting, abort
            if (_notifyIcon?.ContextMenu?.IsOpen == true) return;

            // Grace period: If right click happened recently (e.g. < 1.0s), suppress hover
            if ((DateTime.Now - _lastRightClickTime).TotalSeconds < 1.0) return;
            
            if (GetCursorPos(out Win32Point currentPt))
            {
                 double dx = currentPt.X - _lastHoverPos.X;
                 double dy = currentPt.Y - _lastHoverPos.Y;
                 double dist = Math.Sqrt(dx*dx + dy*dy);

                 if (dist < 40.0) // 40 pixels threshold
                 {
                     _isStickyMode = false; // This is a hover-show
                     if (_notifyIcon?.TrayPopupResolved is Popup popup)
                     {
                         popup.StaysOpen = true; // We will control closing with the watchdog
                         _notifyIcon.ShowTrayPopup();
                     }
                 }
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _isStickyMode = false; // Reset mode when popup closes for any reason
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (_isStickyMode) return; // Don't run watchdog in sticky mode
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
                 // Give a small grace period of 0.5s from last actual move event?
                 if ((DateTime.Now - _lastMoveTime).TotalSeconds < 0.5) isMouseOverIcon = true;
            }

            if (_isPinnedDelegate()) return; // Never close if pinned

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }
                
                // Animate Close
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
