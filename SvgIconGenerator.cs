using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svg;

namespace BatteryMonitor3
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
                // Fallback or throw? constructing a default doc might be better safe
                _svgDoc = new SvgDocument();
                Logger.Error($"SVG file not found at: {_svgPath}");
            }
        }

        public ImageSource GenerateIcon(int batteryPercentage, bool? isCharging = null)
        {
            if (_svgDoc == null) return null;

            // 1. Update Text - REMOVED
            // var textElement = _svgDoc.GetElementById<SvgText>("battery-text");
            // if (textElement != null)
            // {
            //     textElement.Text = batteryPercentage.ToString();
            //     textElement.TextAnchor = SvgTextAnchor.Middle; 
            // }

            // 2. Update Level Rect
            // The max width for 100% seems to be 99 based on "battery-inner" width=99
            // But the initial rect width is 49.5.
            // Let's assume 0-100 maps to 0-99 width.
            var levelRect = _svgDoc.GetElementById<SvgRectangle>("battery-level-rect");
            if (levelRect != null)
            {
                float maxWidth = 108f; // Reverted for 4px stroke SVG (inner width 108)
                float newWidth = (Math.Max(0, Math.Min(100, batteryPercentage)) / 100f) * maxWidth;
                levelRect.Width = new SvgUnit(SvgUnitType.Pixel, newWidth);

                // Update Color based on level
                if (batteryPercentage <= 20)
                {
                    levelRect.Fill = new SvgColourServer(System.Drawing.Color.Red);
                }
                else if (batteryPercentage <= 50)
                {
                    levelRect.Fill = new SvgColourServer(System.Drawing.Color.Orange); // #FFC107 equivalent-ish
                }
                else
                {
                     levelRect.Fill = new SvgColourServer(System.Drawing.ColorTranslator.FromHtml("#4EC9B0")); // VS Class Cyan
                }
                
                // If specific check for charging color is needed
                if (isCharging == true)
                {
                    // Maybe green or blue for charging? For now keep level based.
                }
            }

            // 3. Render to Bitmap
            // Target size: standard tray icon size is often 16x16, 32x32.
            // But we can render larger and let WPF scale it. 120x120 is the SVG viewbox.
            // Let's render at 128x128 for crispness or keep aspect ratio.
            
            using var bitmap = _svgDoc.Draw(); // Draws at ViewBox size (120x120) or defined Width/Height
            
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
