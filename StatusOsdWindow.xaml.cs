using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ModernKey
{
    public partial class StatusOsdWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private Storyboard _fadeStoryboard;

        public StatusOsdWindow()
        {
            InitializeComponent();
            Loaded += StatusOsdWindow_Loaded;
        }

        private void StatusOsdWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        public void ShowStatus(bool isVietnamese)
        {
            Dispatcher.Invoke(() =>
            {
                if (isVietnamese)
                {
                    TxtOsdStatus.Text = "[VI]";
                    var pink = (Color)ColorConverter.ConvertFromString("#FF007F");
                    TxtOsdStatus.Foreground = new SolidColorBrush(pink);
                    OsdBorder.BorderBrush = new SolidColorBrush(pink);
                    OsdShadow.Color = pink;
                }
                else
                {
                    TxtOsdStatus.Text = "[EN]";
                    var cyan = (Color)ColorConverter.ConvertFromString("#00F0FF");
                    TxtOsdStatus.Foreground = new SolidColorBrush(cyan);
                    OsdBorder.BorderBrush = new SolidColorBrush(cyan);
                    OsdShadow.Color = cyan;
                }

                // Định vị gần chuột hoặc góc màn hình
                if (GetCursorPos(out POINT pt))
                {
                    double x = pt.X + 16;
                    double y = pt.Y + 16;

                    // Giữ OSD trong màn hình làm việc
                    var workArea = SystemParameters.WorkArea;
                    if (x + Width > workArea.Right) x = pt.X - Width - 10;
                    if (y + Height > workArea.Bottom) y = pt.Y - Height - 10;

                    Left = Math.Max(workArea.Left, x);
                    Top = Math.Max(workArea.Top, y);
                }
                else
                {
                    var workArea = SystemParameters.WorkArea;
                    Left = workArea.Right - Width - 20;
                    Top = workArea.Bottom - Height - 20;
                }

                Show();
                OsdBorder.Opacity = 0.95;

                // Animation fade out sau 0.8s
                if (_fadeStoryboard != null)
                {
                    _fadeStoryboard.Stop();
                }

                var anim = new DoubleAnimation
                {
                    From = 0.95,
                    To = 0.0,
                    BeginTime = TimeSpan.FromSeconds(0.4),
                    Duration = TimeSpan.FromSeconds(0.5)
                };

                _fadeStoryboard = new Storyboard();
                _fadeStoryboard.Children.Add(anim);
                Storyboard.SetTarget(anim, OsdBorder);
                Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
                _fadeStoryboard.Completed += (s, ev) => Hide();
                _fadeStoryboard.Begin();
            });
        }
    }
}
