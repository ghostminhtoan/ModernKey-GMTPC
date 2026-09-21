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
            BuildVirtualKeyboard();
            PopulateTreeView();
        }

        private void InitActionTypeCombo()
        {
            CmbActionType.Items.Clear();
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Run program", Tag = ShortcutActionType.RunProgram });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Open URL", Tag = ShortcutActionType.OpenUrl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Paste text", Tag = ShortcutActionType.PasteText });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Play keystroke macro", Tag = ShortcutActionType.PlayKeystrokeMacro });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Audio control", Tag = ShortcutActionType.AudioControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Window control", Tag = ShortcutActionType.WindowControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "Monitor control", Tag = ShortcutActionType.MonitorControl });
            CmbActionType.Items.Add(new ComboBoxItem { Content = "System action", Tag = ShortcutActionType.SystemAction });
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

        private void VirtualKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string keyName)
            {
                // Tìm kiếm xem có phím tắt nào gắn với phím này không
                var match = _manager.Shortcuts.FirstOrDefault(s => s.KeyCombination.IndexOf(keyName, StringComparison.OrdinalIgnoreCase) >= 0);
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
                    if (sc.KeyCombination.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        kv.Value.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 180, 210));
                    }
                }
            }

            // Đánh dấu nổi bật rực rỡ tổ hợp phím của mục đang chọn
            if (_selectedItem != null && !string.IsNullOrEmpty(_selectedItem.KeyCombination))
            {
                string combo = _selectedItem.KeyCombination;

                bool hasCtrl = combo.Contains("Ctrl");
                bool hasAlt = combo.Contains("Alt");
                bool hasShift = combo.Contains("Shift");
                bool hasWin = combo.Contains("Win");

                if (hasCtrl) HighlightKey("LeftCtrl", true);
                if (hasAlt) HighlightKey("LeftAlt", true);
                if (hasShift) HighlightKey("LeftShift", true);
                if (hasWin) HighlightKey("LeftWin", true);

                foreach (var kv in _keyButtons)
                {
                    // Kiểm tra phím chính không phải modifier
                    if (kv.Key.Contains("Ctrl") || kv.Key.Contains("Alt") || kv.Key.Contains("Shift") || kv.Key.Contains("Win"))
                        continue;

                    if (combo.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        HighlightKey(kv.Key, false);
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
                "Open URL",
                "Play keystroke macro"
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
                case "Play keystroke macro": return "▶ Play keystroke macro";
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
                case ShortcutActionType.PlayKeystrokeMacro: icon = "▶"; break;
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

            // 4. Macro
            SldMacroSpeed.Value = Math.Max(10, item.PlaySpeed);
            TxtMacroSpeedVal.Text = (int)SldMacroSpeed.Value + "%";
            TxtMacroRepetitions.Text = Math.Max(1, item.Repetitions).ToString();
            TxtMacroActivateProcess.Text = item.ActivateProcess ?? string.Empty;

            // 5. Audio control
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
            PanelKeystrokeMacro.Visibility = Visibility.Collapsed;
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
                case ShortcutActionType.PlayKeystrokeMacro:
                    PanelKeystrokeMacro.Visibility = Visibility.Visible;
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

        private void BtnRunAddMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var itemFile = new MenuItem { Header = "Select File..." };
            itemFile.Click += (s, ev) =>
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
            };
            menu.Items.Add(itemFile);

            var itemFolder = new MenuItem { Header = "Select Folder..." };
            itemFolder.Click += (s, ev) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "Select Folder to Open";
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        AppendToRunPaths(fbd.SelectedPath);
                    }
                }
            };
            menu.Items.Add(itemFolder);

            menu.Items.Add(new Separator());

            // Tự động quét danh sách các tiến trình đang chạy kèm đường dẫn (Running Process Picker từ Đợt 2)
            try
            {
                var runningProcs = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) || IsCommonApp(p.ProcessName))
                    .OrderBy(p => p.ProcessName)
                    .Take(15)
                    .ToList();

                foreach (var proc in runningProcs)
                {
                    try
                    {
                        string exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var procItem = new MenuItem
                            {
                                Header = $"💻 {proc.ProcessName} ({Path.GetFileName(exePath)})",
                                ToolTip = exePath
                            };
                            procItem.Click += (s, ev) => AppendToRunPaths(exePath);
                            menu.Items.Add(procItem);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            menu.PlacementTarget = BtnRunAddMenu;
            menu.IsOpen = true;
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

        private void BtnInsertTag_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            // 1. Paste Current Date (10 định dạng)
            var menuDate = new MenuItem { Header = "Paste Current Date" };
            AddSubItem(menuDate, "Long Default Format (" + DateTime.Now.ToString("dddd, MMMM d, yyyy") + ")", "<DATE_LONG>");
            AddSubItem(menuDate, "Short Default Format (" + DateTime.Now.ToString("M/d/yyyy") + ")", "{date}");
            AddSubItem(menuDate, "mm/dd/yyyy (" + DateTime.Now.ToString("MM/dd/yyyy") + ")", "<DATE_MM_DD_YYYY>");
            AddSubItem(menuDate, "m/d/yyyy (" + DateTime.Now.ToString("M/d/yyyy") + ")", "<DATE_M_D_YYYY>");
            AddSubItem(menuDate, "mm/dd/yy (" + DateTime.Now.ToString("MM/dd/yy") + ")", "<DATE_MM_DD_YY>");
            AddSubItem(menuDate, "m/d/yy (" + DateTime.Now.ToString("M/d/yy") + ")", "<DATE_M_D_YY>");
            AddSubItem(menuDate, "dd.mm.yyyy (" + DateTime.Now.ToString("dd.MM.yyyy") + ")", "<DATE_DD_MM_YYYY>");
            AddSubItem(menuDate, "d.m.yyyy (" + DateTime.Now.ToString("d.M.yyyy") + ")", "<DATE_D_M_YYYY>");
            AddSubItem(menuDate, "dd.mm.yy (" + DateTime.Now.ToString("dd.MM.yy") + ")", "<DATE_DD_MM_YY>");
            AddSubItem(menuDate, "d.m.yy (" + DateTime.Now.ToString("d.M.yy") + ")", "<DATE_D_M_YY>");
            menu.Items.Add(menuDate);

            // 2. Paste Current Time (6 định dạng)
            var menuTime = new MenuItem { Header = "Paste Current Time" };
            AddSubItem(menuTime, "Default Format (" + DateTime.Now.ToString("h:mm:ss tt") + ")", "{time}");
            AddSubItem(menuTime, "hh:mm (" + DateTime.Now.ToString("HH:mm") + ")", "<TIME_HH_MM_24>");
            AddSubItem(menuTime, "h:mm (" + DateTime.Now.ToString("H:mm") + ")", "<TIME_H_MM_24>");
            AddSubItem(menuTime, "hh:mm am/pm (" + DateTime.Now.ToString("hh:mm tt") + ")", "<TIME_HH_MM_12>");
            AddSubItem(menuTime, "h:mm am/pm (" + DateTime.Now.ToString("h:mm tt") + ")", "<TIME_H_MM_12>");
            AddSubItem(menuTime, "hh:mm:ss.zzz (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", "<TIME_MILLIS>");
            menu.Items.Add(menuTime);

            // 3. Paste Date and Time (2 định dạng)
            var menuDateTime = new MenuItem { Header = "Paste Date and Time" };
            AddSubItem(menuDateTime, "Long Default Format (" + DateTime.Now.ToString("dddd, MMMM d, yyyy h:mm:ss tt") + ")", "<DATETIME_LONG>");
            AddSubItem(menuDateTime, "Short Default Format (" + DateTime.Now.ToString("M/d/yyyy h:mm:ss tt") + ")", "<DATETIME_SHORT>");
            menu.Items.Add(menuDateTime);

            // 4. Press Keys (Tab, Enter, Ctrl+Enter, Backspace, Del, Ctrl+A, Space)
            var menuKeys = new MenuItem { Header = "Press Keys" };
            AddSubItem(menuKeys, "Tab", "<KEY_TAB>");
            AddSubItem(menuKeys, "Enter", "<KEY_ENTER>");
            AddSubItem(menuKeys, "Ctrl+Enter", "<KEY_CTRL_ENTER>");
            AddSubItem(menuKeys, "Backspace", "<KEY_BACKSPACE>");
            AddSubItem(menuKeys, "Del", "<KEY_DEL>");
            AddSubItem(menuKeys, "Ctrl+A", "<KEY_CTRL_A>");
            AddSubItem(menuKeys, "Space", "<KEY_SPACE>");
            menu.Items.Add(menuKeys);

            menu.Items.Add(new Separator());

            // 5. Thẻ nâng cao
            AddDirectItem(menu, "Insert <SomeOf> Tag", "<SomeOf: Lựa chọn 1 | Lựa chọn 2 | Lựa chọn 3>");
            AddDirectItem(menu, "Insert <POPUP> Tag", "<POPUP: Tiêu đề menu>");
            AddDirectItem(menu, "Insert <SCRIPT> Tag", "<SCRIPT: run>");
            AddDirectItem(menu, "Insert Text File", "<FILE: C:\\path\\to\\file.txt>");
            AddDirectItem(menu, "Paste Selection Text", "<SELECTION>");
            AddDirectItem(menu, "Paste Clipboard Content", "<CLIPBOARD>");

            menu.PlacementTarget = BtnInsertTag;
            menu.IsOpen = true;
        }

        private void AddSubItem(MenuItem parent, string header, string tagValue)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => InsertTagIntoEditor(tagValue);
            parent.Items.Add(item);
        }

        private void AddDirectItem(ContextMenu parent, string header, string tagValue)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => InsertTagIntoEditor(tagValue);
            parent.Items.Add(item);
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

        // --- 4. PLAY KEYSTROKE MACRO (Đợt 5) ---
        private void BtnMacroRecord_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chế độ Record Macro đang hoạt động: Nhấn các phím cần ghi lại trong bất kỳ ứng dụng nào và nhấn phím Esc để hoàn tất ghi.", "Ghi Macro Trực Tiếp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnMacroEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            var dlg = new EditMacroKeystrokesDialog(_selectedItem.MacroEvents);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                _selectedItem.MacroEvents.Clear();
                foreach (var ev in dlg.Events)
                {
                    _selectedItem.MacroEvents.Add(ev);
                }
                _selectedItem.LastChanged = DateTime.Now;
                TxtLastChanged.Text = _selectedItem.LastChanged.ToString("M/d/yyyy h:mm:ss tt");
            }
        }

        private void SldMacroSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            int speed = (int)SldMacroSpeed.Value;
            _selectedItem.PlaySpeed = speed;
            TxtMacroSpeedVal.Text = speed >= 200 ? "Max" : speed + "%";
            _selectedItem.LastChanged = DateTime.Now;
        }

        private void TxtMacroRepetitions_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            if (int.TryParse(TxtMacroRepetitions.Text, out var rep))
            {
                _selectedItem.Repetitions = Math.Max(1, rep);
                _selectedItem.LastChanged = DateTime.Now;
            }
        }

        private void TxtMacroActivateProcess_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedItem == null) return;
            _selectedItem.ActivateProcess = TxtMacroActivateProcess.Text;
            _selectedItem.LastChanged = DateTime.Now;
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
