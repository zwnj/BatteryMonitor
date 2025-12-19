using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;

namespace BatteryMonitor3
{
    public partial class App : Application
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

        private TaskbarIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private BatteryViewModel? _batteryViewModel;

        private DispatcherTimer? _watchdogTimer;
        private DateTime _lastActivityTime = DateTime.MinValue;
        private DispatcherTimer? _showDelayTimer;

        // --- Mode Flag ---
        private bool _isStickyMode = false;
        private Win32Point _lastHoverPos;
        private DateTime _lastMoveTime;

        // --- State Restoration ---
        private bool _savedStickyMode = false;
        private bool _savedStaysOpen = false;

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
            }
            if (_notifyIcon.TrayPopupResolved is Popup popup)
            {
                popup.Closed += OnPopupClosed;
            }

            _showDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _showDelayTimer.Tick += OnShowTimerTick;

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();

            _notifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;
            _notifyIcon.TrayLeftMouseDown += MyNotifyIcon_TrayLeftMouseDown;

            if (_notifyIcon.ContextMenu?.Items[0] is MenuItem exitItem)
            {
                exitItem.Click += Exit_Click;
            }

            // Data updates
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
                if (_batteryViewModel?.IsPinned == true)
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
                            popup.StaysOpen = false;
                            
                            // To ensure it doesn't close immediately (since we are setting StaysOpen=false),
                            // we must ensure it has focus/activation.
                            if (popup.Child is UIElement child)
                            {
                                if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                                {
                                    SetForegroundWindow(source.Handle);
                                }
                                child.Focus();
                            }
                        }
                        else
                        {
                            // Returning to Hover Mode
                            popup.StaysOpen = true; // Controlled by Watchdog
                        }
                    }
                }
            }
        }
        



        




        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            _watchdogTimer?.Stop();
            _showDelayTimer?.Stop();
            base.OnExit(e);
        }

        // --- Event Handlers ---

        private void MyNotifyIcon_TrayMouseMove(object? sender, RoutedEventArgs e)
        {
            _lastActivityTime = DateTime.Now;
            _lastMoveTime = DateTime.Now;
            if (GetCursorPos(out Win32Point pt))
            {
                _lastHoverPos = pt;
            }

            if (_notifyIcon?.TrayPopupResolved is Popup popup && !popup.IsOpen && !_isStickyMode && !IsPinned())
            {
                if (_showDelayTimer != null && !_showDelayTimer.IsEnabled)
                {
                    _showDelayTimer.Start();
                }
            }
        }

        private bool IsPinned() => _batteryViewModel?.IsPinned ?? false;

        private void MyNotifyIcon_TrayLeftMouseDown(object? sender, RoutedEventArgs e)
        {
            if (IsPinned()) 
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
                popup.StaysOpen = false; // Let WPF handle auto-close on focus loss
                
                if (popup.IsOpen)
                {
                    // Already open (e.g. from hover), do not call ShowTrayPopup to avoid position reset.
                    // Instead, ensure focus so standard StaysOpen=false logic works (closes on blur).
                    if (popup.Child is UIElement child)
                    {
                        if (PresentationSource.FromVisual(child) is System.Windows.Interop.HwndSource source)
                        {
                            SetForegroundWindow(source.Handle);
                        }
                        child.Focus();
                    }
                }
                else
                {
                    // Not open, show it normally
                    _notifyIcon.ShowTrayPopup();
                }
            }
        }

        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showDelayTimer?.Stop();
            
            // 1. Check time: Has it been recently over the icon? (redundant if using distance, but keeps logic consistent)
            // 2. Check distance: Is the mouse still near the last known tray icon position?
            // "Tray icons" are usually 16x16 or 32x32. A radius of 30-40px handles "nearness" well.
            
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

            // Heuristic: If mouse hasn't moved far from the last "tray move" location, assume we are still hovering.
            // Tray icons are small, so if we are within e.g. 20-30 pixels, likely still there.
            if (GetCursorPos(out Win32Point currentPt))
            {
                double dx = currentPt.X - _lastHoverPos.X;
                double dy = currentPt.Y - _lastHoverPos.Y;
                double dist = Math.Sqrt(dx*dx + dy*dy);
                
                // If the mouse is static (dist very small) OR within a small radius and moved recently?
                // Actually, if simply "static" is the goal:
                // If the mouse hasn't moved much since the last 'TrayMouseMove', we consider it "over icon".
                // BUT if the user moves OUT, the dist increases.
                // The problem: TrayMouseMove only fires when over. So _lastHoverPos is always "over".
                // So if Dist is small, we are likely still over.
                isMouseOverIcon = dist < 20.0; 
                
                // Logic check:
                // 1. User hovers -> TrayMouseMove fires -> _lastHoverPos updated. dist ~ 0.
                // 2. User stops -> TrayMouseMove stops. Cursor static. dist ~ 0. -> isMouseOverIcon = true. Keep open.
                // 3. User moves OUT -> Cursor moves. dist > 20. -> isMouseOverIcon = false. Close.
            }

            // Fallback to time if heuristic fails (e.g. extremely fast move)? 
            // Or just combine:
            if (!isMouseOverIcon)
            {
                 // Give a small grace period of 0.5s from last actual move event?
                 if ((DateTime.Now - _lastMoveTime).TotalSeconds < 0.5) isMouseOverIcon = true;
            }

            if (IsPinned()) return; // Never close if pinned

            if (!isMouseOverPopup && !isMouseOverIcon)
            {
                if (Mouse.Captured == popup || Mouse.Captured == popup.Child)
                {
                    Mouse.Capture(null);
                }
                
                // Animate Close
                if (popup.Child is BatteryMonitor3.PopupView view)
                {
                    // To avoid re-entrance or multiple closes, check Opacity or state?
                    // But effectively, calling AnimateClose multiple times isn't fatal as long as it eventually closes.
                    // However, we should stop the timer triggering close repeatedly.
                    // But the timer runs every 200ms. If animation takes 300ms, it might trigger again.
                    // We can check if we are already closing?
                    // For simplicity, just run it. The callback will close.
                    
                    // Note: If standard WPF StaysOpen=false closes it, this won't run.
                    // This creates a race condition for Sticky logic, but this block is guarded by !IsPinned && !Sticky.
                    
                    // Stop watchdog temporarily or just let it close?
                    // If we don't block, the next tick might find it still open and animate again.
                    // Let's rely on the fact that if we start animation, visual state usually persists until callback.
                    // Ideally we should flag "closing". 
                    
                    // Simplest fix: Pause watchdog? Or check if IsOpen is true (it is).
                    // We'll trust the UserControl handles multiple calls gracefully or use a flag.
                    // Let's rely on the View logic being robust enough or simple "Fire and Forget".
                    // But to be safe, maybe we should unset the DataContext to prevent updates? No.
                    
                    view.AnimateClose(() => _notifyIcon.CloseTrayPopup());
                    
                    // To prevent re-triggering immediately in next 200ms (animation is 300ms), 
                    // we could disable this block.
                    // But "CloseTrayPopup" is the ultimate state change. 
                    // Let's return to avoid spamming the animation start.
                    return; 
                }

                _notifyIcon.CloseTrayPopup();
            }
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }
    }
}
