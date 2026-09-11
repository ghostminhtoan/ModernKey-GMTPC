using System;
using ModernKey.Core;

namespace ModernKey.Models
{
    public class AppSettings
    {
        public InputMethod CurrentInputMethod { get; set; } = InputMethod.Telex;
        public Charset CurrentCharset { get; set; } = Charset.Unicode;
        public SwitchKeyMode SwitchMode { get; set; } = SwitchKeyMode.CtrlShift;
        public bool IsVietnamese { get; set; } = true;
        public bool CheckSpelling { get; set; } = true;
        public bool UseMacro { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool StartAsAdmin { get; set; } = false;
        public bool OpenDialogOnStartup { get; set; } = true;
        public bool SendViaClipboard { get; set; } = false;
        public bool ModernDarkTheme { get; set; } = true;
        public bool ModernToneRules { get; set; } = true; // oa` -> oà vs o`a -> òa
        public bool AllowNumberInWordBreak { get; set; } = true; // Bao ve so thuan theo workflow.md
        public bool RestoreIfWrongSpelling { get; set; } = true;
        public bool SwitchBeep { get; set; } = false;

        // Tùy chọn Gõ tắt nâng cao chuẩn OpenKey C++
        public bool AutoCapsMacro { get; set; } = true;
        public bool UseMacroInEnglish { get; set; } = false;
        public int MacroTriggerMask { get; set; } = 3; // 1: Space, 2: Enter, 4: 2x LShift, 8: 2x RShift

        // Tùy chọn gõ tiếng Việt OpenKey C++
        public bool AllowConsonantZFWJ { get; set; } = false;
        public bool FixRecommendBrowser { get; set; } = true;
        public bool UpperCaseFirstChar { get; set; } = false;

        // Cấu hình Tab Phím tắt (Hotkeys F1-F12 with customizable modifiers) chuẩn OpenKey C++
        // Modifier bits: 0x01: Ctrl, 0x02: Shift, 0x04: Alt, 0x08: Win. Mặc định Ctrl+Alt (1 | 4 = 5)
        public int ShortcutModifier { get; set; } = 0x05;
        // Enable mask: bit 1..12 tương ứng các phím F1..F12. Mặc định 0x1FFF (bật tất cả phím F1..F12)
        public int ShortcutEnableMask { get; set; } = 0x1FFF;
        // Bảng mã chuyển nhanh khi bấm F4 (mặc định VNI Windows theo OpenKey C++)
        public Charset ShortcutF4Charset { get; set; } = Charset.VniWindows;

        // Danh sách quy tắc kiểu gõ tự định nghĩa (Custom Input Method)
        public System.Collections.Generic.List<CustomInputRule> CustomRules { get; set; } = CustomInputRule.GetPreset(0);
    }
}
