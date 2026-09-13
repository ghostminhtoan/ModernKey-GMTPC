using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ModernKey.Hook
{
    public static class KeySender
    {
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const byte VK_BACK = 0x08;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        private const uint CF_TEXT = 1;
        private const uint CF_BITMAP = 2;
        private const uint CF_DIB = 8;
        private const uint CF_UNICODETEXT = 13;
        private const uint CF_DIBV5 = 17;

        private const uint GMEM_MOVEABLE = 0x0002;

        public const uint WM_PASTE = 0x0302;
        public const uint SCI_PASTE = 2179; // Scintilla (Notepad++, Code Editor) Direct Paste

        public static readonly IntPtr INJECTED_SIGNATURE = (IntPtr)0x4D4F444B; // "MODK"

        #region Win32 P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #endregion

        #region Native Clipboard Snapshot & Exclude Formats

        private static uint _cfExclude1 = 0;
        private static uint _cfExclude2 = 0;
        private static uint _cfExclude3 = 0;
        private static uint _cfExclude4 = 0;

        private static void EnsureExcludeFormats()
        {
            if (_cfExclude1 == 0)
            {
                try
                {
                    _cfExclude1 = RegisterClipboardFormat("CanIncludeInClipboardHistory");
                    _cfExclude2 = RegisterClipboardFormat("CanUploadToCloudStore");
                    _cfExclude3 = RegisterClipboardFormat("Clipboard Viewer Ignore");
                    _cfExclude4 = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
                }
                catch { }
            }
        }

        private static void SetClipboardFlag(uint format)
        {
            if (format == 0) return;
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)sizeof(uint));
            if (hMem != IntPtr.Zero)
            {
                IntPtr ptr = GlobalLock(hMem);
                if (ptr != IntPtr.Zero)
                {
                    Marshal.WriteInt32(ptr, 0);
                    GlobalUnlock(hMem);
                    if (SetClipboardData(format, hMem) == IntPtr.Zero)
                    {
                        GlobalFree(hMem);
                    }
                }
                else
                {
                    GlobalFree(hMem);
                }
            }
        }

        private sealed class ClipboardFormatItem
        {
            public uint Format;
            public byte[] Data;
        }

        private static readonly object _clipLock = new object();
        private static List<ClipboardFormatItem> _activeBackupSnapshot = null;
        private static bool _hasActiveBackup = false;
        private static bool _activeBackupHadImage = false;
        private static Timer _restoreTimer = null;

        public static volatile bool SuppressClipboardMonitoring = false;
        public static bool IsInternalClipboardActive => _hasActiveBackup;

        private static List<ClipboardFormatItem> NativeBackupClipboard(out bool hasImage)
        {
            EnsureExcludeFormats();
            hasImage = false;
            var list = new List<ClipboardFormatItem>();

            for (int retry = 0; retry < 10; retry++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        uint fmt = 0;
                        while ((fmt = EnumClipboardFormats(fmt)) != 0)
                        {
                            if (fmt == CF_DIB || fmt == CF_DIBV5 || fmt == CF_BITMAP)
                            {
                                hasImage = true;
                            }

                            // Bỏ qua các cờ Exclude Clipboard Monitor
                            if (fmt == _cfExclude1 || fmt == _cfExclude2 || fmt == _cfExclude3 || fmt == _cfExclude4)
                                continue;

                            // Bỏ qua các GDI handle (không phải HGLOBAL byte stream)
                            if (fmt == 2 || fmt == 3 || fmt == 9 || fmt == 14)
                                continue;

                            IntPtr hData = GetClipboardData(fmt);
                            if (hData == IntPtr.Zero) continue;

                            UIntPtr size = GlobalSize(hData);
                            if (size == UIntPtr.Zero) continue;

                            IntPtr ptr = GlobalLock(hData);
                            if (ptr == IntPtr.Zero) continue;

                            try
                            {
                                int len = (int)size;
                                byte[] buf = new byte[len];
                                Marshal.Copy(ptr, buf, 0, len);
                                list.Add(new ClipboardFormatItem { Format = fmt, Data = buf });
                            }
                            finally
                            {
                                GlobalUnlock(hData);
                            }
                        }
                        return list;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(5);
            }

            return list;
        }

        private static bool NativeSetClipboardText(string text)
        {
            EnsureExcludeFormats();

            for (int retry = 0; retry < 15; retry++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();

                        byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
                        IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                        if (hMem != IntPtr.Zero)
                        {
                            IntPtr ptr = GlobalLock(hMem);
                            if (ptr != IntPtr.Zero)
                            {
                                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                                GlobalUnlock(hMem);
                                if (SetClipboardData(CF_UNICODETEXT, hMem) != IntPtr.Zero)
                                {
                                    // Đặt các cờ loại trừ để Comfort Clipboard, Ditto, Windows 10/11 Clipboard History
                                    // không mở/khóa clipboard và không lưu rác vào lịch sử
                                    SetClipboardFlag(_cfExclude1);
                                    SetClipboardFlag(_cfExclude2);
                                    SetClipboardFlag(_cfExclude3);
                                    SetClipboardFlag(_cfExclude4);
                                    return true;
                                }
                            }
                            GlobalFree(hMem);
                        }
                        return false;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(5);
            }
            return false;
        }

        private static string NativeGetClipboardText()
        {
            for (int retry = 0; retry < 5; retry++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr ptr = GlobalLock(hData);
                            if (ptr != IntPtr.Zero)
                            {
                                try
                                {
                                    return Marshal.PtrToStringUni(ptr);
                                }
                                finally
                                {
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                        return null;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(5);
            }
            return null;
        }

        private static bool NativeRestoreClipboard(List<ClipboardFormatItem> items)
        {
            for (int retry = 0; retry < 15; retry++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();

                        if (items != null && items.Count > 0)
                        {
                            foreach (var item in items)
                            {
                                IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)item.Data.Length);
                                if (hMem != IntPtr.Zero)
                                {
                                    IntPtr ptr = GlobalLock(hMem);
                                    if (ptr != IntPtr.Zero)
                                    {
                                        Marshal.Copy(item.Data, 0, ptr, item.Data.Length);
                                        GlobalUnlock(hMem);
                                        if (SetClipboardData(item.Format, hMem) == IntPtr.Zero)
                                        {
                                            GlobalFree(hMem);
                                        }
                                    }
                                    else
                                    {
                                        GlobalFree(hMem);
                                    }
                                }
                            }
                        }
                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(10);
            }
            return false;
        }

        #endregion

        public static void SendBackspaces(int count)
        {
            if (count <= 0) return;

            INPUT[] inputs = new INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                inputs[i * 2] = CreateKeyInput(VK_BACK, 0);
                inputs[i * 2 + 1] = CreateKeyInput(VK_BACK, KEYEVENTF_KEYUP);
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Giải phóng toàn bộ phím Modifier (Shift, Ctrl, Alt) chuẩn OpenKey C++.
        /// Tránh triệt để việc dán phím bị biến thành Ctrl+Shift+V khi kích hoạt bằng Double Shift.
        /// </summary>
        public static void ReleaseAllModifiers()
        {
            INPUT[] cleanModifiers = new INPUT[7];
            cleanModifiers[0] = CreateKeyInput(0xA0, KEYEVENTF_KEYUP); // VK_LSHIFT
            cleanModifiers[1] = CreateKeyInput(0xA1, KEYEVENTF_KEYUP); // VK_RSHIFT
            cleanModifiers[2] = CreateKeyInput(0x10, KEYEVENTF_KEYUP); // VK_SHIFT
            cleanModifiers[3] = CreateKeyInput(0xA2, KEYEVENTF_KEYUP); // VK_LCONTROL
            cleanModifiers[4] = CreateKeyInput(0xA3, KEYEVENTF_KEYUP); // VK_RCONTROL
            cleanModifiers[5] = CreateKeyInput(0xA4, KEYEVENTF_KEYUP); // VK_LMENU
            cleanModifiers[6] = CreateKeyInput(0xA5, KEYEVENTF_KEYUP); // VK_RMENU
            SendInput((uint)cleanModifiers.Length, cleanModifiers, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Gửi phím che mặt nạ (Mask Key 0xE8) chuẩn AutoHotkey / Microsoft PowerToys để triệt tiêu hoàn toàn
        /// việc kích hoạt Menu Bar / Start Menu khi dùng các tổ hợp phím có Alt / Win.
        /// Tuyệt đối KHÔNG gửi KEYUP giả lập của Alt khi phím Alt vật lý chưa nhả, để tránh kẹt phím!
        /// </summary>
        public static void SuppressAltMenuActivation()
        {
            SendMaskKey();
        }

        public static void SendMaskKey()
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = CreateKeyInput(0xE8, 0);
            inputs[1] = CreateKeyInput(0xE8, KEYEVENTF_KEYUP);
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendKeyCode(ushort vkCode)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0] = CreateKeyInput(vkCode, 0);
            inputs[1] = CreateKeyInput(vkCode, KEYEVENTF_KEYUP);
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Gửi phím thay thế nguyên tử (Atomic Replace):
        /// Kết hợp Hủy Autocomplete Selection + Backspaces + Ký tự mới trong 1 lệnh SendInput duy nhất.
        /// Tự động gửi phím vật lý thực sự (VK_SPACE / VK_RETURN) chuẩn OpenKey C++ để kích hoạt tìm kiếm hoặc vào web trên Omnibox trình duyệt.
        /// </summary>
        public static void SendReplaceText(int backspaceCount, string newString, bool fixBrowser, bool forceClipboard, int trailingVkCode = 0)
        {
            if (backspaceCount <= 0 && string.IsNullOrEmpty(newString) && trailingVkCode == 0) return;

            // Nếu chuỗi dài hoặc bắt buộc qua clipboard (Macro dài, emoji ngoài BMP...)
            if (!string.IsNullOrEmpty(newString) && ShouldUseClipboard(newString, forceClipboard))
            {
                if (backspaceCount > 0)
                {
                    SendBackspaces(backspaceCount);
                    Thread.Sleep(10);
                }
                SendViaClipboardPaste(newString);

                // Gửi phím vật lý thực sự (Space / Return) sau khi paste hoàn tất chuẩn OpenKey C++
                if (trailingVkCode != 0)
                {
                    ReleaseAllModifiers();
                    if (trailingVkCode == 0x0D) // VK_RETURN
                    {
                        Thread.Sleep(25);
                        SendKeyCode(0x0D);
                    }
                    else if (trailingVkCode == 0x20) // VK_SPACE
                    {
                        Thread.Sleep(10);
                        SendKeyCode(0x20);
                    }
                    else
                    {
                        Thread.Sleep(5);
                        SendKeyCode((ushort)trailingVkCode);
                    }
                }
                return;
            }

            // Gộp toàn bộ vào 1 mảng INPUT[] duy nhất
            int emptyCharCount = (fixBrowser && backspaceCount > 0) ? 1 : 0;
            int totalBs = backspaceCount + emptyCharCount;
            int textLen = string.IsNullOrEmpty(newString) ? 0 : newString.Length;
            int trailingInputs = (trailingVkCode != 0) ? 2 : 0;

            int totalInputs = (emptyCharCount * 2) + (totalBs * 2) + (textLen * 2) + trailingInputs;
            INPUT[] inputs = new INPUT[totalInputs];
            int idx = 0;

            // 1. Phá vỡ Autocomplete Selection trên Edge, Chrome, Run dialog bằng ký tự vô hình 0x202F (Narrow No-Break Space)
            if (emptyCharCount > 0)
            {
                inputs[idx++] = CreateUnicodeInput((char)0x202F, 0);
                inputs[idx++] = CreateUnicodeInput((char)0x202F, KEYEVENTF_KEYUP);
            }

            // 2. Gửi Backspace để xóa ký tự vô hình và xóa đúng số lượng ký tự cũ
            for (int i = 0; i < totalBs; i++)
            {
                inputs[idx++] = CreateKeyInput(VK_BACK, 0);
                inputs[idx++] = CreateKeyInput(VK_BACK, KEYEVENTF_KEYUP);
            }

            // 3. Gửi chuỗi ký tự Unicode mới
            if (textLen > 0)
            {
                for (int i = 0; i < textLen; i++)
                {
                    char ch = newString[i];
                    inputs[idx++] = CreateUnicodeInput(ch, 0);
                    inputs[idx++] = CreateUnicodeInput(ch, KEYEVENTF_KEYUP);
                }
            }

            // 4. Gửi phím vật lý thực sự (Space / Return) NGUYÊN TỬ trong cùng 1 đợt SendInput chuẩn OpenKey C++
            if (trailingVkCode != 0)
            {
                inputs[idx++] = CreateKeyInput((ushort)trailingVkCode, 0);
                inputs[idx++] = CreateKeyInput((ushort)trailingVkCode, KEYEVENTF_KEYUP);
            }

            // Đẩy vào Message Queue của Windows trong 1 lần gọi SendInput nguyên tử duy nhất (Zero-Latency, không Sleep)
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static bool ShouldUseClipboard(string text, bool forceClipboard)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (forceClipboard) return true;
            // Chỉ dùng clipboard khi có xuống dòng (multiline chuẩn OpenKey C++) hoặc văn bản rất dài (>= 60 ký tự)
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return true;
            if (text.Length >= 60) return true;

            // Ký tự ngoài BMP (như emoji 😏 U+1F60F, surrogate pair)
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsSurrogate(text[i])) return true;
            }
            return false;
        }

        public static void SendTextSmart(string text, bool forceClipboard)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (ShouldUseClipboard(text, forceClipboard))
            {
                SendViaClipboardPaste(text);
            }
            else
            {
                SendUnicodeString(text);
            }
        }

        public static void SendUnicodeString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            INPUT[] inputs = new INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                inputs[i * 2] = CreateUnicodeInput(ch, 0);
                inputs[i * 2 + 1] = CreateUnicodeInput(ch, KEYEVENTF_KEYUP);
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendViaClipboardPaste(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            bool hadImage = false;
            lock (_clipLock)
            {
                if (!_hasActiveBackup)
                {
                    _activeBackupSnapshot = NativeBackupClipboard(out _activeBackupHadImage);
                    _hasActiveBackup = true;
                }

                hadImage = _activeBackupHadImage;

                if (_restoreTimer != null)
                {
                    _restoreTimer.Dispose();
                    _restoreTimer = null;
                }
            }

            // 1. Gán chuỗi gõ tắt vào clipboard bằng native Win32 API kèm các cờ Exclude Clipboard Viewer
            bool setSuccess = NativeSetClipboardText(text);
            if (!setSuccess)
            {
                // Fallback nếu clipboard bị app khác lock hoàn toàn
                SendUnicodeString(text);
                return;
            }

            // 2. Dynamic wait: Đảm bảo clipboard kernel đã cập nhật đúng text macro trước khi paste (chuẩn OpenKey C++)
            for (int retry = 0; retry < 10; retry++)
            {
                string cur = NativeGetClipboardText();
                if (cur == text) break;
                Thread.Sleep(5);
            }

            // 3. Đảm bảo clipboard đang rảnh hoàn toàn (không bị ứng dụng clipboard manager ngoài nào mở khóa)
            for (int retry = 0; retry < 20; retry++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    CloseClipboard();
                    break;
                }
                Thread.Sleep(10);
            }

            // 4. Giải phóng triệt để toàn bộ modifier keys (LShift, RShift, Ctrl, Alt) chuẩn OpenKey C++
            // Tránh triệt để việc dán phím bị biến thành Ctrl+Shift+V khi kích hoạt bằng Double Shift
            ReleaseAllModifiers();

            // Đợi clipboard ổn định (đối với ảnh cần 60ms để Notepad++ và các listener cập nhật trạng thái Enable Paste)
            Thread.Sleep(hadImage ? 60 : 25);

            // 5. Gửi lệnh dán Ctrl+V chuẩn xác bằng SendInput (DUY NHẤT 1 LẦN, không gửi trùng lặp qua PostMessage)
            INPUT[] ctrlDown = new INPUT[] { CreateKeyInput(VK_CONTROL, 0) };
            SendInput(1, ctrlDown, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(8);

            INPUT[] vPress = new INPUT[]
            {
                CreateKeyInput(VK_V, 0),
                CreateKeyInput(VK_V, KEYEVENTF_KEYUP)
            };
            SendInput(2, vPress, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(8);

            INPUT[] ctrlUp = new INPUT[] { CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP) };
            SendInput(1, ctrlUp, Marshal.SizeOf(typeof(INPUT)));

            // 6. Lên lịch phục hồi lại clipboard ban đầu
            // Delay 800ms cho văn bản và 1500ms cho hình ảnh để đảm bảo ứng dụng đích hoàn tất paste trước khi khôi phục,
            // triệt tiêu hoàn toàn lỗi dán nhầm clipboard cũ ở lần gõ đầu tiên!
            int delayMs = hadImage ? 1500 : 800;

            lock (_clipLock)
            {
                _restoreTimer = new Timer(_ =>
                {
                    List<ClipboardFormatItem> toRestore = null;
                    bool hadBackup = false;

                    lock (_clipLock)
                    {
                        toRestore = _activeBackupSnapshot;
                        hadBackup = _hasActiveBackup;

                        _activeBackupSnapshot = null;
                        _hasActiveBackup = false;
                        _activeBackupHadImage = false;

                        if (_restoreTimer != null)
                        {
                            _restoreTimer.Dispose();
                            _restoreTimer = null;
                        }
                    }

                    if (hadBackup)
                    {
                        NativeRestoreClipboard(toRestore);
                    }
                }, null, delayMs, Timeout.Infinite);
            }
        }

        public static void SendCtrlVPaste()
        {
            ReleaseAllModifiers();
            Thread.Sleep(15);
            INPUT[] ctrlDown = new INPUT[] { CreateKeyInput(VK_CONTROL, 0) };
            SendInput(1, ctrlDown, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(8);

            INPUT[] vPress = new INPUT[]
            {
                CreateKeyInput(VK_V, 0),
                CreateKeyInput(VK_V, KEYEVENTF_KEYUP)
            };
            SendInput(2, vPress, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(8);

            INPUT[] ctrlUp = new INPUT[] { CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP) };
            SendInput(1, ctrlUp, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateKeyInput(ushort vk, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = INJECTED_SIGNATURE
                    }
                }
            };
        }

        private static INPUT CreateUnicodeInput(char ch, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)ch,
                        dwFlags = KEYEVENTF_UNICODE | flags,
                        time = 0,
                        dwExtraInfo = INJECTED_SIGNATURE
                    }
                }
            };
        }
    }
}
