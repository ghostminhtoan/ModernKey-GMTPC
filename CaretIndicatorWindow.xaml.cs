using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ModernKey
{
    public partial class CaretIndicatorWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly DispatcherTimer _fadeTimer;

        public CaretIndicatorWindow()
        {
            InitializeComponent();
            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _fadeTimer.Tick += (s, e) =>
            {
                _fadeTimer.Stop();
                Hide();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        public void ShowIndicator(bool isVietnamese)
        {
            TxtLang.Text = isVietnamese ? "[VI]" : "[EN]";

            var color = isVietnamese ? (Color)ColorConverter.ConvertFromString("#00F0FF") : (Color)ColorConverter.ConvertFromString("#FF007F");
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            IndicatorBorder.BorderBrush = brush;
            TxtLang.Foreground = brush;
            GlowEffect.Color = color;

            UpdatePosition();
            Show();
            _fadeTimer.Stop();
            _fadeTimer.Start();
        }

        private void UpdatePosition()
        {
            try
            {
                IntPtr hForeground = GetForegroundWindow();
                if (hForeground != IntPtr.Zero)
                {
                    uint threadId = GetWindowThreadProcessId(hForeground, out _);
                    var gui = new GUITHREADINFO();
                    gui.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));

                    if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndCaret != IntPtr.Zero && (gui.rcCaret.Right - gui.rcCaret.Left >= 0))
                    {
                        var pt = new POINT { X = gui.rcCaret.Left, Y = gui.rcCaret.Bottom };
                        if (ClientToScreen(gui.hwndCaret, ref pt))
                        {
                            Left = pt.X + 8;
                            Top = pt.Y + 4;
                            return;
                        }
                    }
                }
            }
            catch { }

            // Fallback: vị trí con trỏ chuột
            var mouse = System.Windows.Forms.Cursor.Position;
            Left = mouse.X + 12;
            Top = mouse.Y + 16;
        }
    }
}
