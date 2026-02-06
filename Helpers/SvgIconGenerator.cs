using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svg;

namespace BatteryMonitor3.Helpers
{
    public class SvgIconGenerator
    {
        private readonly string _svgPath;
        private readonly SvgDocument _svgDoc;

        public SvgIconGenerator(string svgPath)
        {
            _svgPath = svgPath;
            if (File.Exists(_svgPath))
            {
                _svgDoc = SvgDocument.Open(_svgPath);
            }
            else
            {
                // フォールバックまたは例外送出？安全策としてデフォルトのドキュメントを作成
                _svgDoc = new SvgDocument();
                Logger.Error($"SVGファイルが見つかりません: {_svgPath}");
            }
        }

        public ImageSource GenerateIcon(int batteryPercentage, bool? isCharging = null)
        {
            if (_svgDoc == null) return null;

            // 1. テキストの更新 - 削除済み
            // var textElement = _svgDoc.GetElementById<SvgText>("battery-text");
            // if (textElement != null)
            // {
            //     textElement.Text = batteryPercentage.ToString();
            //     textElement.TextAnchor = SvgTextAnchor.Middle; 
            // }

            // 2. レベル矩形の更新
            // SVGの "battery-inner" の幅に基づくと100%の最大幅は99のようだが
            // 初期矩形の幅は49.5。
            // 0-100 を 0-99 の幅にマッピングすると仮定。
            var levelRect = _svgDoc.GetElementById<SvgRectangle>("battery-level-rect");
            if (levelRect != null)
            {
                float maxWidth = 108f; // 4pxストロークSVG用に戻しました (内部幅108)
                float newWidth = (Math.Max(0, Math.Min(100, batteryPercentage)) / 100f) * maxWidth;
                levelRect.Width = new SvgUnit(SvgUnitType.Pixel, newWidth);

                // レベルに基づいて色を更新
                if (isCharging == true)
                {
                    // 充電中: 緑 + 雷マーク
                    levelRect.Fill = new SvgColourServer(System.Drawing.Color.LimeGreen); 
                }
                else
                {
                    // 充電中でない: 残量に基づく色
                    if (batteryPercentage <= 20)
                    {
                        levelRect.Fill = new SvgColourServer(System.Drawing.Color.Red);
                    }
                    else if (batteryPercentage <= 50)
                    {
                        levelRect.Fill = new SvgColourServer(System.Drawing.Color.Orange); 
                    }
                    else
                    {
                        levelRect.Fill = new SvgColourServer(System.Drawing.ColorTranslator.FromHtml("#4EC9B0")); 
                    }
                }
            }

            // 3. 充電雷マークの可視性更新
            var boltPath = _svgDoc.GetElementById<SvgPath>("charging-bolt");
            if (boltPath != null)
            {
                boltPath.Visibility = (isCharging == true) ? "visible" : "hidden";
            }

            // 3. ビットマップへのレンダリング
            // 目標サイズ: 標準的なトレイアイコンサイズは16x16, 32x32など。
            // しかし、より大きくレンダリングしてWPFに縮小させることも可能。SVGのViewBoxは120x120。
            // 鮮明さを保つため、あるいはアスペクト比を維持するために128x128でレンダリング。
            
            using var bitmap = _svgDoc.Draw(); // ViewBoxサイズ (120x120) または定義されたWidth/Heightで描画
            
            return BitmapToImageSource(bitmap);
        }

        private ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Essential for threading
                return bitmapImage;
            }
        }
    }
}
