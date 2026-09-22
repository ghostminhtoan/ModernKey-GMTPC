using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey
{
    public partial class ComfortShortcutsWindow : Window
    {
        private readonly ComfortShortcutManager _manager = ComfortShortcutManager.Instance;
        private ComfortShortcutItem _selectedItem;
        private bool _isUpdatingUi = false;
        private readonly Dictionary<string, Button> _keyButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _originalKeyLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ComfortShortcutsWindow()
        {
            InitializeComponent();
            PopulateProfilesCombo();
            InitActionTypeCombo();
            PopulateReplaceWithKeyCombo();
            PopulateRunAppCombo();
            BuildVirtualKeyboard();
            PopulateTreeView();
        }

        private void PopulateProfilesCombo()
        {
            _isUpdatingUi = true;
            CmbProfiles.Items.Clear();
            foreach (var profile in _manager.AvailableProfiles)
            {
                var cbi = new ComboBoxItem { Content = profile, Tag = profile };
                CmbProfiles.Items.Add(cbi);
                if (string.Equals(profile, _manager.CurrentProfile, StringComparison.OrdinalIgnoreCase))
                {
                    CmbProfiles.SelectedItem = cbi;
                }
            }
            if (CmbProfiles.SelectedItem == null && CmbProfiles.Items.Count > 0)
            {
                CmbProfiles.SelectedIndex = 0;
            }
            _isUpdatingUi = false;
        }

        private static readonly string[] StandardReplaceKeys = new string[]
        {
            "08 - Backspace", "09 - Tab", "0C - Clear", "0D - Enter", "13 - Pause", "14 - Caps",
            "1B - Esc", "20 - Space", "21 - PgUp", "22 - PgDn", "23 - End", "24 - Home",
            "25 - Left", "26 - Up", "27 - Right", "28 - Down", "2C - PrtSc", "2D - Ins", "2E - Del",
            "30 - 0", "31 - 1", "32 - 2", "33 - 3", "34 - 4", "35 - 5", "36 - 6", "37 - 7", "38 - 8", "39 - 9",
            "41 - A", "42 - B", "43 - C", "44 - D", "45 - E", "46 - F", "47 - G", "48 - H", "49 - I", "4A - J",
            "4B - K", "4C - L", "4D - M", "4E - N", "4F - O", "50 - P", "51 - Q", "52 - R", "53 - S", "54 - T",
            "55 - U", "56 - V", "57 - W", "58 - X", "59 - Y", "5A - Z",
            "5B - Win", "5C - RightWin", "5D - Apps",
            "60 - Num 0", "61 - Num 1", "62 - Num 2", "63 - Num 3", "64 - Num 4",
            "65 - Num 5", "66 - Num 6", "67 - Num 7", "68 - Num 8", "69 - Num 9",
            "6A - Num *", "6B - Num +", "6D - Num -", "6E - Num .", "6F - Num /",
            "70 - F1", "71 - F2", "72 - F3", "73 - F4", "74 - F5", "75 - F6",
            "76 - F7", "77 - F8", "78 - F9", "79 - F10", "7A - F11", "7B - F12",
            "90 - NumLock", "91 - ScrollLock",
            "A0 - LeftShift", "A1 - RightShift", "A2 - LeftCtrl", "A3 - RightCtrl", "A4 - LeftAlt", "A5 - RightAlt",
            "AD - Mute", "AE - Vol -", "AF - Vol +", "B0 - Next Track", "B1 - Prev Track", "B2 - Stop", "B3 - Play/Pause",
            "BA - ;", "BB - =", "BC - ,", "BD - -", "BE - .", "BF - /", "C0 - ~",
            "DB - [", "DC - \\", "DD - ]", "DE - '"
        };

        private void PopulateReplaceWithKeyCombo()
        {
            CmbReplaceWithKey.Items.Clear();
            foreach (var rk in StandardReplaceKeys)
            {
                CmbReplaceWithKey.Items.Add(new ComboBoxItem { Content = rk, Tag = rk });
            }
        }

        private void InitActionTypeCombo()
        {
            CmbActionType.Items.Clear();
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Run program", Tag = ShortcutActionType.RunProgram });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Open URL", Tag = ShortcutActionType.OpenUrl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Paste text", Tag = ShortcutActionType.PasteText });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Audio control", Tag = ShortcutActionType.AudioControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Block key or shortcut", Tag = ShortcutActionType.BlockKey });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Replace key or shortcut", Tag = ShortcutActionType.ReplaceKey });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Window control", Tag = ShortcutActionType.WindowControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Monitor control", Tag = ShortcutActionType.MonitorControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "System action", Tag = ShortcutActionType.SystemAction });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Mouse control", Tag = ShortcutActionType.MouseControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Keystroke macro", Tag = ShortcutActionType.KeystrokeMacro });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Change text case", Tag = ShortcutActionType.ChangeCase });
        }

        private void PopulateRunAppCombo()
        {
            try
            {
                var runningProcs = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) || IsCommonApp(p.ProcessName))
                    .OrderBy(p => p.ProcessName)
                    .Take(20)
                    .ToList();

                foreach (var proc in runningProcs)
                {
                    try
                    {
                        string exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            CmbRunAddApp.Items.Add(new ComboBoxItem
                            {
                                Content = $"💻 {proc.ProcessName} ({Path.GetFileName(exePath)})",
                                Tag = exePath
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        #region Bàn Phím Ảo Trực Quan (Visual Interactive Keyboard)

        private void BuildVirtualKeyboard()
        {
            PnlVirtualKeyboard.Children.Clear();
            _keyButtons.Clear();

            // 1. HÀNG 0: Multimedia & Web Keys (Căn giữa phía trên)
            var pnlMedia = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };
            (string tag, string display, double w)[] mediaKeys = {
                ("Back", "Back", 39), ("Fwd", "Fwd", 39), ("Stop", "Stop", 39), ("Refresh", "Refresh", 50),
                ("Search", "Search", 46), ("Fav", "Fav", 38), ("Home", "Home", 39), ("Mail", "Mail", 39),
                ("Vol -", "Vol -", 41), ("Vol +", "Vol +", 41), ("Mute", "Mute", 39), ("Play", "Play", 39),
                ("Prev", "Prev", 39), ("Next", "Next", 39), ("Media", "Media", 42), ("Calc", "Calc", 39)
            };
            foreach (var mk in mediaKeys)
            {
                pnlMedia.Children.Add(CreateKeyButton(mk.tag, mk.display, mk.w, 20, isMultiMedia: true));
            }
            PnlVirtualKeyboard.Children.Add(pnlMedia);

            // 2. HÀNG 1: Function Keys Row (Esc + F1-F12 + PrtSc/ScrLk/Pause)
            var pnlFuncRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            
            // Cụm Function phím chính
            pnlFuncRow.Children.Add(CreateKeyButton("Esc", "Esc", 29, 24));
            pnlFuncRow.Children.Add(new Border { Width = 14 }); // Gap Esc -> F1

            string[] f1_4 = { "F1", "F2", "F3", "F4" };
            foreach (var f in f1_4) pnlFuncRow.Children.Add(CreateKeyButton(f, f, 29, 24));
            pnlFuncRow.Children.Add(new Border { Width = 10 }); // Gap F4 -> F5

            string[] f5_8 = { "F5", "F6", "F7", "F8" };
            foreach (var f in f5_8) pnlFuncRow.Children.Add(CreateKeyButton(f, f, 29, 24));
            pnlFuncRow.Children.Add(new Border { Width = 10 }); // Gap F8 -> F9

            string[] f9_12 = { "F9", "F10", "F11", "F12" };
            foreach (var f in f9_12) pnlFuncRow.Children.Add(CreateKeyButton(f, f, 29, 24));

            // Gap giữa Main và Nav
            pnlFuncRow.Children.Add(new Border { Width = 23 });

            // Cụm Function Nav
            string[] sysNav = { "PrtSc", "ScrLk", "Pause" };
            foreach (var s in sysNav) pnlFuncRow.Children.Add(CreateKeyButton(s, s, 29, 24));

            // Gap giữa Nav và Numpad
            pnlFuncRow.Children.Add(new Border { Width = 12 });
            // Khoảng trống trên đầu Numpad
            pnlFuncRow.Children.Add(new Border { Width = 124, Height = 24 });

            PnlVirtualKeyboard.Children.Add(pnlFuncRow);

            // 3. THÂN BÀN PHÍM: 3 KHỐI RIÊNG BIỆT (MAIN ALPHANUMERIC - NAVIGATION - NUMPAD)
            var pnlBody = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            // === KHỐI 1: MAIN ALPHANUMERIC BLOCK (15U Width = 465px) ===
            var pnlMainBlock = new StackPanel { Orientation = Orientation.Vertical, Width = 465 };

            // Hàng 1: Number Row (~ 1..0 - = Backspace)
            var rowNum = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            string[] numKeys = { "~", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
            foreach (var k in numKeys) rowNum.Children.Add(CreateKeyButton(k, k, 29, 26));
            rowNum.Children.Add(CreateKeyButton("Backspace", "⌫ Back", 60, 26));
            pnlMainBlock.Children.Add(rowNum);

            // Hàng 2: QWERTY Row (Tab Q..P [ ] \)
            var rowQwerty = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowQwerty.Children.Add(CreateKeyButton("Tab", "Tab", 44.5, 26));
            string[] qwertyKeys = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]" };
            foreach (var k in qwertyKeys) rowQwerty.Children.Add(CreateKeyButton(k, k, 29, 26));
            rowQwerty.Children.Add(CreateKeyButton("\\", "\\", 44.5, 26));
            pnlMainBlock.Children.Add(rowQwerty);

            // Hàng 3: Home Row (Caps A..L ; ' Enter)
            var rowHome = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowHome.Children.Add(CreateKeyButton("Caps", "Caps", 52, 26));
            string[] homeKeys = { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'" };
            foreach (var k in homeKeys) rowHome.Children.Add(CreateKeyButton(k, k, 29, 26));
            rowHome.Children.Add(CreateKeyButton("Enter", "Enter", 68, 26));
            pnlMainBlock.Children.Add(rowHome);

            // Hàng 4: Shift Row (L-Shift Z../ R-Shift)
            var rowShift = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowShift.Children.Add(CreateKeyButton("LeftShift", "L-Shift", 68, 26));
            string[] shiftKeys = { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" };
            foreach (var k in shiftKeys) rowShift.Children.Add(CreateKeyButton(k, k, 29, 26));
            rowShift.Children.Add(CreateKeyButton("RightShift", "R-Shift", 83, 26));
            pnlMainBlock.Children.Add(rowShift);

            // Hàng 5: Bottom Modifiers Row (Ctrl Win Alt Space Alt Win Menu Ctrl)
            var rowBottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowBottom.Children.Add(CreateKeyButton("LeftCtrl", "L-Ctrl", 37, 26));
            rowBottom.Children.Add(CreateKeyButton("LeftWin", "L-Win", 37, 26));
            rowBottom.Children.Add(CreateKeyButton("LeftAlt", "L-Alt", 37, 26));
            rowBottom.Children.Add(CreateKeyButton("Space", "Space", 187, 26));
            rowBottom.Children.Add(CreateKeyButton("RightAlt", "R-Alt", 37, 26));
            rowBottom.Children.Add(CreateKeyButton("RightWin", "R-Win", 37, 26));
            var btnApps = CreateKeyButton("Apps", "Apps", 37, 26);
            _keyButtons["Menu"] = btnApps;
            _originalKeyLabels["Menu"] = "Apps";
            rowBottom.Children.Add(btnApps);
            rowBottom.Children.Add(CreateKeyButton("RightCtrl", "R-Ctrl", 37, 26));
            pnlMainBlock.Children.Add(rowBottom);

            pnlBody.Children.Add(pnlMainBlock);

            // Gap giữa Main và Nav
            pnlBody.Children.Add(new Border { Width = 12 });

            // === KHỐI 2: NAVIGATION & ARROW CLUSTER (3U Width = 93px) ===
            var pnlNavBlock = new StackPanel { Orientation = Orientation.Vertical, Width = 93 };

            // Nav Row 1: Ins, Home, PgUp
            var rowNav1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            string[] nav1 = { "Ins", "Home", "PgUp" };
            foreach (var k in nav1) rowNav1.Children.Add(CreateKeyButton(k, k, 29, 26));
            pnlNavBlock.Children.Add(rowNav1);

            // Nav Row 2: Del, End, PgDn
            var rowNav2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            string[] nav2 = { "Del", "End", "PgDn" };
            foreach (var k in nav2) rowNav2.Children.Add(CreateKeyButton(k, k, 29, 26));
            pnlNavBlock.Children.Add(rowNav2);

            // Nav Row 3: Khoảng trống giữa cụm biên tập và cụm mũi tên
            pnlNavBlock.Children.Add(new Border { Height = 28 });

            // Nav Row 4: Mũi tên Up (chữ T ngược)
            var rowNav4 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowNav4.Children.Add(new Border { Width = 31 }); // Khoảng trống bên trái Up
            rowNav4.Children.Add(CreateKeyButton("Up", "▲", 29, 26));
            rowNav4.Children.Add(new Border { Width = 31 }); // Khoảng trống bên phải Up
            pnlNavBlock.Children.Add(rowNav4);

            // Nav Row 5: Mũi tên Left, Down, Right
            var rowNav5 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            rowNav5.Children.Add(CreateKeyButton("Left", "◀", 29, 26));
            rowNav5.Children.Add(CreateKeyButton("Down", "▼", 29, 26));
            rowNav5.Children.Add(CreateKeyButton("Right", "▶", 29, 26));
            pnlNavBlock.Children.Add(rowNav5);

            pnlBody.Children.Add(pnlNavBlock);

            // Gap giữa Nav và Numpad
            pnlBody.Children.Add(new Border { Width = 12 });

            // === KHỐI 3: NUMPAD CLUSTER (4 Cột x 5 Hàng Lưới Chuẩn = 124px) ===
            var gridNumpad = new Grid { Width = 124 };
            for (int i = 0; i < 4; i++) gridNumpad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
            for (int i = 0; i < 5; i++) gridNumpad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            // Hàng 0: NumLock, /, *, -
            AddKeyToGrid(gridNumpad, CreateKeyButton("NumLock", "Num", 29, 26), 0, 0);
            AddKeyToGrid(gridNumpad, CreateKeyButton("/", "/", 29, 26), 0, 1);
            AddKeyToGrid(gridNumpad, CreateKeyButton("*", "*", 29, 26), 0, 2);
            AddKeyToGrid(gridNumpad, CreateKeyButton("-", "-", 29, 26), 0, 3);

            // Hàng 1: 7, 8, 9, + (cao 2 hàng)
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 7", "7", 29, 26), 1, 0);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 8", "8", 29, 26), 1, 1);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 9", "9", 29, 26), 1, 2);
            AddKeyToGrid(gridNumpad, CreateKeyButton("+", "+", 29, 54), 1, 3, rowSpan: 2);

            // Hàng 2: 4, 5, 6
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 4", "4", 29, 26), 2, 0);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 5", "5", 29, 26), 2, 1);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 6", "6", 29, 26), 2, 2);

            // Hàng 3: 1, 2, 3, Enter (cao 2 hàng)
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 1", "1", 29, 26), 3, 0);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 2", "2", 29, 26), 3, 1);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 3", "3", 29, 26), 3, 2);
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num Enter", "Enter", 29, 54), 3, 3, rowSpan: 2);

            // Hàng 4: 0 (rộng 2 cột), .
            AddKeyToGrid(gridNumpad, CreateKeyButton("Num 0", "0", 60, 26), 4, 0, colSpan: 2);
            AddKeyToGrid(gridNumpad, CreateKeyButton(".", ".", 29, 26), 4, 2);

            pnlBody.Children.Add(gridNumpad);

            PnlVirtualKeyboard.Children.Add(pnlBody);
        }

        private Button CreateKeyButton(string tag, string displayText, double width, double height, bool isMultiMedia = false)
        {
            var btn = new Button
            {
                Content = displayText,
                Tag = tag,
                Width = width,
                Height = height,
                Margin = new Thickness(1),
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                FontSize = isMultiMedia ? 8.5 : (displayText.Length > 4 ? 8.5 : 10),
                Padding = new Thickness(0),
                Background = isMultiMedia ? new SolidColorBrush(Color.FromRgb(15, 30, 45)) : new SolidColorBrush(Color.FromRgb(20, 24, 32)),
                Foreground = isMultiMedia ? new SolidColorBrush(Color.FromRgb(0, 240, 255)) : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 55, 70)),
                BorderThickness = new Thickness(1),
                ToolTip = "Phím: " + tag
            };

            btn.Click += VirtualKey_Click;
            _keyButtons[tag] = btn;
            _originalKeyLabels[tag] = displayText;
            return btn;
        }

        private void AddKeyToGrid(Grid grid, Button btn, int row, int col, int rowSpan = 1, int colSpan = 1)
        {
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            if (rowSpan > 1) Grid.SetRowSpan(btn, rowSpan);
            if (colSpan > 1) Grid.SetColumnSpan(btn, colSpan);
            grid.Children.Add(btn);
        }

        private bool MatchesKeyToken(string combo, string keyName)
        {
            if (string.IsNullOrEmpty(combo) || string.IsNullOrEmpty(keyName)) return false;
            var tokens = combo.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => t.Trim())
                              .ToList();

            foreach (var token in tokens)
            {
                if (string.Equals(token, keyName, StringComparison.OrdinalIgnoreCase)) return true;

                // Khớp phím mũi tên Left/Right/Up/Down
                if (keyName == "Left" && (string.Equals(token, "LeftArrow", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Left", StringComparison.OrdinalIgnoreCase))) return true;
                if (keyName == "Right" && (string.Equals(token, "RightArrow", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Right", StringComparison.OrdinalIgnoreCase))) return true;
                if (keyName == "Up" && (string.Equals(token, "UpArrow", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Up", StringComparison.OrdinalIgnoreCase))) return true;
                if (keyName == "Down" && (string.Equals(token, "DownArrow", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Down", StringComparison.OrdinalIgnoreCase))) return true;

                // Khớp viết tắt Numpad
                if (keyName.StartsWith("Num ") && (string.Equals(token, "Nm " + keyName.Substring(4), StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(token, "NumPad" + keyName.Substring(4), StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(token, "Num" + keyName.Substring(4), StringComparison.OrdinalIgnoreCase))) return true;
                if (token.StartsWith("Num ") && (string.Equals(keyName, "Nm " + token.Substring(4), StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(keyName, "NumPad" + token.Substring(4), StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(keyName, "Num" + token.Substring(4), StringComparison.OrdinalIgnoreCase))) return true;
                if (keyName.StartsWith("Nm ") && string.Equals(token, "Num " + keyName.Substring(3), StringComparison.OrdinalIgnoreCase)) return true;
                if (token.StartsWith("Nm ") && string.Equals(keyName, "Num " + token.Substring(3), StringComparison.OrdinalIgnoreCase)) return true;

                // Khớp phím modifier Left/Right vs Normal
                if ((keyName == "LeftCtrl" || keyName == "RightCtrl") && string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftAlt" || keyName == "RightAlt") && string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftShift" || keyName == "RightShift") && string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftWin" || keyName == "RightWin") && string.Equals(token, "Win", StringComparison.OrdinalIgnoreCase)) return true;

                // Khớp phím Apps/Menu và Pause/Break
                if ((keyName == "Menu" || keyName == "Apps") && (string.Equals(token, "Apps", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Menu", StringComparison.OrdinalIgnoreCase))) return true;
                if (keyName == "Pause" && (string.Equals(token, "Pause", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Break", StringComparison.OrdinalIgnoreCase))) return true;

                // Khớp phím PrtSc / Print / PrintScreen
                if ((keyName == "PrtSc" || keyName == "Print" || keyName == "PrintScreen") &&
                    (string.Equals(token, "PrtSc", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "Print", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "PrintScreen", StringComparison.OrdinalIgnoreCase))) return true;
            }

            return false;
        }

        private void VirtualKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string keyName)
            {
                // Tìm kiếm chính xác theo token xem có shortcut nào khớp với phím này không
                var match = _manager.Shortcuts.FirstOrDefault(s => MatchesKeyToken(s.KeyCombination, keyName));
                if (match != null)
                {
                    SelectShortcutInTree(match);
                }
                else
                {
                    // Đề xuất gán phím mới
                    if (_selectedItem != null)
                    {
                        var res = MessageBox.Show($"Bạn có muốn đổi tổ hợp phím của mục '{_selectedItem.Label}' thành '{keyName}' không?", "Gán phím nhanh", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (res == MessageBoxResult.Yes)
                        {
                            _selectedItem.KeyCombination = keyName;
                            TxtKeyCombination.Text = keyName;
                            _selectedItem.LastChanged = DateTime.Now;
                            TxtLastChanged.Text = _selectedItem.LastChanged.ToString("M/d/yyyy h:mm:ss tt");
                            PopulateTreeView();
                            HighlightVirtualKeyboard();
                        }
                    }
                }
            }
        }

        private string GetCleanKeyName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int idx = raw.IndexOf('-');
            if (idx >= 0 && idx < raw.Length - 1)
            {
                return raw.Substring(idx + 1).Trim();
            }
            return raw.Trim();
        }

        private void HighlightVirtualKeyboard()
        {
            // Reset tất cả phím về trạng thái mặc định
            foreach (var kv in _keyButtons)
            {
                var btn = kv.Value;
                bool isMm = btn.Background is SolidColorBrush scb && scb.Color.B > 40;
                btn.Background = isMm ? new SolidColorBrush(Color.FromRgb(15, 30, 45)) : new SolidColorBrush(Color.FromRgb(20, 24, 32));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 55, 70));
                btn.Foreground = isMm ? new SolidColorBrush(Color.FromRgb(0, 240, 255)) : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                btn.FontWeight = FontWeights.Normal;
                if (_originalKeyLabels.TryGetValue(kv.Key, out var orig))
                {
                    btn.Content = orig;
                    btn.FontSize = isMm ? 8.5 : (orig.Length > 4 ? 8.5 : 10);
                }
                btn.ToolTip = "Phím: " + kv.Key;
            }

            // Đánh dấu các phím có trong danh sách shortcut chung
            foreach (var sc in _manager.Shortcuts)
            {
                foreach (var kv in _keyButtons)
                {
                    if (MatchesKeyToken(sc.KeyCombination, kv.Key))
                    {
                        if (sc.ActionType == ShortcutActionType.BlockKey)
                        {
                            // Chuẩn Comfort Keys Pro: Phím bị Block có biểu tượng ❌ màu đỏ
                            kv.Value.Background = new SolidColorBrush(Color.FromRgb(45, 12, 18));
                            kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 51, 102));
                            kv.Value.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 110));
                            kv.Value.FontWeight = FontWeights.Bold;
                            string origText = _originalKeyLabels.TryGetValue(kv.Key, out var ot) ? ot : kv.Key;
                            kv.Value.Content = "❌" + (kv.Value.Width > 36 ? " " + origText : "");
                            kv.Value.FontSize = 9.0;
                            kv.Value.ToolTip = $"🚫 [BLOCKED] Phím {kv.Key} bị chặn hoàn toàn";
                        }
                        else if (sc.ActionType == ShortcutActionType.ReplaceKey)
                        {
                            // Chuẩn Comfort Keys Pro: Phím bị Replace có viền màu vàng kim hổ phách
                            kv.Value.Background = new SolidColorBrush(Color.FromRgb(48, 38, 10));
                            kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 200, 0));
                            kv.Value.Foreground = new SolidColorBrush(Color.FromRgb(255, 225, 80));
                            string cleanTarget = GetCleanKeyName(sc.ReplaceWithKey);
                            kv.Value.ToolTip = $"🔄 [REPLACED] Thay bằng {cleanTarget}";
                        }
                        else
                        {
                            kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 180, 210));
                        }
                    }
                }
            }

            // Đánh dấu nổi bật mục ĐANG CHỌN (_selectedItem)
            if (_selectedItem != null && !string.IsNullOrEmpty(_selectedItem.KeyCombination))
            {
                string combo = _selectedItem.KeyCombination;

                foreach (var kv in _keyButtons)
                {
                    if (MatchesKeyToken(combo, kv.Key))
                    {
                        if (_selectedItem.ActionType == ShortcutActionType.ReplaceKey)
                        {
                            // Chuẩn Comfort Keys Pro 100%: Highlight màu VÀNG ÓNG rực rỡ (#FFC800) với biểu tượng phím đích!
                            kv.Value.Background = new SolidColorBrush(Color.FromRgb(255, 200, 0));
                            kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(210, 150, 0));
                            kv.Value.Foreground = new SolidColorBrush(Color.FromRgb(15, 15, 15));
                            kv.Value.FontWeight = FontWeights.Bold;

                            string targetClean = GetCleanKeyName(_selectedItem.ReplaceWithKey);
                            if (string.Equals(targetClean, "Win", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(targetClean, "RightWin", StringComparison.OrdinalIgnoreCase))
                            {
                                kv.Value.Content = "❖ Win";
                            }
                            else if (!string.IsNullOrEmpty(targetClean))
                            {
                                kv.Value.Content = targetClean;
                            }
                            kv.Value.FontSize = 9.5;
                            kv.Value.ToolTip = $"🔄 [ĐANG THAY THẾ] Phím {kv.Key} ➔ {targetClean}";
                        }
                        else if (_selectedItem.ActionType == ShortcutActionType.BlockKey)
                        {
                            // Chuẩn Comfort Keys Pro: Block đang chọn highlight đỏ đậm rực rỡ
                            kv.Value.Background = new SolidColorBrush(Color.FromRgb(200, 20, 50));
                            kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 100, 130));
                            kv.Value.Foreground = new SolidColorBrush(Colors.White);
                            kv.Value.FontWeight = FontWeights.Bold;
                            string origText = _originalKeyLabels.TryGetValue(kv.Key, out var ot) ? ot : kv.Key;
                            kv.Value.Content = "❌" + (kv.Value.Width > 36 ? " " + origText : "");
                            kv.Value.FontSize = 9.0;
                            kv.Value.ToolTip = $"🚫 [ĐANG CHẶN] Phím {kv.Key} bị chặn hoàn toàn";
                        }
                        else
                        {
                            bool isMod = kv.Key.Contains("Ctrl") || kv.Key.Contains("Alt") || kv.Key.Contains("Shift") || kv.Key.Contains("Win");
                            HighlightKey(kv.Key, isMod);
                        }
                    }
                }
            }
        }

        private void HighlightKey(string keyName, bool isModifier)
        {
            if (_keyButtons.TryGetValue(keyName, out var btn))
            {
                if (isModifier)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(50, 45, 10));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 230, 0));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 0));
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(40, 15, 25));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 128));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }
        }

        #endregion

        #region TreeView & Data Binding

        private void PopulateTreeView(string filter = null)
        {
            TvShortcuts.Items.Clear();

            var categories = new[]
            {
                "Run program",
                "Paste text",
                "Comfort Keys Pro actions",
                "Audio control",
                "Window control",
                "Monitor control",
                "System actions",
                "Mouse control",
                "Keystroke macro",
                "Change text case",
                "Change language, layout or case",
                "Block key or shortcut",
                "Replace key or shortcut",
                "Open URL"
            };

            var groupNodes = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var cat in categories)
            {
                var node = new TreeViewItem
                {
                    Header = GetCategoryHeader(cat),
                    Tag = cat,
                    IsExpanded = true
                };
                groupNodes[cat] = node;
                TvShortcuts.Items.Add(node);
            }

            string q = (filter ?? TxtSearch?.Text)?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(q);

            foreach (var item in _manager.Shortcuts)
            {
                if (hasFilter)
                {
                    bool match = (item.KeyCombination != null && item.KeyCombination.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.Label != null && item.Label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.TargetApp != null && item.TargetApp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.Category != null && item.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.ProgramPaths != null && item.ProgramPaths.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.PasteText != null && item.PasteText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.Urls != null && item.Urls.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                              || (item.MacroKeystrokes != null && item.MacroKeystrokes.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!match) continue;
                }

                string cat = string.IsNullOrEmpty(item.Category) ? "Run program" : item.Category;
                if (!groupNodes.TryGetValue(cat, out var parentNode))
                {
                    parentNode = new TreeViewItem { Header = "📁 " + cat, Tag = cat, IsExpanded = true };
                    groupNodes[cat] = parentNode;
                    TvShortcuts.Items.Add(parentNode);
                }

                var itemNode = new TreeViewItem
                {
                    Header = GetItemHeader(item),
                    Tag = item
                };
                parentNode.Items.Add(itemNode);

                if (_selectedItem != null && _selectedItem.Id == item.Id)
                {
                    itemNode.IsSelected = true;
                }
            }

            // Xóa các node category rỗng nếu đang filter
            if (hasFilter)
            {
                for (int i = TvShortcuts.Items.Count - 1; i >= 0; i--)
                {
                    if (TvShortcuts.Items[i] is TreeViewItem tvi && tvi.Items.Count == 0)
                    {
                        TvShortcuts.Items.RemoveAt(i);
                    }
                }
            }

            // Mặc định chọn mục đầu tiên nếu chưa chọn
            if (_selectedItem == null && _manager.Shortcuts.Count > 0)
            {
                SelectShortcutInTree(_manager.Shortcuts[0]);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            PopulateTreeView(TxtSearch.Text);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            PopulateTreeView();
        }

        private string GetCategoryHeader(string cat)
        {
            switch (cat)
            {
                case "Run program": return "🚀 Run program";
                case "Paste text": return "📝 Paste text";
                case "Comfort Keys Pro actions": return "⚡ Comfort Keys Pro actions";
                case "Audio control": return "🔊 Audio control";
                case "Monitor control": return "🖥 Monitor control";
                case "Window control": return "🗔 Window control";
                case "System actions": return "⚙ System actions";
                case "Mouse control": return "🖱 Mouse control";
                case "Keystroke macro": return "⚡ Keystroke macro";
                case "Change text case": return "🔤 Change text case";
                case "Change language, layout or case": return "🌐 Change language, layout or case";
                case "Block key or shortcut": return "🚫 Block key or shortcut";
                case "Replace key or shortcut": return "🔄 Replace key or shortcut";
                case "Open URL": return "🌍 Open URL";
                default: return "📁 " + cat;
            }
        }

        private string GetItemHeader(ComfortShortcutItem item)
        {
            string icon = "⌨";
            switch (item.ActionType)
            {
                case ShortcutActionType.RunProgram: icon = "🚀"; break;
                case ShortcutActionType.OpenUrl: icon = "🌍"; break;
                case ShortcutActionType.PasteText: icon = "📝"; break;
                case ShortcutActionType.AudioControl: icon = "🔊"; break;
                case ShortcutActionType.BlockKey: icon = "🚫"; break;
                case ShortcutActionType.ReplaceKey: icon = "🔄"; break;
                case ShortcutActionType.WindowControl: icon = "🗔"; break;
                case ShortcutActionType.MonitorControl: icon = "🖥"; break;
                case ShortcutActionType.SystemAction: icon = "⚙"; break;
                case ShortcutActionType.MouseControl: icon = "🖱"; break;
                case ShortcutActionType.KeystrokeMacro: icon = "⚡"; break;
                case ShortcutActionType.ChangeCase: icon = "🔤"; break;
            }

            string state = item.IsEnabled ? "" : " [Tắt]";
            return $"{icon} {item.KeyCombination}{state}";
        }

        private void SelectShortcutInTree(ComfortShortcutItem target)
        {
            foreach (TreeViewItem catNode in TvShortcuts.Items)
            {
                foreach (TreeViewItem childNode in catNode.Items)
                {
                    if (childNode.Tag is ComfortShortcutItem item && item.Id == target.Id)
                    {
                        childNode.IsSelected = true;
                        childNode.BringIntoView();
                        return;
                    }
                }
            }
        }

        private void TvShortcuts_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is ComfortShortcutItem item)
            {
                BindItemToInspector(item);
            }
        }

        private void BindItemToInspector(ComfortShortcutItem item)
        {
            _isUpdatingUi = true;
            _selectedItem = item;

            ChkShortcutEnabled.IsChecked = item.IsEnabled;
            TxtKeyCombination.Text = item.KeyCombination ?? string.Empty;
            TxtLabel.Text = item.Label ?? string.Empty;
            TxtTargetApp.Text = item.TargetApp ?? string.Empty;
            TxtSoundPath.Text = item.SoundPath ?? string.Empty;
            TxtTriggerCount.Text = $"{item.TriggerCount} lần";
            TxtLastUsed.Text = item.LastUsed.HasValue ? item.LastUsed.Value.ToString("M/d/yyyy h:mm:ss tt") : "Chưa dùng";
            TxtLastChanged.Text = item.LastChanged.ToString("M/d/yyyy h:mm:ss tt");

            // Kiểm tra xung đột phím tắt (Gợi ý 18)
            var conflicts = _manager.CheckConflicts(item.KeyCombination, item.Id);
            if (conflicts != null && conflicts.Count > 0)
            {
                PnlConflictWarning.Visibility = Visibility.Visible;
                TxtConflictWarning.Text = string.Join("\n", conflicts);
            }
            else
            {
                PnlConflictWarning.Visibility = Visibility.Collapsed;
            }

            // Chọn ActionType ComboBox
            foreach (ComboBoxItem cbi in CmbActionType.Items)
            {
                if (cbi.Tag is ShortcutActionType sat && sat == item.ActionType)
                {
                    CmbActionType.SelectedItem = cbi;
                    break;
                }
            }

            // Chọn ActiveScope
            foreach (ComboBoxItem cbi in CmbActiveScope.Items)
            {
                if (string.Equals(cbi.Content?.ToString(), item.ActiveScope, StringComparison.OrdinalIgnoreCase))
                {
                    CmbActiveScope.SelectedItem = cbi;
                    break;
                }
            }

            // 1. Run program
            TxtRunProgramPaths.Text = item.ProgramPaths ?? string.Empty;
            TxtRunStartInFolder.Text = item.StartInFolder ?? string.Empty;
            ChkRunSwitchAlreadyLaunched.IsChecked = item.SwitchToAlreadyLaunched;

            // 2. Open URL
            TxtOpenUrls.Text = item.Urls ?? string.Empty;
            foreach (ComboBoxItem cbi in CmbUrlOpenType.Items)
            {
                if (string.Equals(cbi.Content?.ToString(), item.UrlOpenType, StringComparison.OrdinalIgnoreCase))
                {
                    CmbUrlOpenType.SelectedItem = cbi;
                    break;
                }
            }

            // 3. Paste text
            TxtPasteContent.Text = item.PasteText ?? string.Empty;
            ChkShowTextOnKeyboard.IsChecked = item.ShowTextOnKeyboard;
            ChkPasteAsPlainText.IsChecked = item.PasteAsPlainText;

            // 4. Audio control
            foreach (ListBoxItem lbi in LstAudioActions.Items)
            {
                if (string.Equals(lbi.Tag?.ToString(), item.AudioAction, StringComparison.OrdinalIgnoreCase))
                {
                    LstAudioActions.SelectedItem = lbi;
                    break;
                }
            }
            SldAudioStepSize.Value = Math.Max(2, item.AudioStepSize);
            TxtAudioStepVal.Text = (int)SldAudioStepSize.Value + "%";

            // 5. Replace key
            bool foundReplaceKey = false;
            foreach (ComboBoxItem cbi in CmbReplaceWithKey.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.ReplaceWithKey, StringComparison.OrdinalIgnoreCase))
                {
                    CmbReplaceWithKey.SelectedItem = cbi;
                    foundReplaceKey = true;
                    break;
                }
            }
            if (!foundReplaceKey && CmbReplaceWithKey.Items.Count > 0)
            {
                CmbReplaceWithKey.SelectedIndex = 0;
            }

            // Đồng bộ 4 Checkbox modifier (+) chuẩn Comfort Keys Pro
            ChkReplaceShift.IsChecked = item.ReplaceShift;
            ChkReplaceCtrl.IsChecked = item.ReplaceCtrl;
            ChkReplaceAlt.IsChecked = item.ReplaceAlt;
            ChkReplaceWin.IsChecked = item.ReplaceWin;

            // 6. Window control (Gợi ý 6)
            foreach (ComboBoxItem cbi in CmbWindowAction.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.WindowAction, StringComparison.OrdinalIgnoreCase))
                {
                    CmbWindowAction.SelectedItem = cbi;
                    break;
                }
            }
            SldWindowTransparency.Value = item.WindowTransparency > 0 ? item.WindowTransparency : 80;
            TxtWindowTransparencyVal.Text = (int)SldWindowTransparency.Value + "%";
            PnlWindowTransparency.Visibility = string.Equals(item.WindowAction, "SetTransparency", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

            // 7. Monitor control (Gợi ý 7)
            foreach (ComboBoxItem cbi in CmbMonitorAction.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.MonitorAction, StringComparison.OrdinalIgnoreCase))
                {
                    CmbMonitorAction.SelectedItem = cbi;
                    break;
                }
            }

            // 8. System action (Gợi ý 8)
            foreach (ComboBoxItem cbi in CmbSystemAction.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.SystemActionType, StringComparison.OrdinalIgnoreCase))
                {
                    CmbSystemAction.SelectedItem = cbi;
                    break;
                }
            }

            // 9. Mouse control (Gợi ý 9)
            foreach (ComboBoxItem cbi in CmbMouseAction.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.MouseAction, StringComparison.OrdinalIgnoreCase))
                {
                    CmbMouseAction.SelectedItem = cbi;
                    break;
                }
            }

            // 10. Keystroke macro (Gợi ý 10)
            TxtMacroKeystrokes.Text = item.MacroKeystrokes ?? string.Empty;

            // 11. Change case (Gợi ý 14)
            foreach (ComboBoxItem cbi in CmbChangeCaseMode.Items)
            {
                if (string.Equals(cbi.Tag?.ToString(), item.ChangeCaseMode, StringComparison.OrdinalIgnoreCase))
                {
                    CmbChangeCaseMode.SelectedItem = cbi;
                    break;
                }
            }

            SwitchActionPanel(item.ActionType);
            HighlightVirtualKeyboard();

            _isUpdatingUi = false;
        }

        private void SwitchActionPanel(ShortcutActionType type)
        {
            PanelRunProgram.Visibility = Visibility.Collapsed;
            PanelOpenUrl.Visibility = Visibility.Collapsed;
            PanelPasteText.Visibility = Visibility.Collapsed;
            PanelAudioControl.Visibility = Visibility.Collapsed;
            PanelBlockKey.Visibility = Visibility.Collapsed;
            PanelReplaceKey.Visibility = Visibility.Collapsed;
            PanelWindowControl.Visibility = Visibility.Collapsed;
            PanelMonitorControl.Visibility = Visibility.Collapsed;
            PanelSystemAction.Visibility = Visibility.Collapsed;
            PanelMouseControl.Visibility = Visibility.Collapsed;
            PanelKeystrokeMacro.Visibility = Visibility.Collapsed;
            PanelChangeCase.Visibility = Visibility.Collapsed;
            PanelOtherActions.Visibility = Visibility.Collapsed;

            switch (type)
            {
                case ShortcutActionType.RunProgram:
                    PanelRunProgram.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.OpenUrl:
                    PanelOpenUrl.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.PasteText:
                    PanelPasteText.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.AudioControl:
                    PanelAudioControl.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.BlockKey:
                    PanelBlockKey.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.ReplaceKey:
                    PanelReplaceKey.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.WindowControl:
                    PanelWindowControl.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.MonitorControl:
                    PanelMonitorControl.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.SystemAction:
                    PanelSystemAction.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.MouseControl:
                    PanelMouseControl.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.KeystrokeMacro:
                    PanelKeystrokeMacro.Visibility = Visibility.Visible;
                    break;
                case ShortcutActionType.ChangeCase:
                    PanelChangeCase.Visibility = Visibility.Visible;
                    break;
                default:
                    PanelOtherActions.Visibility = Visibility.Visible;
                    TxtOtherActionTitle.Text = "Action type : " + type.ToString();
                    break;
            }
        }

        private void CmbReplaceWithKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbReplaceWithKey.SelectedItem is ComboBoxItem cbi && cbi.Tag is string keyStr)
            {
                _selectedItem.ReplaceWithKey = keyStr;
                _selectedItem.LastChanged = DateTime.Now;
                HighlightVirtualKeyboard();
            }
        }

        private void ReplaceModifier_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.ReplaceShift = ChkReplaceShift.IsChecked == true;
            _selectedItem.ReplaceCtrl = ChkReplaceCtrl.IsChecked == true;
            _selectedItem.ReplaceAlt = ChkReplaceAlt.IsChecked == true;
            _selectedItem.ReplaceWin = ChkReplaceWin.IsChecked == true;
            _selectedItem.LastChanged = DateTime.Now;
            HighlightVirtualKeyboard();
        }

        #endregion

        #region Event Handlers - Action Details

        private void CommonField_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbActiveScope.SelectedItem is ComboBoxItem cbi)
            {
                _selectedItem.ActiveScope = cbi.Content?.ToString() ?? "In all screen modes";
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void CmbActionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbActionType.SelectedItem is ComboBoxItem cbi && cbi.Tag is ShortcutActionType sat)
            {
                _selectedItem.ActionType = sat;
                _selectedItem.LastChanged = DateTime.Now;

                // Tự động đồng bộ Category tương ứng theo Comfort Keys Pro
                switch (sat)
                {
                    case ShortcutActionType.BlockKey:
                        _selectedItem.Category = "Block key or shortcut";
                        break;
                    case ShortcutActionType.ReplaceKey:
                        _selectedItem.Category = "Replace key or shortcut";
                        break;
                    case ShortcutActionType.RunProgram:
                        _selectedItem.Category = "Run program";
                        break;
                    case ShortcutActionType.OpenUrl:
                        _selectedItem.Category = "Open URL";
                        break;
                    case ShortcutActionType.PasteText:
                        _selectedItem.Category = "Paste text";
                        break;
                    case ShortcutActionType.AudioControl:
                        _selectedItem.Category = "Audio control";
                        break;
                    case ShortcutActionType.WindowControl:
                        _selectedItem.Category = "Window control";
                        break;
                    case ShortcutActionType.MonitorControl:
                        _selectedItem.Category = "Monitor control";
                        break;
                    case ShortcutActionType.SystemAction:
                        _selectedItem.Category = "System actions";
                        break;
                    case ShortcutActionType.MouseControl:
                        _selectedItem.Category = "Mouse control";
                        break;
                    case ShortcutActionType.KeystrokeMacro:
                        _selectedItem.Category = "Keystroke macro";
                        break;
                    case ShortcutActionType.ChangeCase:
                        _selectedItem.Category = "Change text case";
                        break;
                }

                SwitchActionPanel(sat);
                PopulateTreeView();
                HighlightVirtualKeyboard();
            }
        }

        private void ChkShortcutEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.IsEnabled = ChkShortcutEnabled.IsChecked == true;
            _selectedItem.LastChanged = DateTime.Now;
            PopulateTreeView();
        }

        private void TxtTargetApp_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.TargetApp = TxtTargetApp.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void BtnSelectTargetApp_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Chọn ứng dụng mục tiêu (Target Application)"
            };
            if (dlg.ShowDialog() == true)
            {
                string procName = Path.GetFileNameWithoutExtension(dlg.FileName);
                TxtTargetApp.Text = procName;
            }
        }

        private void ChkPasteAsPlainText_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.PasteAsPlainText = ChkPasteAsPlainText.IsChecked == true;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbWindowAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbWindowAction.SelectedItem is ComboBoxItem cbi && cbi.Tag is string act)
            {
                _selectedItem.WindowAction = act;
                _selectedItem.LastChanged = DateTime.Now;
                PnlWindowTransparency.Visibility = string.Equals(act, "SetTransparency", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SldWindowTransparency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            int val = (int)SldWindowTransparency.Value;
            _selectedItem.WindowTransparency = val;
            TxtWindowTransparencyVal.Text = val + "%";
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbMonitorAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbMonitorAction.SelectedItem is ComboBoxItem cbi && cbi.Tag is string act)
            {
                _selectedItem.MonitorAction = act;
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void CmbSystemAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbSystemAction.SelectedItem is ComboBoxItem cbi && cbi.Tag is string act)
            {
                _selectedItem.SystemActionType = act;
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void CmbMouseAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbMouseAction.SelectedItem is ComboBoxItem cbi && cbi.Tag is string act)
            {
                _selectedItem.MouseAction = act;
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void TxtMacroKeystrokes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.MacroKeystrokes = TxtMacroKeystrokes.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbChangeCaseMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbChangeCaseMode.SelectedItem is ComboBoxItem cbi && cbi.Tag is string mode)
            {
                _selectedItem.ChangeCaseMode = mode;
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbProfiles.SelectedItem is ComboBoxItem cbi && cbi.Tag is string prof)
            {
                _manager.SwitchProfile(prof);
                _selectedItem = null;
                PopulateTreeView();
                HighlightVirtualKeyboard();
            }
        }

        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có muốn hoàn tác (khôi phục) cấu hình phím tắt từ bản sao lưu tự động .bak gần nhất không?", "Khôi phục sao lưu", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                bool ok = _manager.RestoreFromBackup();
                if (ok)
                {
                    _selectedItem = null;
                    PopulateTreeView();
                    HighlightVirtualKeyboard();
                    MessageBox.Show("Đã khôi phục thành công từ file sao lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không tìm thấy file sao lưu hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void TxtSoundPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.SoundPath = TxtSoundPath.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void BtnBrowseSound_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Sound Files (*.wav)|*.wav|All Files (*.*)|*.*",
                Title = "Select Sound File"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtSoundPath.Text = dlg.FileName;
            }
        }

        private void TxtLabel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.Label = TxtLabel.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        // --- 1. RUN PROGRAM (Đợt 2) ---
        private void TxtRunProgramPaths_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.ProgramPaths = TxtRunProgramPaths.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void TxtRunStartInFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.StartInFolder = TxtRunStartInFolder.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void ChkRunSwitchAlreadyLaunched_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.SwitchToAlreadyLaunched = ChkRunSwitchAlreadyLaunched.IsChecked == true;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbRunAddApp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbRunAddApp.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                if (tag == "FILE")
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All Files (*.*)|*.*",
                        Title = "Select Program to Run"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        AppendToRunPaths(dlg.FileName);
                    }
                }
                else if (tag == "FOLDER")
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Select Folder to Open";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            AppendToRunPaths(fbd.SelectedPath);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(tag))
                {
                    AppendToRunPaths(tag);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isUpdatingUi = true;
                    CmbRunAddApp.SelectedIndex = 0;
                    _isUpdatingUi = false;
                }));
            }
        }

        private bool IsCommonApp(string name)
        {
            string lower = name.ToLower();
            return lower == "explorer" || lower == "notepad" || lower == "calc" || lower == "cmd" || lower == "taskmgr" || lower == "modernkey";
        }

        private void AppendToRunPaths(string path)
        {
            if (string.IsNullOrWhiteSpace(TxtRunProgramPaths.Text))
            {
                TxtRunProgramPaths.Text = path;
            }
            else
            {
                TxtRunProgramPaths.Text = TxtRunProgramPaths.Text.TrimEnd() + "\n" + path;
            }
        }

        // --- 2. OPEN URL (Đợt 3) ---
        private void TxtOpenUrls_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.Urls = TxtOpenUrls.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbUrlOpenType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (CmbUrlOpenType.SelectedItem is ComboBoxItem cbi)
            {
                _selectedItem.UrlOpenType = cbi.Content?.ToString() ?? "Default";
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void BtnUrlAdd_Click(object sender, RoutedEventArgs e)
        {
            string defaultUrl = "https://";
            if (string.IsNullOrWhiteSpace(TxtOpenUrls.Text))
            {
                TxtOpenUrls.Text = defaultUrl;
            }
            else
            {
                TxtOpenUrls.Text = TxtOpenUrls.Text.TrimEnd() + "\n" + defaultUrl;
            }
            TxtOpenUrls.Focus();
            TxtOpenUrls.CaretIndex = TxtOpenUrls.Text.Length;
        }

        // --- 3. PASTE TEXT (Đợt 4) ---
        private void TxtPasteContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.PasteText = TxtPasteContent.Text;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void ChkShowTextOnKeyboard_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.ShowTextOnKeyboard = ChkShowTextOnKeyboard.IsChecked == true;
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void CmbInsertTag_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbInsertTag.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    InsertTagIntoEditor(tag);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isUpdatingUi = true;
                    CmbInsertTag.SelectedIndex = 0;
                    _isUpdatingUi = false;
                }));
            }
        }

        private void InsertTagIntoEditor(string tag)
        {
            int idx = TxtPasteContent.CaretIndex;
            if (idx >= 0 && idx <= TxtPasteContent.Text.Length)
            {
                TxtPasteContent.Text = TxtPasteContent.Text.Insert(idx, tag);
                TxtPasteContent.CaretIndex = idx + tag.Length;
            }
            else
            {
                TxtPasteContent.Text += tag;
            }
            TxtPasteContent.Focus();
        }

        // --- 5. AUDIO CONTROL (Đợt 1) ---
        private void LstAudioActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (LstAudioActions.SelectedItem is ListBoxItem lbi && lbi.Tag is string act)
            {
                _selectedItem.AudioAction = act;
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void SldAudioStepSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            int step = (int)SldAudioStepSize.Value;
            _selectedItem.AudioStepSize = step;
            TxtAudioStepVal.Text = step + "%";
            _selectedItem.LastChanged = DateTime.Now;
        }

        #endregion

        #region Toolbar & Window Actions

        private void BtnChangeCombination_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            var dlg = new KeyCombinationDialog(_selectedItem.KeyCombination);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.ResultCombination))
            {
                _selectedItem.KeyCombination = dlg.ResultCombination;
                TxtKeyCombination.Text = dlg.ResultCombination;
                _selectedItem.LastChanged = DateTime.Now;
                TxtLastChanged.Text = _selectedItem.LastChanged.ToString("M/d/yyyy h:mm:ss tt");
                PopulateTreeView();
                HighlightVirtualKeyboard();
            }
        }

        private void BtnAddShortcut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new KeyCombinationDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.ResultCombination))
            {
                string targetCat = "Run program";
                ShortcutActionType targetType = ShortcutActionType.RunProgram;

                if (TvShortcuts.SelectedItem is TreeViewItem tvi)
                {
                    if (tvi.Tag is string catName) targetCat = catName;
                    else if (tvi.Tag is ComfortShortcutItem parentItem) targetCat = parentItem.Category;
                }

                switch (targetCat)
                {
                    case "Block key or shortcut":
                        targetType = ShortcutActionType.BlockKey;
                        break;
                    case "Replace key or shortcut":
                        targetType = ShortcutActionType.ReplaceKey;
                        break;
                    case "Open URL":
                        targetType = ShortcutActionType.OpenUrl;
                        break;
                    case "Paste text":
                        targetType = ShortcutActionType.PasteText;
                        break;
                    case "Audio control":
                        targetType = ShortcutActionType.AudioControl;
                        break;
                    default:
                        targetType = ShortcutActionType.RunProgram;
                        break;
                }

                var newItem = new ComfortShortcutItem
                {
                    KeyCombination = dlg.ResultCombination,
                    Category = targetCat,
                    ActionType = targetType,
                    ReplaceWithKey = "5C - RightWin",
                    Label = "Action (" + dlg.ResultCombination + ")",
                    LastChanged = DateTime.Now
                };
                _manager.Shortcuts.Add(newItem);
                _manager.Save();
                PopulateTreeView();
                SelectShortcutInTree(newItem);
            }
        }

        private void BtnCopyShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            var clone = _selectedItem.Clone();
            clone.Label += " (Copy)";
            _manager.Shortcuts.Add(clone);
            _manager.Save();
            PopulateTreeView();
            SelectShortcutInTree(clone);
        }

        private void BtnDeleteShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            var res = MessageBox.Show($"Bạn có chắc chắn muốn xóa phím tắt '{_selectedItem.KeyCombination}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                _manager.Shortcuts.Remove(_selectedItem);
                _manager.Save();
                _selectedItem = null;
                PopulateTreeView();
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            _manager.Save();
            MessageBox.Show("Đã lưu toàn bộ cấu hình phím tắt Comfort Keys thành công!", "Lưu thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có chắc chắn muốn khôi phục toàn bộ danh sách phím tắt về mặc định ban đầu không?\n(Mọi phím tắt tự tùy chỉnh sẽ được đặt lại theo thiết lập chuẩn)", "Xác nhận khôi phục mặc định", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _manager.RestoreDefaults();
                _selectedItem = null;
                PopulateTreeView();
                HighlightVirtualKeyboard();
                MessageBox.Show("Đã khôi phục toàn bộ danh sách phím tắt về mặc định ban đầu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOpenProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Comfort Keys Actions (*.cka;*.json)|*.cka;*.json|All Files (*.*)|*.*",
                Title = "Mở file Profile Comfort Keys"
            };
            if (dlg.ShowDialog() == true)
            {
                _manager.ImportFromFile(dlg.FileName);
                PopulateTreeView();
                MessageBox.Show("Đã nạp thành công profile: " + Path.GetFileName(dlg.FileName), "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Comfort Keys Actions (*.cka)|*.cka|JSON File (*.json)|*.json",
                FileName = "Comfort Keys default CsActions.cka",
                Title = "Xuất file Profile Comfort Keys"
            };
            if (dlg.ShowDialog() == true)
            {
                _manager.ExportToFile(dlg.FileName);
                MessageBox.Show("Đã xuất profile thành công ra: " + dlg.FileName, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnHelpBanner_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hệ thống phím tắt toàn cục mô phỏng trọn vẹn Comfort Keys Pro v9.1:\n- Phân biệt phím trái / phải (Left / Right Control keys)\n- Quản lý Run program, Open URL, Paste text, Keystroke macro, Audio control, Monitor & Window control.", "Trợ giúp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ModernKey GMTPC - Comfort Shortcuts Manager Cyber Edition\nTác giả: GMTPC (ghostminhtoan@gmail.com)", "Giới thiệu", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
