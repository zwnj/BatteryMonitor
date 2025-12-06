using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BatteryMonitor3
{
    public partial class MainWindow : Window
    {
        private readonly BatteryViewModel _viewModel;
        private readonly DispatcherTimer _updateTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new BatteryViewModel();
            this.DataContext = _viewModel;

            // データ更新用タイマー
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += (s, e) => _viewModel.UpdateData();
            _updateTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer.Stop();
            base.OnClosed(e);
        }

        // ウィンドウの「閉じる」ボタンが押された時
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // アプリケーションの終了はApp.xaml.csで管理するため、
            // ここではウィンドウを隠すだけにする
            e.Cancel = true; 
            Visibility = Visibility.Hidden;
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
