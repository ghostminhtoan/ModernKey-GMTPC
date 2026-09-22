using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ModernKey
{
    public partial class VolumeOsdWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private readonly List<Border> _barElements = new List<Border>(25);
        private readonly DispatcherTimer _hideTimer;
        private Storyboard _fadeStoryboard;

        // Frozen Brushes tối ưu hiệu năng bộ nhớ và render
        private static readonly SolidColorBrush _brushWhite;
        private static readonly SolidColorBrush _brushMuteRed;
        private static readonly SolidColorBrush _brushMutedBar;
        private static readonly SolidColorBrush _brushInactiveBar;

        static VolumeOsdWindow()
        {
            _brushWhite = Brushes.White;

            _brushMuteRed = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4D4D"));
            _brushMuteRed.Freeze();

            _brushMutedBar = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66FFFFFF"));
            _brushMutedBar.Freeze();

            _brushInactiveBar = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            _brushInactiveBar.Freeze();
        }

        public static VolumeOsdWindow Instance { get; private set; }

        public VolumeOsdWindow()
        {
            InitializeComponent();
            Instance = this;
            Loaded += VolumeOsdWindow_Loaded;
            InitializeVolumeBars();

            _hideTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1.2)
            };
            _hideTimer.Tick += HideTimer_Tick;

            InitFadeStoryboard();
        }

        private void VolumeOsdWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        }

        private void InitializeVolumeBars()
        {
            VolumeBarsContainer.Children.Clear();
            _barElements.Clear();

            for (int i = 0; i < 25; i++)
            {
                var bar = new Border
                {
                    Width = 4.5,
                    Height = 4.5,
                    CornerRadius = new CornerRadius(1),
                    Background = _brushInactiveBar,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = (i == 24) ? new Thickness(0) : new Thickness(0, 0, 16.5, 0)
                };

                _barElements.Add(bar);
                VolumeBarsContainer.Children.Add(bar);
            }
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

        public void ShowVolume(float volume, bool isMuted)
        {
            // Nếu không ở UI Thread, gửi tác vụ bất đồng bộ mức ưu tiên Send để giải phóng hook thread ngay lập tức
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ShowVolume(volume, isMuted)));
                return;
            }

            int activeCount = (int)Math.Round(Math.Max(0.0f, Math.Min(1.0f, volume)) * 25.0f);

            if (isMuted)
            {
                MuteSlash.Visibility = Visibility.Visible;
                VolumeTriangle.Stroke = _brushMuteRed;
            }
            else
            {
                MuteSlash.Visibility = Visibility.Collapsed;
                VolumeTriangle.Stroke = _brushWhite;
            }

            for (int i = 0; i < 25; i++)
            {
                var bar = _barElements[i];
                if (i < activeCount)
                {
                    bar.Height = 26;
                    bar.Background = isMuted ? _brushMutedBar : _brushWhite;
                }
                else
                {
                    bar.Height = 4.5;
                    bar.Background = _brushInactiveBar;
                }
            }

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Bottom - Height - 14;

            _fadeStoryboard.Stop();
            RootBorder.Opacity = 0.96;
            Show();
            Topmost = true;

            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}
