using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ModernKey
{
    public partial class WebAiToastWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static WebAiToastWindow _currentToast;
        private DispatcherTimer _countdownTimer;
        private int _ticksRemaining = 50; // 50 * 100ms = 5 giây

        public WebAiToastWindow(string serviceName)
        {
            InitializeComponent();
            TxtTitle.Text = $"ĐÃ MỞ {serviceName.ToUpper()}";

            Loaded += (s, e) =>
            {
                var workArea = SystemParameters.WorkArea;
                Left = (workArea.Width - ActualWidth) / 2 + workArea.Left;
                Top = workArea.Bottom - ActualHeight - 30;
            };

            Closed += (s, e) =>
            {
                if (_currentToast == this)
                {
                    _currentToast = null;
                }
            };

            // Đếm ngược 5 giây rồi tự động đóng hoàn toàn
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _countdownTimer.Tick += (s, e) =>
            {
                _ticksRemaining--;
                PbCountdown.Value = _ticksRemaining;
                TxtTimer.Text = $"{Math.Ceiling(_ticksRemaining / 10.0)}s";

                if (_ticksRemaining <= 0)
                {
                    _countdownTimer.Stop();
                    Close();
                }
            };
            _countdownTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        public static void ShowToast(string serviceName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_currentToast != null)
                    {
                        _currentToast._countdownTimer?.Stop();
                        _currentToast.Close();
                        _currentToast = null;
                    }

                    _currentToast = new WebAiToastWindow(serviceName);
                    _currentToast.Show();
                }
                catch
                {
                    // Tránh crash nếu ứng dụng đang đóng
                }
            });
        }
    }
}
