using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
// Timerクラスの競合を避けるため、完全修飾名を使用するか、エイリアスを定義します。
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;
using FormsTimer = System.Windows.Forms.Timer;

namespace BatteryMonitor3
{
    public partial class MainWindow : Window
    {
        private readonly BatteryViewModel _viewModel;
        private readonly DispatcherTimer _updateTimer;
        private readonly FormsTimer _showTimer;
        private readonly FormsTimer _hideTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new BatteryViewModel();
            this.DataContext = _viewModel;

            // データ更新用タイマー
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += (s, e) => _viewModel.UpdateData();
            _updateTimer.Start();

            // ホバー表示用タイマー
            _showTimer = new FormsTimer { Interval = 1000 }; // 1秒
            _showTimer.Tick += OnShowTimerTick;

            // 遅延非表示用タイマー
            _hideTimer = new FormsTimer { Interval = 1000 }; // 1秒
            _hideTimer.Tick += OnHideTimerTick;

            // イベントハンドラをプログラムで接続
            MyNotifyIcon.TrayMouseMove += MyNotifyIcon_TrayMouseMove;
            MyNotifyIcon.MouseLeave += MyNotifyIcon_MouseLeave;
        }

        protected override void OnClosed(EventArgs e)
        {
            // アプリケーション終了時にリソースを解放
            MyNotifyIcon.Dispose();
            _updateTimer.Stop();
            _showTimer.Dispose();
            _hideTimer.Dispose();
            base.OnClosed(e);
        }

        // ウィンドウの「閉じる」ボタンが押された時
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // 終了処理をキャンセル
            Visibility = Visibility.Hidden; // ウィンドウを非表示にする
        }

        // --- 旧ロジック (フォーカス基準) ---
        // ウィンドウがフォーカスを失った時
        //private void Window_Deactivated(object sender, EventArgs e)
        //{
        //    if (this.IsVisible)
        //    {
        //        _hideTimer.Start(); // 非表示タイマーを開始
        //    }
        //}
        // ウィンドウが再びフォーカスを得た時
        //private void Window_Activated(object sender, EventArgs e)
        //{
        //    _hideTimer.Stop(); // 非表示タイマーをキャンセル
        //}


        // --- 新ロジック (マウスカーソル基準) ---
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _hideTimer.Stop(); // 非表示タイマーをキャンセル
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _hideTimer.Start(); // 非表示タイマーを開始
        }
        
        // トレイアイコン上でマウスが動いた時
        private void MyNotifyIcon_TrayMouseMove(object sender, RoutedEventArgs e)
        {
            _hideTimer.Stop(); // 非表示タイマーをキャンセル
            if (!IsVisible) // ウィンドウが表示されていない場合のみ表示タイマーを開始
            {
                _showTimer.Start();
            }
        }

        // トレイアイコンからマウスが離れた時
        private void MyNotifyIcon_MouseLeave(object sender, RoutedEventArgs e)
        {
            _showTimer.Stop();
            _hideTimer.Start(); // 非表示タイマーを開始
        }
        
        // ホバー表示タイマーが1秒経過した時
        private void OnShowTimerTick(object? sender, EventArgs e)
        {
            _showTimer.Stop();
            
            // 画面の右下の座標を取得して、その近くにウィンドウを表示
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width;
            this.Top = desktopWorkingArea.Bottom - this.Height;
            
            this.Visibility = Visibility.Visible;
            this.Activate();
        }
        
        // 遅延非表示タイマーが1秒経過した時
        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            this.Visibility = Visibility.Hidden;
        }

        // 右クリックメニューの「終了」が押された時
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ウィンドウのドラッグ移動
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}