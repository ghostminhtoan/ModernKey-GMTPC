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
        // Kiểu gõ chuyển nhanh khi bấm F6 (mặc định Tư Bình Trần theo yêu cầu)
        public InputMethod ShortcutF6InputMethod { get; set; } = InputMethod.TuBinhTran;

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

        // Làm mờ Clipboard (Blur / Pixelated) với slider từ 0 đến 100
        // Mode: "None", "Blur", "Pixelate"
        public string ClipboardBlurMode { get; set; } = "None";
        public double ClipboardBlurRadius { get; set; } = 20.0;
        public double ClipboardPixelateSize { get; set; } = 15.0;

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

        // 14 Tính Năng Nâng Cấp Độc Đáo
        public bool SmartEnglishBypass { get; set; } = true; // Tính năng 3: Song ngữ Anh - Việt thông minh
        public bool GameModeEnabled { get; set; } = false; // Tính năng 4: Game Mode Zero Latency
        public bool OneKeyUndoRaw { get; set; } = true; // Tính năng 5: Hoàn tác 1 phím về ASCII thô
        public bool DynamicMacroEnabled { get; set; } = true; // Tính năng 11: Dynamic Snippets ({date}, {clipboard}...)
        public bool InlineMathEvaluator { get; set; } = true; // Tính năng 12: Tính toán biểu thức inline (vd: 125*45=)
        public bool QuickTextTransformEnabled { get; set; } = true; // Tính năng 13: Quick Text Transform Toolbar (Ctrl+Shift+U)
        public bool EnableCaretIndicator { get; set; } = false; // Tính năng 16: Chỉ báo ngôn ngữ bám theo con trỏ soạn thảo (Mặc định tắt để tối ưu hiệu năng)
        public bool EnableKeySound { get; set; } = false; // Tính năng 17: Âm thanh phím cơ Cyberpunk
        public bool TypingStatsEnabled { get; set; } = true; // Tính năng 18: Thống kê tốc độ gõ WPM & Heatmap
        public string SyncFolderPath { get; set; } = string.Empty; // Tính năng 20: Thư mục đồng bộ P2P/Local
        public bool P2PAutoSyncEnabled { get; set; } = false; // Tính năng 20: Tự động đồng bộ P2P khi có thay đổi

        // Tương thích ngược cấu hình
        public bool SensitiveDataMasking { get; set; } = false;
        public int SensitiveAutoPurgeMinutes { get; set; } = 0;
        public bool ClipboardMaskSensitive
        {
            get => SensitiveDataMasking;
            set => SensitiveDataMasking = value;
        }
        public bool ClipboardAutoPurgeSensitive
        {
            get => SensitiveAutoPurgeMinutes > 0;
            set => SensitiveAutoPurgeMinutes = value ? 10 : 0;
        }

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
