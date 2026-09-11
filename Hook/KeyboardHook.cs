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

        private bool _ctrlDown = false;
        private bool _shiftDown = false;
        private bool _altDown = false;
        private bool _physicalAltDown = false;
        private bool _swallowNextAltUp = false;
        private bool _winDown = false;
        private bool _hasOtherKeyPressed = false;

        private uint _lastDoubleShiftKey = 0;
        private int _lastDoubleShiftTime = 0;

        public event Action LanguageChanged;
        public event Action<bool> VietnameseStateToggled;
        public event Action<Charset> CharsetChanged;
        public event Action OpenSettingsRequested;
        public event Action OpenMacroTableRequested;
        public event Action ToggleMacroRequested;
        public event Action ResetHookRequested;

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

        private string GetExeNameFromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return string.Empty;
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return string.Empty;

                using (var proc = Process.GetProcessById((int)pid))
                {
                    string pName = proc.ProcessName.ToLowerInvariant();
                    if (!pName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        pName += ".exe";
                    return pName;
                }
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
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    try
                    {
                        _engine?.Reset();
                    }
                    catch { }
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private IntPtr _lastForegroundWindow = IntPtr.Zero;



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

                // 2. Tự động chuyển đổi ngôn ngữ gõ thông minh (Smart Switch Key) theo từng cửa sổ ứng dụng
                IntPtr currentForeground = GetForegroundWindow();
                if (currentForeground != _lastForegroundWindow)
                {
                    _lastForegroundWindow = currentForeground;
                    _engine.Reset();
                    OnForegroundWindowChanged(currentForeground);
                }

                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    uint vkCode = hookStruct.vkCode;
                    uint scanCode = hookStruct.scanCode;

                    // 3. Quản lý trạng thái Modifier (Ctrl, Shift, Alt, Win)
                    bool isLWin = (GetKeyState(0x5B) & 0x8000) != 0;
                    bool isRWin = (GetKeyState(0x5C) & 0x8000) != 0;
                    bool isWin = isLWin || isRWin;
                    bool isShift = (GetKeyState(0x10) & 0x8000) != 0;
                    bool isCtrl = (GetKeyState(0x11) & 0x8000) != 0;
                    bool isAlt = _physicalAltDown || ((GetAsyncKeyState(0x12) & 0x8000) != 0) || ((GetKeyState(0x12) & 0x8000) != 0);
                    bool isCaps = (GetKeyState(0x14) & 0x0001) != 0;

                    if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3) _ctrlDown = true;
                    if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1) _shiftDown = true;
                    if (vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5)
                    {
                        _altDown = true;
                        _physicalAltDown = true;
                    }
                    if (vkCode == 0x5B || vkCode == 0x5C) _winDown = true;

                    // 4. Phím chuyển chế độ gõ tự do (Ctrl/Alt/Win/Shift + KeyChar / Space / CapsLock) chuẩn OpenKey C++
                    bool matchCtrl = !_settings.SwitchCtrl || isCtrl || _ctrlDown;
                    bool matchAlt = !_settings.SwitchAlt || isAlt || _altDown;
                    bool matchWin = !_settings.SwitchWin || isWin || _winDown;
                    bool matchShift = !_settings.SwitchShift || isShift || _shiftDown;
                    bool hasRequiredModifier = (!_settings.SwitchCtrl || isCtrl || _ctrlDown) &&
                                                (!_settings.SwitchAlt || isAlt || _altDown) &&
                                                (!_settings.SwitchWin || isWin || _winDown) &&
                                                (!_settings.SwitchShift || isShift || _shiftDown);

                    bool isModifierOnlyRequired = !_settings.SwitchCtrl && !_settings.SwitchAlt && !_settings.SwitchWin && !_settings.SwitchShift;

                    if (!isModifierOnlyRequired && hasRequiredModifier)
                    {
                        string targetKeyStr = (_settings.SwitchKeyChar ?? "").Trim().ToUpperInvariant();
                        bool isMatchKey = false;

                        if (string.IsNullOrEmpty(targetKeyStr) || targetKeyStr == "SPACE")
                        {
                            isMatchKey = (vkCode == 0x20); // Space
                        }
                        else if (targetKeyStr == "CAPSLOCK" || targetKeyStr == "CAPS")
                        {
                            isMatchKey = (vkCode == 0x14); // CapsLock
                        }
                        else if (targetKeyStr.Length == 1)
                        {
                            char targetChar = targetKeyStr[0];
                            if (targetChar >= 'A' && targetChar <= 'Z')
                            {
                                isMatchKey = (vkCode == (uint)targetChar);
                            }
                            else if (targetChar >= '0' && targetChar <= '9')
                            {
                                isMatchKey = (vkCode == (uint)targetChar);
                            }
                        }

                        if (isMatchKey)
                        {
                            _hasOtherKeyPressed = true;
                            TriggerLanguageSwitch();
                            if (_altDown || isAlt)
                            {
                                KeySender.SuppressAltMenuActivation();
                                _swallowNextAltUp = true;
                            }
                            return (IntPtr)1; // Nuốt phím chuyển
                        }
                    }

                    // 5. Phím Windows (Win+R, Win+D...)
                    if (vkCode == 0x5B || vkCode == 0x5C || isWin)
                    {
                        _engine.Reset();
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 6. Phím CapsLock
                    if (vkCode == 0x14)
                    {
                        _engine.Reset();
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 6.5 Phím Numpad (VK_NUMPAD0..VK_NUMPAD9, Multiply, Add, Separator, Subtract, Decimal, Divide: 0x60..0x6F)
                    // Numpad tuyệt đối không bao giờ gõ tiếng Việt, chỉ cho phép hàng phím số chính (Dpad 0x30..0x39) gõ tiếng Việt
                    if (vkCode >= 0x60 && vkCode <= 0x6F)
                    {
                        _engine.Reset();
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // 7. Các phím Modifier đứng độc lập
                    if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                        vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl
                        vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt
                        vkCode == 0x90 || vkCode == 0x91)                     // NumLock, ScrollLock
                    {
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    // Đánh dấu đã có phím khác được ấn trong phiên giữ modifier
                    _hasOtherKeyPressed = true;
                    _lastDoubleShiftKey = 0;

                    // 7.5 Tab Phím tắt (F1-F12 kết hợp Modifier tùy chọn chuẩn OpenKey C++)
                    if (_settings.ShortcutModifier != 0 && (vkCode >= 0x70 && vkCode <= 0x7B))
                    {
                        int currentMod = 0;
                        if (_ctrlDown || isCtrl) currentMod |= 0x01;
                        if (_shiftDown || isShift) currentMod |= 0x02;
                        if (_altDown || isAlt) currentMod |= 0x04;
                        if (_winDown || isWin) currentMod |= 0x08;

                        if ((currentMod & _settings.ShortcutModifier) == _settings.ShortcutModifier)
                        {
                            int fIndex = (int)(vkCode - 0x70 + 1); // F1=1..F12=12
                            if ((_settings.ShortcutEnableMask & (1 << fIndex)) != 0)
                            {
                                if ((currentMod & 0x04) != 0) // Có phím Alt trong modifier
                                {
                                    KeySender.SuppressAltMenuActivation();
                                    _swallowNextAltUp = true;
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



                    // 8. Phím điều hướng và chức năng (Arrows, Home, End, PgUp, PgDn, Esc, Del, Tab, F1..F12)
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

                    // 9. Bỏ qua phím tắt hệ thống (Ctrl+C, Ctrl+V, Alt+Tab...)
                    if (isCtrl || isAlt)
                    {
                        _engine.Reset();
                        _lastDoubleShiftKey = 0;
                        _lastDoubleShiftTime = 0;
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    char ch = ConvertVkToChar(vkCode, scanCode);

                    bool oldState = _settings.IsVietnamese;

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

                    // Nhả Ctrl
                    if (vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3)
                    {
                        if (_settings.SwitchMode == SwitchKeyMode.CtrlShift && _ctrlDown && _shiftDown && !_hasOtherKeyPressed)
                        {
                            TriggerLanguageSwitch();
                        }
                        _ctrlDown = false;
                    }
                    // Nhả Shift
                    else if (vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1)
                    {
                        if (_settings.SwitchMode == SwitchKeyMode.CtrlShift && _ctrlDown && _shiftDown && !_hasOtherKeyPressed)
                        {
                            TriggerLanguageSwitch();
                        }
                        else if (_settings.SwitchMode == SwitchKeyMode.AltShift && (_altDown || _physicalAltDown) && _shiftDown && !_hasOtherKeyPressed)
                        {
                            TriggerLanguageSwitch();
                            KeySender.SuppressAltMenuActivation();
                            _swallowNextAltUp = true;
                        }
                        else if (_settings.UseMacro && !_hasOtherKeyPressed)
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

                                            // Đẩy việc thực thi dán macro sang ThreadPool để cho phép thông điệp WM_KEYUP của Shift
                                            // được Windows dispatch tới ứng dụng đích (Notepad++) trước, tránh bị hiểu nhầm thành Ctrl+Shift+V!
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
                        _shiftDown = false;
                    }
                    // Nhả Alt
                    else if (vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5)
                    {
                        _physicalAltDown = false;
                        _altDown = false;

                        if (_swallowNextAltUp)
                        {
                            _swallowNextAltUp = false;
                            return (IntPtr)1; // Nuốt phím nhả Alt vật lý vì đã giải phóng trước đó kèm Mask key!
                        }

                        if (_settings.SwitchMode == SwitchKeyMode.AltShift && _altDown && _shiftDown && !_hasOtherKeyPressed)
                        {
                            TriggerLanguageSwitch();
                            KeySender.SuppressAltMenuActivation();
                        }
                    }
                    // Nhả Win
                    else if (vkCode == 0x5B || vkCode == 0x5C)
                    {
                        _winDown = false;
                    }

                    // Khi tất cả modifier đã được thả ra: reset cờ phím phụ
                    if (!_ctrlDown && !_shiftDown && !_altDown && !_winDown)
                    {
                        _hasOtherKeyPressed = false;
                    }
                }
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private char ConvertVkToChar(uint vkCode, uint scanCode)
        {
            byte[] keyStates = new byte[256];
            GetKeyboardState(keyStates);

            IntPtr hWnd = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            IntPtr layout = GetKeyboardLayout(threadId);

            var sb = new StringBuilder(10);
            int rc = ToUnicodeEx(vkCode, scanCode, keyStates, sb, sb.Capacity, 0, layout);
            if (rc > 0 && sb.Length > 0)
            {
                return sb[0];
            }
            return (char)0;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
