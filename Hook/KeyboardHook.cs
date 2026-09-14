using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ModernKey.Config;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey.Hook
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private readonly VietnameseEngine _engine;
        private readonly AppSettings _settings;

        private HookProc _keyboardProc;
        private HookProc _mouseProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        private static bool _isInjecting = false;

        // Mặt nạ phím Modifier chuẩn OpenKey C++ (Bitmask)
        private const int MASK_CTRL = 0x01;
        private const int MASK_SHIFT = 0x02;
        private const int MASK_ALT = 0x04;
        private const int MASK_WIN = 0x08;

        private int _modifierFlag = 0;
        private int _lastModifierFlag = 0;

        private bool _ctrlDown = false;
        private bool _shiftDown = false;
        private bool _altDown = false;
        private bool _physicalAltDown = false;
        private bool _winDown = false;

        private void ResetModifierState()
        {
            _modifierFlag = 0;
            _lastModifierFlag = 0;
            _ctrlDown = false;
            _shiftDown = false;
            _altDown = false;
            _physicalAltDown = false;
            _winDown = false;
        }

        private int GetSwitchModifierMask()
        {
            bool hasAnyCustomMod = _settings.SwitchCtrl || _settings.SwitchAlt || _settings.SwitchWin || _settings.SwitchShift;
            string targetKeyStr = (_settings.SwitchKeyChar ?? "").Trim();
            if (hasAnyCustomMod && string.IsNullOrEmpty(targetKeyStr))
            {
                int mask = 0;
                if (_settings.SwitchCtrl) mask |= MASK_CTRL;
                if (_settings.SwitchShift) mask |= MASK_SHIFT;
                if (_settings.SwitchAlt) mask |= MASK_ALT;
                if (_settings.SwitchWin) mask |= MASK_WIN;
                return mask;
            }

            if (_settings.SwitchMode == SwitchKeyMode.CtrlShift)
            {
                return MASK_CTRL | MASK_SHIFT;
            }
            if (_settings.SwitchMode == SwitchKeyMode.AltShift)
            {
                return MASK_ALT | MASK_SHIFT;
            }

            return 0;
        }

        private void SyncModifierState()
        {
            int flag = 0;
            if (((GetAsyncKeyState(0xA2) & 0x8000) != 0) || ((GetAsyncKeyState(0xA3) & 0x8000) != 0) || ((GetAsyncKeyState(0x11) & 0x8000) != 0))
                flag |= MASK_CTRL;
            if (((GetAsyncKeyState(0xA0) & 0x8000) != 0) || ((GetAsyncKeyState(0xA1) & 0x8000) != 0) || ((GetAsyncKeyState(0x10) & 0x8000) != 0))
                flag |= MASK_SHIFT;
            if (((GetAsyncKeyState(0xA4) & 0x8000) != 0) || ((GetAsyncKeyState(0xA5) & 0x8000) != 0) || ((GetAsyncKeyState(0x12) & 0x8000) != 0))
                flag |= MASK_ALT;
            if (((GetAsyncKeyState(0x5B) & 0x8000) != 0) || ((GetAsyncKeyState(0x5C) & 0x8000) != 0))
                flag |= MASK_WIN;

            _modifierFlag = flag;
            _ctrlDown = (flag & MASK_CTRL) != 0;
            _shiftDown = (flag & MASK_SHIFT) != 0;
            _altDown = (flag & MASK_ALT) != 0;
            _physicalAltDown = _altDown;
            _winDown = (flag & MASK_WIN) != 0;
        }

        private uint _lastDoubleShiftKey = 0;
        private int _lastDoubleShiftTime = 0;

        public event Action LanguageChanged;
        public event Action<bool> VietnameseStateToggled;
        public event Action<Charset> CharsetChanged;
        public event Action OpenSettingsRequested;
        public event Action OpenMacroTableRequested;
        public event Action ToggleMacroRequested;
        public event Action ResetHookRequested;
        public event Action OpenClipboardRequested;
        public event Action OpenClipboardFavoriteRequested;
        public Func<int, uint, bool> CheckClipboardShortcutRequested;
        public event Action OpenTextTransformRequested;
        public static event Action<bool> GameModeChanged;
        public static event Action RequestShowCaretIndicator;

        public void ToggleGameMode()
        {
            _settings.GameModeEnabled = !_settings.GameModeEnabled;
            _engine.Reset();
            GameModeChanged?.Invoke(_settings.GameModeEnabled);
        }

        public void SetGameMode(bool enable)
        {
            _settings.GameModeEnabled = enable;
            _engine.Reset();
            GameModeChanged?.Invoke(_settings.GameModeEnabled);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private WinEventDelegate _winEventProc;
        private IntPtr _winEventHookId = IntPtr.Zero;

        // Lưu vết trạng thái ngôn ngữ gõ [VI/EN] theo từng tiến trình (chuẩn OpenKey C++ Smart Switch Key)
        private static readonly Dictionary<string, bool> _appLanguageMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static string _currentAppExeName = string.Empty;

        private static readonly HashSet<string> _defaultExcludedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cmd.exe", "powershell.exe", "pwsh.exe", "windowsterminal.exe", "wt.exe",
            "mintty.exe", "bash.exe", "git-bash.exe", "conhost.exe", "alacritty.exe", "wezterm-gui.exe",
            "code.exe", "devenv.exe", "clion64.exe", "idea64.exe", "pycharm64.exe",
            "webstorm64.exe", "rider64.exe", "sublime_text.exe", "notepad++.exe",
            "steam.exe", "epicgameslauncher.exe", "league of legends.exe", "valorant.exe",
            "csgo.exe", "cs2.exe", "dota2.exe", "gta5.exe", "overwatch.exe"
        };

        private static readonly Dictionary<uint, string> _pidExeNameCache = new Dictionary<uint, string>();

        private string GetExeNameFromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return string.Empty;
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return string.Empty;

                if (_pidExeNameCache.TryGetValue(pid, out string cachedName))
                {
                    return cachedName;
                }

                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            string fullPath = sb.ToString();
                            string fileName = System.IO.Path.GetFileName(fullPath)?.ToLowerInvariant() ?? string.Empty;
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                if (_pidExeNameCache.Count > 200) _pidExeNameCache.Clear();
                                _pidExeNameCache[pid] = fileName;
                                return fileName;
                            }
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }

                // Nếu không mở được process (tiến trình Game có Anti-Cheat hoặc elevated),
                // TUYỆT ĐỐI KHÔNG DÙNG Process.GetProcessById vì nó quét toàn bộ hệ thống làm treo máy/lag game!
                _pidExeNameCache[pid] = "unknown.exe";
                return "unknown.exe";
            }
            catch
            {
                return string.Empty;
            }
        }

        private void TriggerLanguageSwitch()
        {
            _settings.IsVietnamese = !_settings.IsVietnamese;
            _engine.Reset();

            if (_settings.AutoExcludeEnabled && !string.IsNullOrEmpty(_currentAppExeName))
            {
                _appLanguageMap[_currentAppExeName] = _settings.IsVietnamese;
            }

            if (_settings.SwitchBeep)
            {
                try
                {
                    Console.Beep(_settings.IsVietnamese ? 1000 : 600, 70);
                }
                catch { }
            }
            LanguageChanged?.Invoke();
        }

        private void OnForegroundWindowChanged(IntPtr hWnd)
        {
            ResetModifierState();
            if (hWnd == IntPtr.Zero) return;
            string exe = GetExeNameFromWindow(hWnd);
            if (string.IsNullOrEmpty(exe) || exe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                return;

            _currentAppExeName = exe;

            if (_settings.AutoExcludeEnabled)
            {
                if (_appLanguageMap.TryGetValue(exe, out bool savedLangState))
                {
                    if (_settings.IsVietnamese != savedLangState)
                    {
                        _settings.IsVietnamese = savedLangState;
                        _engine.Reset();
                        LanguageChanged?.Invoke();
                    }
                }
                else
                {
                    // Lần đầu mở app: kiểm tra nếu app thuộc danh sách loại trừ mặc định -> chọn EN (false), ngược lại chọn trạng thái hiện tại
                    bool isDefaultExclude = _defaultExcludedApps.Contains(exe);
                    bool initialLang = isDefaultExclude ? false : _settings.IsVietnamese;
                    _appLanguageMap[exe] = initialLang;

                    if (_settings.IsVietnamese != initialLang)
                    {
                        _settings.IsVietnamese = initialLang;
                        _engine.Reset();
                        LanguageChanged?.Invoke();
                    }
                }
            }
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                OnForegroundWindowChanged(hwnd);
            }
        }

        public KeyboardHook(VietnameseEngine engine, AppSettings settings)
        {
            _engine = engine;
            _settings = settings;
        }

        public void Start()
        {
            if (_keyboardHookId == IntPtr.Zero)
            {
                _keyboardProc = KeyboardHookCallback;
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            if (_mouseHookId == IntPtr.Zero)
            {
                _mouseProc = MouseHookCallback;
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            if (_winEventHookId == IntPtr.Zero)
            {
                _winEventProc = WinEventCallback;
                _winEventHookId = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            }
        }

        public void Stop()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }

            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }

            if (_winEventHookId != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHookId);
                _winEventHookId = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                // Bỏ qua ngay lập tức mọi thông điệp di chuyển và cuộn chuột (chiếm 99.999% sự kiện chuột trong game)
                if (msg == 0x0200 /* WM_MOUSEMOVE */ || msg == 0x020A /* WM_MOUSEWHEEL */ || msg == 0x020E /* WM_MOUSEHWHEEL */)
                {
                    return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                }

                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    try
                    {
                        // Chỉ cần reset bộ gõ khi đang có ký tự gõ dở dang
                        if (_engine != null && _engine.HasPendingWord)
                        {
                            _engine.Reset();
                        }
                        ResetModifierState();
                    }
                    catch { }
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // 1. Tuyệt đối bỏ qua các phím do chính KeySender bơm vào
                if (_isInjecting || hookStruct.dwExtraInfo == KeySender.INJECTED_SIGNATURE)
                {
                    return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }

                // LƯU Ý: WinEventHook (EVENT_SYSTEM_FOREGROUND) đã tự động theo dõi việc đổi cửa sổ ứng dụng
                // một cách chuẩn xác từ hệ điều hành. KHÔNG gọi GetForegroundWindow() trên mọi lần bấm phím để tránh làm chậm game!

                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    uint vkCode = hookStruct.vkCode;
                    uint scanCode = hookStruct.scanCode;

                    // 0. Toggle Game Mode nhanh: Ctrl + Shift + F11 (Feature 4)
                    if (vkCode == 0x7A /* F11 */ && (_modifierFlag & MASK_CTRL) != 0 && (_modifierFlag & MASK_SHIFT) != 0)
                    {
                        ToggleGameMode();
                        return (IntPtr)1;
                    }

                    // Nếu Game Mode đang kích hoạt: bypass tức thì 0ms, không xử lý bất kỳ logic nào
                    if (_settings.GameModeEnabled)
                    {
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 17. Phát âm thanh click phím cơ Cyberpunk nếu được bật
                    if (_settings.EnableKeySound)
                    {
                        SoundManager.PlayKeyClick();
                    }

                    // 16. Báo hiệu hiển thị Caret Indicator khi bắt đầu gõ
                    if (_settings.EnableCaretIndicator)
                    {
                        RequestShowCaretIndicator?.Invoke();
                    }

                    bool isModifierKey = (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl
                                          vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                                          vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt
                                          vkCode == 0x5B || vkCode == 0x5C);                     // Win

                    if (isModifierKey)
                    {
                        if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3)
                        {
                            _modifierFlag |= MASK_CTRL;
                            _engine.Reset(); // Bấm phím Ctrl lập tức reset engine để ký tự sau là ký tự đầu tiên
                        }
                        else if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1) _modifierFlag |= MASK_SHIFT;
                        else if (vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5) _modifierFlag |= MASK_ALT;
                        else if (vkCode == 0x5B || vkCode == 0x5C) _modifierFlag |= MASK_WIN;

                        if (_lastModifierFlag == 0 || _lastModifierFlag < _modifierFlag)
                        {
                            _lastModifierFlag = _modifierFlag;
                        }

                        _ctrlDown = (_modifierFlag & MASK_CTRL) != 0;
                        _shiftDown = (_modifierFlag & MASK_SHIFT) != 0;
                        _altDown = (_modifierFlag & MASK_ALT) != 0;
                        _physicalAltDown = _altDown;
                        _winDown = (_modifierFlag & MASK_WIN) != 0;

                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // Nếu là phím thường (không phải modifier)
                    _lastModifierFlag = 0; // Hủy ngay phiên modifier hotkey chuẩn OpenKey C++
                    _lastDoubleShiftKey = 0;

                    // Self-healing: Tự đồng bộ lại cờ modifier từ bàn phím phần cứng khi gõ phím thường chuẩn OpenKey C++
                    SyncModifierState();

                    // 4. Phím chuyển chế độ gõ có ký tự đi kèm (Alt+Z hoặc Ctrl/Alt/Win/Shift + KeyChar / Space / CapsLock)
                    bool hasAnySwitchModifier = _settings.SwitchCtrl || _settings.SwitchAlt || _settings.SwitchWin || _settings.SwitchShift;
                    string targetKeyStr = (_settings.SwitchKeyChar ?? "").Trim().ToUpperInvariant();

                    bool isAltZMode = (_settings.SwitchMode == SwitchKeyMode.AltZ && (_modifierFlag & MASK_ALT) != 0 && (vkCode == 0x5A));

                    if (isAltZMode || (hasAnySwitchModifier && !string.IsNullOrEmpty(targetKeyStr)))
                    {
                        bool isMatchKey = false;
                        if (isAltZMode)
                        {
                            isMatchKey = true;
                        }
                        else
                        {
                            bool hasRequiredModifier = (!_settings.SwitchCtrl || (_modifierFlag & MASK_CTRL) != 0) &&
                                                        (!_settings.SwitchAlt || (_modifierFlag & MASK_ALT) != 0) &&
                                                        (!_settings.SwitchWin || (_modifierFlag & MASK_WIN) != 0) &&
                                                        (!_settings.SwitchShift || (_modifierFlag & MASK_SHIFT) != 0);

                            if (hasRequiredModifier)
                            {
                                if (targetKeyStr == "SPACE") isMatchKey = (vkCode == 0x20);
                                else if (targetKeyStr == "CAPSLOCK" || targetKeyStr == "CAPS") isMatchKey = (vkCode == 0x14);
                                else if (targetKeyStr.Length == 1)
                                {
                                    char targetChar = targetKeyStr[0];
                                    if (targetChar >= 'A' && targetChar <= 'Z') isMatchKey = (vkCode == (uint)targetChar);
                                    else if (targetChar >= '0' && targetChar <= '9') isMatchKey = (vkCode == (uint)targetChar);
                                }
                            }
                        }

                        if (isMatchKey)
                        {
                            TriggerLanguageSwitch();
                            if ((_modifierFlag & (MASK_ALT | MASK_WIN)) != 0)
                            {
                                KeySender.SuppressAltMenuActivation();
                            }
                            return (IntPtr)1; // Nuốt phím chuyển
                        }
                    }

                    // 5. Phím CapsLock
                    if (vkCode == 0x14)
                    {
                        _engine.Reset();
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 6. Phím Numpad (VK_NUMPAD0..VK_NUMPAD9, Multiply, Add, Separator, Subtract, Decimal, Divide: 0x60..0x6F)
                    if (vkCode >= 0x60 && vkCode <= 0x6F)
                    {
                        _engine.Reset();
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 6.5. Phím tắt mở/toggle nhanh Clipboard History HUD: Win+Ins (chuẩn Comfort Keys) hoặc Ctrl+Alt+V
                    if (((_modifierFlag & MASK_WIN) != 0 && vkCode == 0x2D) ||
                        (((_modifierFlag & (MASK_CTRL | MASK_ALT)) == (MASK_CTRL | MASK_ALT)) && vkCode == 0x56))
                    {
                        KeySender.SuppressAltMenuActivation();
                        _engine.Reset();
                        OpenClipboardRequested?.Invoke();
                        return (IntPtr)1;
                    }

                    // 6.6. Phím tắt mở/toggle nhanh Clipboard Favorite HUD: Alt+Insert (chuẩn Comfort Keys Pro)
                    if ((_modifierFlag & MASK_ALT) != 0 && (_modifierFlag & (MASK_CTRL | MASK_WIN | MASK_SHIFT)) == 0 && vkCode == 0x2D)
                    {
                        KeySender.SuppressAltMenuActivation();
                        _engine.Reset();
                        OpenClipboardFavoriteRequested?.Invoke();
                        return (IntPtr)1;
                    }

                    // 6.8. Phím tắt mở Quick Text Transform Popup: Win+Alt+T hoặc Ctrl+Shift+T (Feature 13)
                    if ((((_modifierFlag & (MASK_WIN | MASK_ALT)) == (MASK_WIN | MASK_ALT)) ||
                         ((_modifierFlag & (MASK_CTRL | MASK_SHIFT)) == (MASK_CTRL | MASK_SHIFT))) && vkCode == 0x54 /* T */)
                    {
                        KeySender.SuppressAltMenuActivation();
                        _engine.Reset();
                        OpenTextTransformRequested?.Invoke();
                        return (IntPtr)1;
                    }

                    // 6.7. Kiểm tra phím tắt dán nhanh cho các mục Clipboard cá nhân hóa (Chuẩn Comfort Keys Pro)
                    if (CheckClipboardShortcutRequested != null && ((_modifierFlag & (MASK_CTRL | MASK_ALT | MASK_WIN)) != 0 || (vkCode >= 0x70 && vkCode <= 0x7B)))
                    {
                        int winMod = 0;
                        if ((_modifierFlag & MASK_ALT) != 0) winMod |= 1;
                        if ((_modifierFlag & MASK_CTRL) != 0) winMod |= 2;
                        if ((_modifierFlag & MASK_SHIFT) != 0) winMod |= 4;
                        if ((_modifierFlag & MASK_WIN) != 0) winMod |= 8;

                        if (CheckClipboardShortcutRequested(winMod, vkCode))
                        {
                            KeySender.SuppressAltMenuActivation();
                            _engine.Reset();
                            return (IntPtr)1; // Nuốt phím khi đã thực hiện dán mục clipboard
                        }
                    }

                    // 7. Tab Phím tắt (F1-F12 kết hợp Modifier tùy chọn chuẩn OpenKey C++)
                    if (_settings.ShortcutModifier != 0 && (vkCode >= 0x70 && vkCode <= 0x7B))
                    {
                        int currentMod = 0;
                        if ((_modifierFlag & MASK_CTRL) != 0) currentMod |= 0x01;
                        if ((_modifierFlag & MASK_SHIFT) != 0) currentMod |= 0x02;
                        if ((_modifierFlag & MASK_ALT) != 0) currentMod |= 0x04;
                        if ((_modifierFlag & MASK_WIN) != 0) currentMod |= 0x08;

                        if ((currentMod & _settings.ShortcutModifier) == _settings.ShortcutModifier)
                        {
                            int fIndex = (int)(vkCode - 0x70 + 1); // F1=1..F12=12
                            if ((_settings.ShortcutEnableMask & (1 << fIndex)) != 0)
                            {
                                if ((currentMod & (MASK_ALT | MASK_WIN)) != 0)
                                {
                                    KeySender.SuppressAltMenuActivation();
                                }
                                _engine.Reset();

                                switch (fIndex)
                                {
                                    case 1: // F1: Bật TV
                                        _settings.IsVietnamese = true;
                                        VietnameseStateToggled?.Invoke(true);
                                        LanguageChanged?.Invoke();
                                        return (IntPtr)1;

                                    case 2: // F2: Tắt TV
                                        _settings.IsVietnamese = false;
                                        VietnameseStateToggled?.Invoke(false);
                                        LanguageChanged?.Invoke();
                                        return (IntPtr)1;

                                    case 3: // F3: Chuyển bảng mã Unicode
                                        _settings.CurrentCharset = Charset.Unicode;
                                        CharsetChanged?.Invoke(Charset.Unicode);
                                        return (IntPtr)1;

                                    case 4: // F4: Chuyển bảng mã tùy chọn (ShortcutF4Charset)
                                        _settings.CurrentCharset = _settings.ShortcutF4Charset;
                                        CharsetChanged?.Invoke(_settings.ShortcutF4Charset);
                                        return (IntPtr)1;

                                    case 5: // F5: Mở bảng cài đặt
                                        OpenSettingsRequested?.Invoke();
                                        return (IntPtr)1;

                                    case 8: // F8: Mở bảng gõ tắt
                                        OpenMacroTableRequested?.Invoke();
                                        return (IntPtr)1;

                                    case 9: // F9: Bật / Tắt gõ tắt
                                        _settings.UseMacro = !_settings.UseMacro;
                                        ToggleMacroRequested?.Invoke();
                                        return (IntPtr)1;

                                    case 11: // F11: Chuyển đổi thông minh / Loại trừ app
                                        _settings.AutoExcludeEnabled = !_settings.AutoExcludeEnabled;
                                        IntPtr fgHwnd = GetForegroundWindow();
                                        if (fgHwnd != IntPtr.Zero) OnForegroundWindowChanged(fgHwnd);
                                        if (_settings.SwitchBeep)
                                        {
                                            try { Console.Beep(_settings.AutoExcludeEnabled ? 900 : 500, 70); } catch { }
                                        }
                                        return (IntPtr)1;

                                    case 12: // F12: Reset ModernKey / Hook
                                        ResetHookRequested?.Invoke();
                                        if (_settings.SwitchBeep)
                                        {
                                            try { Console.Beep(800, 70); Console.Beep(1200, 70); } catch { }
                                        }
                                        return (IntPtr)1;
                                }
                            }
                        }
                    }

                    // 8. Phím điều hướng và chức năng (Arrows, Home, End, PgUp, PgDn, Esc, Del, Tab)
                    if (vkCode == 0x1B) // ESC: Hoàn tác dấu tiếng Việt hoặc reset engine
                    {
                        if (_settings.EscKeyUndo && _engine.HandleEscUndo(out int bc, out string rep))
                        {
                            if (bc > 0 || !string.IsNullOrEmpty(rep))
                            {
                                _isInjecting = true;
                                try
                                {
                                    KeySender.SendReplaceText(bc, rep, _settings.FixRecommendBrowser, _settings.SendViaClipboard, 0);
                                }
                                finally
                                {
                                    _isInjecting = false;
                                }
                                return (IntPtr)1; // Nuốt phím Esc khi đã hoàn tác dấu để tránh hủy form/cửa sổ
                            }
                        }

                        _engine.Reset();
                        _lastDoubleShiftKey = 0;
                        _lastDoubleShiftTime = 0;
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    if ((vkCode >= 0x21 && vkCode <= 0x28) || vkCode == 0x2E || vkCode == 0x09 ||
                        (vkCode >= 0x70 && vkCode <= 0x7B))
                    {
                        _engine.Reset();
                        _lastDoubleShiftKey = 0;
                        _lastDoubleShiftTime = 0;
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 9. Bỏ qua phím tắt hệ thống (Ctrl+C, Ctrl+V, Alt+Tab, Win+R...)
                    if ((_modifierFlag & (MASK_CTRL | MASK_ALT | MASK_WIN)) != 0)
                    {
                        _engine.Reset();
                        _lastDoubleShiftKey = 0;
                        _lastDoubleShiftTime = 0;
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    char ch = ConvertVkToChar(vkCode, scanCode);

                    bool oldState = _settings.IsVietnamese;

                    bool isShift = ((_modifierFlag & MASK_SHIFT) != 0) || _shiftDown || ((GetAsyncKeyState(0x10) & 0x8000) != 0);
                    bool isCtrl = (_modifierFlag & MASK_CTRL) != 0;
                    bool isAlt = (_modifierFlag & MASK_ALT) != 0;
                    bool isCaps = (GetKeyState(0x14) & 0x0001) != 0;

                    if (_engine.ProcessKey(ch, (int)vkCode, isShift, isCaps, isCtrl, isAlt,
                                           out int backspaceCount, out string newString, out int trailingVkCode))
                    {
                        if (backspaceCount > 0 || !string.IsNullOrEmpty(newString) || trailingVkCode != 0)
                        {
                            _isInjecting = true;
                            try
                            {
                                KeySender.SendReplaceText(backspaceCount, newString, _settings.FixRecommendBrowser, _settings.SendViaClipboard, trailingVkCode);
                            }
                            finally
                            {
                                _isInjecting = false;
                            }

                            // Nuốt phím vừa gõ
                            return (IntPtr)1;
                        }
                    }

                    // Nếu trạng thái ngôn ngữ thay đổi bởi phím tắt
                    if (oldState != _settings.IsVietnamese)
                    {
                        LanguageChanged?.Invoke();
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    uint vkCode = hookStruct.vkCode;

                    bool isModifierKey = (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl
                                          vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                                          vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt
                                          vkCode == 0x5B || vkCode == 0x5C);                     // Win

                    if (isModifierKey)
                    {
                        int prevLastFlag = _lastModifierFlag;

                        if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3)
                        {
                            _modifierFlag &= ~MASK_CTRL;
                            _engine.Reset(); // Reset bộ gõ khi nhả Ctrl
                        }
                        else if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1)
                        {
                            _modifierFlag &= ~MASK_SHIFT;

                            if (_settings.UseMacro)
                            {
                                uint shiftCode = (vkCode == 0x10) ? 0xA0 : vkCode;
                                bool canTriggerL = (shiftCode == 0xA0 && (_settings.MacroTriggerMask & 0x04) != 0);
                                bool canTriggerR = (shiftCode == 0xA1 && (_settings.MacroTriggerMask & 0x08) != 0);

                                if (canTriggerL || canTriggerR)
                                {
                                    int now = Environment.TickCount;
                                    uint doubleTime = GetDoubleClickTime();
                                    if (doubleTime < 300) doubleTime = 300;
                                    if (doubleTime > 600) doubleTime = 600;

                                    if (_lastDoubleShiftKey == shiftCode && (now - _lastDoubleShiftTime) <= (int)doubleTime)
                                    {
                                        _lastDoubleShiftKey = 0;
                                        _lastDoubleShiftTime = 0;

                                        if (_engine.TryTriggerMacroDirect(out int bc, out string rep))
                                        {
                                            if (bc > 0 || !string.IsNullOrEmpty(rep))
                                            {
                                                int backspaceCount = bc;
                                                string replacement = rep;
                                                bool fixBrowser = _settings.FixRecommendBrowser;
                                                bool sendViaClip = _settings.SendViaClipboard;

                                                ThreadPool.QueueUserWorkItem(_ =>
                                                {
                                                    Thread.Sleep(30);
                                                    _isInjecting = true;
                                                    try
                                                    {
                                                        KeySender.SendReplaceText(backspaceCount, replacement, fixBrowser, sendViaClip);
                                                    }
                                                    finally
                                                    {
                                                        _isInjecting = false;
                                                    }
                                                });
                                            }
                                        }
                                    }
                                    else
                                    {
                                        _lastDoubleShiftKey = shiftCode;
                                        _lastDoubleShiftTime = now;
                                    }
                                }
                                else
                                {
                                    _lastDoubleShiftKey = 0;
                                }
                            }
                        }
                        else if (vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5)
                        {
                            _modifierFlag &= ~MASK_ALT;
                        }
                        else if (vkCode == 0x5B || vkCode == 0x5C)
                        {
                            _modifierFlag &= ~MASK_WIN;
                        }

                        _ctrlDown = (_modifierFlag & MASK_CTRL) != 0;
                        _shiftDown = (_modifierFlag & MASK_SHIFT) != 0;
                        _altDown = (_modifierFlag & MASK_ALT) != 0;
                        _physicalAltDown = _altDown;
                        _winDown = (_modifierFlag & MASK_WIN) != 0;

                        // Kiểm tra phím chuyển E/V thuần Modifier khi nhả phím chuẩn OpenKey C++
                        if (prevLastFlag > _modifierFlag)
                        {
                            int targetSwitchMask = GetSwitchModifierMask();
                            if (targetSwitchMask != 0 && prevLastFlag == targetSwitchMask)
                            {
                                TriggerLanguageSwitch();
                            }

                            // Cập nhật lại _lastModifierFlag về trạng thái _modifierFlag hiện tại chuẩn OpenKey C++
                            _lastModifierFlag = _modifierFlag;
                        }

                        if (_modifierFlag == 0)
                        {
                            _lastModifierFlag = 0;
                        }

                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }
                }
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private static readonly byte[] _cachedKeyStates = new byte[256];
        private static readonly StringBuilder _cachedCharBuffer = new StringBuilder(16);

        private char ConvertVkToChar(uint vkCode, uint scanCode)
        {
            GetKeyboardState(_cachedKeyStates);

            bool shiftActive = _shiftDown || ((_modifierFlag & MASK_SHIFT) != 0) || ((GetAsyncKeyState(0x10) & 0x8000) != 0);
            _cachedKeyStates[0x10] = (byte)(shiftActive ? 0x80 : 0);
            _cachedKeyStates[0xA0] = (byte)(shiftActive ? 0x80 : 0);
            _cachedKeyStates[0xA1] = (byte)(shiftActive ? 0x80 : 0);
            bool ctrlActive = _ctrlDown || ((_modifierFlag & MASK_CTRL) != 0) || ((GetAsyncKeyState(0x11) & 0x8000) != 0);
            _cachedKeyStates[0x11] = (byte)(ctrlActive ? 0x80 : 0);
            _cachedKeyStates[0xA2] = (byte)(ctrlActive ? 0x80 : 0);
            _cachedKeyStates[0xA3] = (byte)(ctrlActive ? 0x80 : 0);
            bool altActive = _altDown || ((_modifierFlag & MASK_ALT) != 0) || ((GetAsyncKeyState(0x12) & 0x8000) != 0);
            _cachedKeyStates[0x12] = (byte)(altActive ? 0x80 : 0);
            _cachedKeyStates[0xA4] = (byte)(altActive ? 0x80 : 0);
            _cachedKeyStates[0xA5] = (byte)(altActive ? 0x80 : 0);
            _cachedKeyStates[0x14] = (byte)((GetKeyState(0x14) & 0x0001) != 0 ? 0x01 : 0);

            IntPtr hWnd = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            IntPtr layout = GetKeyboardLayout(threadId);

            _cachedCharBuffer.Clear();
            int rc = ToUnicodeEx(vkCode, scanCode, _cachedKeyStates, _cachedCharBuffer, _cachedCharBuffer.Capacity, 0, layout);
            if (rc > 0 && _cachedCharBuffer.Length > 0)
            {
                return _cachedCharBuffer[0];
            }
            return (char)0;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
