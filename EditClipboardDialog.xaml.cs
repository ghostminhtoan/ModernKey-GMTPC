using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ModernKey.Models;

namespace ModernKey
{
    public partial class EditClipboardDialog : Window
    {
        private readonly ClipboardItem _item;
        private uint _currentVk = 0;

        public EditClipboardDialog(ClipboardItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            InitializeComponent();

            Loaded += EditClipboardDialog_Loaded;
        }

        private void EditClipboardDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (_item == null) return;

            TxtItemId.Text = $"ID: #{(_item.Id.Length > 8 ? _item.Id.Substring(0, 8) : _item.Id)}";
            TxtName.Text = _item.CustomTitle ?? string.Empty;

            // 1. Phím tắt
            ChkCtrl.IsChecked = (_item.ShortcutModifiers & 2) != 0;
            ChkAlt.IsChecked = (_item.ShortcutModifiers & 1) != 0;
            ChkShift.IsChecked = (_item.ShortcutModifiers & 4) != 0;
            ChkWin.IsChecked = (_item.ShortcutModifiers & 8) != 0;
            TxtKey.Text = _item.ShortcutKey ?? string.Empty;
            _currentVk = _item.ShortcutVk;

            UpdateShortcutPreview();

            // 2. Màu nền
            SelectColorInCombo(_item.BackgroundColorHex);

            // 3. Nội dung văn bản
            if (_item.ContentType == ClipboardContentType.Text)
            {
                GridContentEditor.Visibility = Visibility.Visible;
                BrdNonTextNotice.Visibility = Visibility.Collapsed;
                TxtContent.Text = _item.TextContent ?? string.Empty;
            }
            else
            {
                GridContentEditor.Visibility = Visibility.Collapsed;
                BrdNonTextNotice.Visibility = Visibility.Visible;
            }

            TxtName.Focus();
            TxtName.SelectAll();
        }

        private void SelectColorInCombo(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                CmbColors.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < CmbColors.Items.Count; i++)
            {
                if (CmbColors.Items[i] is ComboBoxItem cbi && cbi.Tag is string tagHex)
                {
                    if (tagHex.Equals(hex, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbColors.SelectedIndex = i;
                        return;
                    }
                }
            }

            // Nếu màu tùy biến chưa có trong combo: thêm mới
            AddAndSelectColor(hex, $"Tùy biến ({hex})");
        }

        private void AddAndSelectColor(string hex, string label)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var cbi = new ComboBoxItem
                {
                    Tag = hex,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new Border
                            {
                                Width = 14,
                                Height = 14,
                                Background = new SolidColorBrush(color),
                                CornerRadius = new CornerRadius(1),
                                Margin = new Thickness(0, 0, 6, 0)
                            },
                            new TextBlock { Text = label }
                        }
                    }
                };

                CmbColors.Items.Add(cbi);
                CmbColors.SelectedItem = cbi;
            }
            catch { }
        }

        private void CmbColors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Có thể dùng để preview nếu cần
        }

        private void BtnPickCustomColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var cd = new System.Windows.Forms.ColorDialog())
                {
                    cd.AllowFullOpen = true;
                    cd.AnyColor = true;

                    string curHex = GetSelectedColorHex();
                    if (!string.IsNullOrEmpty(curHex) && !curHex.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var wpfCol = (Color)ColorConverter.ConvertFromString(curHex);
                            cd.Color = System.Drawing.Color.FromArgb(wpfCol.R, wpfCol.G, wpfCol.B);
                        }
                        catch { }
                    }

                    if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var c = cd.Color;
                        string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                        AddAndSelectColor(hex, $"Màu đã chọn ({hex})");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi mở bảng màu: " + ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private int GetSelectedModifiers()
        {
            int mod = 0;
            if (ChkAlt.IsChecked == true) mod |= 1;
            if (ChkCtrl.IsChecked == true) mod |= 2;
            if (ChkShift.IsChecked == true) mod |= 4;
            if (ChkWin.IsChecked == true) mod |= 8;
            return mod;
        }

        private string GetSelectedColorHex()
        {
            if (CmbColors.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                return tag;
            }
            return "Default";
        }

        private void ShortcutInput_Changed(object sender, RoutedEventArgs e)
        {
            UpdateShortcutPreview();
        }

        private void TxtKey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Tab) return; // Cho phép tab chuyển control

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            if (key == Key.Escape)
            {
                TxtKey.Text = string.Empty;
                _currentVk = 0;
                e.Handled = true;
                UpdateShortcutPreview();
                return;
            }

            _currentVk = (uint)KeyInterop.VirtualKeyFromKey(key);
            string keyName = GetFriendlyKeyName(key);
            TxtKey.Text = keyName;
            e.Handled = true;

            UpdateShortcutPreview();
        }

        private void TxtKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateShortcutPreview();
        }

        private string GetFriendlyKeyName(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return key.ToString();
            if (key >= Key.D0 && key <= Key.D9) return key.ToString().Substring(1);
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num" + key.ToString().Substring(6);
            if (key >= Key.F1 && key <= Key.F12) return key.ToString();

            switch (key)
            {
                case Key.Space: return "Space";
                case Key.Insert: return "Ins";
                case Key.Delete: return "Del";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PgUp";
                case Key.PageDown: return "PgDn";
                case Key.Back: return "Backspace";
                case Key.Enter: return "Enter";
                case Key.OemTilde: return "~";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "+";
                case Key.OemQuestion: return "?";
                default: return key.ToString();
            }
        }

        private void BtnClearShortcut_Click(object sender, RoutedEventArgs e)
        {
            ChkCtrl.IsChecked = false;
            ChkAlt.IsChecked = false;
            ChkShift.IsChecked = false;
            ChkWin.IsChecked = false;
            TxtKey.Text = string.Empty;
            _currentVk = 0;
            UpdateShortcutPreview();
        }

        private void UpdateShortcutPreview()
        {
            if (TxtShortcutPreview == null) return;

            string keyText = TxtKey?.Text?.Trim()?.ToUpperInvariant();
            int mod = GetSelectedModifiers();

            if (string.IsNullOrEmpty(keyText))
            {
                TxtShortcutPreview.Text = "(Chưa gán)";
                TxtShortcutPreview.Foreground = (Brush)FindResource("CyberTextDim");
                return;
            }

            var parts = new List<string>();
            if ((mod & 2) != 0) parts.Add("Ctrl");
            if ((mod & 1) != 0) parts.Add("Alt");
            if ((mod & 4) != 0) parts.Add("Shift");
            if ((mod & 8) != 0) parts.Add("Win");
            parts.Add(keyText);

            string full = string.Join(" + ", parts);
            TxtShortcutPreview.Text = $"[ {full} ]";
            TxtShortcutPreview.Foreground = (Brush)FindResource("CyberNeonYellow");
        }

        private void ChkWordWrap_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtContent != null) TxtContent.TextWrapping = TextWrapping.Wrap;
        }

        private void ChkWordWrap_Unchecked(object sender, RoutedEventArgs e)
        {
            if (TxtContent != null) TxtContent.TextWrapping = TextWrapping.NoWrap;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (_item != null)
            {
                // 1. Tên gợi nhớ
                _item.CustomTitle = TxtName.Text.Trim();

                // 2. Phím tắt
                string key = TxtKey.Text.Trim().ToUpperInvariant();
                int mod = GetSelectedModifiers();

                if (!string.IsNullOrEmpty(key))
                {
                    _item.ShortcutKey = key;
                    _item.ShortcutModifiers = mod;
                    _item.ShortcutVk = _currentVk;
                }
                else
                {
                    _item.ShortcutKey = string.Empty;
                    _item.ShortcutModifiers = 0;
                    _item.ShortcutVk = 0;
                }

                // 3. Màu nền
                string colorHex = GetSelectedColorHex();
                _item.BackgroundColorHex = colorHex.Equals("Default", StringComparison.OrdinalIgnoreCase) ? string.Empty : colorHex;

                // 4. Nội dung văn bản
                if (_item.ContentType == ClipboardContentType.Text)
                {
                    _item.TextContent = TxtContent.Text ?? string.Empty;
                    _item.CharCount = _item.TextContent.Length;
                    _item.ByteSize = Encoding.UTF8.GetByteCount(_item.TextContent);
                    string p = _item.TextContent.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
                    _item.PreviewText = p.Length > 120 ? p.Substring(0, 117) + "..." : p;
                }
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                BtnOk_Click(sender, null);
                e.Handled = true;
            }
        }
    }
}
