using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

using BatteryMonitor3.Services;
using BatteryMonitor3.Helpers;
using BatteryMonitor3.Models;

namespace BatteryMonitor3.Views
{
    /// <summary>
    /// Interaction logic for PopupView.xaml
    /// </summary>
    public partial class PopupView : UserControl
    {
        private bool _isDragging;
        private Point _startPoint;
        private Popup? _parentPopup;

        public PopupView()
        {
            InitializeComponent();
            this.Loaded += PopupView_Loaded;
            this.MouseLeftButtonDown += PopupView_MouseLeftButtonDown;
            this.MouseLeftButtonUp += PopupView_MouseLeftButtonUp;
            this.MouseMove += PopupView_MouseMove;
            this.Loaded += PopupView_Loaded;
            ThemeManager.ThemeChanged += (s, args) => UpdateTheme();
            
            // Initial theme apply
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            var uri = ThemeManager.GetThemeUri(ThemeManager.CurrentTheme);
            var newDict = new ResourceDictionary { Source = uri };

            // Clear old theme dictionaries from local resources
            var oldDicts = this.Resources.MergedDictionaries
                .Where(d => d.Source != null &&
                            (d.Source.OriginalString.EndsWith("DarkTheme.xaml") ||
                             d.Source.OriginalString.EndsWith("LightTheme.xaml")))
                .ToList();

            foreach (var oldDict in oldDicts)
            {
                this.Resources.MergedDictionaries.Remove(oldDict);
            }

            this.Resources.MergedDictionaries.Add(newDict);
            
            // Sync ToggleButton state
            // IsChecked = True -> Dark Mode
            // IsChecked = False -> Light Mode
            if (ThemeToggle != null)
            {
                ThemeToggle.IsChecked = ThemeManager.CurrentTheme == ThemeType.Dark;
            }

            // Apply Acrylic Effect with correct tint
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                bool isDark = ThemeManager.CurrentTheme == ThemeType.Dark;
                WindowBackdrop.ApplyAcrylic(source.Handle, isDark);
            }
        }

        private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        {
            // 1. Capture current visual as bitmap
            if (MainBorder != null)
            {
                int w = (int)MainBorder.ActualWidth;
                int h = (int)MainBorder.ActualHeight;
                if (w > 0 && h > 0)
                {
                    var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bmp.Render(MainBorder);
                    
                    TransitionOverlay.Source = bmp;
                    TransitionOverlay.Opacity = 1;
                    TransitionOverlay.Visibility = Visibility.Visible;
                }
            }

            // 2. Switch Theme (instant update of underlying UI controls)
            ThemeManager.ToggleTheme();

            // 3. Animate Overlay opacity 1 -> 0
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
            fadeOut.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            fadeOut.Completed += (s, _) => 
            {
                TransitionOverlay.Visibility = Visibility.Collapsed;
                TransitionOverlay.Source = null; // release memory
            };
            
            TransitionOverlay.BeginAnimation(Image.OpacityProperty, fadeOut);
        }

        private void PopupView_Loaded(object sender, RoutedEventArgs e)
        {
            // TaskbarIcon hosts the UserControl inside a Popup
            _parentPopup = this.Parent as Popup;

            if (_parentPopup != null)
            {
                // 保存された位置を読み込んで適用
                var settings = AppSettings.Load();
                if (!double.IsNaN(settings.WindowLeft))
                {
                    _parentPopup.HorizontalOffset = settings.WindowLeft;
                }
                if (!double.IsNaN(settings.WindowTop))
                {
                    _parentPopup.VerticalOffset = settings.WindowTop;
                }

                // Apply Acrylic Effect
                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    bool isDark = ThemeManager.CurrentTheme == ThemeType.Dark;
                    WindowBackdrop.ApplyAcrylic(source.Handle, isDark);
                    WindowBackdrop.SetRoundedCorners(source.Handle);
                }
            }
        }

        private void PopupView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // インタラクティブなコントロール（TextBox, Button等）の上でのクリックはドラッグを開始しない
            if (IsInputEelement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            _parentPopup ??= this.Parent as Popup;
            if (_parentPopup == null) return;

            _isDragging = true;
            _startPoint = e.GetPosition(null);
            this.CaptureMouse();
        }

        private bool IsInputEelement(DependencyObject? obj)
        {
            while (obj != null && obj != this)
            {
                if (obj is System.Windows.Controls.TextBox || obj is System.Windows.Controls.Primitives.ButtonBase)
                {
                    return true;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }

        private void PopupView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _parentPopup != null)
            {
                var currentPoint = e.GetPosition(null);
                var offsetX = currentPoint.X - _startPoint.X;
                var offsetY = currentPoint.Y - _startPoint.Y;

                // Update the popup's offset
                _parentPopup.HorizontalOffset += offsetX;
                _parentPopup.VerticalOffset += offsetY;
            }
        }

        private void PopupView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();

                // ドラッグ終了時に位置を保存
                if (_parentPopup != null)
                {
                    var currentSettings = AppSettings.Load();
                    AppSettings.Save(_parentPopup.HorizontalOffset, _parentPopup.VerticalOffset, currentSettings.ChargeLimit);
                }
            }
        }

        public void AnimateClose(Action onCompleted)
        {
            if (this.Content is FrameworkElement border && border.Resources["HideAnimation"] is Storyboard sb)
            {
                var clone = sb.Clone();
                clone.Completed += (s, e) => onCompleted();
                clone.Begin(border);
            }
            else
            {
                onCompleted();
            }
        }
    }
}
