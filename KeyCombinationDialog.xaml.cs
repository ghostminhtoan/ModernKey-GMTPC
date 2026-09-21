using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ModernKey.Core;

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

        public KeyCombinationDialog(string initialCombo = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialCombo))
            {
                ResultCombination = initialCombo;
                TxtCombination.Text = initialCombo;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            UpdateCombinationFromInput(e.Key, e.SystemKey);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
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
                if (lWin || rWin) parts.Add("Win");
                if (lCtrl || rCtrl) parts.Add("Ctrl");
                if (lAlt || rAlt) parts.Add("Alt");
                if (lShift || rShift) parts.Add("Shift");
            }

            // Bỏ qua nếu chính phím nhấn là phím modifier
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
            // Numpad
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "Nm " + (key - Key.NumPad0);
            }

            // Digit 0..9
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
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "+";
                case Key.Enter: return "Enter";
                case Key.Back: return "Backspace";
                default: return key.ToString();
            }
        }

        private void ChkDistinguishLeftRight_Checked(object sender, RoutedEventArgs e)
        {
            // Trigger refresh nếu cần
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
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
                System.Windows.MessageBox.Show("Vui lòng nhấn một tổ hợp phím hợp lệ trước khi bấm OK.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
