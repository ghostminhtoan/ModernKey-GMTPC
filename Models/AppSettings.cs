using System;
using ModernKey.Core;

namespace ModernKey.Models
{
    public class AppSettings
    {
        public InputMethod CurrentInputMethod { get; set; } = InputMethod.Telex;
        public Charset CurrentCharset { get; set; } = Charset.Unicode;
        public SwitchKeyMode SwitchMode { get; set; } = SwitchKeyMode.AltZ;

        // Tùy chọn phím chuyển VI/EN tự do chuẩn OpenKey C++
        public bool SwitchCtrl { get; set; } = false;
        public bool SwitchAlt { get; set; } = true;
        public bool SwitchWin { get; set; } = false;
        public bool SwitchShift { get; set; } = false;
        public string SwitchKeyChar { get; set; } = "Z";
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

        // Danh sách ứng dụng loại trừ (Exclude Apps / App Blacklist)
        public System.Collections.Generic.List<string> ExcludedApps { get; set; } = new System.Collections.Generic.List<string>
        {
            "cs2.exe", "valorant.exe", "dota2.exe", "league of legends.exe"
        };
        public bool AutoExcludeEnabled { get; set; } = true;

        // OSD hiển thị trạng thái VI/EN
        public bool EnableStatusOsd { get; set; } = true;

        // Quản lý Clipboard (Lịch sử, phím tắt, lọc trùng, tự ẩn)
        public bool EnableClipboardHistory { get; set; } = true;
        public bool ClipboardAutoHide { get; set; } = true;
        public bool ClipboardAlwaysOnTop { get; set; } = true;
        public bool ClipboardIgnoreDuplicates { get; set; } = true;
        public bool ClipboardPasteAsPlainText { get; set; } = false;
        public int ClipboardMaxItems { get; set; } = 200;

        // Tùy chọn gộp Clipboard khi chọn nhiều mục (Ctrl+C / Paste)
        // 0: Không đánh số, 1: Số thường (1. 2. 3.), 2: Số La Mã (I. II. III.), 3: Alphabet thường (a. b. c.), 4: Alphabet hoa (A. B. C.)
        public int ClipboardMergeNumbering { get; set; } = 0;
        public bool ClipboardMergePrefixDash { get; set; } = false;      // Dấu gạch nối: - 
        public bool ClipboardMergePrefixArrow { get; set; } = false;     // Dấu mũi tên: -> 
        public bool ClipboardMergePrefixImplies { get; set; } = false;   // Dấu suy ra: => 
        public bool ClipboardMergePrefixAsterisk { get; set; } = false;  // Dấu hoa thị: * 
        public bool ClipboardMergeDoubleSpacing { get; set; } = false;   // Xuống dòng đúp (\n\n) thay vì đơn (\n)

        // Tạm dừng thông minh cho lập trình viên (Smart Passthrough & Esc Undo)
        public bool SmartCodePassthrough { get; set; } = true;
        public bool EscKeyUndo { get; set; } = true;

        // Chế độ thu nhỏ Compact HUD
        public bool IsCompactMode { get; set; } = false;

        // Quản lý Profile (Office, Coding, Gaming)
        public string ActiveProfile { get; set; } = "Office";

        public void ApplyProfile(string profileName)
        {
            ActiveProfile = profileName;
            if (string.Equals(profileName, "Gaming", StringComparison.OrdinalIgnoreCase))
            {
                UseMacro = false;
                CheckSpelling = false;
                AutoExcludeEnabled = true;
                SendViaClipboard = false;
                SmartCodePassthrough = false;
            }
            else if (string.Equals(profileName, "Coding", StringComparison.OrdinalIgnoreCase))
            {
                SmartCodePassthrough = true;
                EscKeyUndo = true;
                AllowConsonantZFWJ = true;
                FixRecommendBrowser = true;
                UseMacroInEnglish = false;
                CheckSpelling = true;
                RestoreIfWrongSpelling = true;
            }
            else // "Office" or Default
            {
                CheckSpelling = true;
                RestoreIfWrongSpelling = true;
                UseMacro = true;
                AutoCapsMacro = true;
                ModernToneRules = true;
                SmartCodePassthrough = true;
                EscKeyUndo = true;
            }
        }
    }
}
