using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ModernKey
{
    public partial class KeyCombinationDialog : Window
    {
        public string ResultCombination { get; private set; }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private string _lastChosenKey = string.Empty;
        private bool _isUpdatingQuickPalette = false;

        public KeyCombinationDialog(string initialCombo = null)
        {
            InitializeComponent();
            InitQuickPalette();

            if (!string.IsNullOrEmpty(initialCombo))
            {
                ResultCombination = initialCombo;
                TxtCombination.Text = initialCombo;
                ParseInitialCombination(initialCombo);
            }
        }

        private void ParseInitialCombination(string combo)
        {
            _isUpdatingQuickPalette = true;
            try
            {
                var parts = combo.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    string p = part.Trim();
                    if (string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "RightCtrl", StringComparison.OrdinalIgnoreCase))
                    {
                        ChkModCtrl.IsChecked = true;
                    }
                    else if (string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "RightAlt", StringComparison.OrdinalIgnoreCase))
                    {
                        ChkModAlt.IsChecked = true;
                    }
                    else if (string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "LeftShift", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "RightShift", StringComparison.OrdinalIgnoreCase))
                    {
                        ChkModShift.IsChecked = true;
                    }
                    else if (string.Equals(p, "Win", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "LeftWin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p, "RightWin", StringComparison.OrdinalIgnoreCase))
                    {
                        ChkModWin.IsChecked = true;
                    }
                    else
                    {
                        _lastChosenKey = p;
                    }
                }
            }
            finally
            {
                _isUpdatingQuickPalette = false;
            }
        }

        private void InitQuickPalette()
        {
            // 1. F1..F12
            for (int i = 1; i <= 12; i++)
            {
                string key = "F" + i;
                PnlFKeys.Children.Add(CreatePaletteButton(key, 40, 24));
            }

            // 2. 0..9 (Top row numbers)
            string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "`" };
            foreach (var d in digits)
            {
                PnlDigitKeys.Children.Add(CreatePaletteButton(d, 36, 24));
            }

            // 3. Numpad Nm 0..Nm 9
            string[] numpadKeys = { "Nm 0", "Nm 1", "Nm 2", "Nm 3", "Nm 4", "Nm 5", "Nm 6", "Nm 7", "Nm 8", "Nm 9", "Nm +", "Nm -", "Nm *", "Nm /", "Nm Enter", "Nm ." };
            foreach (var n in numpadKeys)
            {
                PnlNumPadKeys.Children.Add(CreatePaletteButton(n, 58, 24));
            }

            // 4. Special & Navigation Keys
            string[] specialKeys = { "Ins", "Del", "Home", "End", "PgUp", "PgDn", "Space", "Enter", "Tab", "Esc", "Left", "Up", "Right", "Down", "Caps", "Pause", "PrtSc", "Apps", "LeftWin", "RightWin" };
            foreach (var s in specialKeys)
            {
                PnlSpecialKeys.Children.Add(CreatePaletteButton(s, 50, 24));
            }

            // 6. Media & Browser Keys (Chuẩn ảnh 3)
            string[] mediaKeys = {
                "Play/Pause", "Prev Track", "Next Track", "Media Stop",
                "Vol +", "Vol -", "Mute", "Mail", "Media",
                "Browser Back", "Browser Forward", "Browser Refresh", "Browser Stop", "Browser Search", "Browser Favorites", "Browser Home"
            };
            foreach (var m in mediaKeys)
            {
                PnlMediaKeys.Children.Add(CreatePaletteButton(m, 72, 24));
            }
        }

        private Button CreatePaletteButton(string text, double width, double height)
        {
            var btn = new Button
            {
                Content = text,
                Tag = text,
                Width = width,
                Height = height,
                Margin = new Thickness(2, 2, 2, 2),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(20, 26, 36)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 60, 80)),
                Padding = new Thickness(0)
            };
            btn.Click += PaletteButton_Click;
            return btn;
        }

        private void PaletteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string keyName)
            {
                _lastChosenKey = keyName;
                RebuildCombinationFromPalette();
            }
        }

        private void ModifierQuickCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingQuickPalette) return;
            RebuildCombinationFromPalette();
        }

        private void RebuildCombinationFromPalette()
        {
            var parts = new List<string>();
            if (ChkModWin.IsChecked == true) parts.Add("Win");
            if (ChkModCtrl.IsChecked == true) parts.Add("Ctrl");
            if (ChkModAlt.IsChecked == true) parts.Add("Alt");
            if (ChkModShift.IsChecked == true) parts.Add("Shift");

            if (!string.IsNullOrEmpty(_lastChosenKey))
            {
                parts.Add(_lastChosenKey);
            }

            if (parts.Count > 0)
            {
                string combo = string.Join("+", parts);
                ResultCombination = combo;
                TxtCombination.Text = combo;
            }
            else
            {
                ResultCombination = string.Empty;
                TxtCombination.Text = "[ Đang chờ phím... ]";
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            UpdateCombinationFromInput(e.Key, e.SystemKey);
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void UpdateCombinationFromInput(Key normalKey, Key systemKey)
        {
            Key key = (normalKey != Key.None && normalKey != Key.System) ? normalKey : systemKey;

            bool isLeftRight = ChkDistinguishLeftRight.IsChecked == true;

            // Kiểm tra trạng thái phím modifier thực tế
            bool lCtrl = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0;
            bool rCtrl = (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
            bool lAlt = (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0;
            bool rAlt = (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
            bool lShift = (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0;
            bool rShift = (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
            bool lWin = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0;
            bool rWin = (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

            bool hasCtrl = lCtrl || rCtrl;
            bool hasAlt = lAlt || rAlt;
            bool hasShift = lShift || rShift;
            bool hasWin = lWin || rWin;

            // Đồng bộ sang UI Quick Modifiers
            _isUpdatingQuickPalette = true;
            ChkModCtrl.IsChecked = hasCtrl;
            ChkModAlt.IsChecked = hasAlt;
            ChkModShift.IsChecked = hasShift;
            ChkModWin.IsChecked = hasWin;
            _isUpdatingQuickPalette = false;

            var parts = new List<string>();

            if (isLeftRight)
            {
                if (lWin) parts.Add("LeftWin");
                if (rWin) parts.Add("RightWin");
                if (lCtrl) parts.Add("LeftCtrl");
                if (rCtrl) parts.Add("RightCtrl");
                if (lAlt) parts.Add("LeftAlt");
                if (rAlt) parts.Add("RightAlt");
                if (lShift) parts.Add("LeftShift");
                if (rShift) parts.Add("RightShift");
            }
            else
            {
                if (hasWin) parts.Add("Win");
                if (hasCtrl) parts.Add("Ctrl");
                if (hasAlt) parts.Add("Alt");
                if (hasShift) parts.Add("Shift");
            }

            // Bỏ qua nếu chính phím nhấn là phím modifier đơn thuần
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                if (parts.Count > 0)
                {
                    TxtCombination.Text = string.Join("+", parts) + "+...";
                }
                return;
            }

            string mainKeyStr = FormatKeyName(key);
            if (!string.IsNullOrEmpty(mainKeyStr))
            {
                _lastChosenKey = mainKeyStr;
                parts.Add(mainKeyStr);
            }

            if (parts.Count > 0)
            {
                string combo = string.Join("+", parts);
                ResultCombination = combo;
                TxtCombination.Text = combo;
            }
        }

        private string FormatKeyName(Key key)
        {
            // Numpad 0..9
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "Nm " + (key - Key.NumPad0);
            }

            // Top row digits 0..9
            if (key >= Key.D0 && key <= Key.D9)
            {
                return (key - Key.D0).ToString();
            }

            // Letters A..Z
            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString();
            }

            // F1..F12
            if (key >= Key.F1 && key <= Key.F12)
            {
                return key.ToString();
            }

            switch (key)
            {
                case Key.Insert: return "Ins";
                case Key.Delete: return "Del";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PgUp";
                case Key.PageDown: return "PgDn";
                case Key.Left: return "Left";
                case Key.Right: return "Right";
                case Key.Up: return "Up";
                case Key.Down: return "Down";
                case Key.Space: return "Space";
                case Key.Tab: return "Tab";
                case Key.CapsLock: return "Caps";
                case Key.Escape: return "Esc";
                case Key.Pause: return "Pause";
                case Key.PrintScreen: return "PrtSc";
                case Key.Apps: return "Apps";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "+";
                case Key.OemTilde: return "~";
                case Key.Enter: return "Enter";
                case Key.Back: return "Backspace";
                case Key.Add: return "Nm +";
                case Key.Subtract: return "Nm -";
                case Key.Multiply: return "Nm *";
                case Key.Divide: return "Nm /";
                case Key.Decimal: return "Nm .";

                // Media & Browser Keys (Chuẩn ảnh 3)
                case Key.MediaPlayPause: return "Play/Pause";
                case Key.MediaPreviousTrack: return "Prev Track";
                case Key.MediaNextTrack: return "Next Track";
                case Key.MediaStop: return "Media Stop";
                case Key.VolumeUp: return "Vol +";
                case Key.VolumeDown: return "Vol -";
                case Key.VolumeMute: return "Mute";
                case Key.BrowserBack: return "Browser Back";
                case Key.BrowserForward: return "Browser Forward";
                case Key.BrowserRefresh: return "Browser Refresh";
                case Key.BrowserStop: return "Browser Stop";
                case Key.BrowserSearch: return "Browser Search";
                case Key.BrowserFavorites: return "Browser Favorites";
                case Key.BrowserHome: return "Browser Home";
                case Key.LaunchMail: return "Mail";
                case Key.SelectMedia: return "Media";
                case Key.LaunchApplication1: return "App1";
                case Key.LaunchApplication2: return "App2";

                default: return key.ToString();
            }
        }

        private void ChkDistinguishLeftRight_Checked(object sender, RoutedEventArgs e)
        {
            RebuildCombinationFromPalette();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _lastChosenKey = string.Empty;
            _isUpdatingQuickPalette = true;
            ChkModCtrl.IsChecked = false;
            ChkModAlt.IsChecked = false;
            ChkModShift.IsChecked = false;
            ChkModWin.IsChecked = false;
            _isUpdatingQuickPalette = false;

            ResultCombination = string.Empty;
            TxtCombination.Text = "[ Chưa gán phím ]";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ResultCombination) || ResultCombination.EndsWith("+..."))
            {
                MessageBox.Show("Vui lòng nhấn hoặc chọn một tổ hợp phím hợp lệ trước khi bấm OK.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
