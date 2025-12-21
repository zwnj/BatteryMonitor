using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace BatteryMonitor3
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
        }

        private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
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
            }
        }

        private void PopupView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _parentPopup ??= this.Parent as Popup;
            if (_parentPopup == null) return;

            _isDragging = true;
            _startPoint = e.GetPosition(null);
            this.CaptureMouse();
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
                    AppSettings.Save(_parentPopup.HorizontalOffset, _parentPopup.VerticalOffset);
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
