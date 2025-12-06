using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BatteryMonitor3
{
    public partial class MainWindow : Window
    {
        private readonly BatteryViewModel _viewModel;
        private readonly DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new BatteryViewModel();
            this.DataContext = _viewModel;

            // 初回実行
            _viewModel.UpdateData();

            // タイマー設定 (1秒ごとに更新)
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => _viewModel.UpdateData();
            _timer.Start();
        }

        // ウィンドウをドラッグ移動
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}