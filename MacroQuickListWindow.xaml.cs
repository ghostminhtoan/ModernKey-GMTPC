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
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly MacroManager _macroManager;
        private readonly AppSettings _settings;
        private IntPtr _lastTargetHwnd = IntPtr.Zero;
        private bool _isDialogOpen = false;
        private List<MacroEntry> _currentFilteredList = new List<MacroEntry>();

        public MacroQuickListWindow(MacroManager macroManager, AppSettings settings)
        {
            InitializeComponent();
            _macroManager = macroManager;
            _settings = settings;

            RefreshMacroList();
        }

        public void ShowQuickList(IntPtr targetHwnd = default)
        {
            IntPtr myHandle = new WindowInteropHelper(this).Handle;
            _lastTargetHwnd = (targetHwnd != IntPtr.Zero && targetHwnd != myHandle)
                ? targetHwnd
                : GetForegroundWindow();

            if (_lastTargetHwnd == myHandle)
            {
                _lastTargetHwnd = IntPtr.Zero;
            }

            TxtSearch.Text = string.Empty;
            RefreshMacroList();

            Show();
            Activate();
            TxtSearch.Focus();
        }

        private void RefreshMacroList()
        {
            string query = TxtSearch?.Text?.Trim() ?? string.Empty;
            FilterList(query);
        }

        private void FilterList(string query)
        {
            if (_macroManager == null || _macroManager.MacroList == null) return;

            int totalCount = _macroManager.MacroList.Count;

            if (string.IsNullOrEmpty(query))
            {
                _currentFilteredList = _macroManager.MacroList.ToList();
            }
            else
            {
                _currentFilteredList = _macroManager.MacroList
                    .Where(m => (m.Shortcut != null && m.Shortcut.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (m.Replacement != null && m.Replacement.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(m =>
                    {
                        // 1. Khớp chính xác Shortcut lên đầu
                        if (string.Equals(m.Shortcut, query, StringComparison.OrdinalIgnoreCase)) return 0;
                        // 2. Bắt đầu bằng Shortcut
                        if (m.Shortcut != null && m.Shortcut.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
                        // 3. Chứa Shortcut
                        if (m.Shortcut != null && m.Shortcut.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
                        // 4. Còn lại (khớp nội dung Replacement)
                        return 3;
                    })
                    .ThenBy(m => m.Shortcut)
                    .ToList();
            }

            DgMacroList.ItemsSource = _currentFilteredList;

            if (TxtCount != null)
            {
                TxtCount.Text = $"{_currentFilteredList.Count} / {totalCount}";
            }

            if (_currentFilteredList.Count > 0)
            {
                DgMacroList.SelectedIndex = 0;
                DgMacroList.ScrollIntoView(_currentFilteredList[0]);
            }

            if (BtnClearSearch != null)
            {
                BtnClearSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private MacroEntry GetSelectedOrFirstEntry()
        {
            if (DgMacroList.SelectedItem is MacroEntry selected)
            {
                return selected;
            }
            if (_currentFilteredList.Count > 0)
            {
                return _currentFilteredList[0];
            }
            return null;
        }

        private void ExecutePaste()
        {
            var entry = GetSelectedOrFirstEntry();
            if (entry == null) return;

            string text = entry.Replacement ?? string.Empty;
            if (_settings != null && _settings.DynamicMacroEnabled)
            {
                text = MacroManager.ExpandDynamicPlaceholders(text);
            }

            IntPtr target = _lastTargetHwnd;
            Hide();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (target != IntPtr.Zero)
                {
                    SetForegroundWindow(target);
                    Thread.Sleep(50);
                }
                KeySender.SendViaClipboardPaste(text);
            });
        }

        private void ExecuteSequenceKey()
        {
            var entry = GetSelectedOrFirstEntry();
            if (entry == null) return;

            string text = entry.Replacement ?? string.Empty;
            if (_settings != null && _settings.DynamicMacroEnabled)
            {
                text = MacroManager.ExpandDynamicPlaceholders(text);
            }

            IntPtr target = _lastTargetHwnd;
            Hide();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (target != IntPtr.Zero)
                {
                    SetForegroundWindow(target);
                    Thread.Sleep(50);
                }
                KeySender.SendKeystrokeSequence(text);
            });
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            ExecutePaste();
        }

        private void BtnSequenceKey_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSequenceKey();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _isDialogOpen = true;
            try
            {
                string query = TxtSearch?.Text?.Trim();
                var dlg = new EditMacroDialog(initialShortcut: query, isEdit: false)
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
                        DgMacroList.SelectedItem = added;
                        DgMacroList.ScrollIntoView(added);
                    }
                }
            }
            finally
            {
                _isDialogOpen = false;
                TxtSearch?.Focus();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var entry = GetSelectedOrFirstEntry();
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
                        DgMacroList.SelectedItem = updated;
                        DgMacroList.ScrollIntoView(updated);
                    }
                }
            }
            finally
            {
                _isDialogOpen = false;
                TxtSearch?.Focus();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            TxtSearch.Focus();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshMacroList();
        }

        private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (_currentFilteredList.Count > 0)
                {
                    DgMacroList.Focus();
                    if (DgMacroList.SelectedIndex < 0)
                    {
                        DgMacroList.SelectedIndex = 0;
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    ExecuteSequenceKey();
                }
                else
                {
                    ExecutePaste();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void DgMacroList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up && DgMacroList.SelectedIndex == 0)
            {
                TxtSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    ExecuteSequenceKey();
                }
                else
                {
                    ExecutePaste();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void DgMacroList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecutePaste();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_isDialogOpen && IsVisible)
            {
                Hide();
            }
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
