using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace BatteryMonitor3
{
    public partial class PopupMovedWindow : Window
    {
        public PopupMovedWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var position = SettingsService.LoadWindowPosition();
            if (position.HasValue)
            {
                // Make sure the window is visible on some screen
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)position.Value.X, (int)position.Value.Y));
                if (screen != null)
                {
                    this.Left = position.Value.X;
                    this.Top = position.Value.Y;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SettingsService.SaveWindowPosition(new System.Windows.Point(this.Left, this.Top));
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Hide the window when it loses focus.
            this.Hide();
        }
    }
}
