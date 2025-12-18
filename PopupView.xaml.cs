using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
    }
}
