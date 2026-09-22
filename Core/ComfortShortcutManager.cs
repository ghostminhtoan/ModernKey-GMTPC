using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ModernKey.Config;
using ModernKey.Hook;
using ModernKey.Models;

namespace ModernKey.Core
{
    public class ComfortShortcutManager
    {
        private static ComfortShortcutManager _instance;
        public static ComfortShortcutManager Instance => _instance ?? (_instance = new ComfortShortcutManager());

        public ObservableCollection<ComfortShortcutItem> Shortcuts { get; } = new ObservableCollection<ComfortShortcutItem>();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        private const int SW_RESTORE = 9;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public ComfortShortcutManager()
        {
            Load();
        }

        public string GetShortcutsFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "ModernKey");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "comfort_shortcuts.json");
        }

        public void Load()
        {
            Shortcuts.Clear();
            string path = GetShortcutsFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var list = DeserializeJson(json);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list)
                        {
                            Shortcuts.Add(item);
                        }
                        return;
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            // Tạo các mục mẫu chuẩn xác mô phỏng trọn vẹn Đợt 1 đến Đợt 5 từ Comfort Keys Pro
            LoadDefaultPresets();
            Save();
        }

        public void Save()
        {
            try
            {
                string path = GetShortcutsFilePath();
                string json = SerializeJson(Shortcuts.ToList());
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch { }
        }

        public void ExportToFile(string filePath)
        {
            string json = SerializeJson(Shortcuts.ToList());
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        public void ImportFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            var list = DeserializeJson(json);
            if (list != null)
            {
                Shortcuts.Clear();
                foreach (var item in list)
                {
                    Shortcuts.Add(item);
                }
                Save();
            }
        }

        private void LoadDefaultPresets()
        {
            // 1. Run program (Đợt 2)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Run program",
                KeyCombination = "Ctrl+Nm 1",
                ActionType = ShortcutActionType.RunProgram,
                ActiveScope = "In all screen modes",
                Label = "Open Q-Dir Explorer",
                ProgramPaths = @"T:\[HDD T]\Program files\Q-Dir explorer\Q-Dir_x64.exe" + "\n" + @"C:\Windows\explorer.exe",
                StartInFolder = @"C:\",
                SwitchToAlreadyLaunched = true,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Run program",
                KeyCombination = "Ctrl+Nm 2",
                ActionType = ShortcutActionType.RunProgram,
                ActiveScope = "In all screen modes",
                Label = "Open Notepad",
                ProgramPaths = "notepad.exe",
                SwitchToAlreadyLaunched = true,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Run program",
                KeyCombination = "Ctrl+Nm 3",
                ActionType = ShortcutActionType.RunProgram,
                ActiveScope = "In all screen modes",
                Label = "Open Calculator",
                ProgramPaths = "calc.exe",
                SwitchToAlreadyLaunched = true,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Run program",
                KeyCombination = "Ctrl+Nm 4",
                ActionType = ShortcutActionType.RunProgram,
                ActiveScope = "In all screen modes",
                Label = "Open Task Manager",
                ProgramPaths = "taskmgr.exe",
                SwitchToAlreadyLaunched = true,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Run program",
                KeyCombination = "Ctrl+Nm 5",
                ActionType = ShortcutActionType.RunProgram,
                ActiveScope = "In all screen modes",
                Label = "Open Terminal",
                ProgramPaths = "cmd.exe",
                SwitchToAlreadyLaunched = false,
                LastChanged = DateTime.Now
            });

            // 2. Open URL (Đợt 3)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Open URL",
                KeyCombination = "Win+Alt+W",
                ActionType = ShortcutActionType.OpenUrl,
                ActiveScope = "In all screen modes",
                Label = "Open Web Search",
                Urls = "https://www.google.com",
                UrlOpenType = "Default",
                LastChanged = DateTime.Now
            });

            // 3. Paste text (Đợt 4)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Paste text",
                KeyCombination = "Ctrl+Alt+Ins",
                ActionType = ShortcutActionType.PasteText,
                ActiveScope = "In all screen modes",
                Label = "Insert Timestamp & Note",
                PasteText = "=== BÁO CÁO CÔNG VIỆC ===\nNgày: {date}\nGiờ: {time}\nNội dung: ",
                ShowTextOnKeyboard = true,
                LastChanged = DateTime.Now
            });


            // 5. Audio control (Đợt 1)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Audio control",
                KeyCombination = "Win+Alt+Right",
                ActionType = ShortcutActionType.AudioControl,
                ActiveScope = "In all screen modes",
                Label = "Tăng âm lượng",
                AudioAction = "Volume up",
                AudioStepSize = 10,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Audio control",
                KeyCombination = "Win+Alt+Left",
                ActionType = ShortcutActionType.AudioControl,
                ActiveScope = "In all screen modes",
                Label = "Giảm âm lượng",
                AudioAction = "Volume down",
                AudioStepSize = 10,
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Audio control",
                KeyCombination = "Win+Ctrl+M",
                ActionType = ShortcutActionType.AudioControl,
                ActiveScope = "In all screen modes",
                Label = "Bật / Tắt tiếng (Mute)",
                AudioAction = "Volume on/off",
                AudioStepSize = 10,
                LastChanged = DateTime.Now
            });

            // 6. Monitor control & Window control (Đợt 1)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Window control",
                KeyCombination = "Ctrl+Alt+Shift+F4",
                ActionType = ShortcutActionType.WindowControl,
                ActiveScope = "In all screen modes",
                Label = "Đóng cửa sổ hiện tại",
                LastChanged = DateTime.Now
            });
        }

        public bool TryExecuteMatchingShortcut(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            string combo = BuildCombinationString(vk, ctrl, alt, shift, win);
            if (string.IsNullOrEmpty(combo)) return false;

            var match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, combo, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                // Thử khớp dạng viết tắt numpad (vd: Ctrl+Nm 1 vs Ctrl+Num 1)
                string altCombo = combo.Replace("Num ", "Nm ").Replace("Numpad ", "Nm ");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, altCombo, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null) return false;

            // Kích hoạt action trong background task để không block keyboard hook
            Task.Run(() => ExecuteShortcutAction(match));
            return true;
        }

        public void ExecuteShortcutAction(ComfortShortcutItem item)
        {
            if (item == null) return;

            // Phát âm thanh nếu có cấu hình Sound
            if (!string.IsNullOrEmpty(item.SoundPath) && File.Exists(item.SoundPath))
            {
                try
                {
                    PlaySound(item.SoundPath, IntPtr.Zero, 0x0001 /* SND_ASYNC */);
                }
                catch { }
            }

            switch (item.ActionType)
            {
                case ShortcutActionType.RunProgram:
                    ExecuteRunProgram(item);
                    break;

                case ShortcutActionType.OpenUrl:
                    ExecuteOpenUrl(item);
                    break;

                case ShortcutActionType.PasteText:
                    ExecutePasteText(item);
                    break;

                case ShortcutActionType.AudioControl:
                    ExecuteAudioControl(item);
                    break;

                case ShortcutActionType.WindowControl:
                    ExecuteWindowControl(item);
                    break;
            }
        }

        private void ExecuteRunProgram(ComfortShortcutItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProgramPaths)) return;

            string[] lines = item.ProgramPaths.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                string target = rawLine.Trim();
                if (string.IsNullOrEmpty(target)) continue;

                try
                {
                    // Nếu tùy chọn SwitchToAlreadyLaunched được bật, thử tìm tiến trình đang chạy
                    if (item.SwitchToAlreadyLaunched)
                    {
                        string exeName = Path.GetFileNameWithoutExtension(target);
                        var procs = Process.GetProcessesByName(exeName);
                        if (procs.Length > 0)
                        {
                            IntPtr hWnd = procs[0].MainWindowHandle;
                            if (hWnd != IntPtr.Zero)
                            {
                                ShowWindowAsync(hWnd, SW_RESTORE);
                                SetForegroundWindow(hWnd);
                                continue;
                            }
                        }
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true
                    };

                    if (!string.IsNullOrWhiteSpace(item.StartInFolder) && Directory.Exists(item.StartInFolder))
                    {
                        psi.WorkingDirectory = item.StartInFolder;
                    }

                    Process.Start(psi);
                }
                catch { }
            }
        }

        private void ExecuteOpenUrl(ComfortShortcutItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Urls)) return;

            string[] lines = item.Urls.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawUrl in lines)
            {
                string url = rawUrl.Trim();
                if (string.IsNullOrEmpty(url)) continue;

                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                try
                {
                    if (string.Equals(item.UrlOpenType, "In New Window", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{url}\"",
                            UseShellExecute = true
                        });
                    }
                    else if (string.Equals(item.UrlOpenType, "Working In Background", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            WindowStyle = ProcessWindowStyle.Minimized,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
                catch { }
            }
        }

        public string ExpandDynamicPasteTags(string template)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            var now = DateTime.Now;
            string text = template;

            // Ngày (10 định dạng từ Đợt 4)
            text = text.Replace("{date}", now.ToShortDateString());
            text = text.Replace("<DATE_LONG>", now.ToString("dddd, MMMM d, yyyy"));
            text = text.Replace("<DATE_SHORT>", now.ToString("M/d/yyyy"));
            text = text.Replace("<DATE_MM_DD_YYYY>", now.ToString("MM/dd/yyyy"));
            text = text.Replace("<DATE_M_D_YYYY>", now.ToString("M/d/yyyy"));
            text = text.Replace("<DATE_MM_DD_YY>", now.ToString("MM/dd/yy"));
            text = text.Replace("<DATE_M_D_YY>", now.ToString("M/d/yy"));
            text = text.Replace("<DATE_DD_MM_YYYY>", now.ToString("dd.MM.yyyy"));
            text = text.Replace("<DATE_D_M_YYYY>", now.ToString("d.M.yyyy"));
            text = text.Replace("<DATE_DD_MM_YY>", now.ToString("dd.MM.yy"));
            text = text.Replace("<DATE_D_M_YY>", now.ToString("d.M.yy"));

            // Giờ (6 định dạng từ Đợt 4)
            text = text.Replace("{time}", now.ToString("h:mm:ss tt"));
            text = text.Replace("<TIME_DEFAULT>", now.ToString("h:mm:ss tt"));
            text = text.Replace("<TIME_HH_MM_24>", now.ToString("HH:mm"));
            text = text.Replace("<TIME_H_MM_24>", now.ToString("H:mm"));
            text = text.Replace("<TIME_HH_MM_12>", now.ToString("hh:mm tt"));
            text = text.Replace("<TIME_H_MM_12>", now.ToString("h:mm tt"));
            text = text.Replace("<TIME_MILLIS>", now.ToString("HH:mm:ss.fff"));

            // Ngày & Giờ
            text = text.Replace("<DATETIME_LONG>", now.ToString("dddd, MMMM d, yyyy h:mm:ss tt"));
            text = text.Replace("<DATETIME_SHORT>", now.ToString("M/d/yyyy h:mm:ss tt"));

            // Clipboard Content
            if (text.Contains("<CLIPBOARD>") || text.Contains("{clipboard}"))
            {
                string clip = string.Empty;
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                        clip = System.Windows.Clipboard.GetText();
                }
                catch { }
                text = text.Replace("<CLIPBOARD>", clip).Replace("{clipboard}", clip);
            }

            // Xử lý thẻ <SomeOf: A | B | C>
            var someOfRegex = new Regex(@"<SomeOf:(.*?)>", RegexOptions.IgnoreCase);
            text = someOfRegex.Replace(text, m =>
            {
                string choicesRaw = m.Groups[1].Value;
                var choices = choicesRaw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (choices.Length > 0)
                {
                    var rnd = new Random();
                    return choices[rnd.Next(choices.Length)].Trim();
                }
                return string.Empty;
            });

            return text;
        }

        private void ExecutePasteText(ComfortShortcutItem item)
        {
            if (string.IsNullOrEmpty(item.PasteText)) return;

            string expanded = ExpandDynamicPasteTags(item.PasteText);

            // Kiểm tra nếu có phím điều khiển (Tab, Enter, Backspace...)
            bool hasKeyControl = expanded.Contains("<KEY_TAB>") || expanded.Contains("<KEY_ENTER>") ||
                                 expanded.Contains("<KEY_BACKSPACE>") || expanded.Contains("<KEY_DEL>") ||
                                 expanded.Contains("<KEY_CTRL_ENTER>");

            if (!hasKeyControl)
            {
                // Dán trực tiếp qua mô phỏng Clipboard an toàn
                KeySender.SendViaClipboardPaste(expanded);
            }
            else
            {
                // Chia nhỏ chuỗi và gửi tuần tự cùng phím điều khiển
                string[] parts = Regex.Split(expanded, @"(<KEY_TAB>|<KEY_ENTER>|<KEY_BACKSPACE>|<KEY_DEL>|<KEY_CTRL_ENTER>|<KEY_SPACE>)");
                foreach (var part in parts)
                {
                    if (part == "<KEY_TAB>")
                    {
                        keybd_event(0x09, 0, 0, UIntPtr.Zero);
                        keybd_event(0x09, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (part == "<KEY_ENTER>")
                    {
                        keybd_event(0x0D, 0, 0, UIntPtr.Zero);
                        keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (part == "<KEY_CTRL_ENTER>")
                    {
                        keybd_event(0x11, 0, 0, UIntPtr.Zero);
                        keybd_event(0x0D, 0, 0, UIntPtr.Zero);
                        keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (part == "<KEY_BACKSPACE>")
                    {
                        keybd_event(0x08, 0, 0, UIntPtr.Zero);
                        keybd_event(0x08, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (part == "<KEY_DEL>")
                    {
                        keybd_event(0x2E, 0, 0, UIntPtr.Zero);
                        keybd_event(0x2E, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (part == "<KEY_SPACE>")
                    {
                        keybd_event(0x20, 0, 0, UIntPtr.Zero);
                        keybd_event(0x20, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    else if (!string.IsNullOrEmpty(part))
                    {
                        KeySender.SendViaClipboardPaste(part);
                    }
                    Thread.Sleep(30);
                }
            }
        }


        public void AdjustVolume(float deltaPercent)
        {
            var res = VolumeController.AdjustVolume(deltaPercent);
            ModernKey.VolumeOsdWindow.Instance?.ShowVolume(res.volume, res.isMuted);
        }

        public void ToggleMute()
        {
            var res = VolumeController.ToggleMute();
            ModernKey.VolumeOsdWindow.Instance?.ShowVolume(res.volume, res.isMuted);
        }

        private void ExecuteAudioControl(ComfortShortcutItem item)
        {
            int stepSize = item.AudioStepSize > 0 ? item.AudioStepSize : 10;

            if (item.AudioAction == "Volume up")
            {
                AdjustVolume(stepSize);
            }
            else if (item.AudioAction == "Volume down")
            {
                AdjustVolume(-stepSize);
            }
            else if (item.AudioAction == "Volume on/off")
            {
                ToggleMute();
            }
            else if (item.AudioAction == "Eject/Close CD door")
            {
                mciSendString("set cdaudio door open", null, 0, IntPtr.Zero);
            }
        }

        private void ExecuteWindowControl(ComfortShortcutItem item)
        {
            // Close window: Alt + F4
            keybd_event(0x12, 0, 0, UIntPtr.Zero); // Alt Down
            keybd_event(0x73, 0, 0, UIntPtr.Zero); // F4 Down
            keybd_event(0x73, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private string BuildCombinationString(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new List<string>();
            if (win) parts.Add("Win");
            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");

            string keyName = GetKeyFriendlyName(vk);
            if (string.IsNullOrEmpty(keyName)) return string.Empty;

            parts.Add(keyName);
            return string.Join("+", parts);
        }

        public static string GetKeyFriendlyName(uint vk)
        {
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();

            // Numpad
            if (vk >= 0x60 && vk <= 0x69) return "Nm " + (vk - 0x60);

            // Function keys
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x6F);

            switch (vk)
            {
                case 0x0D: return "Enter";
                case 0x08: return "Backspace";
                case 0x2D: return "Ins";
                case 0x2E: return "Del";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PgUp";
                case 0x22: return "PgDn";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x14: return "Caps";
                case 0x1B: return "Esc";
                case 0x13: return "Pause";
                case 0x2C: return "PrtSc";
                case 0x90: return "NumL";
                case 0x91: return "ScrLk";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xBD: return "-";
                case 0xBB: return "+";
                case 0xC0: return "~";
                case 0x6A: return "*";
                case 0x6B: return "+";
                case 0x6D: return "-";
                case 0x6E: return ".";
                case 0x6F: return "/";
                default: return string.Empty;
            }
        }

        #region Native Zero-Dependency JSON Serialization

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\t", "\t")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
        }

        public static string SerializeJson(List<ComfortShortcutItem> list)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Id\": \"{Escape(item.Id)}\",");
                sb.AppendLine($"    \"Category\": \"{Escape(item.Category)}\",");
                sb.AppendLine($"    \"KeyCombination\": \"{Escape(item.KeyCombination)}\",");
                sb.AppendLine($"    \"ActionType\": \"{item.ActionType}\",");
                sb.AppendLine($"    \"ActiveScope\": \"{Escape(item.ActiveScope)}\",");
                sb.AppendLine($"    \"SoundPath\": \"{Escape(item.SoundPath)}\",");
                sb.AppendLine($"    \"Label\": \"{Escape(item.Label)}\",");
                sb.AppendLine($"    \"LastChanged\": \"{item.LastChanged:o}\",");
                sb.AppendLine($"    \"ProgramPaths\": \"{Escape(item.ProgramPaths)}\",");
                sb.AppendLine($"    \"StartInFolder\": \"{Escape(item.StartInFolder)}\",");
                sb.AppendLine($"    \"SwitchToAlreadyLaunched\": {item.SwitchToAlreadyLaunched.ToString().ToLower()},");
                sb.AppendLine($"    \"Urls\": \"{Escape(item.Urls)}\",");
                sb.AppendLine($"    \"UrlOpenType\": \"{Escape(item.UrlOpenType)}\",");
                sb.AppendLine($"    \"PasteText\": \"{Escape(item.PasteText)}\",");
                sb.AppendLine($"    \"ShowTextOnKeyboard\": {item.ShowTextOnKeyboard.ToString().ToLower()},");
                sb.AppendLine($"    \"AudioAction\": \"{Escape(item.AudioAction)}\",");
                sb.AppendLine($"    \"AudioStepSize\": {item.AudioStepSize}");
                sb.Append("  }");
                if (i < list.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        public static List<ComfortShortcutItem> DeserializeJson(string json)
        {
            var result = new List<ComfortShortcutItem>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                // Tách từng object {...} ở mức cao nhất
                int depth = 0;
                int startIdx = -1;
                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '{')
                    {
                        if (depth == 1) startIdx = i;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 1 && startIdx >= 0)
                        {
                            string itemJson = json.Substring(startIdx, i - startIdx + 1);
                            var item = ParseSingleItem(itemJson);
                            if (item != null) result.Add(item);
                            startIdx = -1;
                        }
                    }
                    else if (c == '[')
                    {
                        depth++;
                    }
                    else if (c == ']')
                    {
                        depth--;
                    }
                }
            }
            catch { }

            return result;
        }

        private static ComfortShortcutItem ParseSingleItem(string objJson)
        {
            var item = new ComfortShortcutItem();
            var matches = Regex.Matches(objJson, "\"(\\w+)\"\\s*:\\s*(\"[^\"]*\"|true|false|\\d+|null)");
            foreach (Match m in matches)
            {
                string key = m.Groups[1].Value;
                string val = m.Groups[2].Value;
                if (val.StartsWith("\"") && val.EndsWith("\""))
                {
                    val = Unescape(val.Substring(1, val.Length - 2));
                }

                switch (key)
                {
                    case "Id": item.Id = val; break;
                    case "Category": item.Category = val; break;
                    case "KeyCombination": item.KeyCombination = val; break;
                    case "ActionType":
                        if (Enum.TryParse<ShortcutActionType>(val, out var at)) item.ActionType = at;
                        break;
                    case "ActiveScope": item.ActiveScope = val; break;
                    case "SoundPath": item.SoundPath = val; break;
                    case "Label": item.Label = val; break;
                    case "LastChanged":
                        if (DateTime.TryParse(val, out var dt)) item.LastChanged = dt;
                        break;
                    case "ProgramPaths": item.ProgramPaths = val; break;
                    case "StartInFolder": item.StartInFolder = val; break;
                    case "SwitchToAlreadyLaunched":
                        if (bool.TryParse(val, out var sw)) item.SwitchToAlreadyLaunched = sw;
                        break;
                    case "Urls": item.Urls = val; break;
                    case "UrlOpenType": item.UrlOpenType = val; break;
                    case "PasteText": item.PasteText = val; break;
                    case "ShowTextOnKeyboard":
                        if (bool.TryParse(val, out var stk)) item.ShowTextOnKeyboard = stk;
                        break;
                    case "AudioAction": item.AudioAction = val; break;
                    case "AudioStepSize":
                        if (int.TryParse(val, out var ass)) item.AudioStepSize = ass;
                        break;
                }
            }

            return item;
        }

        #endregion
    }
}
