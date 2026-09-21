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

        public ComfortShortcutsWindow()
        {
            InitializeComponent();
            InitActionTypeCombo();
            PopulateRunAppCombo();
            BuildVirtualKeyboard();
            PopulateTreeView();
        }

        private void InitActionTypeCombo()
        {
            CmbActionType.Items.Clear();
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Run program", Tag = ShortcutActionType.RunProgram });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Open URL", Tag = ShortcutActionType.OpenUrl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Paste text", Tag = ShortcutActionType.PasteText });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Audio control", Tag = ShortcutActionType.AudioControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Window control", Tag = ShortcutActionType.WindowControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Monitor control", Tag = ShortcutActionType.MonitorControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "System action", Tag = ShortcutActionType.SystemAction });
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

            // Hàng 0: Multimedia & Web keys
            string[] row0 = { "Back", "Fwd", "Stop", "Refresh", "Search", "Fav", "Home", "Mail", "Vol -", "Vol +", "Mute", "Play", "Prev", "Next", "Media", "Calc" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row0, 42, 22, true));

            // Hàng 1: Function keys
            string[] row1 = { "Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "PrtSc", "ScrLk", "Pause" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row1, 42, 26, false));

            // Hàng 2: Number row + Nav + Numpad
            string[] row2 = { "~", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Backspace", "Ins", "Home", "PgUp", "NumL", "/", "*", "-" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row2, 34, 26, false));

            // Hàng 3: Tab + QWERTY + Nav + Numpad
            string[] row3 = { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\", "Del", "End", "PgDn", "Nm 7", "Nm 8", "Nm 9", "+" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row3, 34, 26, false));

            // Hàng 4: Caps + Home row + Numpad
            string[] row4 = { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter", "Nm 4", "Nm 5", "Nm 6" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row4, 36, 26, false));

            // Hàng 5: Shift + Bottom row + Arrows + Numpad
            string[] row5 = { "LeftShift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "RightShift", "Up", "Nm 1", "Nm 2", "Nm 3", "Nm Enter" };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row5, 36, 26, false));

            // Hàng 6: Modifiers + Space + Arrows + Numpad 0
            string[] row6 = { "LeftCtrl", "LeftWin", "LeftAlt", "Space", "RightAlt", "RightWin", "Menu", "RightCtrl", "Left", "Down", "Right", "Nm 0", "." };
            PnlVirtualKeyboard.Children.Add(CreateKeyRow(row6, 40, 26, false));
        }

        private StackPanel CreateKeyRow(string[] keys, double defaultWidth, double height, bool isMultiMedia)
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };

            foreach (var k in keys)
            {
                double w = defaultWidth;
                if (k == "Space") w = 180;
                else if (k == "Backspace" || k == "Tab" || k == "Caps") w = 54;
                else if (k == "Enter" || k == "LeftShift" || k == "RightShift") w = 62;
                else if (k == "Nm 0") w = 70;
                else if (k.StartsWith("Left") || k.StartsWith("Right")) w = 46;

                var btn = new Button
                {
                    Content = k.Replace("Left", "L-").Replace("Right", "R-"),
                    Tag = k,
                    Width = w,
                    Height = height,
                    Margin = new Thickness(1, 0, 1, 0),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = isMultiMedia ? 8.5 : 10,
                    Padding = new Thickness(0),
                    Background = isMultiMedia ? new SolidColorBrush(Color.FromRgb(15, 30, 45)) : new SolidColorBrush(Color.FromRgb(20, 24, 32)),
                    Foreground = isMultiMedia ? new SolidColorBrush(Color.FromRgb(0, 240, 255)) : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 55, 70)),
                    BorderThickness = new Thickness(1),
                    ToolTip = "Phím: " + k
                };

                btn.Click += VirtualKey_Click;
                rowPanel.Children.Add(btn);

                _keyButtons[k] = btn;
            }

            return rowPanel;
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

                // Khớp viết tắt Numpad
                if (keyName.StartsWith("Nm ") && string.Equals(token, "Num " + keyName.Substring(3), StringComparison.OrdinalIgnoreCase)) return true;
                if (token.StartsWith("Nm ") && string.Equals(keyName, "Num " + token.Substring(3), StringComparison.OrdinalIgnoreCase)) return true;

                // Khớp phím modifier Left/Right vs Normal
                if ((keyName == "LeftCtrl" || keyName == "RightCtrl") && string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftAlt" || keyName == "RightAlt") && string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftShift" || keyName == "RightShift") && string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase)) return true;
                if ((keyName == "LeftWin" || keyName == "RightWin") && string.Equals(token, "Win", StringComparison.OrdinalIgnoreCase)) return true;
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
            }

            // Đánh dấu các phím có chứa shortcut nói chung bằng viền sáng nhẹ
            foreach (var sc in _manager.Shortcuts)
            {
                foreach (var kv in _keyButtons)
                {
                    if (MatchesKeyToken(sc.KeyCombination, kv.Key))
                    {
                        kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 180, 210));
                    }
                }
            }

            // Đánh dấu nổi bật rực rỡ tổ hợp phím của mục đang chọn
            if (_selectedItem != null && !string.IsNullOrEmpty(_selectedItem.KeyCombination))
            {
                string combo = _selectedItem.KeyCombination;

                foreach (var kv in _keyButtons)
                {
                    if (MatchesKeyToken(combo, kv.Key))
                    {
                        bool isMod = kv.Key.Contains("Ctrl") || kv.Key.Contains("Alt") || kv.Key.Contains("Shift") || kv.Key.Contains("Win");
                        HighlightKey(kv.Key, isMod);
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

        private void PopulateTreeView()
        {
            TvShortcuts.Items.Clear();

            var categories = new[]
            {
                "Run program",
                "Paste text",
                "Comfort Keys Pro actions",
                "Audio control",
                "Monitor control",
                "Window control",
                "System actions",
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

            foreach (var item in _manager.Shortcuts)
            {
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

            // Mặc định chọn mục đầu tiên nếu chưa chọn
            if (_selectedItem == null && _manager.Shortcuts.Count > 0)
            {
                SelectShortcutInTree(_manager.Shortcuts[0]);
            }
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
                case ShortcutActionType.WindowControl: icon = "🗔"; break;
                case ShortcutActionType.MonitorControl: icon = "🖥"; break;
            }

            return $"{icon} {item.KeyCombination}";
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

            TxtKeyCombination.Text = item.KeyCombination ?? string.Empty;
            TxtLabel.Text = item.Label ?? string.Empty;
            TxtSoundPath.Text = item.SoundPath ?? string.Empty;
            TxtLastChanged.Text = item.LastChanged.ToString("M/d/yyyy h:mm:ss tt");

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
                default:
                    PanelOtherActions.Visibility = Visibility.Visible;
                    TxtOtherActionTitle.Text = "Action type : " + type.ToString();
                    break;
            }
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
                SwitchActionPanel(sat);
                PopulateTreeView();
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
                var newItem = new ComfortShortcutItem
                {
                    KeyCombination = dlg.ResultCombination,
                    Category = "Run program",
                    ActionType = ShortcutActionType.RunProgram,
                    Label = "New Action (" + dlg.ResultCombination + ")",
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
