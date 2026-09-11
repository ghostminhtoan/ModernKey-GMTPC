using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModernKey.Config;
using ModernKey.Core;
using ModernKey.Models;
using ModernKey.Tray;

namespace ModernKey
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly MacroManager _macroManager;
        private readonly SystemTrayManager _trayManager;
        private bool _isRealExit = false;
        private bool _isUpdatingUi = false;

        private readonly SolidColorBrush _brushPink = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x55));
        private readonly SolidColorBrush _brushPinkBg = new SolidColorBrush(Color.FromRgb(0x2B, 0x09, 0x14));
        private readonly SolidColorBrush _brushCyan = new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
        private readonly SolidColorBrush _brushCyanBg = new SolidColorBrush(Color.FromRgb(0x0C, 0x20, 0x28));

        public MainWindow(AppSettings settings, MacroManager macroManager, SystemTrayManager trayManager)
        {
            _settings = settings ?? new AppSettings();
            _macroManager = macroManager ?? new MacroManager();
            _trayManager = trayManager;
            _isUpdatingUi = true; // Khóa toàn bộ event handlers trong suốt quá trình dựng UI ban đầu

            InitializeComponent();
            InitializeControls();
            RefreshState();

            _isUpdatingUi = false; // Mở khóa sau khi UI đã sẵn sàng hoàn toàn
        }

        private void InitializeControls()
        {
            // Bảng mã
            CmbCharset.ItemsSource = Enum.GetValues(typeof(Charset));
            // Kiểu gõ
            CmbInputMethod.ItemsSource = Enum.GetValues(typeof(InputMethod));
            // Phím chuyển
            CmbSwitchMode.ItemsSource = Enum.GetValues(typeof(SwitchKeyMode));

            // Macro DataGrid
            if (_macroManager != null)
            {
                DgMacro.ItemsSource = _macroManager.MacroList;
            }

            // Converter ComboBoxes
            CmbConvertSource.ItemsSource = Enum.GetValues(typeof(Charset));
            CmbConvertSource.SelectedItem = Charset.Unicode;
            CmbConvertTarget.ItemsSource = Enum.GetValues(typeof(Charset));
            CmbConvertTarget.SelectedItem = Charset.TCVN3;

            // Bảng mã cho phím F4 (Tab Phím tắt)
            if (CmbShortcutF4Charset != null)
            {
                CmbShortcutF4Charset.ItemsSource = Enum.GetValues(typeof(Charset));
            }

            // Đồng bộ dữ liệu cài đặt từ _settings ra UI
            SyncSettingsToUi();
        }

        public void RefreshState()
        {
            if (_settings.IsVietnamese)
            {
                TxtQuickLangStatus.Text = "[VI] TIẾNG VIỆT";
                TxtQuickLangStatus.Foreground = _brushPink;
                BtnQuickLangToggle.Background = _brushPinkBg;
                BtnQuickLangToggle.BorderBrush = _brushPink;
            }
            else
            {
                TxtQuickLangStatus.Text = "[EN] TIẾNG ANH";
                TxtQuickLangStatus.Foreground = _brushCyan;
                BtnQuickLangToggle.Background = _brushCyanBg;
                BtnQuickLangToggle.BorderBrush = _brushCyan;
            }

            SyncSettingsToUi();
        }

        private void SyncSettingsToUi()
        {
            if (_settings == null) return;

            bool previousState = _isUpdatingUi;
            _isUpdatingUi = true;
            try
            {
                if (CmbCharset != null)
                {
                    if (!(CmbCharset.SelectedItem is Charset cs) || cs != _settings.CurrentCharset)
                        CmbCharset.SelectedItem = _settings.CurrentCharset;
                }

                if (CmbInputMethod != null)
                {
                    if (!(CmbInputMethod.SelectedItem is InputMethod im) || im != _settings.CurrentInputMethod)
                        CmbInputMethod.SelectedItem = _settings.CurrentInputMethod;
                }

                if (CmbSwitchMode != null)
                {
                    if (!(CmbSwitchMode.SelectedItem is SwitchKeyMode sm) || sm != _settings.SwitchMode)
                        CmbSwitchMode.SelectedItem = _settings.SwitchMode;
                }

                if (ChkSwitchBeep != null) ChkSwitchBeep.IsChecked = _settings.SwitchBeep;

                // Tab Tùy chọn
                if (ChkCheckSpelling != null) ChkCheckSpelling.IsChecked = _settings.CheckSpelling;
                if (ChkRestoreIfWrongSpelling != null) ChkRestoreIfWrongSpelling.IsChecked = _settings.RestoreIfWrongSpelling;
                if (ChkModernTone != null) ChkModernTone.IsChecked = _settings.ModernToneRules;
                if (ChkAllowConsonantZFWJ != null) ChkAllowConsonantZFWJ.IsChecked = _settings.AllowConsonantZFWJ;
                if (ChkUpperCaseFirstChar != null) ChkUpperCaseFirstChar.IsChecked = _settings.UpperCaseFirstChar;
                if (ChkFixRecommendBrowser != null) ChkFixRecommendBrowser.IsChecked = _settings.FixRecommendBrowser;
                if (ChkClipboard != null) ChkClipboard.IsChecked = _settings.SendViaClipboard;
                if (ChkStartWithWindows != null) ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
                if (ChkStartAsAdmin != null) ChkStartAsAdmin.IsChecked = _settings.StartAsAdmin;
                if (ChkOpenDialogOnStartup != null) ChkOpenDialogOnStartup.IsChecked = _settings.OpenDialogOnStartup;

                // Tab Gõ tắt
                if (ChkUseMacro != null) ChkUseMacro.IsChecked = _settings.UseMacro;
                if (ChkUseMacroInEnglish != null) ChkUseMacroInEnglish.IsChecked = _settings.UseMacroInEnglish;
                if (ChkAutoCapsMacro != null) ChkAutoCapsMacro.IsChecked = _settings.AutoCapsMacro;

                // Phím kích hoạt (Trigger Mask: bit 0=Space, bit 1=Enter, bit 2=2x LShift, bit 3=2x RShift)
                if (ChkTriggerSpace != null) ChkTriggerSpace.IsChecked = (_settings.MacroTriggerMask & 0x01) != 0;
                if (ChkTriggerEnter != null) ChkTriggerEnter.IsChecked = (_settings.MacroTriggerMask & 0x02) != 0;
                if (ChkTriggerDoubleLShift != null) ChkTriggerDoubleLShift.IsChecked = (_settings.MacroTriggerMask & 0x04) != 0;
                if (ChkTriggerDoubleRShift != null) ChkTriggerDoubleRShift.IsChecked = (_settings.MacroTriggerMask & 0x08) != 0;

                // Tab Phím tắt (Modifiers: Ctrl, Shift, Alt, Win)
                if (ChkShortcutCtrl != null) ChkShortcutCtrl.IsChecked = (_settings.ShortcutModifier & 0x01) != 0;
                if (ChkShortcutShift != null) ChkShortcutShift.IsChecked = (_settings.ShortcutModifier & 0x02) != 0;
                if (ChkShortcutAlt != null) ChkShortcutAlt.IsChecked = (_settings.ShortcutModifier & 0x04) != 0;
                if (ChkShortcutWin != null) ChkShortcutWin.IsChecked = (_settings.ShortcutModifier & 0x08) != 0;

                // Tab Phím tắt (F1 - F12)
                if (ChkShortcutF1 != null) ChkShortcutF1.IsChecked = (_settings.ShortcutEnableMask & (1 << 1)) != 0;
                if (ChkShortcutF2 != null) ChkShortcutF2.IsChecked = (_settings.ShortcutEnableMask & (1 << 2)) != 0;
                if (ChkShortcutF3 != null) ChkShortcutF3.IsChecked = (_settings.ShortcutEnableMask & (1 << 3)) != 0;
                if (ChkShortcutF4 != null) ChkShortcutF4.IsChecked = (_settings.ShortcutEnableMask & (1 << 4)) != 0;
                if (CmbShortcutF4Charset != null)
                {
                    CmbShortcutF4Charset.SelectedItem = _settings.ShortcutF4Charset;
                    CmbShortcutF4Charset.IsEnabled = (ChkShortcutF4?.IsChecked == true);
                }
                if (ChkShortcutF5 != null) ChkShortcutF5.IsChecked = (_settings.ShortcutEnableMask & (1 << 5)) != 0;
                if (ChkShortcutF8 != null) ChkShortcutF8.IsChecked = (_settings.ShortcutEnableMask & (1 << 8)) != 0;
                if (ChkShortcutF9 != null) ChkShortcutF9.IsChecked = (_settings.ShortcutEnableMask & (1 << 9)) != 0;
                if (ChkShortcutF11 != null) ChkShortcutF11.IsChecked = (_settings.ShortcutEnableMask & (1 << 11)) != 0;
                if (ChkShortcutF12 != null) ChkShortcutF12.IsChecked = (_settings.ShortcutEnableMask & (1 << 12)) != 0;
            }
            finally
            {
                _isUpdatingUi = previousState;
            }
        }

        private void SyncUiToSettings()
        {
            if (_isUpdatingUi) return;

            if (CmbCharset?.SelectedItem is Charset cs) _settings.CurrentCharset = cs;
            if (CmbInputMethod?.SelectedItem is InputMethod im) _settings.CurrentInputMethod = im;
            if (CmbSwitchMode?.SelectedItem is SwitchKeyMode sm) _settings.SwitchMode = sm;
            if (ChkSwitchBeep != null) _settings.SwitchBeep = ChkSwitchBeep.IsChecked == true;

            // Tab Tùy chọn
            if (ChkCheckSpelling != null) _settings.CheckSpelling = ChkCheckSpelling.IsChecked == true;
            if (ChkRestoreIfWrongSpelling != null) _settings.RestoreIfWrongSpelling = ChkRestoreIfWrongSpelling.IsChecked == true;
            if (ChkModernTone != null) _settings.ModernToneRules = ChkModernTone.IsChecked == true;
            if (ChkAllowConsonantZFWJ != null) _settings.AllowConsonantZFWJ = ChkAllowConsonantZFWJ.IsChecked == true;
            if (ChkUpperCaseFirstChar != null) _settings.UpperCaseFirstChar = ChkUpperCaseFirstChar.IsChecked == true;
            if (ChkFixRecommendBrowser != null) _settings.FixRecommendBrowser = ChkFixRecommendBrowser.IsChecked == true;
            if (ChkClipboard != null) _settings.SendViaClipboard = ChkClipboard.IsChecked == true;
            if (ChkStartWithWindows != null) _settings.StartWithWindows = ChkStartWithWindows.IsChecked == true;
            if (ChkStartAsAdmin != null) _settings.StartAsAdmin = ChkStartAsAdmin.IsChecked == true;
            if (ChkOpenDialogOnStartup != null) _settings.OpenDialogOnStartup = ChkOpenDialogOnStartup.IsChecked == true;

            // Tab Gõ tắt
            if (ChkUseMacro != null) _settings.UseMacro = ChkUseMacro.IsChecked == true;
            if (ChkUseMacroInEnglish != null) _settings.UseMacroInEnglish = ChkUseMacroInEnglish.IsChecked == true;
            if (ChkAutoCapsMacro != null) _settings.AutoCapsMacro = ChkAutoCapsMacro.IsChecked == true;

            // Trigger Mask
            int mask = 0;
            if (ChkTriggerSpace?.IsChecked == true) mask |= 0x01;
            if (ChkTriggerEnter?.IsChecked == true) mask |= 0x02;
            if (ChkTriggerDoubleLShift?.IsChecked == true) mask |= 0x04;
            if (ChkTriggerDoubleRShift?.IsChecked == true) mask |= 0x08;
            _settings.MacroTriggerMask = mask;

            // Tab Phím tắt (Modifiers: Ctrl, Shift, Alt, Win)
            int mod = 0;
            if (ChkShortcutCtrl?.IsChecked == true) mod |= 0x01;
            if (ChkShortcutShift?.IsChecked == true) mod |= 0x02;
            if (ChkShortcutAlt?.IsChecked == true) mod |= 0x04;
            if (ChkShortcutWin?.IsChecked == true) mod |= 0x08;
            _settings.ShortcutModifier = mod;

            // Tab Phím tắt (F1 - F12)
            int fMask = 0;
            if (ChkShortcutF1?.IsChecked == true) fMask |= (1 << 1);
            if (ChkShortcutF2?.IsChecked == true) fMask |= (1 << 2);
            if (ChkShortcutF3?.IsChecked == true) fMask |= (1 << 3);
            if (ChkShortcutF4?.IsChecked == true) fMask |= (1 << 4);
            if (ChkShortcutF5?.IsChecked == true) fMask |= (1 << 5);
            if (ChkShortcutF8?.IsChecked == true) fMask |= (1 << 8);
            if (ChkShortcutF9?.IsChecked == true) fMask |= (1 << 9);
            if (ChkShortcutF11?.IsChecked == true) fMask |= (1 << 11);
            if (ChkShortcutF12?.IsChecked == true) fMask |= (1 << 12);
            _settings.ShortcutEnableMask = fMask;

            if (CmbShortcutF4Charset != null)
            {
                CmbShortcutF4Charset.IsEnabled = (ChkShortcutF4?.IsChecked == true);
                if (CmbShortcutF4Charset.SelectedItem is Charset f4cs)
                {
                    _settings.ShortcutF4Charset = f4cs;
                }
            }
        }

        private void BtnQuickLangToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsVietnamese = !_settings.IsVietnamese;
            SettingsManager.SaveSettings(_settings);
            _trayManager.UpdateTrayIcon();
            _trayManager.BuildContextMenu();
            RefreshState();
        }

        private void CmbCharset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbCharset.SelectedItem is Charset cs)
            {
                _settings.CurrentCharset = cs;
                SettingsManager.SaveSettings(_settings);
                _trayManager?.BuildContextMenu();
            }
        }

        private void CmbInputMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbInputMethod.SelectedItem is InputMethod im)
            {
                _settings.CurrentInputMethod = im;
                SettingsManager.SaveSettings(_settings);
                _trayManager?.UpdateTrayIcon();
                _trayManager?.BuildContextMenu();
            }
        }

        private void BtnCustomInputMethod_Click(object sender, RoutedEventArgs e)
        {
            var customWnd = new CustomInputMethodWindow(_settings)
            {
                Owner = this
            };

            if (customWnd.ShowDialog() == true)
            {
                if (_settings.CurrentInputMethod != InputMethod.Custom)
                {
                    _settings.CurrentInputMethod = InputMethod.Custom;
                    CmbInputMethod.SelectedItem = InputMethod.Custom;
                }
                SettingsManager.SaveSettings(_settings);
                _trayManager?.BuildContextMenu();
            }
        }

        private void CmbSwitchMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbSwitchMode.SelectedItem is SwitchKeyMode sm)
            {
                _settings.SwitchMode = sm;
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void CmbShortcutF4Charset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbShortcutF4Charset.SelectedItem is Charset cs)
            {
                _settings.ShortcutF4Charset = cs;
                SettingsManager.SaveSettings(_settings);
            }
        }

        public void SelectMacroTab()
        {
            if (MainTabControl != null)
            {
                MainTabControl.SelectedIndex = 2; // Tab GÕ TẮT
            }
        }

        private void SettingCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;

            SyncUiToSettings();
            SettingsManager.SaveSettings(_settings);
            _trayManager?.BuildContextMenu();
        }

        private void DgMacro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgMacro.SelectedItem is MacroEntry selected)
            {
                TxtMacroShortcut.Text = selected.Shortcut;
                TxtMacroReplacement.Text = selected.Replacement;
            }
        }

        private void BtnAddMacro_Click(object sender, RoutedEventArgs e)
        {
            string shortcut = TxtMacroShortcut.Text?.Trim();
            string replacement = TxtMacroReplacement.Text?.Trim();

            if (string.IsNullOrEmpty(shortcut) || string.IsNullOrEmpty(replacement))
            {
                MessageBox.Show("Vui lòng nhập cả từ viết tắt và cụm từ thay thế!", "ModernKey HUD",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in _macroManager.MacroList)
            {
                if (string.Equals(item.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase))
                {
                    item.Replacement = replacement;
                    TxtMacroShortcut.Clear();
                    TxtMacroReplacement.Clear();
                    return;
                }
            }

            _macroManager.MacroList.Add(new MacroEntry(shortcut, replacement));
            TxtMacroShortcut.Clear();
            TxtMacroReplacement.Clear();
        }

        private void BtnDeleteMacro_Click(object sender, RoutedEventArgs e)
        {
            if (DgMacro.SelectedItem is MacroEntry selected)
            {
                _macroManager.MacroList.Remove(selected);
            }
            else
            {
                string shortcut = TxtMacroShortcut.Text?.Trim();
                if (!string.IsNullOrEmpty(shortcut))
                {
                    for (int i = 0; i < _macroManager.MacroList.Count; i++)
                    {
                        if (string.Equals(_macroManager.MacroList[i].Shortcut, shortcut, StringComparison.OrdinalIgnoreCase))
                        {
                            _macroManager.MacroList.RemoveAt(i);
                            TxtMacroShortcut.Clear();
                            TxtMacroReplacement.Clear();
                            return;
                        }
                    }
                }
                MessageBox.Show("Vui lòng chọn từ gõ tắt cần xóa!", "ModernKey HUD",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDeleteAllMacro_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn xóa TOÀN BỘ danh sách gõ tắt không?",
                                         "Xác nhận xóa tất cả", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _macroManager.MacroList.Clear();
                _macroManager.Save();
                TxtMacroShortcut.Clear();
                TxtMacroReplacement.Clear();
                MessageBox.Show("Đã xóa toàn bộ danh sách gõ tắt!", "ModernKey HUD",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnImportMacro_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
                Title = "Nhập danh sách gõ tắt từ file (OpenKey / Unikey)"
            };

            if (dlg.ShowDialog() == true)
            {
                var result = MessageBox.Show("Bạn có muốn giữ lại dữ liệu gõ tắt hiện tại không?\n\n- Chọn Yes để GỘP thêm dữ liệu từ file.\n- Chọn No để GHI ĐÈ toàn bộ danh sách.",
                                             "Dữ liệu gõ tắt", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return;
                bool append = (result == MessageBoxResult.Yes);

                int count = _macroManager.ImportFromFile(dlg.FileName, append);
                if (count > 0)
                {
                    _macroManager.Save();
                    MessageBox.Show($"Đã nạp thành công {count} mục gõ tắt từ file!", "ModernKey HUD",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không tìm thấy mục gõ tắt hợp lệ nào trong file!", "ModernKey HUD",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnExportMacro_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = "openkeymacro.txt",
                Title = "Xuất danh sách gõ tắt ra file"
            };

            if (dlg.ShowDialog() == true)
            {
                _macroManager.ExportToFile(dlg.FileName);
                MessageBox.Show("Đã xuất danh sách gõ tắt thành công!", "ModernKey HUD",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnConvertEvKey_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "EVKey Macro (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Chuyển đổi file gõ tắt từ EVKey"
            };

            if (dlg.ShowDialog() == true)
            {
                var result = MessageBox.Show("Bạn có muốn giữ lại dữ liệu gõ tắt hiện tại không?\n\n- Chọn Yes để GỘP thêm.\n- Chọn No để GHI ĐÈ toàn bộ.",
                                             "Chuyển đổi EVKey Macro", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return;
                bool append = (result == MessageBoxResult.Yes);

                int count = _macroManager.ConvertEvKeyFromFile(dlg.FileName, append);
                if (count > 0)
                {
                    _macroManager.Save();
                    MessageBox.Show($"Đã chuyển đổi thành công {count} mục gõ tắt từ EVKey!", "ModernKey HUD",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không tìm thấy macro EVKey hợp lệ trong file!", "ModernKey HUD",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnSaveMacro_Click(object sender, RoutedEventArgs e)
        {
            _macroManager.Save();
            MessageBox.Show("Đã lưu danh sách gõ tắt thành công!", "ModernKey HUD",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnConvertText_Click(object sender, RoutedEventArgs e)
        {
            var src = (Charset)CmbConvertSource.SelectedItem;
            var dst = (Charset)CmbConvertTarget.SelectedItem;
            TxtConvertOutput.Text = CharsetConverter.Convert(TxtConvertInput.Text, src, dst);
        }

        private void BtnConvertClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    var src = (Charset)CmbConvertSource.SelectedItem;
                    var dst = (Charset)CmbConvertTarget.SelectedItem;
                    string converted = CharsetConverter.Convert(text, src, dst);
                    Clipboard.SetText(converted);
                    TxtConvertInput.Text = text;
                    TxtConvertOutput.Text = converted;
                    MessageBox.Show("Đã chuyển mã và sao chép vào Clipboard thành công!", "ModernKey HUD",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi chuyển mã Clipboard: " + ex.Message, "ModernKey HUD",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemoveDiacritics_Click(object sender, RoutedEventArgs e)
        {
            TxtConvertOutput.Text = CharsetConverter.RemoveDiacritics(TxtConvertInput.Text);
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SyncUiToSettings();
            SettingsManager.SaveSettings(_settings);
            SettingsManager.ApplyStartupConfig(_settings.StartWithWindows, _settings.StartAsAdmin);
            _trayManager?.BuildContextMenu();
            MessageBox.Show("Đã lưu cấu hình ModernKey GMTPC thành công!", "ModernKey HUD",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCloseToTray_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void BtnExitApp_Click(object sender, RoutedEventArgs e)
        {
            _isRealExit = true;
            (Application.Current as App)?.ExitApplication();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isRealExit)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
