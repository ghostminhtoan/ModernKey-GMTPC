using System;
using System.Windows;
using System.Windows.Input;

namespace ModernKey
{
    public partial class StickyNoteWindow : Window
    {
        public StickyNoteWindow(string initialText)
        {
            InitializeComponent();
            TxtContent.Text = initialText ?? string.Empty;
            TxtTime.Text = DateTime.Now.ToString("HH:mm:ss");
            UpdateCharCount();

            TxtContent.TextChanged += (s, e) => UpdateCharCount();

            // Đặt vị trí xuất hiện góc trên bên phải màn hình
            Left = SystemParameters.WorkArea.Right - Width - 30;
            Top = SystemParameters.WorkArea.Top + 50;
        }

        private void UpdateCharCount()
        {
            int len = TxtContent.Text?.Length ?? 0;
            TxtCharCount.Text = $"{len} ký tự";
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnPinToggle_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            BtnPinToggle.Foreground = Topmost ? (System.Windows.Media.Brush)FindResource("CyberNeonCyan") : System.Windows.Media.Brushes.Gray;
            BtnPinToggle.BorderBrush = Topmost ? (System.Windows.Media.Brush)FindResource("CyberNeonCyan") : System.Windows.Media.Brushes.Gray;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtContent.Text))
                {
                    Clipboard.SetText(TxtContent.Text);
                }
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
