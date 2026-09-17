using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ModernKey.Core;
using ModernKey.Hook;
using ModernKey.Models;

namespace ModernKey
{
    public partial class MacroQuickListWindow : Window
    {
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        public static MacroQuickListWindow Instance { get; private set; }
        public static bool IsQuickListOpen => Instance != null && Instance.IsVisible;

        private readonly MacroManager _macroManager;
        private readonly AppSettings _settings;
        private IntPtr _lastTargetHwnd = IntPtr.Zero;
        private IntPtr _lastFocusHwnd = IntPtr.Zero;
        private string _typedPrefix = string.Empty;
        private List<MacroEntry> _allMacros = new List<MacroEntry>();
        private List<MacroEntry> _currentFilteredList = new List<MacroEntry>();
        private bool _isDialogOpen = false;

        public MacroQuickListWindow(MacroManager macroManager, AppSettings settings)
        {
            InitializeComponent();
            Instance = this;
            _macroManager = macroManager;
            _settings = settings;

            RefreshMacroList();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        public void ShowQuickList(string initialKeyword = null, IntPtr targetHwnd = default)
        {
            IntPtr myHandle = new WindowInteropHelper(this).Handle;
            IntPtr fg = (targetHwnd != IntPtr.Zero && targetHwnd != myHandle)
                ? targetHwnd
                : GetForegroundWindow();

            if (fg != myHandle)
            {
                _lastTargetHwnd = fg;
                uint tid = GetWindowThreadProcessId(fg, out _);
                var gui = new GUITHREADINFO();
                gui.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                if (GetGUIThreadInfo(tid, ref gui) && gui.hwndFocus != IntPtr.Zero)
                {
                    _lastFocusHwnd = gui.hwndFocus;
                }
                else
                {
                    _lastFocusHwnd = fg;
                }
            }
            else
            {
                _lastTargetHwnd = IntPtr.Zero;
                _lastFocusHwnd = IntPtr.Zero;
            }

            _allMacros = _macroManager.MacroList.ToList();
            _typedPrefix = initialKeyword ?? string.Empty;
            FilterList(_typedPrefix);

            PositionAtCaret();
            Show();
        }

        private void PositionAtCaret()
        {
            double anchorPixelX = 0;
            double anchorPixelY = 0;
            double targetPixelX = 0;
            double targetPixelY = 0;
            bool hasCaret = false;

            try
            {
                IntPtr hForeground = GetForegroundWindow();
                if (hForeground != IntPtr.Zero)
                {
                    uint threadId = GetWindowThreadProcessId(hForeground, out _);
                    var gui = new GUITHREADINFO();
                    gui.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));

                    if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndCaret != IntPtr.Zero && (gui.rcCaret.Right - gui.rcCaret.Left >= 0))
                    {
                        var pt = new POINT { X = gui.rcCaret.Left, Y = gui.rcCaret.Bottom };
                        if (ClientToScreen(gui.hwndCaret, ref pt))
                        {
                            anchorPixelX = pt.X;
                            anchorPixelY = pt.Y;
                            targetPixelX = pt.X;
                            targetPixelY = pt.Y + 6;
                            hasCaret = true;
                        }
                    }
                }
            }
            catch { }

            if (!hasCaret)
            {
                var mouse = System.Windows.Forms.Cursor.Position;
                anchorPixelX = mouse.X;
                anchorPixelY = mouse.Y;
                targetPixelX = mouse.X + 8;
                targetPixelY = mouse.Y + 16;
            }

            ClampAndSetPosition(anchorPixelX, anchorPixelY, targetPixelX, targetPixelY, hasCaret);
        }

        private void ClampAndSetPosition(double anchorPixelX, double anchorPixelY, double targetPixelX, double targetPixelY, bool isCaret)
        {
            try
            {
                // Lấy hệ số co giãn DPI thực tế của cửa sổ
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                try
                {
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                }
                catch
                {
                    dpiScaleX = 1.0;
                    dpiScaleY = 1.0;
                }

                // Kích thước cửa sổ (DIPs)
                double w = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 560);
                double h = ActualHeight > 0 ? ActualHeight : (Height > 0 ? Height : 290);
                if (double.IsNaN(w) || w <= 0) w = 560;
                if (double.IsNaN(h) || h <= 0) h = 290;

                // Xác định màn hình chứa điểm neo để lấy WorkingArea (loại trừ Taskbar)
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)anchorPixelX, (int)anchorPixelY));
                var workArea = screen.WorkingArea;

                // Quy đổi WorkingArea sang đơn vị DIPs
                double workLeft = workArea.Left / dpiScaleX;
                double workTop = workArea.Top / dpiScaleY;
                double workRight = workArea.Right / dpiScaleX;
                double workBottom = workArea.Bottom / dpiScaleY;

                // Quy đổi tọa độ mục tiêu sang DIPs
                double x = targetPixelX / dpiScaleX;
                double y = targetPixelY / dpiScaleY;

                const double margin = 10;

                // Xử lý chống tràn ngang: nếu tràn mép phải, lật sang bên trái điểm neo
                if (x + w > workRight - margin)
                {
                    double flippedX = (anchorPixelX / dpiScaleX) - w - (isCaret ? 4 : 8);
                    if (flippedX >= workLeft + margin)
                    {
                        x = flippedX;
                    }
                    else
                    {
                        x = workRight - w - margin;
                    }
                }

                // Đảm bảo không bao giờ tràn mép phải hoặc mép trái
                if (x + w > workRight - margin)
                {
                    x = workRight - w - margin;
                }
                if (x < workLeft + margin)
                {
                    x = workLeft + margin;
                }

                // Xử lý chống tràn dọc: nếu tràn mép dưới (che Taskbar/rơi khỏi màn hình), lật lên trên điểm neo
                if (y + h > workBottom - margin)
                {
                    double flippedY = (anchorPixelY / dpiScaleY) - h - (isCaret ? 6 : 8);
                    if (flippedY >= workTop + margin)
                    {
                        y = flippedY;
                    }
                    else
                    {
                        y = workBottom - h - margin;
                    }
                }

                // Đảm bảo không bao giờ tràn mép dưới hoặc mép trên
                if (y + h > workBottom - margin)
                {
                    y = workBottom - h - margin;
                }
                if (y < workTop + margin)
                {
                    y = workTop + margin;
                }

                Left = x;
                Top = y;
            }
            catch
            {
                // Fallback an toàn dùng SystemParameters.WorkArea
                var wa = SystemParameters.WorkArea;
                double w = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 560);
                double h = ActualHeight > 0 ? ActualHeight : (Height > 0 ? Height : 290);
                Left = Math.Max(wa.Left + 10, Math.Min(targetPixelX, wa.Right - w - 10));
                Top = Math.Max(wa.Top + 10, Math.Min(targetPixelY, wa.Bottom - h - 10));
            }
        }

        private void RefreshMacroList()
        {
            if (_macroManager != null && _macroManager.MacroList != null)
            {
                _allMacros = _macroManager.MacroList.ToList();
            }
            FilterList(_typedPrefix);
        }

        private void FilterList(string query)
        {
            if (_allMacros == null) return;
            int totalCount = _allMacros.Count;

            if (string.IsNullOrEmpty(query))
            {
                _currentFilteredList = _allMacros;
            }
            else
            {
                _currentFilteredList = _allMacros
                    .Where(m => !string.IsNullOrEmpty(m.Shortcut) && m.Shortcut.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(m =>
                    {
                        if (string.Equals(m.Shortcut, query, StringComparison.OrdinalIgnoreCase)) return 0;
                        if (m.Shortcut.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
                        return 2;
                    })
                    .ThenBy(m => m.Shortcut)
                    .ToList();
            }

            LstShortcuts.ItemsSource = _currentFilteredList;

            if (TxtCount != null)
            {
                TxtCount.Text = $"{_currentFilteredList.Count} / {totalCount}";
            }

            if (_currentFilteredList.Count > 0)
            {
                LstShortcuts.SelectedIndex = 0;
                LstShortcuts.ScrollIntoView(_currentFilteredList[0]);
            }
        }

        public void HandleCharTyped(char ch)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible) return;
                _typedPrefix += ch;
                FilterList(_typedPrefix);
                if (_currentFilteredList.Count == 0)
                {
                    CloseQuickList();
                }
            }));
        }

        public void HandleBackspace()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible) return;
                if (_typedPrefix.Length > 0)
                {
                    _typedPrefix = _typedPrefix.Substring(0, _typedPrefix.Length - 1);
                    FilterList(_typedPrefix);
                }
                else
                {
                    FilterList(string.Empty);
                }
            }));
        }

        public void SelectNext()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || _currentFilteredList == null || _currentFilteredList.Count == 0) return;
                int next = LstShortcuts.SelectedIndex + 1;
                if (next < _currentFilteredList.Count)
                {
                    LstShortcuts.SelectedIndex = next;
                    LstShortcuts.ScrollIntoView(LstShortcuts.SelectedItem);
                }
            }));
        }

        public void SelectPrevious()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || _currentFilteredList == null || _currentFilteredList.Count == 0) return;
                int prev = LstShortcuts.SelectedIndex - 1;
                if (prev >= 0)
                {
                    LstShortcuts.SelectedIndex = prev;
                    LstShortcuts.ScrollIntoView(LstShortcuts.SelectedItem);
                }
            }));
        }

        public void SelectNextPage()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || _currentFilteredList == null || _currentFilteredList.Count == 0) return;
                int next = Math.Min(LstShortcuts.SelectedIndex + 8, _currentFilteredList.Count - 1);
                LstShortcuts.SelectedIndex = next;
                LstShortcuts.ScrollIntoView(LstShortcuts.SelectedItem);
            }));
        }

        public void SelectPreviousPage()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || _currentFilteredList == null || _currentFilteredList.Count == 0) return;
                int prev = Math.Max(LstShortcuts.SelectedIndex - 8, 0);
                LstShortcuts.SelectedIndex = prev;
                LstShortcuts.ScrollIntoView(LstShortcuts.SelectedItem);
            }));
        }

        public void CloseQuickList()
        {
            if (_isDialogOpen) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _typedPrefix = string.Empty;
                Hide();
            }));
        }

        public bool IsPointInside(int screenX, int screenY)
        {
            bool inside = false;
            Dispatcher.Invoke(new Action(() =>
            {
                if (!IsVisible) return;
                inside = screenX >= Left && screenX <= Left + ActualWidth &&
                         screenY >= Top && screenY <= Top + ActualHeight;
            }));
            return inside;
        }

        public void ExecutePaste()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var entry = LstShortcuts.SelectedItem as MacroEntry;
                if (entry == null && _currentFilteredList.Count > 0)
                {
                    entry = _currentFilteredList[0];
                }
                if (entry == null)
                {
                    CloseQuickList();
                    return;
                }

                string text = entry.Replacement ?? string.Empty;
                if (_settings != null && _settings.DynamicMacroEnabled)
                {
                    text = MacroManager.ExpandDynamicPlaceholders(text);
                }

                int charsToDelete = _typedPrefix.Length;
                IntPtr target = _lastTargetHwnd;
                IntPtr targetFocus = _lastFocusHwnd;
                bool forceClip = _settings != null && _settings.SendViaClipboard;

                CloseQuickList();

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (target != IntPtr.Zero)
                    {
                        SetForegroundWindow(target);
                        Thread.Sleep(20);
                    }
                    if (charsToDelete > 0)
                    {
                        KeySender.SendBackspaces(charsToDelete);
                        Thread.Sleep(10);
                    }

                    // Nếu macro 1 dòng thông thường (không có multiline newline), gửi trực tiếp bằng SendUnicodeString
                    // Đảm bảo không delay, không lag, không phụ thuộc clipboard, tương thích 100% Notepad++ và mọi app
                    if (!KeySender.ShouldUseClipboard(text, forceClip))
                    {
                        KeySender.SendUnicodeString(text);
                    }
                    else
                    {
                        KeySender.SendViaClipboardPaste(text, targetFocus != IntPtr.Zero ? targetFocus : target);
                    }
                });
            }));
        }

        public void ExecuteSequenceKey()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var entry = LstShortcuts.SelectedItem as MacroEntry;
                if (entry == null && _currentFilteredList.Count > 0)
                {
                    entry = _currentFilteredList[0];
                }
                if (entry == null)
                {
                    CloseQuickList();
                    return;
                }

                string text = entry.Replacement ?? string.Empty;
                if (_settings != null && _settings.DynamicMacroEnabled)
                {
                    text = MacroManager.ExpandDynamicPlaceholders(text);
                }

                int charsToDelete = _typedPrefix.Length;
                IntPtr target = _lastTargetHwnd;

                CloseQuickList();

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (target != IntPtr.Zero)
                    {
                        SetForegroundWindow(target);
                        Thread.Sleep(20);
                    }
                    if (charsToDelete > 0)
                    {
                        KeySender.SendBackspaces(charsToDelete);
                        Thread.Sleep(10);
                    }
                    KeySender.SendKeystrokeSequence(text);
                });
            }));
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            ExecutePaste();
        }

        private void BtnSequenceKey_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSequenceKey();
        }

        private void LstShortcuts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecutePaste();
        }

        private void LstShortcuts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TxtPreview automatically updates via ElementName binding in XAML
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _isDialogOpen = true;
            try
            {
                var dlg = new EditMacroDialog(initialShortcut: _typedPrefix, isEdit: false)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    _macroManager.AddOrUpdate(dlg.Shortcut, dlg.Replacement);
                    _macroManager.Save();
                    RefreshMacroList();

                    var added = _currentFilteredList.FirstOrDefault(x => string.Equals(x.Shortcut, dlg.Shortcut, StringComparison.OrdinalIgnoreCase));
                    if (added != null)
                    {
                        LstShortcuts.SelectedItem = added;
                        LstShortcuts.ScrollIntoView(added);
                    }
                }
            }
            finally
            {
                _isDialogOpen = false;
                if (_lastTargetHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(_lastTargetHwnd);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstShortcuts.SelectedItem as MacroEntry;
            if (entry == null && _currentFilteredList.Count > 0) entry = _currentFilteredList[0];
            if (entry == null) return;

            _isDialogOpen = true;
            try
            {
                string oldShortcut = entry.Shortcut;
                var dlg = new EditMacroDialog(entry.Shortcut, entry.Replacement, isEdit: true)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    if (!string.Equals(oldShortcut, dlg.Shortcut, StringComparison.OrdinalIgnoreCase))
                    {
                        _macroManager.Remove(oldShortcut);
                    }
                    _macroManager.AddOrUpdate(dlg.Shortcut, dlg.Replacement);
                    _macroManager.Save();
                    RefreshMacroList();

                    var updated = _currentFilteredList.FirstOrDefault(x => string.Equals(x.Shortcut, dlg.Shortcut, StringComparison.OrdinalIgnoreCase));
                    if (updated != null)
                    {
                        LstShortcuts.SelectedItem = updated;
                        LstShortcuts.ScrollIntoView(updated);
                    }
                }
            }
            finally
            {
                _isDialogOpen = false;
                if (_lastTargetHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(_lastTargetHwnd);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseQuickList();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}

