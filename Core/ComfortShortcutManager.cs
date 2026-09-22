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
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        [DllImport("shell32.dll")]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        [DllImport("Powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool bHibernate, bool bForce, bool bWakeupEventsDisabled);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const int SW_RESTORE = 9;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x0008;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_CLOSE = 0xF060;
        private const uint SC_MONITORPOWER = 0xF170;
        private const uint SC_SCREENSAVE = 0xF140;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly HashSet<IntPtr> _hiddenBossWindows = new HashSet<IntPtr>();

        private string _currentProfile = "Default";
        public string CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (_currentProfile != value)
                {
                    _currentProfile = value;
                    Load();
                }
            }
        }

        public void SwitchProfile(string profileName)
        {
            CurrentProfile = profileName;
        }

        public string[] AvailableProfiles => new[] { "Default", "Work", "Gaming", "Office", "Dev" };

        private string _pendingChordPrefix = null;
        private int _chordPrefixTick = 0;

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

        public string GetShortcutsFilePath(string profile = null)
        {
            string prof = profile ?? _currentProfile;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "ModernKey");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (string.Equals(prof, "Default", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(dir, "comfort_shortcuts.json");
            return Path.Combine(dir, $"comfort_shortcuts_{prof.ToLower()}.json");
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
                            // Tự động nâng cấp Ctrl+Nm 1 nếu còn là Q-Dir cũ và tắt SwitchToAlreadyLaunched
                            if (item.KeyCombination == "Ctrl+Nm 1")
                            {
                                item.Label = "Mở File Explorer";
                                item.ProgramPaths = "explorer.exe";
                                item.StartInFolder = @"C:\";
                                item.SwitchToAlreadyLaunched = false;
                            }

                            // Xóa các key mặc định cho Block key (LeftWin) theo yêu cầu người dùng
                            if (item.ActionType == ShortcutActionType.BlockKey &&
                                (item.Label == "Chặn phím Start Menu (LeftWin)" || item.KeyCombination == "LeftWin"))
                            {
                                continue;
                            }

                            // Replace key or shortcut: xóa hết trừ nút Apps theo yêu cầu người dùng
                            if (item.ActionType == ShortcutActionType.ReplaceKey && item.KeyCombination != "Apps")
                            {
                                continue;
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
            bool hasAppsReplace = Shortcuts.Any(s => s.ActionType == ShortcutActionType.ReplaceKey && s.KeyCombination == "Apps");

            if (!hasAppsReplace)
            {
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
                Save();
            }
        }

        public void Save()
        {
            try
            {
                string path = GetShortcutsFilePath();
                if (File.Exists(path))
                {
                    try
                    {
                        string bak = path + ".bak";
                        File.Copy(path, bak, true);
                    }
                    catch { }
                }
                string json = SerializeJson(Shortcuts.ToList());
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch { }
        }

        public bool RestoreFromBackup()
        {
            try
            {
                string path = GetShortcutsFilePath();
                string bak = path + ".bak";
                if (File.Exists(bak))
                {
                    string json = File.ReadAllText(bak, Encoding.UTF8);
                    var list = DeserializeJson(json);
                    if (list != null && list.Count > 0)
                    {
                        Shortcuts.Clear();
                        foreach (var item in list) Shortcuts.Add(item);
                        Save();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public List<string> CheckConflicts(string combo, string excludeId = null)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(combo)) return list;

            var match = Shortcuts.FirstOrDefault(s => s.Id != excludeId &&
                                                      string.Equals(s.KeyCombination, combo, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                list.Add($"Trùng với phím tắt '{match.Label}' trong danh sách");
            }

            string upper = combo.Replace(" ", "").ToUpper();
            if (upper == "WIN+D") list.Add("Trùng phím tắt Windows: Show Desktop");
            else if (upper == "WIN+E") list.Add("Trùng phím tắt Windows: File Explorer");
            else if (upper == "WIN+R") list.Add("Trùng phím tắt Windows: Run");
            else if (upper == "WIN+L") list.Add("Trùng phím tắt Windows: Lock");
            else if (upper == "WIN+X") list.Add("Trùng phím tắt Windows: Quick Link Menu");
            else if (upper == "ALT+TAB") list.Add("Trùng phím tắt Windows: Chuyển cửa sổ");
            else if (upper == "ALT+F4") list.Add("Trùng phím tắt Windows: Đóng ứng dụng");
            else if (upper == "CTRL+SHIFT+ESC") list.Add("Trùng phím tắt Windows: Task Manager");
            else if (upper == "CTRL+C" || upper == "CTRL+V" || upper == "CTRL+X" || upper == "CTRL+Z" || upper == "CTRL+A")
            {
                list.Add($"Trùng phím thao tác soạn thảo ({combo})");
            }

            return list;
        }

        public static bool IsMatchingTargetApp(string targetApp)
        {
            if (string.IsNullOrWhiteSpace(targetApp)) return true;
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName;
                string exeName = string.Empty;
                try { exeName = Path.GetFileName(proc.MainModule?.FileName ?? ""); } catch { }

                string filter = targetApp.Trim();
                if (string.Equals(procName, filter, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(exeName, filter, StringComparison.OrdinalIgnoreCase) ||
                    procName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    exeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                if (sb.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        public static bool IsForegroundFullscreenGame()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return false;

                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 256);
                string className = sb.ToString();
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
                    return false;

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    int screenW = (int)SystemParameters.PrimaryScreenWidth;
                    int screenH = (int)SystemParameters.PrimaryScreenHeight;
                    if (w >= screenW && h >= screenH)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
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
                SwitchToAlreadyLaunched = false,
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
            // 4. Change text case (Gợi ý 14)
            Shortcuts.Add(new ComfortShortcutItem
            {
                Category = "Change text case",
                KeyCombination = "Win+Alt+U",
                ActionType = ShortcutActionType.ChangeCase,
                ActiveScope = "In all screen modes",
                Label = "Chuyển chữ hoa / thường (Change case)",
                ChangeCaseMode = "UPPERCASE",
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
                AudioStepSize = 5,
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
                AudioStepSize = 5,
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
                AudioStepSize = 5,
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

            // 7. Replace key or shortcut: Chỉ giữ lại nút Apps theo yêu cầu
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

            if (!match.IsEnabled) return false;
            if (!IsMatchingTargetApp(match.TargetApp)) return false;
            if (match.ActiveScope == "Disabled in full screen games" && IsForegroundFullscreenGame()) return false;

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

            return null;
        }

        public bool TryExecuteMatchingShortcut(uint vk, bool ctrl, bool alt, bool shift, bool win)
        {
            string combo = BuildCombinationString(vk, ctrl, alt, shift, win);
            if (string.IsNullOrEmpty(combo)) return false;

            // Xử lý tổ hợp phím 2 bước (Sequential Key Chords)
            if (!string.IsNullOrEmpty(_pendingChordPrefix))
            {
                if (Math.Abs(Environment.TickCount - _chordPrefixTick) < 2000)
                {
                    string fullChord = _pendingChordPrefix + ", " + combo;
                    var chordMatch = FindShortcut(fullChord);
                    _pendingChordPrefix = null;
                    if (chordMatch != null && chordMatch.IsEnabled && IsMatchingTargetApp(chordMatch.TargetApp))
                    {
                        Task.Run(() => ExecuteShortcutAction(chordMatch));
                        return true;
                    }
                }
                else
                {
                    _pendingChordPrefix = null;
                }
            }

            var match = FindShortcut(combo);
            if (match == null)
            {
                // Kiểm tra xem combo này có phải là tiền tố của bất kỳ shortcut nào có dạng "combo, ..." không
                bool isPrefix = Shortcuts.Any(s => s.IsEnabled && s.KeyCombination != null &&
                                                    s.KeyCombination.StartsWith(combo + ",", StringComparison.OrdinalIgnoreCase));
                if (isPrefix)
                {
                    _pendingChordPrefix = combo;
                    _chordPrefixTick = Environment.TickCount;
                    return true;
                }
                return false;
            }

            if (!match.IsEnabled) return false;
            if (!IsMatchingTargetApp(match.TargetApp)) return false;
            if (match.ActiveScope == "Disabled in full screen games" && IsForegroundFullscreenGame()) return false;

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

                case ShortcutActionType.MonitorControl:
                    ExecuteMonitorControl(item);
                    break;

                case ShortcutActionType.SystemAction:
                    ExecuteSystemAction(item);
                    break;

                case ShortcutActionType.MouseControl:
                    ExecuteMouseControl(item);
                    break;

                case ShortcutActionType.KeystrokeMacro:
                    ExecuteKeystrokeMacro(item);
                    break;

                case ShortcutActionType.ChangeCase:
                    ExecuteChangeCase(item);
                    break;
            }

            // Ghi nhận thống kê sử dụng (Gợi ý 5)
            try
            {
                item.TriggerCount++;
                item.LastUsed = DateTime.Now;
                Save();
            }
            catch { }
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

            // Xử lý thẻ <INPUT: Prompt Text> (Gợi ý 11)
            var inputRegex = new Regex(@"<INPUT:(.*?)>", RegexOptions.IgnoreCase);
            text = inputRegex.Replace(text, m =>
            {
                string prompt = m.Groups[1].Value.Trim();
                return QuickInputDialog.ShowInput(prompt);
            });

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

        private static int _lastPasteTick = 0;

        private void ExecutePasteText(ComfortShortcutItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.PasteText)) return;

            int currentTick = Environment.TickCount;
            if (Math.Abs(currentTick - _lastPasteTick) < 400)
            {
                return; // Chống lặp dán khi phím tắt bị giữ (key repeat)
            }
            _lastPasteTick = currentTick;

            string expanded = ExpandDynamicPasteTags(item.PasteText);

            // Dán văn bản thuần túy (Gợi ý 13)
            if (item.PasteAsPlainText)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            expanded = System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText);
                        }
                    }
                    catch { }
                });
            }

            // Kiểm tra thẻ định vị con trỏ <CURSOR> (Gợi ý 12)
            int cursorBackCount = 0;
            if (expanded.Contains("<CURSOR>"))
            {
                int cursorIdx = expanded.IndexOf("<CURSOR>");
                string afterCursor = expanded.Substring(cursorIdx + "<CURSOR>".Length);
                cursorBackCount = afterCursor.Length;
                expanded = expanded.Replace("<CURSOR>", "");
            }

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

            // Tự động lùi con trỏ về vị trí thẻ <CURSOR> (Gợi ý 12)
            if (cursorBackCount > 0)
            {
                Thread.Sleep(80);
                for (int i = 0; i < cursorBackCount; i++)
                {
                    keybd_event(0x25 /* Left Arrow */, 0, 0, IntPtr.Zero);
                    keybd_event(0x25, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
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
            int stepSize = item.AudioStepSize > 0 ? item.AudioStepSize : 5;

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
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return;

                string act = item.WindowAction ?? "Close window";
                switch (act)
                {
                    case "Close window":
                        SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
                        break;
                    case "Pin always on top (Toggle)":
                        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                        bool isTop = (exStyle & WS_EX_TOPMOST) != 0;
                        SetWindowPos(hWnd, isTop ? HWND_NOTOPMOST : HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                        break;
                    case "Hide window / Boss key (Toggle)":
                        lock (_hiddenBossWindows)
                        {
                            if (_hiddenBossWindows.Contains(hWnd))
                            {
                                ShowWindow(hWnd, SW_SHOW);
                                SetForegroundWindow(hWnd);
                                _hiddenBossWindows.Remove(hWnd);
                            }
                            else if (_hiddenBossWindows.Count > 0)
                            {
                                foreach (var h in _hiddenBossWindows.ToList())
                                {
                                    ShowWindow(h, SW_SHOW);
                                    SetForegroundWindow(h);
                                }
                                _hiddenBossWindows.Clear();
                            }
                            else
                            {
                                _hiddenBossWindows.Add(hWnd);
                                ShowWindow(hWnd, SW_HIDE);
                            }
                        }
                        break;
                    case "Minimize window":
                        ShowWindow(hWnd, SW_MINIMIZE);
                        break;
                    case "Set transparency":
                        int trans = item.WindowTransparency;
                        if (trans < 10) trans = 10;
                        if (trans > 100) trans = 100;
                        int style = GetWindowLong(hWnd, GWL_EXSTYLE);
                        SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
                        byte alpha = (byte)(255 * trans / 100);
                        SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
                        break;
                    case "Move to next monitor":
                        MoveWindowToNextMonitor(hWnd);
                        break;
                }
            }
            catch { }
        }

        private void ExecuteMonitorControl(ComfortShortcutItem item)
        {
            try
            {
                string act = item.MonitorAction ?? "Turn off monitor";
                switch (act)
                {
                    case "Turn off monitor":
                        SendMessage((IntPtr)0xFFFF, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
                        break;
                    case "Start screensaver":
                        SendMessage((IntPtr)0xFFFF, WM_SYSCOMMAND, (IntPtr)SC_SCREENSAVE, IntPtr.Zero);
                        break;
                    case "Lock workstation":
                        LockWorkStation();
                        break;
                }
            }
            catch { }
        }

        private void ExecuteSystemAction(ComfortShortcutItem item)
        {
            try
            {
                string act = item.SystemActionType ?? "Lock workstation";
                switch (act)
                {
                    case "Lock workstation":
                        LockWorkStation();
                        break;
                    case "Sleep":
                        SetSuspendState(false, true, false);
                        break;
                    case "Hibernate":
                        SetSuspendState(true, true, false);
                        break;
                    case "Restart computer":
                        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
                        break;
                    case "Shutdown computer":
                        Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
                        break;
                    case "Empty Recycle Bin":
                        SHEmptyRecycleBin(IntPtr.Zero, null, 7);
                        break;
                    case "Capture Screen (Snipping Tool)":
                        try
                        {
                            Process.Start("ms-screenclip:");
                        }
                        catch
                        {
                            Process.Start("snippingtool.exe");
                        }
                        break;
                }
            }
            catch { }
        }

        private void ExecuteMouseControl(ComfortShortcutItem item)
        {
            try
            {
                string act = item.MouseAction ?? "Left click";
                switch (act)
                {
                    case "Left click":
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                        break;
                    case "Right click":
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
                        break;
                    case "Middle click":
                        mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, IntPtr.Zero);
                        Thread.Sleep(20);
                        mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, IntPtr.Zero);
                        break;
                    case "Double click":
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                        Thread.Sleep(50);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                        break;
                    case "Wheel up":
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, IntPtr.Zero);
                        break;
                    case "Wheel down":
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120, IntPtr.Zero);
                        break;
                }
            }
            catch { }
        }

        private void ExecuteKeystrokeMacro(ComfortShortcutItem item)
        {
            if (string.IsNullOrWhiteSpace(item.MacroKeystrokes)) return;
            string macro = item.MacroKeystrokes;

            string[] lines = macro.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool isJitbitScript = false;
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.StartsWith("MOUSE_", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("KEY_", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("DELAY :", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("TEXT :", StringComparison.OrdinalIgnoreCase))
                {
                    isJitbitScript = true;
                    break;
                }
            }

            if (isJitbitScript)
            {
                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//")) continue;

                    string[] parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                    if (parts.Length == 0) continue;

                    string cmd = parts[0].ToUpperInvariant();
                    try
                    {
                        switch (cmd)
                        {
                            case "DELAY":
                                if (parts.Length >= 2 && int.TryParse(parts[1], out int ms) && ms > 0)
                                {
                                    Thread.Sleep(Math.Min(ms, 10000));
                                }
                                break;

                            case "MOUSE_CLICK":
                                if (parts.Length >= 4 && int.TryParse(parts[2], out int mcX) && int.TryParse(parts[3], out int mcY))
                                {
                                    SetCursorPos(mcX, mcY);
                                    Thread.Sleep(10);
                                    string btn = parts[1].ToUpperInvariant();
                                    if (btn == "RIGHT")
                                    {
                                        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                        Thread.Sleep(15);
                                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    }
                                    else if (btn == "MIDDLE")
                                    {
                                        mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                        Thread.Sleep(15);
                                        mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    }
                                    else
                                    {
                                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                        Thread.Sleep(15);
                                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    }
                                }
                                break;

                            case "MOUSE_DBLCLICK":
                                if (parts.Length >= 4 && int.TryParse(parts[2], out int mdX) && int.TryParse(parts[3], out int mdY))
                                {
                                    SetCursorPos(mdX, mdY);
                                    Thread.Sleep(10);
                                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    Thread.Sleep(45);
                                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                }
                                break;

                            case "MOUSE_MOVE":
                                if (parts.Length >= 3 && int.TryParse(parts[1], out int mmX) && int.TryParse(parts[2], out int mmY))
                                {
                                    SetCursorPos(mmX, mmY);
                                }
                                break;

                            case "MOUSE_WHEEL":
                                if (parts.Length >= 4 && int.TryParse(parts[1], out int wheelDelta) &&
                                    int.TryParse(parts[2], out int mwX) && int.TryParse(parts[3], out int mwY))
                                {
                                    SetCursorPos(mwX, mwY);
                                    Thread.Sleep(10);
                                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheelDelta, KeySender.INJECTED_SIGNATURE);
                                }
                                break;

                            case "MOUSE_DOWN":
                                if (parts.Length >= 4 && int.TryParse(parts[2], out int mdnX) && int.TryParse(parts[3], out int mdnY))
                                {
                                    SetCursorPos(mdnX, mdnY);
                                    Thread.Sleep(10);
                                    string btn = parts[1].ToUpperInvariant();
                                    if (btn == "RIGHT") mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    else if (btn == "MIDDLE") mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    else mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                }
                                break;

                            case "MOUSE_UP":
                                if (parts.Length >= 4 && int.TryParse(parts[2], out int mupX) && int.TryParse(parts[3], out int mupY))
                                {
                                    SetCursorPos(mupX, mupY);
                                    Thread.Sleep(10);
                                    string btn = parts[1].ToUpperInvariant();
                                    if (btn == "RIGHT") mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    else if (btn == "MIDDLE") mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                    else mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, KeySender.INJECTED_SIGNATURE);
                                }
                                break;

                            case "KEY_COMBINATION":
                            case "KEY_PRESS":
                                if (parts.Length >= 2)
                                {
                                    string keyParam = parts[1];
                                    if (keyParam.Contains("+"))
                                    {
                                        string[] subKeys = keyParam.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                                        var modVks = new List<uint>();
                                        uint mainVk = 0;

                                        foreach (var sk in subKeys)
                                        {
                                            uint vk = GetVkFromFriendlyName(sk);
                                            if (vk == 0) continue;

                                            if (vk == 0x11 || vk == 0x10 || vk == 0x12 || vk == 0x5B ||
                                                vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3 || vk == 0xA4 || vk == 0xA5)
                                            {
                                                modVks.Add(vk);
                                            }
                                            else
                                            {
                                                mainVk = vk;
                                            }
                                        }

                                        // 1. Nhấn giữ các modifier
                                        foreach (var m in modVks) SendSingleKeyEvent(m, false);
                                        Thread.Sleep(15);

                                        // 2. Nhấn và nhả phím chính
                                        if (mainVk > 0)
                                        {
                                            SendSingleKeyEvent(mainVk, false);
                                            Thread.Sleep(20);
                                            SendSingleKeyEvent(mainVk, true);
                                        }

                                        // 3. Nhả các modifier
                                        Thread.Sleep(15);
                                        for (int i = modVks.Count - 1; i >= 0; i--)
                                        {
                                            SendSingleKeyEvent(modVks[i], true);
                                        }
                                    }
                                    else
                                    {
                                        uint vk = GetVkFromFriendlyName(keyParam);
                                        if (vk > 0)
                                        {
                                            SendSingleKeyEvent(vk, false);
                                            Thread.Sleep(15);
                                            SendSingleKeyEvent(vk, true);
                                        }
                                    }
                                }
                                break;

                            case "KEY_DOWN":
                                if (parts.Length >= 2)
                                {
                                    uint vk = GetVkFromFriendlyName(parts[1]);
                                    if (vk > 0) SendSingleKeyEvent(vk, false);
                                }
                                break;

                            case "KEY_UP":
                                if (parts.Length >= 2)
                                {
                                    uint vk = GetVkFromFriendlyName(parts[1]);
                                    if (vk > 0) SendSingleKeyEvent(vk, true);
                                }
                                break;

                            case "TEXT":
                                if (parts.Length >= 2)
                                {
                                    int colonIdx = rawLine.IndexOf(':');
                                    string textContent = colonIdx >= 0 ? rawLine.Substring(colonIdx + 1).Trim() : string.Empty;
                                    if (!string.IsNullOrEmpty(textContent))
                                    {
                                        KeySender.SendUnicodeString(textContent);
                                    }
                                }
                                break;
                        }
                    }
                    catch { }
                }
                return;
            }

            // Fallback: Xử lý cú pháp token cổ điển ({DELAY:...}, {KEY:...}, text tự do)
            var tokens = Regex.Split(macro, @"(\{[A-Za-z0-9_:+]+?\})");
            foreach (var tok in tokens)
            {
                if (string.IsNullOrEmpty(tok)) continue;
                if (tok.StartsWith("{DELAY:", StringComparison.OrdinalIgnoreCase) && tok.EndsWith("}"))
                {
                    string msStr = tok.Substring(7, tok.Length - 8);
                    if (int.TryParse(msStr, out int ms) && ms > 0)
                    {
                        Thread.Sleep(Math.Min(ms, 5000));
                    }
                }
                else if (tok.StartsWith("{KEY:", StringComparison.OrdinalIgnoreCase) && tok.EndsWith("}"))
                {
                    string keyName = tok.Substring(5, tok.Length - 6);
                    uint vk = GetVkFromFriendlyName(keyName);
                    if (vk > 0)
                    {
                        SendSingleKeyEvent(vk, false);
                        Thread.Sleep(20);
                        SendSingleKeyEvent(vk, true);
                    }
                }
                else
                {
                    KeySender.SendViaClipboardPaste(tok);
                }
            }
        }

        private void ExecuteChangeCase(ComfortShortcutItem item)
        {
            try
            {
                string mode = (item.ChangeCaseMode ?? "UPPERCASE").Trim().ToLowerInvariant();

                KeySender.CutTransformAndPaste(cutText =>
                {
                    if (string.IsNullOrEmpty(cutText)) return cutText;

                    if (mode.Contains("upper"))
                    {
                        return cutText.ToUpper();
                    }
                    else if (mode.Contains("lower"))
                    {
                        return cutText.ToLower();
                    }
                    else if (mode.Contains("title"))
                    {
                        try
                        {
                            return new System.Globalization.CultureInfo("vi-VN").TextInfo.ToTitleCase(cutText.ToLower());
                        }
                        catch
                        {
                            try
                            {
                                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cutText.ToLower());
                            }
                            catch
                            {
                                return cutText;
                            }
                        }
                    }
                    else if (mode.Contains("sentence"))
                    {
                        var parts = Regex.Split(cutText, @"(?<=[.!?\r\n]+\s*)");
                        var sb = new StringBuilder();
                        foreach (var p in parts)
                        {
                            if (p.Length > 0)
                            {
                                string trimmed = p.TrimStart();
                                int leadSpaces = p.Length - trimmed.Length;
                                string prefix = p.Substring(0, leadSpaces);
                                if (trimmed.Length > 0)
                                {
                                    sb.Append(prefix + char.ToUpper(trimmed[0]) + (trimmed.Length > 1 ? trimmed.Substring(1).ToLower() : ""));
                                }
                                else sb.Append(p);
                            }
                        }
                        return sb.ToString();
                    }
                    else if (mode.Contains("toggle") || mode.Contains("invert"))
                    {
                        var sb = new StringBuilder(cutText.Length);
                        foreach (char c in cutText)
                        {
                            if (char.IsUpper(c)) sb.Append(char.ToLower(c));
                            else if (char.IsLower(c)) sb.Append(char.ToUpper(c));
                            else sb.Append(c);
                        }
                        return sb.ToString();
                    }
                    else if (mode.Contains("cycle") || mode.Contains("rotate"))
                    {
                        // Chế độ xoay vòng chuẩn OpenKey C++: lower -> UPPER -> Title Case -> lower
                        bool isAllLower = true;
                        bool isAllUpper = true;
                        bool hasLetter = false;
                        foreach (char c in cutText)
                        {
                            if (char.IsLetter(c))
                            {
                                hasLetter = true;
                                if (char.IsLower(c)) isAllUpper = false;
                                if (char.IsUpper(c)) isAllLower = false;
                            }
                        }

                        if (!hasLetter) return cutText;

                        if (isAllLower)
                        {
                            return cutText.ToUpper();
                        }
                        else if (isAllUpper)
                        {
                            try
                            {
                                return new System.Globalization.CultureInfo("vi-VN").TextInfo.ToTitleCase(cutText.ToLower());
                            }
                            catch
                            {
                                return cutText.ToLower();
                            }
                        }
                        else
                        {
                            return cutText.ToLower();
                        }
                    }

                    return cutText;
                });
            }
            catch { }
        }

        private static void MoveWindowToNextMonitor(IntPtr hWnd)
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length <= 1) return;

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    var curScreen = System.Windows.Forms.Screen.FromHandle(hWnd);
                    int curIdx = Array.IndexOf(screens, curScreen);
                    int nextIdx = (curIdx + 1) % screens.Length;
                    var nextScreen = screens[nextIdx];

                    int newX = nextScreen.Bounds.Left + (rect.Left - curScreen.Bounds.Left);
                    int newY = nextScreen.Bounds.Top + (rect.Top - curScreen.Bounds.Top);

                    SetWindowPos(hWnd, IntPtr.Zero, newX, newY, w, h, SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        public static uint GetVkFromFriendlyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            name = name.Trim();
            if (name.Length == 1)
            {
                char c = char.ToUpper(name[0]);
                if (c >= '0' && c <= '9') return (uint)c;
                if (c >= 'A' && c <= 'Z') return (uint)c;
            }
            if (name.StartsWith("F") && int.TryParse(name.Substring(1), out int f) && f >= 1 && f <= 12)
            {
                return (uint)(0x6F + f);
            }
            if ((name.StartsWith("Nm ") || name.StartsWith("Num ")) && int.TryParse(name.Substring(name.IndexOf(' ') + 1), out int n) && n >= 0 && n <= 9)
            {
                return (uint)(0x60 + n);
            }
            switch (name.ToUpper())
            {
                case "ENTER": return 0x0D;
                case "BACKSPACE": case "BACK": return 0x08;
                case "TAB": return 0x09;
                case "SPACE": return 0x20;
                case "ESC": return 0x1B;
                case "CAPS": return 0x14;
                case "DEL": case "DELETE": return 0x2E;
                case "INS": case "INSERT": return 0x2D;
                case "HOME": return 0x24;
                case "END": return 0x23;
                case "PGUP": return 0x21;
                case "PGDN": return 0x22;
                case "UP": return 0x26;
                case "DOWN": return 0x28;
                case "LEFT": return 0x25;
                case "RIGHT": return 0x27;
                case "PRTSC": return 0x2C;
                case "PAUSE": case "BREAK": return 0x13;
                case "APPS": case "MENU": return 0x5D;
                case "LEFTWIN": case "WIN": return 0x5B;
                case "RIGHTWIN": return 0x5C;
                case "CTRL": case "LEFTCTRL": return 0x11;
                case "RIGHTCTRL": return 0xA3;
                case "ALT": case "LEFTALT": return 0x12;
                case "RIGHTALT": return 0xA5;
                case "SHIFT": case "LEFTSHIFT": return 0x10;
                case "RIGHTSHIFT": return 0xA1;
                default: return 0;
            }
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
                sb.AppendLine($"    \"PasteAsPlainText\": {item.PasteAsPlainText.ToString().ToLower()},");
                sb.AppendLine($"    \"IsEnabled\": {item.IsEnabled.ToString().ToLower()},");
                sb.AppendLine($"    \"TriggerCount\": {item.TriggerCount},");
                if (item.LastUsed.HasValue)
                    sb.AppendLine($"    \"LastUsed\": \"{item.LastUsed.Value:o}\",");
                else
                    sb.AppendLine("    \"LastUsed\": null,");
                sb.AppendLine($"    \"TargetApp\": \"{Escape(item.TargetApp)}\",");
                sb.AppendLine($"    \"AudioAction\": \"{Escape(item.AudioAction)}\",");
                sb.AppendLine($"    \"AudioStepSize\": {item.AudioStepSize},");
                sb.AppendLine($"    \"ReplaceWithKey\": \"{Escape(item.ReplaceWithKey)}\",");
                sb.AppendLine($"    \"ReplaceShift\": {item.ReplaceShift.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceCtrl\": {item.ReplaceCtrl.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceAlt\": {item.ReplaceAlt.ToString().ToLower()},");
                sb.AppendLine($"    \"ReplaceWin\": {item.ReplaceWin.ToString().ToLower()},");
                sb.AppendLine($"    \"WindowAction\": \"{Escape(item.WindowAction)}\",");
                sb.AppendLine($"    \"WindowTransparency\": {item.WindowTransparency},");
                sb.AppendLine($"    \"MonitorAction\": \"{Escape(item.MonitorAction)}\",");
                sb.AppendLine($"    \"SystemActionType\": \"{Escape(item.SystemActionType)}\",");
                sb.AppendLine($"    \"MouseAction\": \"{Escape(item.MouseAction)}\",");
                sb.AppendLine($"    \"MacroKeystrokes\": \"{Escape(item.MacroKeystrokes)}\",");
                sb.AppendLine($"    \"ChangeCaseMode\": \"{Escape(item.ChangeCaseMode)}\"");
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
                    case "PasteAsPlainText":
                        if (bool.TryParse(val, out var pap)) item.PasteAsPlainText = pap;
                        break;
                    case "IsEnabled":
                        if (bool.TryParse(val, out var enb)) item.IsEnabled = enb;
                        break;
                    case "TriggerCount":
                        if (int.TryParse(val, out var tc)) item.TriggerCount = tc;
                        break;
                    case "LastUsed":
                        if (DateTime.TryParse(val, out var lu)) item.LastUsed = lu;
                        break;
                    case "TargetApp": item.TargetApp = val; break;
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
                    case "WindowAction": item.WindowAction = val; break;
                    case "WindowTransparency":
                        if (int.TryParse(val, out var wt)) item.WindowTransparency = wt;
                        break;
                    case "MonitorAction": item.MonitorAction = val; break;
                    case "SystemActionType": item.SystemActionType = val; break;
                    case "MouseAction": item.MouseAction = val; break;
                    case "MacroKeystrokes": item.MacroKeystrokes = val; break;
                    case "ChangeCaseMode": item.ChangeCaseMode = val; break;
                }
            }

            return item;
        }

        #endregion
    }
}
