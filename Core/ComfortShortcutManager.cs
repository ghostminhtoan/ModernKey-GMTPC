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
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const int SW_RESTORE = 9;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static bool IsExtendedKey(uint vk)
        {
            return vk == 0x5B || vk == 0x5C || vk == 0x5D || // LeftWin, RightWin, Apps
                   vk == 0x2C || // PrtSc / Print
                   vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24 || // PgUp, PgDn, End, Home
                   vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 || // Left, Up, Right, Down
                   vk == 0x2D || vk == 0x2E || // Ins, Del
                   vk == 0x6F || // Num /
                   vk == 0x90;   // NumLock
        }

        public static void SendSingleKeyEvent(uint vk, bool isKeyUp)
        {
            uint flags = 0;
            if (IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
            if (isKeyUp) flags |= KEYEVENTF_KEYUP;
            byte scan = (byte)MapVirtualKey(vk, 0);
            keybd_event((byte)vk, scan, flags, KeySender.INJECTED_SIGNATURE);
        }

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
                            // Tự động nâng cấp Ctrl+Nm 1 nếu còn là Q-Dir cũ
                            if (item.KeyCombination == "Ctrl+Nm 1" && !string.IsNullOrEmpty(item.ProgramPaths) && item.ProgramPaths.Contains("Q-Dir"))
                            {
                                item.Label = "Mở File Explorer";
                                item.ProgramPaths = "explorer.exe";
                                item.StartInFolder = @"C:\";
                            }
                            Shortcuts.Add(item);
                        }
                        EnsureBlockAndReplacePresets();
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

        private void EnsureBlockAndReplacePresets()
        {
            bool hasBlock = Shortcuts.Any(s => s.ActionType == ShortcutActionType.BlockKey);
            bool hasReplace = Shortcuts.Any(s => s.ActionType == ShortcutActionType.ReplaceKey);

            if (!hasBlock)
            {
                Shortcuts.Add(new ComfortShortcutItem
                {
                    Category = "Block key or shortcut",
                    KeyCombination = "LeftWin",
                    ActionType = ShortcutActionType.BlockKey,
                    ActiveScope = "In all screen modes",
                    Label = "Chặn phím Start Menu (LeftWin)",
                    LastChanged = DateTime.Now
                });
            }

            if (!hasReplace)
            {
                Shortcuts.Add(new ComfortShortcutItem
                {
                    Category = "Replace key or shortcut",
                    KeyCombination = "Pause",
                    ActionType = ShortcutActionType.ReplaceKey,
                    ReplaceWithKey = "13 - Pause",
                    ActiveScope = "In all screen modes",
                    Label = "Thay thế phím Pause",
                    LastChanged = DateTime.Now
                });

                Shortcuts.Add(new ComfortShortcutItem
                {
                    Category = "Replace key or shortcut",
                    KeyCombination = "Apps",
                    ActionType = ShortcutActionType.ReplaceKey,
                    ReplaceWithKey = "5B - Win",
                    ActiveScope = "In all screen modes",
                    Label = "Thay phím Apps thành phím Win",
                    LastChanged = DateTime.Now
                });

                Shortcuts.Add(new ComfortShortcutItem
                {
                    Category = "Replace key or shortcut",
                    KeyCombination = "RightCtrl+Break",
                    ActionType = ShortcutActionType.ReplaceKey,
                    ReplaceWithKey = "13 - Pause",
                    ActiveScope = "In all screen modes",
                    Label = "RightCtrl+Break -> Pause",
                    LastChanged = DateTime.Now
                });

                Shortcuts.Add(new ComfortShortcutItem
                {
                    Category = "Replace key or shortcut",
                    KeyCombination = "RightCtrl+RightShift+Break",
                    ActionType = ShortcutActionType.ReplaceKey,
                    ReplaceWithKey = "13 - Pause",
                    ActiveScope = "In all screen modes",
                    Label = "RightCtrl+RightShift+Break -> Pause",
                    LastChanged = DateTime.Now
                });
            }

            if (!hasBlock || !hasReplace)
            {
                Save();
            }
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

        public void RestoreDefaults()
        {
            Shortcuts.Clear();
            LoadDefaultPresets();
            Save();
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
                Label = "Mở File Explorer",
                ProgramPaths = "explorer.exe",
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

            // 7. Block key or shortcut (Theo Comfort Keys Pro)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Block key or shortcut",
                KeyCombination = "LeftWin",
                ActionType = ShortcutActionType.BlockKey,
                ActiveScope = "In all screen modes",
                Label = "Chặn phím Start Menu (LeftWin)",
                LastChanged = DateTime.Now
            });

            // 8. Replace key or shortcut (Theo Comfort Keys Pro)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Replace key or shortcut",
                KeyCombination = "Pause",
                ActionType = ShortcutActionType.ReplaceKey,
                ReplaceWithKey = "13 - Pause",
                ActiveScope = "In all screen modes",
                Label = "Thay thế phím Pause",
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Replace key or shortcut",
                KeyCombination = "Apps",
                ActionType = ShortcutActionType.ReplaceKey,
                ReplaceWithKey = "5B - Win",
                ActiveScope = "In all screen modes",
                Label = "Thay phím Apps thành phím Win",
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Replace key or shortcut",
                KeyCombination = "RightCtrl+Break",
                ActionType = ShortcutActionType.ReplaceKey,
                ReplaceWithKey = "13 - Pause",
                ActiveScope = "In all screen modes",
                Label = "RightCtrl+Break -> Pause",
                LastChanged = DateTime.Now
            });

            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Replace key or shortcut",
                KeyCombination = "RightCtrl+RightShift+Break",
                ActionType = ShortcutActionType.ReplaceKey,
                ReplaceWithKey = "13 - Pause",
                ActiveScope = "In all screen modes",
                Label = "RightCtrl+RightShift+Break -> Pause",
                LastChanged = DateTime.Now
            });
        }

        public class ActiveKeyReplacement
        {
            public uint TargetVk { get; set; }
            public bool Shift { get; set; }
            public bool Ctrl { get; set; }
            public bool Alt { get; set; }
            public bool Win { get; set; }
        }

        private readonly object _lockObj = new object();
        private readonly HashSet<uint> _currentlyBlockedKeys = new HashSet<uint>();
        private readonly Dictionary<uint, ActiveKeyReplacement> _activeReplacements = new Dictionary<uint, ActiveKeyReplacement>();

        public bool TryHandleBlockOrReplaceKeyDown(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            string combo = BuildCombinationString(vk, ctrl, alt, shift, win);
            if (string.IsNullOrEmpty(combo)) return false;

            var match = FindShortcut(combo);
            if (match == null) return false;

            if (match.ActionType == ShortcutActionType.BlockKey)
            {
                lock (_lockObj)
                {
                    _currentlyBlockedKeys.Add(vk);
                }
                return true;
            }

            if (match.ActionType == ShortcutActionType.ReplaceKey)
            {
                uint targetVk = match.GetReplaceTargetVk();
                if (targetVk > 0)
                {
                    var act = new ActiveKeyReplacement
                    {
                        TargetVk = targetVk,
                        Shift = match.ReplaceShift,
                        Ctrl = match.ReplaceCtrl,
                        Alt = match.ReplaceAlt,
                        Win = match.ReplaceWin
                    };

                    lock (_lockObj)
                    {
                        _activeReplacements[vk] = act;
                    }

                    // Bơm các modifier tương ứng trước nếu có chọn (+)
                    if (act.Ctrl) SendSingleKeyEvent(0x11, false);
                    if (act.Alt) SendSingleKeyEvent(0x12, false);
                    if (act.Shift) SendSingleKeyEvent(0x10, false);
                    if (act.Win) SendSingleKeyEvent(0x5B, false);

                    // Bơm phím thay thế đích Down với đầy đủ scan code và Extended flag
                    SendSingleKeyEvent(targetVk, false);
                    return true;
                }
            }

            return false;
        }

        public bool TryHandleBlockOrReplaceKeyUp(uint vk)
        {
            ActiveKeyReplacement act = null;

            lock (_lockObj)
            {
                if (_activeReplacements.TryGetValue(vk, out act))
                {
                    _activeReplacements.Remove(vk);
                }
                else if (_currentlyBlockedKeys.Contains(vk))
                {
                    _currentlyBlockedKeys.Remove(vk);
                    return true;
                }
            }

            if (act != null && act.TargetVk > 0)
            {
                // Nhả phím thay thế Up
                SendSingleKeyEvent(act.TargetVk, true);

                // Nhả các modifier theo thứ tự đảo ngược
                if (act.Win) SendSingleKeyEvent(0x5B, true);
                if (act.Shift) SendSingleKeyEvent(0x10, true);
                if (act.Alt) SendSingleKeyEvent(0x12, true);
                if (act.Ctrl) SendSingleKeyEvent(0x11, true);

                return true;
            }

            string keyName = GetKeyFriendlyName(vk);
            if (!string.IsNullOrEmpty(keyName))
            {
                var blockMatch = Shortcuts.FirstOrDefault(s => s.ActionType == ShortcutActionType.BlockKey &&
                                                              string.Equals(s.KeyCombination, keyName, StringComparison.OrdinalIgnoreCase));
                if (blockMatch != null) return true;
            }

            return false;
        }

        public ComfortShortcutItem FindShortcut(string combo)
        {
            if (string.IsNullOrEmpty(combo)) return null;

            var match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, combo, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // Thử các biến thể tương thích (Num <-> Nm, Menu <-> Apps, Pause <-> Break)
            string altCombo = combo.Replace("Num ", "Nm ").Replace("Numpad ", "Nm ");
            match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, altCombo, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            if (combo.Contains("Break"))
            {
                string pauseCombo = combo.Replace("Break", "Pause");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, pauseCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            if (combo.Contains("Pause"))
            {
                string breakCombo = combo.Replace("Pause", "Break");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, breakCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            if (combo.Contains("Apps"))
            {
                string menuCombo = combo.Replace("Apps", "Menu");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, menuCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            if (combo.Contains("Menu"))
            {
                string appsCombo = combo.Replace("Menu", "Apps");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, appsCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            if (combo.Contains("PrintScreen") || combo.Contains("Print"))
            {
                string prtCombo = combo.Replace("PrintScreen", "PrtSc").Replace("Print", "PrtSc");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, prtCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            if (combo.Contains("PrtSc"))
            {
                string printCombo = combo.Replace("PrtSc", "Print");
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, printCombo, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // Hỗ trợ bàn phím Thinkpad / laptop: Phím PrtSc (Print) nằm ở vị trí phím Apps (giữa RightAlt và RightCtrl)
            if (combo.Equals("PrtSc", StringComparison.OrdinalIgnoreCase) || combo.Equals("Print", StringComparison.OrdinalIgnoreCase))
            {
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, "Apps", StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(s.KeyCombination, "Menu", StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            else if (combo.Equals("Apps", StringComparison.OrdinalIgnoreCase) || combo.Equals("Menu", StringComparison.OrdinalIgnoreCase))
            {
                match = Shortcuts.FirstOrDefault(s => string.Equals(s.KeyCombination, "PrtSc", StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(s.KeyCombination, "Print", StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return null;
        }

        public bool TryExecuteMatchingShortcut(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            string combo = BuildCombinationString(vk, ctrl, alt, shift, win);
            if (string.IsNullOrEmpty(combo)) return false;

            var match = FindShortcut(combo);
            if (match == null) return false;

            // Nếu là BlockKey hoặc ReplaceKey thì đã được xử lý ở TryHandleBlockOrReplaceKeyDown
            if (match.ActionType == ShortcutActionType.BlockKey || match.ActionType == ShortcutActionType.ReplaceKey)
            {
                return true;
            }

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
                        keybd_event(0x09, 0, 0, IntPtr.Zero);
                        keybd_event(0x09, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    }
                    else if (part == "<KEY_ENTER>")
                    {
                        keybd_event(0x0D, 0, 0, IntPtr.Zero);
                        keybd_event(0x0D, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    }
                    else if (part == "<KEY_CTRL_ENTER>")
                    {
                        keybd_event(0x11, 0, 0, IntPtr.Zero);
                        keybd_event(0x0D, 0, 0, IntPtr.Zero);
                        keybd_event(0x0D, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                        keybd_event(0x11, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    }
                    else if (part == "<KEY_BACKSPACE>")
                    {
                        keybd_event(0x08, 0, 0, IntPtr.Zero);
                        keybd_event(0x08, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    }
                    else if (part == "<KEY_DEL>")
                    {
                        keybd_event(0x2E, 0, 0, IntPtr.Zero);
                        keybd_event(0x2E, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                    }
                    else if (part == "<KEY_SPACE>")
                    {
                        keybd_event(0x20, 0, 0, IntPtr.Zero);
                        keybd_event(0x20, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
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
                ThreadPool.QueueUserWorkItem(_ => AdjustVolume(stepSize));
            }
            else if (item.AudioAction == "Volume down")
            {
                ThreadPool.QueueUserWorkItem(_ => AdjustVolume(-stepSize));
            }
            else if (item.AudioAction == "Volume on/off")
            {
                ThreadPool.QueueUserWorkItem(_ => ToggleMute());
            }
            else if (item.AudioAction == "Eject/Close CD door")
            {
                ThreadPool.QueueUserWorkItem(_ => mciSendString("set cdaudio door open", null, 0, IntPtr.Zero));
            }
        }

        private void ExecuteWindowControl(ComfortShortcutItem item)
        {
            // Close window: Alt + F4
            keybd_event(0x12, 0, 0, IntPtr.Zero); // Alt Down
            keybd_event(0x73, 0, 0, IntPtr.Zero); // F4 Down
            keybd_event(0x73, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        private string BuildCombinationString(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new List<string>();

            bool isVkWin = (vk == 0x5B || vk == 0x5C);
            bool isVkCtrl = (vk == 0x11 || vk == 0xA2 || vk == 0xA3);
            bool isVkAlt = (vk == 0x12 || vk == 0xA4 || vk == 0xA5);
            bool isVkShift = (vk == 0x10 || vk == 0xA0 || vk == 0xA1);

            if (win && !isVkWin) parts.Add("Win");
            if (ctrl && !isVkCtrl) parts.Add("Ctrl");
            if (alt && !isVkAlt) parts.Add("Alt");
            if (shift && !isVkShift) parts.Add("Shift");

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
                case 0x5B: return "LeftWin";
                case 0x5C: return "RightWin";
                case 0x5D: return "Apps";
                case 0xA0: return "LeftShift";
                case 0xA1: return "RightShift";
                case 0xA2: return "LeftCtrl";
                case 0xA3: return "RightCtrl";
                case 0xA4: return "LeftAlt";
                case 0xA5: return "RightAlt";
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
                sb.AppendLine($"    \"AudioStepSize\": {item.AudioStepSize},");
                sb.AppendLine($"    \"ReplaceWithKey\": \"{Escape(item.ReplaceWithKey)}\",");
                sb.AppendLine($"    \"ReplaceShift\": {item.ReplaceShift.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceCtrl\": {item.ReplaceCtrl.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceAlt\": {item.ReplaceAlt.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceWin\": {item.ReplaceWin.ToString().ToLower()}");
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
                    case "ReplaceWithKey": item.ReplaceWithKey = val; break;
                    case "ReplaceShift":
                        if (bool.TryParse(val, out var rsh)) item.ReplaceShift = rsh;
                        break;
                    case "ReplaceCtrl":
                        if (bool.TryParse(val, out var rct)) item.ReplaceCtrl = rct;
                        break;
                    case "ReplaceAlt":
                        if (bool.TryParse(val, out var ral)) item.ReplaceAlt = ral;
                        break;
                    case "ReplaceWin":
                        if (bool.TryParse(val, out var rwn)) item.ReplaceWin = rwn;
                        break;
                }
            }

            return item;
        }

        #endregion
    }
}
