using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ModernKey.Core;
using ModernKey.Models;

namespace ModernKey.Hook
{
    public class ClipboardListener : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int HOTKEY_ID_WIN_INS = 0xCB01;
        private const int HOTKEY_ID_CTRL_ALT_V = 0xCB02;

        private const uint CF_BITMAP = 2;
        private const uint CF_DIB = 8;
        private const uint CF_UNICODETEXT = 13;
        private const uint CF_HDROP = 15;
        private const uint CF_DIBV5 = 17;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly ClipboardHistoryManager _historyManager;
        private readonly AppSettings _settings;
        private HwndSource _hwndSource;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isDisposed = false;

        private uint _cfExclude1 = 0; // CanIncludeInClipboardHistory
        private uint _cfExclude3 = 0; // Clipboard Viewer Ignore
        private uint _cfExclude4 = 0; // ExcludeClipboardContentFromMonitorProcessing
        private uint _cfPng = 0;

        public ClipboardListener(ClipboardHistoryManager historyManager, AppSettings settings)
        {
            _historyManager = historyManager;
            _settings = settings;

            try
            {
                _cfExclude1 = RegisterClipboardFormat("CanIncludeInClipboardHistory");
                _cfExclude3 = RegisterClipboardFormat("Clipboard Viewer Ignore");
                _cfExclude4 = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
                _cfPng = RegisterClipboardFormat("PNG");
            }
            catch { }

            // Khởi tạo cửa sổ ẩn trên luồng giao diện STA để nhận thông điệp WM_CLIPBOARDUPDATE
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var parameters = new HwndSourceParameters("ModernKey_ClipboardListener_HUD")
                    {
                        Width = 0,
                        Height = 0,
                        PositionX = -32000,
                        PositionY = -32000,
                        WindowStyle = unchecked((int)0x80000000) // WS_POPUP
                    };

                    _hwndSource = new HwndSource(parameters);
                    _hwndSource.AddHook(HwndHook);
                    _hwnd = _hwndSource.Handle;

                    if (_hwnd != IntPtr.Zero)
                    {
                        AddClipboardFormatListener(_hwnd);

                        // Đăng ký phím tắt cấp OS: Win+Insert và Ctrl+Alt+V
                        try
                        {
                            RegisterHotKey(_hwnd, HOTKEY_ID_WIN_INS, MOD_WIN | MOD_NOREPEAT, 0x2D);
                            RegisterHotKey(_hwnd, HOTKEY_ID_CTRL_ALT_V, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x56);
                        }
                        catch { }
                    }
                });
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdate();
                handled = true;
            }
            else if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_WIN_INS || id == HOTKEY_ID_CTRL_ALT_V)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (Application.Current is App app)
                        {
                            app.ShowClipboardWindow();
                        }
                    }));
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void OnClipboardUpdate()
        {
            if (_settings == null || !_settings.EnableClipboardHistory) return;
            if (KeySender.SuppressClipboardMonitoring) return;
            if (KeySender.IsInternalClipboardActive) return;

            IntPtr fgHwnd = GetForegroundWindow();

            // Xử lý đọc dữ liệu bất đồng bộ ngắn trên luồng nền để không bao giờ làm đơ UI hoặc hook bàn phím
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // Delay 30ms để các công cụ chụp màn hình (Snipping Tool, Snip & Sketch) ghi xong dữ liệu vào kernel
                Thread.Sleep(30);

                if (KeySender.SuppressClipboardMonitoring || KeySender.IsInternalClipboardActive) return;

                var appInfo = GetActiveAppInfo(fgHwnd);

                bool hasExcludeFlag = false;
                bool hasFileDrop = false;
                bool hasUnicodeText = false;
                bool hasImage = false;
                string textContent = null;

                for (int retry = 0; retry < 12; retry++)
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        try
                        {
                            uint fmt = 0;
                            while ((fmt = EnumClipboardFormats(fmt)) != 0)
                            {
                                // 1. Bỏ qua nếu có cờ yêu cầu loại trừ rõ ràng từ phần mềm khác hoặc macro injection
                                if (fmt == _cfExclude3 || fmt == _cfExclude4)
                                {
                                    hasExcludeFlag = true;
                                    break;
                                }

                                // 2. CanIncludeInClipboardHistory: CHỈ loại trừ khi giá trị DWORD bên trong bằng 0!
                                // Khi chụp ảnh qua Windows Snipping Tool (Win+Shift+S), giá trị là 1 (nghĩa là cho phép lưu lịch sử)
                                if (fmt == _cfExclude1)
                                {
                                    IntPtr hData = GetClipboardData(_cfExclude1);
                                    if (hData != IntPtr.Zero)
                                    {
                                        IntPtr ptr = GlobalLock(hData);
                                        if (ptr != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                int val = Marshal.ReadInt32(ptr);
                                                if (val == 0)
                                                {
                                                    hasExcludeFlag = true;
                                                    break;
                                                }
                                            }
                                            finally
                                            {
                                                GlobalUnlock(hData);
                                            }
                                        }
                                    }
                                }

                                if (fmt == CF_HDROP)
                                {
                                    hasFileDrop = true;
                                }
                                else if (fmt == CF_DIB || fmt == CF_DIBV5 || fmt == CF_BITMAP || (fmt == _cfPng && _cfPng != 0))
                                {
                                    hasImage = true;
                                }
                                else if (fmt == CF_UNICODETEXT)
                                {
                                    hasUnicodeText = true;
                                }
                            }

                            if (!hasExcludeFlag && !hasFileDrop && !hasImage && hasUnicodeText)
                            {
                                IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                                if (hData != IntPtr.Zero)
                                {
                                    IntPtr ptr = GlobalLock(hData);
                                    if (ptr != IntPtr.Zero)
                                    {
                                        try
                                        {
                                            textContent = Marshal.PtrToStringUni(ptr);
                                        }
                                        finally
                                        {
                                            GlobalUnlock(hData);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Clipboard inspect error: " + ex.Message);
                        }
                        finally
                        {
                            CloseClipboard();
                        }
                        break;
                    }
                    Thread.Sleep(20);
                }

                if (hasExcludeFlag) return;

                // 1. Nếu là Tệp tin / Thư mục (File / Folder Drop) -> Ưu tiên cao nhất
                if (hasFileDrop)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            System.Collections.Specialized.StringCollection files = null;
                            if (System.Windows.Forms.Clipboard.ContainsFileDropList())
                            {
                                files = System.Windows.Forms.Clipboard.GetFileDropList();
                            }
                            else if (Clipboard.ContainsFileDropList())
                            {
                                files = Clipboard.GetFileDropList();
                            }

                            if (files != null && files.Count > 0)
                            {
                                var fileList = new System.Collections.Generic.List<string>();
                                foreach (var f in files)
                                {
                                    if (!string.IsNullOrEmpty(f)) fileList.Add(f);
                                }

                                if (fileList.Count > 0)
                                {
                                    int dropEffect = 1; // 1 = Copy, 2 = Move/Cut
                                    try
                                    {
                                        if (System.Windows.Forms.Clipboard.ContainsData("Preferred DropEffect"))
                                        {
                                            var effectObj = System.Windows.Forms.Clipboard.GetData("Preferred DropEffect");
                                            if (effectObj is MemoryStream ms)
                                            {
                                                byte[] bytes = ms.ToArray();
                                                if (bytes.Length >= 4)
                                                {
                                                    dropEffect = BitConverter.ToInt32(bytes, 0);
                                                }
                                            }
                                        }
                                        else if (Clipboard.ContainsData("Preferred DropEffect"))
                                        {
                                            var effectObj = Clipboard.GetData("Preferred DropEffect");
                                            if (effectObj is MemoryStream ms)
                                            {
                                                byte[] bytes = ms.ToArray();
                                                if (bytes.Length >= 4)
                                                {
                                                    dropEffect = BitConverter.ToInt32(bytes, 0);
                                                }
                                            }
                                        }
                                    }
                                    catch { }

                                    _historyManager.AddFiles(fileList, dropEffect, appInfo.AppName, appInfo.IconPath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error reading file drop list: " + ex.Message);
                        }
                    }));
                    return;
                }

                // 2. Nếu có Hình ảnh (Image / Screenshot) -> Đọc qua WinForms GDI+ OLE để tương thích mọi định dạng DIB/PNG
                if (hasImage)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        System.Drawing.Image gdiImg = null;
                        for (int retry = 0; retry < 10; retry++)
                        {
                            try
                            {
                                if (System.Windows.Forms.Clipboard.ContainsImage())
                                {
                                    gdiImg = System.Windows.Forms.Clipboard.GetImage();
                                    if (gdiImg != null) break;
                                }
                                if (System.Windows.Forms.Clipboard.ContainsData("PNG"))
                                {
                                    var data = System.Windows.Forms.Clipboard.GetData("PNG");
                                    if (data is Stream stream)
                                    {
                                        gdiImg = System.Drawing.Image.FromStream(stream);
                                        if (gdiImg != null) break;
                                    }
                                }
                            }
                            catch { }
                            Thread.Sleep(25);
                        }

                        if (gdiImg != null)
                        {
                            try
                            {
                                using (gdiImg)
                                using (var ms = new MemoryStream())
                                {
                                    gdiImg.Save(ms, ImageFormat.Png);
                                    byte[] pngBytes = ms.ToArray();
                                    _historyManager.AddImage(pngBytes, gdiImg.Width, gdiImg.Height, appInfo.AppName, appInfo.IconPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error saving GDI screenshot: " + ex.Message);
                            }
                            return;
                        }

                        // Fallback sang WPF Clipboard nếu GDI không bắt được
                        try
                        {
                            if (Clipboard.ContainsImage())
                            {
                                var wpfImg = Clipboard.GetImage();
                                if (wpfImg != null)
                                {
                                    var encoder = new PngBitmapEncoder();
                                    encoder.Frames.Add(BitmapFrame.Create(wpfImg));
                                    using (var ms = new MemoryStream())
                                    {
                                        encoder.Save(ms);
                                        byte[] bytes = ms.ToArray();
                                        _historyManager.AddImage(bytes, wpfImg.PixelWidth, wpfImg.PixelHeight, appInfo.AppName, appInfo.IconPath);
                                    }
                                }
                            }
                        }
                        catch { }
                    }));
                    return;
                }

                // 2. Nếu là Văn bản (Text)
                if (!string.IsNullOrEmpty(textContent))
                {
                    _historyManager.AddText(textContent, appInfo.AppName, appInfo.IconPath);
                }
            });
        }

        private struct SourceAppInfo
        {
            public string AppName;
            public string IconPath;
        }

        private SourceAppInfo GetActiveAppInfo(IntPtr fgHwnd)
        {
            if (fgHwnd == IntPtr.Zero) return default;

            try
            {
                GetWindowThreadProcessId(fgHwnd, out uint pid);
                if (pid == 0 || pid == (uint)Process.GetCurrentProcess().Id) return default;

                string procName = null;
                try
                {
                    using (var proc = Process.GetProcessById((int)pid))
                    {
                        procName = proc.ProcessName;
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(procName)) return default;

                string iconDir = Path.Combine(_historyManager.GetCacheDirectory(), "icons");
                if (!Directory.Exists(iconDir))
                {
                    try { Directory.CreateDirectory(iconDir); } catch { }
                }

                string safeName = string.Join("_", procName.Split(Path.GetInvalidFileNameChars())).ToLowerInvariant();
                string iconPath = Path.Combine(iconDir, safeName + ".png");

                if (!File.Exists(iconPath))
                {
                    string exePath = null;
                    IntPtr hProc = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            var sb = new StringBuilder(1024);
                            int size = sb.Capacity;
                            if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                            {
                                exePath = sb.ToString();
                            }
                        }
                        finally
                        {
                            CloseHandle(hProc);
                        }
                    }

                    if (string.IsNullOrEmpty(exePath))
                    {
                        try
                        {
                            using (var proc = Process.GetProcessById((int)pid))
                            {
                                exePath = proc.MainModule?.FileName;
                            }
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        try
                        {
                            using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                            {
                                if (sysIcon != null)
                                {
                                    using (var bmp = new System.Drawing.Bitmap(16, 16))
                                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                                    {
                                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                        g.DrawIcon(sysIcon, new System.Drawing.Rectangle(0, 0, 16, 16));
                                        bmp.Save(iconPath, System.Drawing.Imaging.ImageFormat.Png);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                return new SourceAppInfo
                {
                    AppName = procName,
                    IconPath = File.Exists(iconPath) ? iconPath : null
                };
            }
            catch
            {
                return default;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_hwnd != IntPtr.Zero)
            {
                try
                {
                    UnregisterHotKey(_hwnd, HOTKEY_ID_WIN_INS);
                    UnregisterHotKey(_hwnd, HOTKEY_ID_CTRL_ALT_V);
                    RemoveClipboardFormatListener(_hwnd);
                }
                catch { }
                _hwnd = IntPtr.Zero;
            }

            if (_hwndSource != null)
            {
                try
                {
                    _hwndSource.RemoveHook(HwndHook);
                    _hwndSource.Dispose();
                }
                catch { }
                _hwndSource = null;
            }
        }
    }
}
