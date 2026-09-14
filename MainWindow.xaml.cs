using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModernKey.Config;
using ModernKey.Core;
using ModernKey.Hook;
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
        private readonly System.Windows.Threading.DispatcherTimer _statsTimer;

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
            if (SettingsManager.IsPortableMode())
            {
                Title = "ModernKey GMTPC [Portable]";
            }
            InitializeControls();
            RefreshState();

            _statsTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statsTimer.Tick += (s, e) =>
            {
                if (IsVisible && MainTabControl?.SelectedIndex == 6)
                {
                    RenderTypingStats();
                }
            };
            _statsTimer.Start();

            if (MainTabControl != null)
            {
                MainTabControl.SelectionChanged += (s, e) =>
                {
                    if (e.Source == MainTabControl && MainTabControl.SelectedIndex == 6)
                    {
                        RenderTypingStats();
                    }
                };
            }

            _isUpdatingUi = false; // Mở khóa sau khi UI đã sẵn sàng hoàn toàn
        }

        private void InitializeControls()
        {
            // Bảng mã
            CmbCharset.ItemsSource = Enum.GetValues(typeof(Charset));
            // Kiểu gõ
            CmbInputMethod.ItemsSource = Enum.GetValues(typeof(InputMethod));

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
            // Kiểu gõ cho phím F6 (Tab Phím tắt)
            if (CmbShortcutF6InputMethod != null)
            {
                CmbShortcutF6InputMethod.ItemsSource = Enum.GetValues(typeof(InputMethod));
            }
            // Kiểu gõ cho phím F7 (Tab Phím tắt)
            if (CmbShortcutF7InputMethod != null)
            {
                CmbShortcutF7InputMethod.ItemsSource = Enum.GetValues(typeof(InputMethod));
            }

            // Compact Mode ComboBoxes
            if (CmbCompactCharset != null)
            {
                CmbCompactCharset.ItemsSource = Enum.GetValues(typeof(Charset));
            }
            if (CmbCompactInputMethod != null)
            {
                CmbCompactInputMethod.ItemsSource = Enum.GetValues(typeof(InputMethod));
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
            RenderTypingStats();
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

                if (ChkSwitchCtrl != null) ChkSwitchCtrl.IsChecked = _settings.SwitchCtrl;
                if (ChkSwitchAlt != null) ChkSwitchAlt.IsChecked = _settings.SwitchAlt;
                if (ChkSwitchWin != null) ChkSwitchWin.IsChecked = _settings.SwitchWin;
                if (ChkSwitchShift != null) ChkSwitchShift.IsChecked = _settings.SwitchShift;
                if (TxtSwitchKeyChar != null && TxtSwitchKeyChar.Text != _settings.SwitchKeyChar)
                {
                    TxtSwitchKeyChar.Text = _settings.SwitchKeyChar ?? "Z";
                }
                if (ChkSwitchBeep != null) ChkSwitchBeep.IsChecked = _settings.SwitchBeep;

                // Tab Tùy chọn
                if (ChkCheckSpelling != null) ChkCheckSpelling.IsChecked = _settings.CheckSpelling;
                if (ChkRestoreIfWrongSpelling != null) ChkRestoreIfWrongSpelling.IsChecked = _settings.RestoreIfWrongSpelling;
                if (ChkModernTone != null) ChkModernTone.IsChecked = _settings.ModernToneRules;
                if (ChkFreeMark != null) ChkFreeMark.IsChecked = _settings.FreeMark;
                if (ChkAllowConsonantZFWJ != null) ChkAllowConsonantZFWJ.IsChecked = _settings.AllowConsonantZFWJ;
                if (ChkUpperCaseFirstChar != null) ChkUpperCaseFirstChar.IsChecked = _settings.UpperCaseFirstChar;
                if (ChkFixRecommendBrowser != null) ChkFixRecommendBrowser.IsChecked = _settings.FixRecommendBrowser;
                if (ChkClipboard != null) ChkClipboard.IsChecked = _settings.SendViaClipboard;
                if (ChkSmartCodePassthrough != null) ChkSmartCodePassthrough.IsChecked = _settings.SmartCodePassthrough;
                if (ChkEscKeyUndo != null) ChkEscKeyUndo.IsChecked = _settings.EscKeyUndo;
                if (ChkEnableStatusOsd != null) ChkEnableStatusOsd.IsChecked = _settings.EnableStatusOsd;
                if (ChkAutoExclude != null) ChkAutoExclude.IsChecked = _settings.AutoExcludeEnabled;
                if (ChkStartWithWindows != null) ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
                if (ChkStartAsAdmin != null) ChkStartAsAdmin.IsChecked = _settings.StartAsAdmin;
                if (ChkOpenDialogOnStartup != null) ChkOpenDialogOnStartup.IsChecked = _settings.OpenDialogOnStartup;

                // Tab Clipboard
                if (ChkEnableClipboard != null) ChkEnableClipboard.IsChecked = _settings.EnableClipboardHistory;
                if (ChkClipboardAutoHide != null) ChkClipboardAutoHide.IsChecked = _settings.ClipboardAutoHide;
                if (ChkClipboardAlwaysOnTop != null) ChkClipboardAlwaysOnTop.IsChecked = _settings.ClipboardAlwaysOnTop;
                if (ChkClipboardIgnoreDuplicates != null) ChkClipboardIgnoreDuplicates.IsChecked = _settings.ClipboardIgnoreDuplicates;
                if (ChkClipboardPasteAsPlainText != null) ChkClipboardPasteAsPlainText.IsChecked = _settings.ClipboardPasteAsPlainText;
                if (ChkClipboardMaskSensitive != null) ChkClipboardMaskSensitive.IsChecked = _settings.ClipboardMaskSensitive;
                if (ChkClipboardAutoPurgeSensitive != null) ChkClipboardAutoPurgeSensitive.IsChecked = _settings.ClipboardAutoPurgeSensitive;
                if (TxtClipboardMaxItems != null) TxtClipboardMaxItems.Text = _settings.ClipboardMaxItems.ToString();
                if (TxtClipboardStoragePath != null)
                {
                    string cfgDir = SettingsManager.GetConfigDirectory();
                    TxtClipboardStoragePath.Text = "Thư mục lưu trữ: " + System.IO.Path.Combine(cfgDir, "clipboard");
                }

                // Các tính năng mở rộng Cyberpunk & Checkbox quản lý
                if (ChkSmartEnglishBypass != null) ChkSmartEnglishBypass.IsChecked = _settings.SmartEnglishBypass;
                if (ChkOneKeyUndoRaw != null) ChkOneKeyUndoRaw.IsChecked = _settings.OneKeyUndoRaw;
                if (ChkInlineMathEvaluator != null) ChkInlineMathEvaluator.IsChecked = _settings.InlineMathEvaluator;
                if (ChkEnableCaretIndicator != null) ChkEnableCaretIndicator.IsChecked = _settings.EnableCaretIndicator;
                if (ChkEnableKeySound != null) ChkEnableKeySound.IsChecked = _settings.EnableKeySound;
                if (ChkGameMode != null) ChkGameMode.IsChecked = _settings.GameModeEnabled;
                if (ChkQuickTextTransform != null) ChkQuickTextTransform.IsChecked = _settings.QuickTextTransformEnabled;
                if (ChkP2PAutoSync != null) ChkP2PAutoSync.IsChecked = _settings.P2PAutoSyncEnabled;
                if (ChkTypingStats != null) ChkTypingStats.IsChecked = _settings.TypingStatsEnabled;
                if (TxtSyncFolderPath != null && TxtSyncFolderPath.Text != _settings.SyncFolderPath)
                {
                    TxtSyncFolderPath.Text = _settings.SyncFolderPath ?? string.Empty;
                }


                // Đồng bộ ComboBox Compact Mode
                if (CmbCompactCharset != null)
                {
                    if (!(CmbCompactCharset.SelectedItem is Charset ccs) || ccs != _settings.CurrentCharset)
                        CmbCompactCharset.SelectedItem = _settings.CurrentCharset;
                }
                if (CmbCompactInputMethod != null)
                {
                    if (!(CmbCompactInputMethod.SelectedItem is InputMethod cim) || cim != _settings.CurrentInputMethod)
                        CmbCompactInputMethod.SelectedItem = _settings.CurrentInputMethod;
                }

                // Profile ComboBox
                if (CmbProfile != null)
                {
                    string p = _settings.ActiveProfile ?? "Office";
                    foreach (ComboBoxItem it in CmbProfile.Items)
                    {
                        if (string.Equals(it.Tag as string, p, StringComparison.OrdinalIgnoreCase))
                        {
                            it.IsSelected = true;
                            break;
                        }
                    }
                }

                ApplyCompactModeUi(_settings.IsCompactMode);

                // Tab Gõ tắt
                if (ChkUseMacro != null) ChkUseMacro.IsChecked = _settings.UseMacro;
                if (ChkUseMacroInEnglish != null) ChkUseMacroInEnglish.IsChecked = _settings.UseMacroInEnglish;
                if (ChkAutoCapsMacro != null) ChkAutoCapsMacro.IsChecked = _settings.AutoCapsMacro;
                if (ChkDynamicMacro != null) ChkDynamicMacro.IsChecked = _settings.DynamicMacroEnabled;

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
                bool isF7 = (_settings.CurrentInputMethod == _settings.ShortcutF7InputMethod);
                bool isF6 = (_settings.CurrentInputMethod == _settings.ShortcutF6InputMethod) || !isF7;
                if (isF7)
                {
                    if (RadShortcutF7 != null) RadShortcutF7.IsChecked = true;
                }
                else
                {
                    if (RadShortcutF6 != null) RadShortcutF6.IsChecked = true;
                }
                if (CmbShortcutF6InputMethod != null)
                {
                    CmbShortcutF6InputMethod.SelectedItem = _settings.ShortcutF6InputMethod;
                    CmbShortcutF6InputMethod.IsEnabled = true;
                }
                if (CmbShortcutF7InputMethod != null)
                {
                    CmbShortcutF7InputMethod.SelectedItem = _settings.ShortcutF7InputMethod;
                    CmbShortcutF7InputMethod.IsEnabled = true;
                }
                if (ChkShortcutF8 != null) ChkShortcutF8.IsChecked = (_settings.ShortcutEnableMask & (1 << 8)) != 0;
                if (ChkShortcutF9 != null) ChkShortcutF9.IsChecked = (_settings.ShortcutEnableMask & (1 << 9)) != 0;
                if (ChkShortcutF11 != null) ChkShortcutF11.IsChecked = (_settings.ShortcutEnableMask & (1 << 11)) != 0;
                if (ChkShortcutF12 != null) ChkShortcutF12.IsChecked = (_settings.ShortcutEnableMask & (1 << 12)) != 0;

                // Đồng bộ phím tắt Game Mode & Quick Text
                if (ChkGameModeCtrl != null) ChkGameModeCtrl.IsChecked = _settings.GameModeCtrl;
                if (ChkGameModeAlt != null) ChkGameModeAlt.IsChecked = _settings.GameModeAlt;
                if (ChkGameModeWin != null) ChkGameModeWin.IsChecked = _settings.GameModeWin;
                if (ChkGameModeShift != null) ChkGameModeShift.IsChecked = _settings.GameModeShift;
                if (TxtGameModeKeyChar != null) TxtGameModeKeyChar.Text = _settings.GameModeKeyChar ?? "F11";

                if (ChkQuickTextCtrl != null) ChkQuickTextCtrl.IsChecked = _settings.QuickTextCtrl;
                if (ChkQuickTextAlt != null) ChkQuickTextAlt.IsChecked = _settings.QuickTextAlt;
                if (ChkQuickTextWin != null) ChkQuickTextWin.IsChecked = _settings.QuickTextWin;
                if (ChkQuickTextShift != null) ChkQuickTextShift.IsChecked = _settings.QuickTextShift;
                if (TxtQuickTextKeyChar != null) TxtQuickTextKeyChar.Text = _settings.QuickTextKeyChar ?? "U";
            }
            finally
            {
                _isUpdatingUi = previousState;
            }
        }

        private void SyncUiToSettings()
        {
            if (_isUpdatingUi) return;

            if (ChkSwitchCtrl != null) _settings.SwitchCtrl = ChkSwitchCtrl.IsChecked == true;
            if (ChkSwitchAlt != null) _settings.SwitchAlt = ChkSwitchAlt.IsChecked == true;
            if (ChkSwitchWin != null) _settings.SwitchWin = ChkSwitchWin.IsChecked == true;
            if (ChkSwitchShift != null) _settings.SwitchShift = ChkSwitchShift.IsChecked == true;
            if (TxtSwitchKeyChar != null) _settings.SwitchKeyChar = TxtSwitchKeyChar.Text.Trim();
            if (ChkSwitchBeep != null) _settings.SwitchBeep = ChkSwitchBeep.IsChecked == true;

            // Tab Tùy chọn
            if (ChkCheckSpelling != null) _settings.CheckSpelling = ChkCheckSpelling.IsChecked == true;
            if (ChkRestoreIfWrongSpelling != null) _settings.RestoreIfWrongSpelling = ChkRestoreIfWrongSpelling.IsChecked == true;
            if (ChkModernTone != null) _settings.ModernToneRules = ChkModernTone.IsChecked == true;
            if (ChkFreeMark != null) _settings.FreeMark = ChkFreeMark.IsChecked == true;
            if (ChkAllowConsonantZFWJ != null) _settings.AllowConsonantZFWJ = ChkAllowConsonantZFWJ.IsChecked == true;
            if (ChkUpperCaseFirstChar != null) _settings.UpperCaseFirstChar = ChkUpperCaseFirstChar.IsChecked == true;
            if (ChkFixRecommendBrowser != null) _settings.FixRecommendBrowser = ChkFixRecommendBrowser.IsChecked == true;
            if (ChkClipboard != null) _settings.SendViaClipboard = ChkClipboard.IsChecked == true;
            if (ChkSmartCodePassthrough != null) _settings.SmartCodePassthrough = ChkSmartCodePassthrough.IsChecked == true;
            if (ChkEscKeyUndo != null) _settings.EscKeyUndo = ChkEscKeyUndo.IsChecked == true;
            if (ChkEnableStatusOsd != null) _settings.EnableStatusOsd = ChkEnableStatusOsd.IsChecked == true;
            if (ChkAutoExclude != null) _settings.AutoExcludeEnabled = ChkAutoExclude.IsChecked == true;
            if (ChkStartWithWindows != null) _settings.StartWithWindows = ChkStartWithWindows.IsChecked == true;
            if (ChkStartAsAdmin != null) _settings.StartAsAdmin = ChkStartAsAdmin.IsChecked == true;
            if (ChkOpenDialogOnStartup != null) _settings.OpenDialogOnStartup = ChkOpenDialogOnStartup.IsChecked == true;

            // Tab Clipboard
            if (ChkEnableClipboard != null) _settings.EnableClipboardHistory = ChkEnableClipboard.IsChecked == true;
            if (ChkClipboardAutoHide != null) _settings.ClipboardAutoHide = ChkClipboardAutoHide.IsChecked == true;
            if (ChkClipboardAlwaysOnTop != null) _settings.ClipboardAlwaysOnTop = ChkClipboardAlwaysOnTop.IsChecked == true;
            if (ChkClipboardIgnoreDuplicates != null) _settings.ClipboardIgnoreDuplicates = ChkClipboardIgnoreDuplicates.IsChecked == true;
            if (ChkClipboardPasteAsPlainText != null) _settings.ClipboardPasteAsPlainText = ChkClipboardPasteAsPlainText.IsChecked == true;
            if (ChkClipboardMaskSensitive != null) _settings.ClipboardMaskSensitive = ChkClipboardMaskSensitive.IsChecked == true;
            if (ChkClipboardAutoPurgeSensitive != null) _settings.ClipboardAutoPurgeSensitive = ChkClipboardAutoPurgeSensitive.IsChecked == true;

            // Mở rộng Cyberpunk & Checkbox quản lý
            if (ChkSmartEnglishBypass != null) _settings.SmartEnglishBypass = ChkSmartEnglishBypass.IsChecked == true;
            if (ChkOneKeyUndoRaw != null) _settings.OneKeyUndoRaw = ChkOneKeyUndoRaw.IsChecked == true;
            if (ChkInlineMathEvaluator != null) _settings.InlineMathEvaluator = ChkInlineMathEvaluator.IsChecked == true;
            if (ChkEnableCaretIndicator != null) _settings.EnableCaretIndicator = ChkEnableCaretIndicator.IsChecked == true;
            if (ChkEnableKeySound != null) _settings.EnableKeySound = ChkEnableKeySound.IsChecked == true;
            if (ChkGameMode != null) _settings.GameModeEnabled = ChkGameMode.IsChecked == true;
            if (ChkQuickTextTransform != null) _settings.QuickTextTransformEnabled = ChkQuickTextTransform.IsChecked == true;
            if (ChkP2PAutoSync != null) _settings.P2PAutoSyncEnabled = ChkP2PAutoSync.IsChecked == true;
            if (ChkTypingStats != null) _settings.TypingStatsEnabled = ChkTypingStats.IsChecked == true;
            if (TxtSyncFolderPath != null) _settings.SyncFolderPath = TxtSyncFolderPath.Text.Trim();

            // Tab Gõ tắt
            if (ChkUseMacro != null) _settings.UseMacro = ChkUseMacro.IsChecked == true;
            if (ChkUseMacroInEnglish != null) _settings.UseMacroInEnglish = ChkUseMacroInEnglish.IsChecked == true;
            if (ChkAutoCapsMacro != null) _settings.AutoCapsMacro = ChkAutoCapsMacro.IsChecked == true;
            if (ChkDynamicMacro != null) _settings.DynamicMacroEnabled = ChkDynamicMacro.IsChecked == true;

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
            // F6 và F7 luôn được bật để người dùng dùng phím tắt chuyển đổi bất kỳ lúc nào
            fMask |= (1 << 6);
            fMask |= (1 << 7);
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

            if (CmbShortcutF6InputMethod != null)
            {
                CmbShortcutF6InputMethod.IsEnabled = true;
                if (CmbShortcutF6InputMethod.SelectedItem is InputMethod f6im)
                {
                    _settings.ShortcutF6InputMethod = f6im;
                }
            }

            if (CmbShortcutF7InputMethod != null)
            {
                CmbShortcutF7InputMethod.IsEnabled = true;
                if (CmbShortcutF7InputMethod.SelectedItem is InputMethod f7im)
                {
                    _settings.ShortcutF7InputMethod = f7im;
                }
            }

            if (ChkGameModeCtrl != null) _settings.GameModeCtrl = ChkGameModeCtrl.IsChecked == true;
            if (ChkGameModeAlt != null) _settings.GameModeAlt = ChkGameModeAlt.IsChecked == true;
            if (ChkGameModeWin != null) _settings.GameModeWin = ChkGameModeWin.IsChecked == true;
            if (ChkGameModeShift != null) _settings.GameModeShift = ChkGameModeShift.IsChecked == true;
            if (TxtGameModeKeyChar != null) _settings.GameModeKeyChar = TxtGameModeKeyChar.Text.Trim();

            if (ChkQuickTextCtrl != null) _settings.QuickTextCtrl = ChkQuickTextCtrl.IsChecked == true;
            if (ChkQuickTextAlt != null) _settings.QuickTextAlt = ChkQuickTextAlt.IsChecked == true;
            if (ChkQuickTextWin != null) _settings.QuickTextWin = ChkQuickTextWin.IsChecked == true;
            if (ChkQuickTextShift != null) _settings.QuickTextShift = ChkQuickTextShift.IsChecked == true;
            if (TxtQuickTextKeyChar != null) _settings.QuickTextKeyChar = TxtQuickTextKeyChar.Text.Trim();
        }

        private void BtnQuickLangToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsVietnamese = !_settings.IsVietnamese;
            SettingsManager.SaveSettings(_settings);
            _trayManager.UpdateTrayIcon();
            _trayManager.BuildContextMenu();
            RefreshState();
            (Application.Current as App)?.ShowStatusOsd(_settings.IsVietnamese);
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
                _isUpdatingUi = true;
                try
                {
                    if (CmbCompactInputMethod != null) CmbCompactInputMethod.SelectedItem = im;
                    if (im == _settings.ShortcutF6InputMethod && RadShortcutF6 != null)
                    {
                        RadShortcutF6.IsChecked = true;
                    }
                    else if (im == _settings.ShortcutF7InputMethod && RadShortcutF7 != null)
                    {
                        RadShortcutF7.IsChecked = true;
                    }
                }
                finally
                {
                    _isUpdatingUi = false;
                }
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

        private void BtnOpenSpellingDict_Click(object sender, RoutedEventArgs e)
        {
            SpellingCorrectionManager.Instance.OpenDictionaryFile();
        }

        private void TxtSwitchKeyChar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (TxtSwitchKeyChar != null)
            {
                _settings.SwitchKeyChar = TxtSwitchKeyChar.Text.Trim();
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

        private void RadShortcutF6_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _settings.ShortcutEnableMask |= (1 << 6) | (1 << 7);
            _settings.CurrentInputMethod = _settings.ShortcutF6InputMethod;
            SettingsManager.SaveSettings(_settings);

            _isUpdatingUi = true;
            try
            {
                if (CmbInputMethod != null) CmbInputMethod.SelectedItem = _settings.CurrentInputMethod;
                if (CmbCompactInputMethod != null) CmbCompactInputMethod.SelectedItem = _settings.CurrentInputMethod;
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (_settings.SwitchBeep)
            {
                KeyboardHook.PlayInputMethodBeep(_settings.CurrentInputMethod);
            }
            _trayManager?.UpdateTrayIcon();
            _trayManager?.BuildContextMenu();
            (Application.Current as App)?.ShowStatusOsd(_settings.IsVietnamese);
        }

        private void RadShortcutF7_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _settings.ShortcutEnableMask |= (1 << 6) | (1 << 7);
            _settings.CurrentInputMethod = _settings.ShortcutF7InputMethod;
            SettingsManager.SaveSettings(_settings);

            _isUpdatingUi = true;
            try
            {
                if (CmbInputMethod != null) CmbInputMethod.SelectedItem = _settings.CurrentInputMethod;
                if (CmbCompactInputMethod != null) CmbCompactInputMethod.SelectedItem = _settings.CurrentInputMethod;
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (_settings.SwitchBeep)
            {
                KeyboardHook.PlayInputMethodBeep(_settings.CurrentInputMethod);
            }
            _trayManager?.UpdateTrayIcon();
            _trayManager?.BuildContextMenu();
            (Application.Current as App)?.ShowStatusOsd(_settings.IsVietnamese);
        }

        private void CmbShortcutF6InputMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbShortcutF6InputMethod.SelectedItem is InputMethod im)
            {
                _settings.ShortcutF6InputMethod = im;
                if (RadShortcutF6?.IsChecked == true)
                {
                    _settings.CurrentInputMethod = im;
                    _isUpdatingUi = true;
                    try
                    {
                        if (CmbInputMethod != null) CmbInputMethod.SelectedItem = im;
                        if (CmbCompactInputMethod != null) CmbCompactInputMethod.SelectedItem = im;
                    }
                    finally
                    {
                        _isUpdatingUi = false;
                    }
                    _trayManager?.UpdateTrayIcon();
                    _trayManager?.BuildContextMenu();
                }
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void CmbShortcutF7InputMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (CmbShortcutF7InputMethod.SelectedItem is InputMethod im)
            {
                _settings.ShortcutF7InputMethod = im;
                if (RadShortcutF7?.IsChecked == true)
                {
                    _settings.CurrentInputMethod = im;
                    _isUpdatingUi = true;
                    try
                    {
                        if (CmbInputMethod != null) CmbInputMethod.SelectedItem = im;
                        if (CmbCompactInputMethod != null) CmbCompactInputMethod.SelectedItem = im;
                    }
                    finally
                    {
                        _isUpdatingUi = false;
                    }
                    _trayManager?.UpdateTrayIcon();
                    _trayManager?.BuildContextMenu();
                }
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void TxtGameModeKeyChar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (TxtGameModeKeyChar != null)
            {
                _settings.GameModeKeyChar = TxtGameModeKeyChar.Text.Trim();
                SettingsManager.SaveSettings(_settings);
            }
        }

        private void TxtQuickTextKeyChar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (TxtQuickTextKeyChar != null)
            {
                _settings.QuickTextKeyChar = TxtQuickTextKeyChar.Text.Trim();
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

            // Loại trừ tương hỗ giữa OSD chuột và Caret HUD để không bao giờ bị hiện đúp 2 huy hiệu [VI]
            if (sender == ChkEnableStatusOsd && ChkEnableStatusOsd.IsChecked == true && ChkEnableCaretIndicator != null)
            {
                ChkEnableCaretIndicator.IsChecked = false;
            }
            else if (sender == ChkEnableCaretIndicator && ChkEnableCaretIndicator.IsChecked == true && ChkEnableStatusOsd != null)
            {
                ChkEnableStatusOsd.IsChecked = false;
            }

            SyncUiToSettings();
            SettingsManager.SaveSettings(_settings);
            _trayManager?.BuildContextMenu();
        }

        private bool _isUpdatingMacroFields = false;

        private void DgMacro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingMacroFields) return;

            if (DgMacro.SelectedItem is MacroEntry selected)
            {
                _isUpdatingMacroFields = true;
                TxtMacroShortcut.Text = selected.Shortcut;
                TxtMacroReplacement.Text = selected.Replacement;
                _isUpdatingMacroFields = false;
            }
        }

        private void TxtMacroShortcut_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingMacroFields) return;

            string query = TxtMacroShortcut.Text?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _isUpdatingMacroFields = true;
                TxtMacroReplacement.Text = string.Empty;
                DgMacro.SelectedItem = null;
                _isUpdatingMacroFields = false;
                return;
            }

            if (_macroManager?.MacroList == null || _macroManager.MacroList.Count == 0)
                return;

            // Cơ chế OpenKey C++ (MacroDialog.cpp dòng 164-184):
            // 1. Exact match trước: item.Shortcut.Equals(query, OrdinalIgnoreCase)
            MacroEntry matched = null;
            foreach (var item in _macroManager.MacroList)
            {
                if (string.Equals(item.Shortcut, query, StringComparison.OrdinalIgnoreCase))
                {
                    matched = item;
                    break;
                }
            }

            // 2. Prefix match nếu chưa tìm thấy exact match
            if (matched == null)
            {
                foreach (var item in _macroManager.MacroList)
                {
                    if (item.Shortcut != null && item.Shortcut.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = item;
                        break;
                    }
                }
            }

            if (matched != null)
            {
                _isUpdatingMacroFields = true;
                DgMacro.SelectedItem = matched;
                DgMacro.ScrollIntoView(matched);
                TxtMacroReplacement.Text = matched.Replacement;
                _isUpdatingMacroFields = false;
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
                    _macroManager.Save();
                    TxtMacroShortcut.Clear();
                    TxtMacroReplacement.Clear();
                    return;
                }
            }

            _macroManager.MacroList.Add(new MacroEntry(shortcut, replacement));
            _macroManager.Save();
            TxtMacroShortcut.Clear();
            TxtMacroReplacement.Clear();
        }

        private void BtnDeleteMacro_Click(object sender, RoutedEventArgs e)
        {
            if (DgMacro.SelectedItem is MacroEntry selected)
            {
                _macroManager.MacroList.Remove(selected);
                _macroManager.Save();
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
                            _macroManager.Save();
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

        public void ApplyCompactModeUi(bool isCompact)
        {
            if (isCompact)
            {
                if (MainTabControl != null) MainTabControl.Visibility = Visibility.Collapsed;
                if (MainFooter != null) MainFooter.Visibility = Visibility.Collapsed;
                if (CompactPanel != null) CompactPanel.Visibility = Visibility.Visible;
                if (BtnToggleCompact != null) BtnToggleCompact.Content = "[MỞ RỘNG]";
                Height = 190;
                Width = 580;
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                if (CompactPanel != null) CompactPanel.Visibility = Visibility.Collapsed;
                if (MainTabControl != null) MainTabControl.Visibility = Visibility.Visible;
                if (MainFooter != null) MainFooter.Visibility = Visibility.Visible;
                if (BtnToggleCompact != null) BtnToggleCompact.Content = "[HUD COMPACT]";
                Height = 720;
                Width = 820;
                ResizeMode = ResizeMode.CanResize;
            }
        }

        private void BtnToggleCompact_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsCompactMode = !_settings.IsCompactMode;
            ApplyCompactModeUi(_settings.IsCompactMode);
            SettingsManager.SaveSettings(_settings);
        }

        private void BtnApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (CmbProfile?.SelectedItem is ComboBoxItem item && item.Tag is string profName)
            {
                _settings.ApplyProfile(profName);
                SettingsManager.SaveSettings(_settings);
                RefreshState();
                MessageBox.Show($"Đã áp dụng thành công profile: [{profName}]!", "ModernKey Profile",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportProfileJson_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Profile (*.json)|*.json|All Files (*.*)|*.*",
                FileName = $"ModernKey_Profile_{_settings.ActiveProfile}.json",
                Title = "Xuất cấu hình ModernKey ra file JSON"
            };

            if (sfd.ShowDialog() == true)
            {
                if (SettingsManager.ExportProfileJson(_settings, sfd.FileName))
                {
                    MessageBox.Show("Xuất file JSON cấu hình thành công!", "Thông báo",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Có lỗi xảy ra khi xuất file JSON!", "Lỗi",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnImportProfileJson_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Profile (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Nhập cấu hình ModernKey từ file JSON"
            };

            if (ofd.ShowDialog() == true)
            {
                if (SettingsManager.ImportProfileJson(ofd.FileName, _settings))
                {
                    SettingsManager.SaveSettings(_settings);
                    RefreshState();
                    MessageBox.Show("Nhập cấu hình JSON thành công!", "Thông báo",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không thể đọc cấu hình từ file JSON đã chọn!", "Lỗi",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtClipboardMaxItems_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (int.TryParse(TxtClipboardMaxItems.Text.Trim(), out int val) && val >= 5 && val <= 99999)
            {
                _settings.ClipboardMaxItems = val;
                if (Application.Current is App app && app.ClipboardHistory != null)
                {
                    app.ClipboardHistory.ApplyMaxLimit();
                }
            }
        }

        private void TxtClipboardMaxItems_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (!int.TryParse(TxtClipboardMaxItems.Text.Trim(), out int val) || val < 5 || val > 99999)
            {
                int fallback = (_settings.ClipboardMaxItems >= 5 && _settings.ClipboardMaxItems <= 99999) ? _settings.ClipboardMaxItems : 200;
                _settings.ClipboardMaxItems = fallback;
                TxtClipboardMaxItems.Text = fallback.ToString();
            }
            else
            {
                _settings.ClipboardMaxItems = val;
                TxtClipboardMaxItems.Text = val.ToString();
            }
            if (Application.Current is App app && app.ClipboardHistory != null)
            {
                app.ClipboardHistory.ApplyMaxLimit();
            }
        }

        private void BtnOpenClipboardHud_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowClipboardWindow();
            }
        }

        private void BtnClearClipboardHistory_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && app.ClipboardHistory != null)
            {
                var res = MessageBox.Show("Bạn có chắc chắn muốn xóa lịch sử clipboard (giữ lại các mục ghim yêu thích)?",
                                          "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    app.ClipboardHistory.ClearAll(keepFavorites: true);
                    MessageBox.Show("Đã làm sạch lịch sử clipboard!", "ModernKey HUD", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnClearAllClipboardHistory_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && app.ClipboardHistory != null)
            {
                var res = MessageBox.Show("Bạn có chắc chắn muốn xóa TOÀN BỘ lịch sử clipboard (kể cả mục yêu thích)?",
                                          "Cảnh báo xóa sạch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    app.ClipboardHistory.ClearAll(keepFavorites: false);
                    MessageBox.Show("Đã xóa toàn bộ dữ liệu clipboard!", "ModernKey HUD", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void TxtSyncFolderPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            if (TxtSyncFolderPath != null)
            {
                _settings.SyncFolderPath = TxtSyncFolderPath.Text.Trim();
                SettingsManager.SaveSettings(_settings);
                SettingsManager.SetupSyncWatcher(_settings.SyncFolderPath);
            }
        }

        private void BtnBrowseSyncFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Chọn thư mục đồng bộ ModernKey (LAN, OneDrive, Dropbox, v.v.):";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtSyncFolderPath.Text = dlg.SelectedPath;
                    _settings.SyncFolderPath = dlg.SelectedPath;
                    SettingsManager.SaveSettings(_settings);
                    SettingsManager.SetupSyncWatcher(_settings.SyncFolderPath);
                    SettingsManager.ExportToSyncFolder(_settings.SyncFolderPath, _settings);
                    MessageBox.Show("Đã thiết lập thư mục đồng bộ thành công!", "ModernKey Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnSyncNow_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtSyncFolderPath?.Text?.Trim();
            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                MessageBox.Show("Vui lòng chọn một thư mục hợp lệ trước khi đồng bộ!", "Lỗi đồng bộ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SettingsManager.ExportToSyncFolder(path, _settings);
            MessageBox.Show("Đã đồng bộ cấu hình và file gõ tắt sang thư mục thành công!", "ModernKey Sync", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefreshStats_Click(object sender, RoutedEventArgs e)
        {
            RenderTypingStats();
        }

        private void BtnResetStats_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Bạn có chắc chắn muốn đặt lại toàn bộ thống kê gõ phím về 0?", "Xác nhận đặt lại", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                TypingStatsManager.Instance.ResetStats();
                RenderTypingStats();
            }
        }

        private void RenderTypingStats()
        {
            try
            {
                var stats = TypingStatsManager.Instance;
                if (TxtStatsWpm != null) TxtStatsWpm.Text = stats.CurrentWpm.ToString();
                if (TxtStatsKeystrokes != null) TxtStatsKeystrokes.Text = stats.TotalKeystrokes.ToString("N0");
                if (TxtStatsWords != null) TxtStatsWords.Text = stats.TotalWords.ToString("N0");
                if (TxtStatsTime != null)
                {
                    int mins = stats.ActiveTypingMinutes;
                    if (mins < 60) TxtStatsTime.Text = $"{mins}m";
                    else TxtStatsTime.Text = $"{mins / 60}h {mins % 60}m";
                }

                if (GridHourlyHeatmap != null)
                {
                    GridHourlyHeatmap.Children.Clear();
                    GridHourlyHeatmap.ColumnDefinitions.Clear();

                    int[] hourly = stats.GetHourlyActivity();
                    int max = 1;
                    for (int i = 0; i < hourly.Length; i++)
                    {
                        if (hourly[i] > max) max = hourly[i];
                    }

                    for (int i = 0; i < 24; i++)
                    {
                        GridHourlyHeatmap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        double ratio = (double)hourly[i] / max;
                        double barHeight = Math.Max(4, ratio * 90);

                        var bar = new Border
                        {
                            VerticalAlignment = VerticalAlignment.Bottom,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Width = 14,
                            Height = barHeight,
                            CornerRadius = new CornerRadius(2, 2, 0, 0),
                            Background = hourly[i] == 0 ? new SolidColorBrush(Color.FromRgb(0x18, 0x22, 0x2A))
                                        : ratio > 0.7 ? new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x7F))
                                        : ratio > 0.3 ? new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF))
                                        : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66)),
                            ToolTip = $"{i}h: {hourly[i]} phím"
                        };
                        Grid.SetColumn(bar, i);
                        GridHourlyHeatmap.Children.Add(bar);
                    }
                }
            }
            catch { }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isRealExit)
            {
                e.Cancel = true;
                Hide();
                App.TrimWorkingSet();
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
