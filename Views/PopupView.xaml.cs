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
    /// PopupView.xaml の相互作用ロジック
    /// </summary>
    public partial class PopupView : UserControl
    {
        private bool _isDragging;
        private Point _lastScreenPoint;
        private Popup? _parentPopup;

        public PopupView()
        {
            InitializeComponent();
            this.Loaded += PopupView_Loaded;
            this.MouseLeftButtonDown += PopupView_MouseLeftButtonDown;
            this.MouseLeftButtonUp += PopupView_MouseLeftButtonUp;
            this.MouseMove += PopupView_MouseMove;
            
            ThemeManager.ThemeChanged += (s, args) => UpdateTheme();
            
            // 透明効果や電源設定の変更イベントを購読
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) => 
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
                {
                    Dispatcher.Invoke(() => CheckTransparencyStatus());
                }
            };
            Microsoft.Win32.SystemEvents.PowerModeChanged += (s, e) =>
            {
                Dispatcher.Invoke(() => CheckTransparencyStatus());
            };

            // 初回のテーマ適用
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            var uri = ThemeManager.GetThemeUri(ThemeManager.CurrentTheme);
            var newDict = new ResourceDictionary { Source = uri };

            // リソースから古いテーマ辞書を削除
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
            
            // トグルボタンの状態を同期
            // IsChecked = True -> ダークモード
            // IsChecked = False -> ライトモード
            if (ThemeToggle != null)
            {
                ThemeToggle.IsChecked = ThemeManager.CurrentTheme == ThemeType.Dark;
            }

            // 透明度ロジックを適用
            CheckTransparencyStatus();

            // 適切な色合いでアクリル効果を適用
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                bool isDark = ThemeManager.CurrentTheme == ThemeType.Dark;
                WindowBackdrop.ApplyAcrylic(source.Handle, isDark);
            }
        }

        private void CheckTransparencyStatus()
        {
            if (MainBorder == null) return;

            bool isTransparencyEnabled = true;

            // 1. レジストリを確認 (個人用設定 > 色 > 透明効果)
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("EnableTransparency");
                    if (val is int iVal && iVal == 0)
                    {
                        isTransparencyEnabled = false;
                    }
                }
            }
            catch { /* アクセスエラーは無視 */ }

            // 2. 電源状態を確認 (省電力モード / バッテリー節約機能の簡易チェック)
            // 注: SystemParameters.PowerLineStatus はバッテリー節約機能そのものではないが、目安として使用。
            if (SystemParameters.PowerLineStatus == PowerLineStatus.Offline)
            {
                // バッテリー駆動時は、透明効果が無効化されている可能性があるため安全策をとる
                // ユーザー設定で「バッテリー節約機能」がオンの場合などはOS側で透明効果が切れる
                
                // ハイコントラストモードの場合は無効
                if (SystemParameters.HighContrast) isTransparencyEnabled = false;
            }

            if (!isTransparencyEnabled)
            {
                MainBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush_Opaque");
            }
            else
            {
                MainBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");
            }
        }

        private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        {
            // 1. 現在の表示内容をビットマップとしてキャプチャ
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

            // 2. テーマを切り替え (UIコントロールのスタイルが即座に変更される)
            ThemeManager.ToggleTheme();

            // 3. オーバーレイの不透明度を 1 -> 0 にアニメーション
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
            fadeOut.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            fadeOut.Completed += (s, _) => 
            {
                TransitionOverlay.Visibility = Visibility.Collapsed;
                TransitionOverlay.Source = null; // メモリ解放
            };
            
            TransitionOverlay.BeginAnimation(Image.OpacityProperty, fadeOut);
        }

        private void PopupView_Loaded(object sender, RoutedEventArgs e)
        {
            // TaskbarIcon は UserControl を Popup 内に配置している
            _parentPopup = this.Parent as Popup;

            if (_parentPopup != null)
            {
                // Screen Edgeでのワープ（自動位置補正）を防ぐために Absolute に設定
                _parentPopup.Placement = PlacementMode.Absolute;

                // 保存された位置を読み込んで適用
                var settings = AppSettings.Load();
                if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
                {
                    _parentPopup.HorizontalOffset = settings.WindowLeft;
                    _parentPopup.VerticalOffset = settings.WindowTop;
                }
                else
                {
                    // 初回表示時（保存値がない場合）はマウス位置に表示
                    // Placement=Absoluteにしたため、手動で座標を設定する必要がある
                    if (NativeMethods.GetCursorPos(out var p))
                    {
                                var initialSource = PresentationSource.FromVisual(this);
                        if (initialSource?.CompositionTarget != null)
                        {
                            var matrix = initialSource.CompositionTarget.TransformFromDevice;
                            var logicalPos = matrix.Transform(new Point(p.X, p.Y));
                            
                            // カーソルの少し右下に表示、あるいは中央揃え
                            // ここではカーソル位置を左上とする（必要に応じて調整）
                            _parentPopup.HorizontalOffset = logicalPos.X;
                            _parentPopup.VerticalOffset = logicalPos.Y;
                        }
                    }
                }

                // アクリル効果を適用
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
            try
            {
                // Win32 APIを使用して生のカーソル位置（スクリーン座標）を取得
                NativeMethods.POINT p;
                NativeMethods.GetCursorPos(out p);
                _lastScreenPoint = p;
                
                this.CaptureMouse();
            }
            catch (InvalidOperationException)
            {
                _isDragging = false;
            }
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
                try
                {
                    // 1. レスポンス向上のため、前回からの差分（Delta）方式に変更
                    NativeMethods.POINT p;
                    NativeMethods.GetCursorPos(out p);
                    var currentScreenPoint = (Point)p;
                    
                    var screenDelta = currentScreenPoint - _lastScreenPoint;

                    // 差異がない場合は処理スキップ
                    if (screenDelta.X == 0 && screenDelta.Y == 0) return;

                    // 3. DPIスケーリングを考慮して論理ピクセルに変換
                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget != null)
                    {
                        var matrix = source.CompositionTarget.TransformFromDevice;
                        var logicalDelta = matrix.Transform(screenDelta);

                        // 4. 現在の位置（固定されているかもしれない）に対して、今回の移動分だけを加算
                        var newH = _parentPopup.HorizontalOffset + logicalDelta.X;
                        var newV = _parentPopup.VerticalOffset + logicalDelta.Y;

                    // 5. スマートクランプ (Monitor-aware)
                        // マウス位置にあるモニターの作業領域を取得してクランプする
                        // これにより、オフセットの過剰な蓄積（デッドゾーンの原因）を防ぎつつ、マルチモニター間の移動も阻害しない
                        var monitorHandle = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = (int)currentScreenPoint.X, Y = (int)currentScreenPoint.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
                        var monitorInfo = new NativeMethods.MONITORINFO();
                        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(monitorInfo);

                        if (NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo))
                        {
                            // 論理ピクセルへの変換が必要
                            // モニター座標(物理) -> 論理
                            // NOTE: 簡易的に現在のTransformを使用するが、モニターごとのDPI違いはWPFのPopupがある程度吸収することを期待
                            // 厳密にはMonitor DPIを取得すべきだが、ここでは「行き過ぎ防止」が主目的なので、
                            // 現在のDPI倍率で物理RECTを割ることで論理RECTを推定する

                            // TransformFromDeviceは 「物理 -> 論理」
                            var p1 = matrix.Transform(new Point(monitorInfo.rcWork.Left, monitorInfo.rcWork.Top));
                            var p2 = matrix.Transform(new Point(monitorInfo.rcWork.Right, monitorInfo.rcWork.Bottom));
                            
                            var minX = Math.Min(p1.X, p2.X);
                            var maxX = Math.Max(p1.X, p2.X);
                            var minY = Math.Min(p1.Y, p2.Y);
                            var maxY = Math.Max(p1.Y, p2.Y);

                            var w = MainBorder?.ActualWidth ?? 0;
                            var h = MainBorder?.ActualHeight ?? 0;

                            if (w > 0 && h > 0)
                            {
                                newH = Math.Max(minX, Math.Min(maxX - w, newH));
                                newV = Math.Max(minY, Math.Min(maxY - h, newV));
                            }
                        }

                        _parentPopup.HorizontalOffset = newH;
                        _parentPopup.VerticalOffset = newV;

                        // 計算および更新が完了した後に、今回の座標を「前回」として保存
                        _lastScreenPoint = currentScreenPoint;
                    }
                }
                catch (InvalidOperationException)
                {
                    // ウィンドウが閉じられたりした場合の安全策
                    _isDragging = false;
                    this.ReleaseMouseCapture();
                }
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

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT p)
            {
                return new Point(p.X, p.Y);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        internal struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
