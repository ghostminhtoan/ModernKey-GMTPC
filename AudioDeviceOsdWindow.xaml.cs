using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ModernKey
{
    public partial class AudioDeviceOsdWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private readonly DispatcherTimer _hideTimer;
        private Storyboard _fadeStoryboard;

        public static AudioDeviceOsdWindow Instance { get; private set; }

        public AudioDeviceOsdWindow()
        {
            InitializeComponent();
            Instance = this;
            Loaded += AudioDeviceOsdWindow_Loaded;

            _hideTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1.8)
            };
            _hideTimer.Tick += HideTimer_Tick;

            InitFadeStoryboard();
        }

        private void AudioDeviceOsdWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        }

        private void InitFadeStoryboard()
        {
            var anim = new DoubleAnimation
            {
                From = 0.96,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.25)
            };

            _fadeStoryboard = new Storyboard();
            _fadeStoryboard.Children.Add(anim);
            Storyboard.SetTarget(anim, RootBorder);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
            _fadeStoryboard.Completed += (s, ev) => Hide();
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            _fadeStoryboard.Begin();
        }

        public void ShowDevice(string deviceName)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ShowDevice(deviceName)));
                return;
            }

            TxtDeviceName.Text = deviceName;

            UpdateLayout();

            var workArea = SystemParameters.WorkArea;
            double w = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 260);
            double h = ActualHeight > 0 ? ActualHeight : (Height > 0 ? Height : 42);

            Left = workArea.Right - w - 16;
            Top = workArea.Bottom - h - 14;

            _fadeStoryboard.Stop();
            RootBorder.Opacity = 0.96;
            Show();
            Topmost = true;

            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}
