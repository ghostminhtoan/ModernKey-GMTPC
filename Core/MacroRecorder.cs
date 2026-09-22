using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using ModernKey.Hook;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ ghi Macro bàn phím & chuột thời gian thực chuẩn Jitbit Macro Recorder.
    /// Tự động bắt tọa độ chuột, phím nhấn, tổ hợp phím, hold & drag mouse, tuân thủ Primary Mouse Button.
    /// Dừng ghi tức thì bằng phím F11 hoặc Escape.
    /// </summary>
    public class MacroRecorder
    {
        private static MacroRecorder _instance;
        public static MacroRecorder Instance => _instance ?? (_instance = new MacroRecorder());

        #region Win32 API Definitions

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;

        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_F11 = 0x7A;

        private const int SM_SWAPBUTTON = 23;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private LowLevelProc _keyboardProc;
        private LowLevelProc _mouseProc;

        private readonly List<string> _recordedCommands = new List<string>();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        // Mouse Drag & Click tracking
        private bool _isMouseDown = false;
        private string _mouseDownButton = "LEFT";
        private int _mouseDownX = -9999;
        private int _mouseDownY = -9999;
        private long _mouseDownTick = 0;
        private bool _isDragging = false;
        private long _lastDragRecordTick = 0;

        private int _lastClickX = -9999;
        private int _lastClickY = -9999;
        private long _lastClickTick = 0;
        private bool _lastWasLeftClick = false;

        // Key Combo tracking
        private readonly HashSet<uint> _activeModifiers = new HashSet<uint>();

        public bool IsRecording { get; private set; }

        public event Action OnRecordingStarted;
        public event Action<string> OnCommandRecorded;
        public event Action<string> OnRecordingFinished;

        public IReadOnlyList<string> RecordedCommands => _recordedCommands;

        public static bool IsMouseButtonSwapped()
        {
            return GetSystemMetrics(SM_SWAPBUTTON) != 0;
        }

        public static string GetLogicalButton(int msg)
        {
            bool swapped = IsMouseButtonSwapped();
            switch (msg)
            {
                case WM_LBUTTONDOWN:
                case WM_LBUTTONUP:
                    return swapped ? "RIGHT" : "LEFT";

                case WM_RBUTTONDOWN:
                case WM_RBUTTONUP:
                    return swapped ? "LEFT" : "RIGHT";

                case WM_MBUTTONDOWN:
                case WM_MBUTTONUP:
                    return "MIDDLE";

                default:
                    return "LEFT";
            }
        }

        public void StartRecording()
        {
            if (IsRecording) return;

            _recordedCommands.Clear();
            _activeModifiers.Clear();

            _isMouseDown = false;
            _mouseDownButton = "LEFT";
            _mouseDownX = -9999;
            _mouseDownY = -9999;
            _mouseDownTick = 0;
            _isDragging = false;
            _lastDragRecordTick = 0;

            _lastClickX = -9999;
            _lastClickY = -9999;
            _lastClickTick = 0;
            _lastWasLeftClick = false;

            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                IntPtr modHandle = GetModuleHandle(curModule.ModuleName);
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, modHandle, 0);
                _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, modHandle, 0);
            }

            IsRecording = true;
            _stopwatch.Restart();

            OnRecordingStarted?.Invoke();
        }

        public void StopRecording()
        {
            if (!IsRecording) return;

            IsRecording = false;
            _stopwatch.Stop();

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

            _activeModifiers.Clear();
            _isMouseDown = false;
            _isDragging = false;

            string fullScript = GetScriptText();
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnRecordingFinished?.Invoke(fullScript);
            });
        }

        public string GetScriptText()
        {
            return string.Join(Environment.NewLine, _recordedCommands);
        }

        private void AppendCommand(string cmd)
        {
            long elapsed = _stopwatch.ElapsedMilliseconds;
            if (elapsed >= 40 && _recordedCommands.Count > 0)
            {
                int delayMs = (int)Math.Min(elapsed, 4000);
                string delayCmd = $"DELAY : {delayMs}";
                _recordedCommands.Add(delayCmd);
                OnCommandRecorded?.Invoke(delayCmd);
            }

            _recordedCommands.Add(cmd);
            _stopwatch.Restart();
            OnCommandRecorded?.Invoke(cmd);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsRecording)
            {
                int msg = (int)wParam;
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Bỏ qua các phím do chính bộ gõ / macro tự inject
                if (kb.dwExtraInfo == (IntPtr)KeySender.INJECTED_SIGNATURE)
                {
                    return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }

                // Phím tắt dừng ghi: F11 hoặc Escape
                if (kb.vkCode == VK_F11 || kb.vkCode == VK_ESCAPE)
                {
                    if (msg == WM_KEYDOWN)
                    {
                        StopRecording();
                        return (IntPtr)1;
                    }
                    return (IntPtr)1;
                }

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    string keyName = GetFriendlyKeyName(kb.vkCode);

                    if (IsModifierKey(kb.vkCode))
                    {
                        _activeModifiers.Add(NormalizeModifierVk(kb.vkCode));
                        AppendCommand($"KEY_DOWN : {keyName}");
                    }
                    else
                    {
                        // Nếu có Modifier đang được nhấn giữ -> Ghi nhận thành TỔ HỢP PHÍM (Key Combination)
                        if (_activeModifiers.Count > 0)
                        {
                            string combo = BuildComboString(kb.vkCode);
                            AppendCommand($"KEY_COMBINATION : {combo}");
                        }
                        else
                        {
                            AppendCommand($"KEY_PRESS : {keyName}");
                        }
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (IsModifierKey(kb.vkCode))
                    {
                        _activeModifiers.Remove(NormalizeModifierVk(kb.vkCode));
                        string keyName = GetFriendlyKeyName(kb.vkCode);
                        AppendCommand($"KEY_UP : {keyName}");
                    }
                }
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private uint NormalizeModifierVk(uint vk)
        {
            if (vk == 0xA0 || vk == 0xA1) return 0x10; // Shift
            if (vk == 0xA2 || vk == 0xA3) return 0x11; // Ctrl
            if (vk == 0xA4 || vk == 0xA5) return 0x12; // Alt
            if (vk == 0x5C) return 0x5B;               // Win
            return vk;
        }

        private string BuildComboString(uint mainVk)
        {
            var parts = new List<string>();
            if (_activeModifiers.Contains(0x11)) parts.Add("CONTROL");
            if (_activeModifiers.Contains(0x10)) parts.Add("SHIFT");
            if (_activeModifiers.Contains(0x12)) parts.Add("ALT");
            if (_activeModifiers.Contains(0x5B)) parts.Add("WIN");

            parts.Add(GetFriendlyKeyName(mainVk));
            return string.Join("+", parts);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsRecording)
            {
                int msg = (int)wParam;
                var ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                // Bỏ qua các sự kiện chuột do macro inject
                if (ms.dwExtraInfo == (IntPtr)KeySender.INJECTED_SIGNATURE)
                {
                    return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                }

                int x = ms.pt.x;
                int y = ms.pt.y;
                long now = Environment.TickCount;

                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        _isMouseDown = true;
                        _mouseDownButton = GetLogicalButton(msg);
                        _mouseDownX = x;
                        _mouseDownY = y;
                        _mouseDownTick = now;
                        _isDragging = false;
                        break;

                    case WM_MOUSEMOVE:
                        if (_isMouseDown)
                        {
                            int dx = Math.Abs(x - _mouseDownX);
                            int dy = Math.Abs(y - _mouseDownY);

                            // Khi khoảng cách di chuyển >= 6px trong lúc giữ chuột -> Bắt đầu Hold & Drag
                            if (!_isDragging && (dx >= 6 || dy >= 6))
                            {
                                _isDragging = true;
                                AppendCommand($"MOUSE_DOWN : {_mouseDownButton} : {_mouseDownX} : {_mouseDownY}");
                                AppendCommand($"MOUSE_MOVE : {x} : {y}");
                                _lastDragRecordTick = now;
                            }
                            else if (_isDragging)
                            {
                                // Ghi nhận đường di chuyển kéo chuột
                                if (now - _lastDragRecordTick >= 75 || dx >= 20 || dy >= 20)
                                {
                                    AppendCommand($"MOUSE_MOVE : {x} : {y}");
                                    _lastDragRecordTick = now;
                                }
                            }
                        }
                        break;

                    case WM_LBUTTONUP:
                    case WM_RBUTTONUP:
                    case WM_MBUTTONUP:
                        string upBtn = GetLogicalButton(msg);
                        if (_isMouseDown && upBtn == _mouseDownButton)
                        {
                            if (_isDragging)
                            {
                                // Kết thúc Hold & Drag: Di chuyển đến tọa độ đích và nhả chuột (Drop)
                                AppendCommand($"MOUSE_MOVE : {x} : {y}");
                                AppendCommand($"MOUSE_UP : {_mouseDownButton} : {x} : {y}");
                                _isDragging = false;
                                _isMouseDown = false;
                                _lastWasLeftClick = false;
                            }
                            else
                            {
                                // Click thông thường tại chỗ (hoặc Double Click)
                                if (_lastWasLeftClick && _mouseDownButton == "LEFT" && (now - _lastClickTick <= 400) &&
                                    Math.Abs(x - _lastClickX) <= 6 && Math.Abs(y - _lastClickY) <= 6)
                                {
                                    if (_recordedCommands.Count > 0 && _recordedCommands[_recordedCommands.Count - 1].StartsWith("MOUSE_CLICK : LEFT"))
                                    {
                                        _recordedCommands[_recordedCommands.Count - 1] = $"MOUSE_DBLCLICK : LEFT : {x} : {y}";
                                    }
                                    else
                                    {
                                        AppendCommand($"MOUSE_DBLCLICK : LEFT : {x} : {y}");
                                    }
                                    _lastWasLeftClick = false;
                                }
                                else
                                {
                                    AppendCommand($"MOUSE_CLICK : {_mouseDownButton} : {x} : {y}");
                                    _lastClickX = x;
                                    _lastClickY = y;
                                    _lastClickTick = now;
                                    _lastWasLeftClick = (_mouseDownButton == "LEFT");
                                }
                                _isMouseDown = false;
                            }
                        }
                        break;

                    case WM_MOUSEWHEEL:
                        short delta = (short)(ms.mouseData >> 16);
                        AppendCommand($"MOUSE_WHEEL : {delta} : {x} : {y}");
                        break;
                }
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        public static bool IsModifierKey(uint vk)
        {
            return vk == 0x10 || vk == 0xA0 || vk == 0xA1 || // Shift
                   vk == 0x11 || vk == 0xA2 || vk == 0xA3 || // Ctrl
                   vk == 0x12 || vk == 0xA4 || vk == 0xA5 || // Alt
                   vk == 0x5B || vk == 0x5C;                 // Win
        }

        public static string GetFriendlyKeyName(uint vk)
        {
            switch (vk)
            {
                case 0x10: case 0xA0: case 0xA1: return "SHIFT";
                case 0x11: case 0xA2: case 0xA3: return "CONTROL";
                case 0x12: case 0xA4: case 0xA5: return "ALT";
                case 0x5B: case 0x5C: return "WIN";
                case 0x0D: return "ENTER";
                case 0x08: return "BACKSPACE";
                case 0x09: return "TAB";
                case 0x20: return "SPACE";
                case 0x1B: return "ESC";
                case 0x14: return "CAPSLOCK";
                case 0x2E: return "DELETE";
                case 0x2D: return "INSERT";
                case 0x24: return "HOME";
                case 0x23: return "END";
                case 0x21: return "PAGEUP";
                case 0x22: return "PAGEDOWN";
                case 0x26: return "UP";
                case 0x28: return "DOWN";
                case 0x25: return "LEFT";
                case 0x27: return "RIGHT";
                case 0x2C: return "PRINTSCREEN";
                case 0x90: return "NUMLOCK";
                case 0x91: return "SCROLLLOCK";
                case 0x5D: return "APPS";
            }

            if (vk >= 0x70 && vk <= 0x7B) // F1 -> F12
            {
                return "F" + (vk - 0x70 + 1);
            }

            if (vk >= 0x30 && vk <= 0x39) // 0 - 9
            {
                return ((char)vk).ToString();
            }

            if (vk >= 0x41 && vk <= 0x5A) // A - Z
            {
                return ((char)vk).ToString();
            }

            if (vk >= 0x60 && vk <= 0x69) // Num 0 - Num 9
            {
                return "NUMPAD" + (vk - 0x60);
            }

            // Một số phím dấu thường gặp
            switch (vk)
            {
                case 0xBA: return "SEMICOLON";
                case 0xBB: return "PLUS";
                case 0xBC: return "COMMA";
                case 0xBD: return "MINUS";
                case 0xBE: return "PERIOD";
                case 0xBF: return "SLASH";
                case 0xC0: return "TILDE";
                case 0xDB: return "LBRACKET";
                case 0xDC: return "BACKSLASH";
                case 0xDD: return "RBRACKET";
                case 0xDE: return "QUOTE";
            }

            return "VK_" + vk.ToString("X2");
        }
    }
}
