using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ModernKey.Core;
using ModernKey.Hook;
using ModernKey.Models;

namespace ModernKey
{
    public partial class ClipboardWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly ClipboardHistoryManager _historyManager;
        private readonly AppSettings _settings;
        private IntPtr _lastTargetHwnd = IntPtr.Zero;
        private ICollectionView _itemsView;
        private string _activeFilter = "ALL";
        private string _currentMode = "HISTORY";
        public string CurrentMode => _currentMode;
        private string _activeGroupFilter = null;
        private bool _isSortDescending = true;
        private string _lastActiveItemId = null;
        private readonly DateTime _sessionStartTime = DateTime.Now;

        private DateTime _showTime = DateTime.MinValue;

        public ClipboardWindow(ClipboardHistoryManager historyManager, AppSettings settings)
        {
            _historyManager = historyManager;
            _settings = settings;
            InitializeComponent();

            InitCollectionView();

            if (_historyManager != null)
            {
                _historyManager.Items.CollectionChanged += HistoryItems_CollectionChanged;
                _historyManager.FavoriteItems.CollectionChanged += FavoriteItems_CollectionChanged;
            }

            Loaded += ClipboardWindow_Loaded;
            Deactivated += ClipboardWindow_Deactivated;
            if (TxtPreview != null)
            {
                TxtPreview.LostFocus += TxtPreview_LostFocus;
            }
        }

        private void InitCollectionView()
        {
            if (_historyManager == null) return;

            var targetCollection = _currentMode == "FAVORITES" ? _historyManager.FavoriteItems : _historyManager.Items;
            _itemsView = CollectionViewSource.GetDefaultView(targetCollection);

            if (_itemsView != null)
            {
                _itemsView.Filter = FilterClipboardItem;
                ApplySortOrder();
            }

            if (LstClipboard != null)
            {
                LstClipboard.ItemsSource = _itemsView;
            }
        }

        private void HistoryItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentMode == "HISTORY")
                {
                    if (LstClipboard != null && LstClipboard.ItemsSource != _itemsView)
                    {
                        InitCollectionView();
                    }
                    else
                    {
                        _itemsView?.Refresh();
                    }

                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
                    {
                        if (e.NewItems[0] is ClipboardItem newItem)
                        {
                            _lastActiveItemId = newItem.Id;
                        }
                    }

                    EnsureAppropriateSelection();
                }
            }));
        }

        private void FavoriteItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentMode == "FAVORITES")
                {
                    if (LstClipboard != null && LstClipboard.ItemsSource != _itemsView)
                    {
                        InitCollectionView();
                    }
                    else
                    {
                        _itemsView?.Refresh();
                    }

                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
                    {
                        if (e.NewItems[0] is ClipboardItem newItem)
                        {
                            _lastActiveItemId = newItem.Id;
                        }
                    }

                    EnsureAppropriateSelection();
                }
            }));
        }

        private void TxtPreview_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TxtPreview != null && !TxtPreview.IsReadOnly && LstClipboard.SelectedItem is ClipboardItem curItem && curItem.ContentType == ClipboardContentType.Text)
            {
                curItem.TextContent = TxtPreview.Text;
                curItem.CharCount = curItem.TextContent.Length;
                curItem.ByteSize = Encoding.UTF8.GetByteCount(curItem.TextContent);
                string p = curItem.TextContent.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
                curItem.PreviewText = p.Length > 120 ? p.Substring(0, 117) + "..." : p;
                TxtPreview.IsReadOnly = true;
                _historyManager?.SaveHistoryNow();
                _itemsView?.Refresh();
                if (TxtStatus != null)
                {
                    TxtStatus.Text = "✓ Đã lưu thay đổi nội dung văn bản!";
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void ClipboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMergeOptionsButtonLabel();
            if (LstClipboard != null && (LstClipboard.ItemsSource == null || LstClipboard.ItemsSource != _itemsView || _itemsView == null))
            {
                InitCollectionView();
            }
            else
            {
                _itemsView?.Refresh();
            }

            EnsureAppropriateSelection();
        }

        private void ClipboardWindow_Deactivated(object sender, EventArgs e)
        {
            // Tránh đóng nhầm trong 600ms đầu tiên khi vừa mở HUD (do chuyển đổi focus giữa các cửa sổ)
            if (DateTime.Now.Subtract(_showTime).TotalMilliseconds < 600) return;

            // Nếu người dùng chọn "Luôn nổi trên cùng (Always on top)", HUD không bị ẩn khi mất focus
            if (_settings != null && _settings.ClipboardAutoHide && !_settings.ClipboardAlwaysOnTop)
            {
                Hide();
            }
        }

        public void ShowHud(IntPtr targetHwnd, string targetMode = null)
        {
            _showTime = DateTime.Now;
            _lastTargetHwnd = (targetHwnd != IntPtr.Zero && targetHwnd != new System.Windows.Interop.WindowInteropHelper(this).Handle)
                ? targetHwnd
                : GetForegroundWindow();

            Topmost = _settings != null && _settings.ClipboardAlwaysOnTop;
            UpdateMergeOptionsButtonLabel();

            if (!string.IsNullOrEmpty(targetMode))
            {
                if (string.Equals(targetMode, "FAVORITES", StringComparison.OrdinalIgnoreCase))
                {
                    _currentMode = "FAVORITES";
                    if (RbModeFavorites != null) RbModeFavorites.IsChecked = true;
                    if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ YÊU THÍCH";
                }
                else if (string.Equals(targetMode, "HISTORY", StringComparison.OrdinalIgnoreCase))
                {
                    _currentMode = "HISTORY";
                    if (RbModeHistory != null) RbModeHistory.IsChecked = true;
                    if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ LỊCH SỬ";
                }
            }

            UpdateGroupFilterButtonLabel();

            if (LstClipboard != null && (LstClipboard.ItemsSource == null || LstClipboard.ItemsSource != _itemsView || _itemsView == null))
            {
                InitCollectionView();
            }
            else if (_itemsView != null)
            {
                _itemsView.Refresh();
            }

            EnsureAppropriateSelection();

            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
            TxtSearch?.Focus();
            TxtSearch?.SelectAll();
        }

        private void EnsureAppropriateSelection()
        {
            if (LstClipboard == null) return;
            ClipboardItem match = null;
            if (!string.IsNullOrEmpty(_lastActiveItemId) && LstClipboard.Items.Count > 0)
            {
                match = LstClipboard.Items.Cast<ClipboardItem>().FirstOrDefault(x => x.Id == _lastActiveItemId);
            }

            if (match != null)
            {
                LstClipboard.SelectedItem = match;
                LstClipboard.ScrollIntoView(match);
            }
            else if (LstClipboard.Items.Count > 0)
            {
                // BẤT KỂ ĐƯỢC SORT THEO CHIỀU NÀO, LUÔN LUÔN FOCUS THEO ITEM MỚI NHẤT
                var newest = LstClipboard.Items.Cast<ClipboardItem>().OrderByDescending(x => x.Timestamp).FirstOrDefault();
                LstClipboard.SelectedItem = newest;
                if (newest != null)
                {
                    LstClipboard.ScrollIntoView(newest);
                }
            }
            else
            {
                ClearPreview();
            }
        }

        private bool FilterClipboardItem(object obj)
        {
            if (!(obj is ClipboardItem item)) return false;

            // 1. Lọc theo tab danh mục & thời gian (Chuẩn Comfort Keys Pro)
            if (_activeFilter == "FAV" && !item.IsFavorite) return false;
            if (_activeFilter == "TXT" && item.ContentType != ClipboardContentType.Text) return false;
            if (_activeFilter == "IMG" && item.ContentType != ClipboardContentType.Image) return false;
            if (_activeFilter == "FILES" && item.ContentType != ClipboardContentType.Files) return false;

            if (_activeFilter == "SESSION" && item.Timestamp < _sessionStartTime) return false;
            if (_activeFilter == "TODAY" && item.Timestamp.Date != DateTime.Today) return false;
            if (_activeFilter == "WEEK" && item.Timestamp < DateTime.Now.AddDays(-7)) return false;
            if (_activeFilter == "MONTH" && (item.Timestamp.Year != DateTime.Now.Year || item.Timestamp.Month != DateTime.Now.Month)) return false;

            // 1.1. Lọc theo Group Yêu thích (Chuẩn Comfort Keys Pro)
            if (_currentMode == "FAVORITES" && !string.IsNullOrEmpty(_activeGroupFilter) && _activeGroupFilter != "-All-")
            {
                if (!string.Equals(item.GroupName ?? string.Empty, _activeGroupFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 2. Lọc theo từ khóa tìm kiếm
            string query = TxtSearch.Text?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                if (item.TextContent != null && item.TextContent.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (item.PreviewText != null && item.PreviewText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                return false;
            }

            return true;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LstClipboard == null) return;
            _itemsView?.Refresh();
            EnsureAppropriateSelection();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (TxtSearch != null)
            {
                TxtSearch.Text = string.Empty;
                TxtSearch.Focus();
            }
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (_historyManager == null) return;

            if (sender == RbModeFavorites)
            {
                _currentMode = "FAVORITES";
                if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ YÊU THÍCH";
            }
            else
            {
                _currentMode = "HISTORY";
                if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ LỊCH SỬ";
            }

            UpdateGroupFilterButtonLabel();
            InitCollectionView();
            EnsureAppropriateSelection();
        }

        public void UpdateGroupFilterButtonLabel()
        {
            if (TxtGroupFilterCurrentLabel == null) return;

            if (_currentMode != "FAVORITES")
            {
                if (BtnGroupFilterMenu != null) BtnGroupFilterMenu.Opacity = 0.5;
                TxtGroupFilterCurrentLabel.Text = "GROUP: -ALL-";
                return;
            }

            if (BtnGroupFilterMenu != null) BtnGroupFilterMenu.Opacity = 1.0;

            if (string.IsNullOrEmpty(_activeGroupFilter) || _activeGroupFilter == "-All-")
            {
                int total = _historyManager?.FavoriteItems?.Count ?? 0;
                TxtGroupFilterCurrentLabel.Text = total > 0 ? $"GROUP: -ALL- ({total})" : "GROUP: -ALL-";
            }
            else
            {
                int count = _historyManager?.FavoriteItems?.Count(x => string.Equals(x.GroupName, _activeGroupFilter, StringComparison.OrdinalIgnoreCase)) ?? 0;
                TxtGroupFilterCurrentLabel.Text = $"📁 {_activeGroupFilter} ({count})";
            }
        }

        private void BtnGroupFilterMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_historyManager == null) return;

            // Tự động chuyển sang tab Favorites nếu đang ở History
            if (_currentMode != "FAVORITES")
            {
                if (RbModeFavorites != null) RbModeFavorites.IsChecked = true;
            }

            var favList = _historyManager.FavoriteItems.ToList();
            int totalFavorites = favList.Count;

            var groupCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in favList)
            {
                string g = (it.GroupName ?? "").Trim();
                if (!string.IsNullOrEmpty(g))
                {
                    if (groupCounts.ContainsKey(g)) groupCounts[g]++;
                    else groupCounts[g] = 1;
                }
            }

            var cm = new ContextMenu { Style = (Style)FindResource("CyberContextMenu") };

            // 1. Dòng -All-
            var miAll = new MenuItem
            {
                Header = $"-All- ({totalFavorites})",
                Style = (Style)FindResource("CyberMenuItem")
            };
            if (string.IsNullOrEmpty(_activeGroupFilter) || _activeGroupFilter == "-All-")
            {
                miAll.FontWeight = FontWeights.Bold;
                miAll.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow");
            }
            miAll.Click += (s, ev) =>
            {
                _activeGroupFilter = null;
                UpdateGroupFilterButtonLabel();
                _itemsView?.Refresh();
                EnsureAppropriateSelection();
            };
            cm.Items.Add(miAll);

            if (groupCounts.Count > 0)
            {
                cm.Items.Add(new Separator { Style = (Style)FindResource("CyberMenuSeparator") });

                // 2. Từng Group sắp xếp theo Alphabet A-Z (Chuẩn Comfort Keys Pro - Ảnh 2)
                var sorted = groupCounts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in sorted)
                {
                    string gName = kvp.Key;
                    int gCount = kvp.Value;

                    var miGroup = new MenuItem
                    {
                        Header = $"{gName} ({gCount})",
                        Style = (Style)FindResource("CyberMenuItem")
                    };
                    if (string.Equals(_activeGroupFilter, gName, StringComparison.OrdinalIgnoreCase))
                    {
                        miGroup.FontWeight = FontWeights.Bold;
                        miGroup.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow");
                    }
                    miGroup.Click += (s, ev) =>
                    {
                        _activeGroupFilter = gName;
                        UpdateGroupFilterButtonLabel();
                        _itemsView?.Refresh();
                        EnsureAppropriateSelection();
                    };
                    cm.Items.Add(miGroup);
                }
            }

            cm.PlacementTarget = BtnGroupFilterMenu;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            cm.IsOpen = true;
        }

        private void BtnFilterMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_historyManager == null) return;

            var targetItems = (_currentMode == "FAVORITES" ? _historyManager.FavoriteItems : _historyManager.Items).ToList();
            int allCount = targetItems.Count;
            int sessionCount = targetItems.Count(x => x.Timestamp >= _sessionStartTime);
            int todayCount = targetItems.Count(x => x.Timestamp.Date == DateTime.Today);
            int weekCount = targetItems.Count(x => x.Timestamp >= DateTime.Now.AddDays(-7));
            int monthCount = targetItems.Count(x => x.Timestamp.Year == DateTime.Now.Year && x.Timestamp.Month == DateTime.Now.Month);

            int txtCount = targetItems.Count(x => x.ContentType == ClipboardContentType.Text);
            int imgCount = targetItems.Count(x => x.ContentType == ClipboardContentType.Image);
            int filesCount = targetItems.Count(x => x.ContentType == ClipboardContentType.Files);

            var cm = new ContextMenu { Style = (Style)FindResource("CyberContextMenu") };

            void AddFilterItem(string header, string code, int count, string icon = "")
            {
                var mi = new MenuItem
                {
                    Header = $"{icon}{header} ({count})",
                    Style = (Style)FindResource("CyberMenuItem")
                };
                if (_activeFilter == code)
                {
                    mi.FontWeight = FontWeights.Bold;
                    mi.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonCyan");
                }
                mi.Click += (s, ev) =>
                {
                    _activeFilter = code;
                    if (TxtFilterCurrentLabel != null)
                    {
                        TxtFilterCurrentLabel.Text = $"LỌC: {header.ToUpper()}";
                    }

                    _itemsView?.Refresh();
                    EnsureAppropriateSelection();
                };
                cm.Items.Add(mi);
            }

            // 1. Nhóm thời gian (Chuẩn Comfort Keys Pro)
            AddFilterItem("- Tất cả -", "ALL", allCount, "• ");
            AddFilterItem("Phiên này (This session)", "SESSION", sessionCount, "• ");
            AddFilterItem("Hôm nay (Today)", "TODAY", todayCount, "• ");
            AddFilterItem("Tuần này (This week)", "WEEK", weekCount, "• ");
            AddFilterItem("Tháng này (This month)", "MONTH", monthCount, "• ");

            cm.Items.Add(new Separator { Style = (Style)FindResource("CyberMenuSeparator") });

            // 2. Nhóm định dạng (Chuẩn Comfort Keys Pro)
            AddFilterItem("Văn bản (Text)", "TXT", txtCount, "• ");
            AddFilterItem("Hình ảnh (Picture)", "IMG", imgCount, "• ");
            AddFilterItem("Tệp tin & Thư mục (Files)", "FILES", filesCount, "• ");

            cm.PlacementTarget = BtnFilterMenu;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            cm.IsOpen = true;
        }

        private void ApplySortOrder()
        {
            if (_itemsView == null) return;

            _itemsView.SortDescriptions.Clear();
            if (_isSortDescending)
            {
                _itemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItem.Timestamp), ListSortDirection.Descending));
                if (TxtSortIcon != null) TxtSortIcon.Text = "▼";
                if (TxtSortLabel != null) TxtSortLabel.Text = "MỚI NHẤT";
                if (BtnToggleSortOrder != null)
                {
                    BtnToggleSortOrder.BorderBrush = (System.Windows.Media.Brush)FindResource("CyberNeonCyan");
                    BtnToggleSortOrder.ToolTip = "Thứ tự: Mới nhất ở trên (Click để đổi sang Cũ nhất)";
                }
                if (TxtSortIcon != null) TxtSortIcon.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonCyan");
                if (TxtSortLabel != null) TxtSortLabel.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonCyan");
            }
            else
            {
                _itemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItem.Timestamp), ListSortDirection.Ascending));
                if (TxtSortIcon != null) TxtSortIcon.Text = "▲";
                if (TxtSortLabel != null) TxtSortLabel.Text = "CŨ NHẤT";
                if (BtnToggleSortOrder != null)
                {
                    BtnToggleSortOrder.BorderBrush = (System.Windows.Media.Brush)FindResource("CyberNeonYellow");
                    BtnToggleSortOrder.ToolTip = "Thứ tự: Cũ nhất ở trên (Click để đổi sang Mới nhất)";
                }
                if (TxtSortIcon != null) TxtSortIcon.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow");
                if (TxtSortLabel != null) TxtSortLabel.Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow");
            }

            _itemsView.Refresh();
            EnsureAppropriateSelection();
        }

        private void BtnToggleSortOrder_Click(object sender, RoutedEventArgs e)
        {
            _isSortDescending = !_isSortDescending;
            ApplySortOrder();
        }

        #region Clipboard Merge Options (Tùy chọn gộp Clipboard khi chọn nhiều mục)

        public static string ToRoman(int number)
        {
            if (number < 1) return number.ToString();
            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            if (number >= 4000) return number.ToString();
            return thousands[number / 1000] +
                   hundreds[(number % 1000) / 100] +
                   tens[(number % 100) / 10] +
                   ones[number % 10];
        }

        public static string ToAlpha(int number, bool uppercase)
        {
            if (number < 1) return number.ToString();
            string result = "";
            while (number > 0)
            {
                number--;
                char c = (char)((uppercase ? 'A' : 'a') + (number % 26));
                result = c + result;
                number /= 26;
            }
            return result;
        }

        public string GetLinePrefix(int index)
        {
            if (_settings == null) return string.Empty;
            var sb = new StringBuilder();

            // 1. Số thứ tự / Số La Mã / Alphabet thường / Alphabet hoa
            switch (_settings.ClipboardMergeNumbering)
            {
                case 1: // 1. 2. 3.
                    sb.Append($"{index}. ");
                    break;
                case 2: // I. II. III.
                    sb.Append($"{ToRoman(index)}. ");
                    break;
                case 3: // a. b. c.
                    sb.Append($"{ToAlpha(index, false)}. ");
                    break;
                case 4: // A. B. C.
                    sb.Append($"{ToAlpha(index, true)}. ");
                    break;
            }

            // 2. Ký hiệu đầu dòng (Bullets / Markers)
            if (_settings.ClipboardMergePrefixDash)
            {
                sb.Append("- ");
            }
            if (_settings.ClipboardMergePrefixArrow)
            {
                sb.Append("-> ");
            }
            if (_settings.ClipboardMergePrefixImplies)
            {
                sb.Append("=> ");
            }
            if (_settings.ClipboardMergePrefixAsterisk)
            {
                sb.Append("* ");
            }

            return sb.ToString();
        }

        public string FormatMergedText(IEnumerable<string> textItems)
        {
            if (textItems == null) return string.Empty;
            var list = textItems.Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (list.Count == 0) return string.Empty;

            var formatted = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                string pfx = GetLinePrefix(i + 1);
                formatted.Add(pfx + list[i]);
            }

            string sep = (_settings != null && _settings.ClipboardMergeDoubleSpacing)
                ? (Environment.NewLine + Environment.NewLine)
                : Environment.NewLine;

            return string.Join(sep, formatted);
        }

        private string GetMergeFormatSummary()
        {
            if (_settings == null) return "Mặc định (Xuống dòng)";
            var parts = new List<string>();

            switch (_settings.ClipboardMergeNumbering)
            {
                case 1: parts.Add("Số 1. 2."); break;
                case 2: parts.Add("La Mã I. II."); break;
                case 3: parts.Add("Alpha a. b."); break;
                case 4: parts.Add("Alpha A. B."); break;
            }

            if (_settings.ClipboardMergePrefixDash) parts.Add("Gạch nối (-)");
            if (_settings.ClipboardMergePrefixArrow) parts.Add("Mũi tên (->)");
            if (_settings.ClipboardMergePrefixImplies) parts.Add("Suy ra (=>)");
            if (_settings.ClipboardMergePrefixAsterisk) parts.Add("Hoa thị (*)");

            parts.Add(_settings.ClipboardMergeDoubleSpacing ? "Dòng đúp" : "Dòng đơn");

            return string.Join(" + ", parts);
        }

        private void UpdateMergeOptionsButtonLabel()
        {
            if (TxtMergeOptionsLabel == null || _settings == null) return;

            var active = new List<string>();
            switch (_settings.ClipboardMergeNumbering)
            {
                case 1: active.Add("1. 2."); break;
                case 2: active.Add("I. II."); break;
                case 3: active.Add("a. b."); break;
                case 4: active.Add("A. B."); break;
            }

            if (_settings.ClipboardMergePrefixDash) active.Add("-");
            if (_settings.ClipboardMergePrefixArrow) active.Add("->");
            if (_settings.ClipboardMergePrefixImplies) active.Add("=>");
            if (_settings.ClipboardMergePrefixAsterisk) active.Add("*");

            if (_settings.ClipboardMergeDoubleSpacing) active.Add("Đúp");

            if (active.Count == 0)
            {
                TxtMergeOptionsLabel.Text = "GỘP: MẶC ĐỊNH";
            }
            else
            {
                TxtMergeOptionsLabel.Text = "GỘP: " + string.Join(" + ", active);
            }
        }

        private void BtnMergeOptionsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;

            var cm = new ContextMenu { Style = (Style)FindResource("CyberContextMenu") };

            void AddHeader(string text)
            {
                var h = new MenuItem
                {
                    Header = text,
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonCyan"),
                    Style = (Style)FindResource("CyberMenuItem")
                };
                cm.Items.Add(h);
            }

            void AddSeparator()
            {
                cm.Items.Add(new Separator { Style = (Style)FindResource("CyberMenuSeparator") });
            }

            MenuItem miPreview1 = null;
            MenuItem miPreview2 = null;

            Action updatePreviews = () =>
            {
                string l1 = GetLinePrefix(1) + "Đoạn văn bản A (Passage A)";
                string l2 = GetLinePrefix(2) + "Đoạn văn bản B (Passage B)";
                if (miPreview1 != null) miPreview1.Header = "  " + l1;
                if (miPreview2 != null) miPreview2.Header = (_settings.ClipboardMergeDoubleSpacing ? "  [Dòng trống]\n  " : "  ") + l2;
                UpdateMergeOptionsButtonLabel();
                Config.SettingsManager.SaveSettings(_settings);

                var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
                if (selected != null && selected.Count > 1)
                {
                    UpdateMultiPreview(selected);
                }
            };

            // 1. Nhóm Đánh số thứ tự (Chọn 1 hoặc không chọn)
            AddHeader("--- 1. ĐÁNH SỐ THỨ TỰ (CHỌN 1 KIỂU) ---");

            var numberingItems = new List<(MenuItem item, int value)>();

            void AddNumberingOption(string title, int value)
            {
                var mi = new MenuItem
                {
                    Header = title,
                    Style = (Style)FindResource("CyberMenuItem"),
                    StaysOpenOnClick = true
                };
                mi.Icon = (_settings.ClipboardMergeNumbering == value) ? "☑" : "☐";
                mi.Click += (s, ev) =>
                {
                    _settings.ClipboardMergeNumbering = (_settings.ClipboardMergeNumbering == value && value != 0) ? 0 : value;
                    foreach (var n in numberingItems)
                    {
                        n.item.Icon = (_settings.ClipboardMergeNumbering == n.value) ? "☑" : "☐";
                    }
                    updatePreviews();
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã chọn kiểu đánh số: {title}";
                };
                numberingItems.Add((mi, value));
                cm.Items.Add(mi);
            }

            AddNumberingOption("Không đánh số (Mặc định)", 0);
            AddNumberingOption("1. 2. 3. (Số thứ tự thường)", 1);
            AddNumberingOption("I. II. III. (Số thứ tự La Mã)", 2);
            AddNumberingOption("a. b. c. (Alphabet thường)", 3);
            AddNumberingOption("A. B. C. (Alphabet hoa)", 4);

            AddSeparator();

            // 2. Nhóm Ký hiệu đầu dòng (Bullets / Prefix) - Checkbox chọn 1 hoặc nhiều option
            AddHeader("--- 2. KÝ HIỆU ĐẦU DÒNG (CHỌN NHIỀU) ---");

            void AddPrefixCheckOption(string title, Func<bool> getter, Action<bool> setter)
            {
                var mi = new MenuItem
                {
                    Header = title,
                    Style = (Style)FindResource("CyberMenuItem"),
                    StaysOpenOnClick = true
                };
                mi.Icon = getter() ? "☑" : "☐";
                mi.Click += (s, ev) =>
                {
                    bool newVal = !getter();
                    setter(newVal);
                    mi.Icon = newVal ? "☑" : "☐";
                    updatePreviews();
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã {(newVal ? "bật" : "tắt")}: {title}";
                };
                cm.Items.Add(mi);
            }

            AddPrefixCheckOption("Dấu gạch nối: -", () => _settings.ClipboardMergePrefixDash, v => _settings.ClipboardMergePrefixDash = v);
            AddPrefixCheckOption("Dấu mũi tên: ->", () => _settings.ClipboardMergePrefixArrow, v => _settings.ClipboardMergePrefixArrow = v);
            AddPrefixCheckOption("Dấu suy ra: =>", () => _settings.ClipboardMergePrefixImplies, v => _settings.ClipboardMergePrefixImplies = v);
            AddPrefixCheckOption("Dấu hoa thị: *", () => _settings.ClipboardMergePrefixAsterisk, v => _settings.ClipboardMergePrefixAsterisk = v);

            AddSeparator();

            // 3. Nhóm Khoảng cách dòng
            AddHeader("--- 3. KHOẢNG CÁCH DÒNG ---");

            MenuItem miSingleSpace = null;
            MenuItem miDoubleSpace = null;

            miSingleSpace = new MenuItem
            {
                Header = "Xuống dòng đơn (\\n)",
                Style = (Style)FindResource("CyberMenuItem"),
                StaysOpenOnClick = true,
                Icon = (!_settings.ClipboardMergeDoubleSpacing) ? "☑" : "☐"
            };
            miDoubleSpace = new MenuItem
            {
                Header = "Xuống dòng đúp (\\n\\n)",
                Style = (Style)FindResource("CyberMenuItem"),
                StaysOpenOnClick = true,
                Icon = (_settings.ClipboardMergeDoubleSpacing) ? "☑" : "☐"
            };

            miSingleSpace.Click += (s, ev) =>
            {
                _settings.ClipboardMergeDoubleSpacing = false;
                miSingleSpace.Icon = "☑";
                miDoubleSpace.Icon = "☐";
                updatePreviews();
                if (TxtStatus != null) TxtStatus.Text = "✓ Đã chọn: Xuống dòng đơn";
            };

            miDoubleSpace.Click += (s, ev) =>
            {
                _settings.ClipboardMergeDoubleSpacing = true;
                miSingleSpace.Icon = "☐";
                miDoubleSpace.Icon = "☑";
                updatePreviews();
                if (TxtStatus != null) TxtStatus.Text = "✓ Đã chọn: Xuống dòng đúp";
            };

            cm.Items.Add(miSingleSpace);
            cm.Items.Add(miDoubleSpace);

            AddSeparator();

            // 4. Mẫu xem trước trực tiếp (Live Preview)
            AddHeader("--- MẪU KẾT QUẢ GỘP (PREVIEW) ---");

            miPreview1 = new MenuItem
            {
                Header = "  " + GetLinePrefix(1) + "Đoạn văn bản A (Passage A)",
                IsEnabled = false,
                Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow"),
                Style = (Style)FindResource("CyberMenuItem")
            };
            miPreview2 = new MenuItem
            {
                Header = (_settings.ClipboardMergeDoubleSpacing ? "  [Dòng trống]\n  " : "  ") + GetLinePrefix(2) + "Đoạn văn bản B (Passage B)",
                IsEnabled = false,
                Foreground = (System.Windows.Media.Brush)FindResource("CyberNeonYellow"),
                Style = (Style)FindResource("CyberMenuItem")
            };
            cm.Items.Add(miPreview1);
            cm.Items.Add(miPreview2);

            AddSeparator();

            // 5. Đặt lại mặc định
            var miReset = new MenuItem
            {
                Header = "↺ Đặt lại mặc định (Chỉ xuống dòng)",
                Style = (Style)FindResource("CyberMenuItem"),
                StaysOpenOnClick = true
            };
            miReset.Click += (s, ev) =>
            {
                _settings.ClipboardMergeNumbering = 0;
                _settings.ClipboardMergePrefixDash = false;
                _settings.ClipboardMergePrefixArrow = false;
                _settings.ClipboardMergePrefixImplies = false;
                _settings.ClipboardMergePrefixAsterisk = false;
                _settings.ClipboardMergeDoubleSpacing = false;

                foreach (var n in numberingItems)
                {
                    n.item.Icon = (n.value == 0) ? "☑" : "☐";
                }
                miSingleSpace.Icon = "☑";
                miDoubleSpace.Icon = "☐";
                updatePreviews();
                if (TxtStatus != null) TxtStatus.Text = "✓ Đã đặt lại tùy chọn gộp về mặc định!";
            };
            cm.Items.Add(miReset);

            // 6. Tác vụ gộp ảnh nhanh nếu người dùng đang chọn từ 2 ảnh trở lên
            var selectedItems = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            int imgSelCount = selectedItems != null ? selectedItems.Count(x => x.ContentType == ClipboardContentType.Image) : 0;
            if (imgSelCount >= 2)
            {
                AddSeparator();
                AddHeader($"--- GỘP {imgSelCount} HÌNH ẢNH ĐÃ CHỌN ---");
                var miMergeV = new MenuItem
                {
                    Header = "↕ Ghép ảnh dọc (Trên - Dưới)",
                    Style = (Style)FindResource("CyberMenuItem")
                };
                miMergeV.Click += (s, ev) => MergeSelectedImages(true);
                cm.Items.Add(miMergeV);

                var miMergeH = new MenuItem
                {
                    Header = "↔ Ghép ảnh ngang (Trái - Phải)",
                    Style = (Style)FindResource("CyberMenuItem")
                };
                miMergeH.Click += (s, ev) => MergeSelectedImages(false);
                cm.Items.Add(miMergeH);
            }

            cm.PlacementTarget = sender as UIElement ?? BtnMergeOptionsMenu;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            cm.IsOpen = true;
        }

        #endregion

        private void LstClipboard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0)
            {
                ClearPreview();
                return;
            }

            _lastActiveItemId = selected.Last().Id;

            if (selected.Count == 1)
            {
                UpdatePreview(selected[0]);
            }
            else
            {
                UpdateMultiPreview(selected);
            }
        }

        private void UpdateMultiPreview(List<ClipboardItem> items)
        {
            if (items == null || items.Count == 0)
            {
                ClearPreview();
                return;
            }

            int imgCount = items.Count(x => x.ContentType == ClipboardContentType.Image);
            int filesCount = items.Count(x => x.ContentType == ClipboardContentType.Files);
            int txtCount = items.Count(x => x.ContentType == ClipboardContentType.Text);

            if (ImgPreview != null) ImgPreview.Visibility = Visibility.Collapsed;
            if (TxtPreview != null)
            {
                TxtPreview.Visibility = Visibility.Visible;
                var sb = new StringBuilder();
                sb.AppendLine($"=== ĐÃ CHỌN {items.Count} MỤC CLIPBOARD ===");
                if (imgCount >= 2)
                {
                    sb.AppendLine($"• Chuột phải -> Gộp ảnh: Ghép dọc (↕ Trên - Dưới) hoặc Ghép ngang (↔ Trái - Phải)");
                }
                sb.AppendLine($"• Định dạng gộp: {GetMergeFormatSummary()}");
                sb.AppendLine($"• [Enter] hoặc [Ctrl+V]: Dán gộp vào ứng dụng đích");
                sb.AppendLine($"• [Ctrl+C]: Sao chép gộp vào Clipboard hệ thống");
                sb.AppendLine($"• [Del]: Xóa toàn bộ {items.Count} mục đã chọn");
                sb.AppendLine();
                sb.AppendLine("--- NỘI DUNG TỔNG HỢP (XEM TRƯỚC GỘP) ---");

                var textItems = items.Where(x => x.ContentType == ClipboardContentType.Text && !string.IsNullOrEmpty(x.TextContent))
                                     .Select(x => x.TextContent)
                                     .ToList();
                if (textItems.Count == items.Count)
                {
                    sb.AppendLine(FormatMergedText(textItems));
                }
                else
                {
                    int idx = 1;
                    foreach (var it in items)
                    {
                        string content;
                        if (it.ContentType == ClipboardContentType.Text)
                        {
                            content = GetLinePrefix(idx) + it.TextContent;
                        }
                        else if (it.ContentType == ClipboardContentType.Files)
                        {
                            content = $"[{it.TypeBadge}] " + it.PreviewText + Environment.NewLine + it.TextContent;
                        }
                        else
                        {
                            content = it.PreviewText;
                        }
                        sb.AppendLine($"[{idx++}] " + content);
                        if (_settings != null && _settings.ClipboardMergeDoubleSpacing) sb.AppendLine();
                    }
                }
                TxtPreview.Text = sb.ToString();
            }

            int totalChars = items.Where(x => x.ContentType == ClipboardContentType.Text).Sum(x => x.CharCount);
            long totalBytes = items.Sum(x => x.ByteSize);

            if (TxtMetaInfo != null)
            {
                var parts = new List<string>();
                if (txtCount > 0) parts.Add($"{txtCount} văn bản");
                if (imgCount > 0) parts.Add($"{imgCount} ảnh");
                if (filesCount > 0) parts.Add($"{filesCount} mục tệp/thư mục");
                string detail = string.Join(", ", parts);
                string sizeStr = totalBytes >= 1024 * 1024 ? $"{totalBytes / (1024 * 1024)} MB" : $"{Math.Max(1, totalBytes / 1024)} KB";
                TxtMetaInfo.Text = $"Đang chọn {items.Count} mục ({detail}) • {sizeStr}";
            }
        }

        private void UpdatePreview(ClipboardItem item)
        {
            if (item == null)
            {
                ClearPreview();
                return;
            }

            string appPart = !string.IsNullOrEmpty(item.SourceApp) ? $" • Nguồn: {item.SourceApp}" : "";

            if (item.ContentType == ClipboardContentType.Image)
            {
                if (TxtPreview != null) TxtPreview.Visibility = Visibility.Collapsed;
                if (ImgPreview != null)
                {
                    ImgPreview.Visibility = Visibility.Visible;
                    ImgPreview.Source = item.ImageSource;
                }
                if (TxtMetaInfo != null) TxtMetaInfo.Text = $"[HÌNH ẢNH]{appPart} • Dung lượng: {item.ByteSize / 1024} KB • Thời gian: {item.TimeDisplay}";
            }
            else if (item.ContentType == ClipboardContentType.Files)
            {
                if (ImgPreview != null) ImgPreview.Visibility = Visibility.Collapsed;
                if (TxtPreview != null)
                {
                    TxtPreview.Visibility = Visibility.Visible;
                    var sb = new StringBuilder();
                    string mode = item.DropEffect == 2 ? "CUT (Di chuyển tệp)" : "COPY (Sao chép tệp)";
                    sb.AppendLine($"=== DANH SÁCH TỆP / THƯ MỤC ({item.CharCount} mục) ===");
                    sb.AppendLine($"• Chế độ: {mode}");
                    sb.AppendLine($"• Phím tắt: [Enter] hoặc [Ctrl+V] để dán vào thư mục đích");
                    sb.AppendLine($"• [Ctrl+C] để nạp lại vào Clipboard hệ thống");
                    sb.AppendLine();
                    sb.AppendLine("--- DANH SÁCH CHI TIẾT ---");
                    var paths = item.FilePaths;
                    for (int i = 0; i < paths.Count; i++)
                    {
                        string p = paths[i];
                        string status = "Tệp tin";
                        string sizeInfo = "";
                        try
                        {
                            if (Directory.Exists(p))
                            {
                                status = "Thư mục";
                            }
                            else if (File.Exists(p))
                            {
                                long len = new FileInfo(p).Length;
                                sizeInfo = len >= 1024 * 1024 ? $" ({len / (1024 * 1024)} MB)" : $" ({Math.Max(1, len / 1024)} KB)";
                            }
                            else
                            {
                                status = "[Không tồn tại]";
                            }
                        }
                        catch { }
                        sb.AppendLine($"[{i + 1}] [{status}] {p}{sizeInfo}");
                    }
                    TxtPreview.Text = sb.ToString();
                }
                if (TxtMetaInfo != null)
                {
                    string sizeStr = item.ByteSize > 0 ? (item.ByteSize >= 1024 * 1024 ? $"{item.ByteSize / (1024 * 1024)} MB" : $"{Math.Max(1, item.ByteSize / 1024)} KB") : "0 KB";
                    TxtMetaInfo.Text = $"[TỆP TIN]{appPart} • {item.CharCount} tệp/thư mục • {sizeStr} • {item.TimeDisplay}";
                }
            }
            else
            {
                if (ImgPreview != null) ImgPreview.Visibility = Visibility.Collapsed;
                if (TxtPreview != null)
                {
                    TxtPreview.Visibility = Visibility.Visible;
                    TxtPreview.Text = item.TextContent ?? string.Empty;
                }
                int lineCount = (item.TextContent ?? "").Split('\n').Length;
                if (TxtMetaInfo != null) TxtMetaInfo.Text = $"Văn bản{appPart} • {item.CharCount} ký tự • {lineCount} dòng • {item.ByteSize} B • {item.TimeDisplay}";
            }
        }

        private void ClearPreview()
        {
            if (TxtPreview != null)
            {
                TxtPreview.Visibility = Visibility.Visible;
                TxtPreview.Text = string.Empty;
            }
            if (ImgPreview != null)
            {
                ImgPreview.Visibility = Visibility.Collapsed;
                ImgPreview.Source = null;
            }
            if (TxtMetaInfo != null)
            {
                TxtMetaInfo.Text = "Chọn một mục bên trái để xem chi tiết nội dung";
            }
        }

        private void LstClipboard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecutePasteSelected(false, false);
        }

        private void BtnPasteDirect_Click(object sender, RoutedEventArgs e)
        {
            ExecutePasteSelected(false, false);
        }

        private void BtnCopyDirect_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCopySelectedToClipboard();
        }

        private void BtnPastePlainText_Click(object sender, RoutedEventArgs e)
        {
            ExecutePasteSelected(true, false);
        }

        private void BtnPasteKeystroke_Click(object sender, RoutedEventArgs e)
        {
            ExecutePasteSelected(false, true);
        }

        private static void SetClipboardDataForFiles(List<string> filePaths, int dropEffect)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            try
            {
                var data = new DataObject();

                // 1. FileDrop list chuẩn cho Explorer
                var sc = new System.Collections.Specialized.StringCollection();
                foreach (var p in filePaths)
                {
                    if (!string.IsNullOrEmpty(p)) sc.Add(p);
                }
                data.SetFileDropList(sc);

                // 2. Preferred DropEffect (1 = Copy, 2 = Move/Cut)
                byte[] effectBytes = BitConverter.GetBytes(dropEffect > 0 ? dropEffect : 1);
                var effectStream = new MemoryStream(effectBytes);
                data.SetData("Preferred DropEffect", effectStream);

                // 3. Fallback text để dán danh sách đường dẫn vào Notepad, Word, Browser
                string text = string.Join(Environment.NewLine, filePaths);
                data.SetText(text);

                Clipboard.SetDataObject(data, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetClipboardDataForFiles error: " + ex.Message);
            }
        }

        private static void SafeSetClipboardText(string text)
        {
            if (text == null) text = string.Empty;
            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true);
                    return;
                }
                catch
                {
                    try
                    {
                        System.Windows.Forms.Clipboard.SetText(text);
                        return;
                    }
                    catch
                    {
                        Thread.Sleep(30);
                    }
                }
            }
        }

        private static void SafeSetClipboardImage(string imagePath, System.Windows.Media.ImageSource bmpSource)
        {
            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        using (var img = System.Drawing.Image.FromFile(imagePath))
                        {
                            System.Windows.Forms.Clipboard.SetImage(img);
                            return;
                        }
                    }
                    else if (bmpSource is BitmapSource bs)
                    {
                        Clipboard.SetImage(bs);
                        return;
                    }
                }
                catch
                {
                    Thread.Sleep(30);
                }
            }
        }

        private void ExecuteCopySelectedToClipboard()
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            try
            {
                KeySender.SuppressClipboardMonitoring = true;

                if (selected.Count == 1)
                {
                    var item = selected[0];
                    if (item.ContentType == ClipboardContentType.Files)
                    {
                        SetClipboardDataForFiles(item.FilePaths, item.DropEffect);
                    }
                    else if (item.ContentType == ClipboardContentType.Image)
                    {
                        SafeSetClipboardImage(item.ImagePath, item.ImageSource);
                    }
                    else
                    {
                        SafeSetClipboardText(item.TextContent ?? string.Empty);
                    }
                }
                else
                {
                    var fileItems = selected.Where(x => x.ContentType == ClipboardContentType.Files).ToList();
                    if (fileItems.Count > 0 && selected.All(x => x.ContentType == ClipboardContentType.Files))
                    {
                        var allFiles = fileItems.SelectMany(x => x.FilePaths).Distinct().ToList();
                        int effect = fileItems[0].DropEffect;
                        SetClipboardDataForFiles(allFiles, effect);
                    }
                    else
                    {
                        var textItems = selected.Select(x => x.TextContent)
                                                .Where(x => !string.IsNullOrEmpty(x))
                                                .ToList();
                        if (textItems.Count > 0)
                        {
                            string combined = FormatMergedText(textItems);
                            SafeSetClipboardText(combined);
                        }
                    }
                }

                if (selected.Count > 0)
                {
                    _lastActiveItemId = selected.Last().Id;
                }

                if (TxtStatus != null)
                {
                    TxtStatus.Text = selected.Count > 1
                        ? $"✓ Đã gộp và sao chép {selected.Count} mục vào Clipboard ({GetMergeFormatSummary()})!"
                        : $"✓ Đã sao chép 1 mục vào Clipboard hệ thống!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error copying to clipboard: " + ex.Message);
            }
            finally
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(500);
                    KeySender.SuppressClipboardMonitoring = false;
                });
            }
        }

        private void ExecutePasteSelected(bool forcePlainText, bool forceKeystroke)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            if (selected.Count == 1)
            {
                ExecutePaste(selected[0], forcePlainText, forceKeystroke);
                return;
            }

            // Nếu toàn bộ là Files: Gộp danh sách tệp
            var fileItems = selected.Where(x => x.ContentType == ClipboardContentType.Files).ToList();
            if (fileItems.Count > 0 && selected.All(x => x.ContentType == ClipboardContentType.Files))
            {
                var allFiles = fileItems.SelectMany(x => x.FilePaths).Distinct().ToList();
                var virtualItem = new ClipboardItem
                {
                    ContentType = ClipboardContentType.Files,
                    DropEffect = fileItems[0].DropEffect,
                    TextContent = string.Join(Environment.NewLine, allFiles),
                    PreviewText = $"[Gộp {allFiles.Count} tệp/thư mục]",
                    CharCount = allFiles.Count
                };
                ExecutePaste(virtualItem, forcePlainText, forceKeystroke);
                return;
            }

            // Nhiều mục: Gộp nội dung văn bản theo thứ tự hiển thị
            var textItems = selected.Where(x => x.ContentType != ClipboardContentType.Image && !string.IsNullOrEmpty(x.TextContent))
                                    .Select(x => x.TextContent)
                                    .ToList();

            if (textItems.Count == 0)
            {
                // Toàn là ảnh: Dán ảnh đầu tiên
                ExecutePaste(selected[0], forcePlainText, forceKeystroke);
                return;
            }

            string combinedText = FormatMergedText(textItems);
            var virtualItemText = new ClipboardItem
            {
                ContentType = ClipboardContentType.Text,
                TextContent = combinedText,
                PreviewText = $"[Gộp {textItems.Count} mục: {GetMergeFormatSummary()}]",
                CharCount = combinedText.Length,
                ByteSize = Encoding.UTF8.GetByteCount(combinedText)
            };

            ExecutePaste(virtualItemText, forcePlainText, forceKeystroke);
        }

        private void ExecutePaste(ClipboardItem item, bool forcePlainText, bool forceKeystroke)
        {
            if (item == null) return;
            _lastActiveItemId = item.Id;

            // Ẩn HUD ngay lập tức nếu bật AutoHide
            if (_settings != null && _settings.ClipboardAutoHide)
            {
                Hide();
            }

            IntPtr target = _lastTargetHwnd;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (target != IntPtr.Zero)
                {
                    SetForegroundWindow(target);
                    Thread.Sleep(50);
                }

                if (forceKeystroke && item.ContentType == ClipboardContentType.Text)
                {
                    // Mô phỏng gõ từng ký tự Unicode trực tiếp
                    KeySender.SendUnicodeString(item.TextContent);
                    return;
                }

                if (item.ContentType == ClipboardContentType.Files)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            KeySender.SuppressClipboardMonitoring = true;
                            if (forcePlainText)
                            {
                                Clipboard.SetText(item.TextContent ?? string.Empty);
                            }
                            else
                            {
                                SetClipboardDataForFiles(item.FilePaths, item.DropEffect);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error setting files to clipboard: " + ex.Message);
                        }
                    });

                    Thread.Sleep(50);
                    KeySender.SendCtrlVPaste();

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        Thread.Sleep(500);
                        KeySender.SuppressClipboardMonitoring = false;
                    });
                    return;
                }

                if (item.ContentType == ClipboardContentType.Image)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            KeySender.SuppressClipboardMonitoring = true;
                            if (!string.IsNullOrEmpty(item.ImagePath) && System.IO.File.Exists(item.ImagePath))
                            {
                                using (var fullImg = System.Drawing.Image.FromFile(item.ImagePath))
                                {
                                    System.Windows.Forms.Clipboard.SetImage(fullImg);
                                }
                            }
                            else if (item.ImageSource != null)
                            {
                                Clipboard.SetImage(item.ImageSource);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error setting image to clipboard: " + ex.Message);
                        }
                    });

                    Thread.Sleep(50);
                    KeySender.SendCtrlVPaste();

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        Thread.Sleep(500);
                        KeySender.SuppressClipboardMonitoring = false;
                    });
                    return;
                }

                // Dán dạng Text
                string textToPaste = item.TextContent ?? string.Empty;
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        KeySender.SuppressClipboardMonitoring = true;
                        Clipboard.SetText(textToPaste);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error setting text to clipboard: " + ex.Message);
                    }
                });

                Thread.Sleep(30);
                KeySender.SendCtrlVPaste();

                ThreadPool.QueueUserWorkItem(__ =>
                {
                    Thread.Sleep(400);
                    KeySender.SuppressClipboardMonitoring = false;
                });
            });
        }

        public void PromptSelectGroupForSelectedItems(IEnumerable<ClipboardItem> targetItems = null)
        {
            var selected = (targetItems ?? LstClipboard?.SelectedItems?.Cast<ClipboardItem>())?.ToList();
            if (selected == null || selected.Count == 0) return;

            string defaultGroup = selected.FirstOrDefault(x => !string.IsNullOrEmpty(x.GroupName))?.GroupName;

            var dlg = new SelectGroupDialog(_historyManager?.FavoriteItems, defaultGroup)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                string groupName = dlg.SelectedGroupName;
                foreach (var item in selected)
                {
                    _historyManager?.AddToFavoritesWithGroup(item, groupName);
                }

                _itemsView?.Refresh();
                UpdateGroupFilterButtonLabel();

                string groupLabel = string.IsNullOrEmpty(groupName) ? "-All-" : groupName;
                if (TxtStatus != null)
                {
                    TxtStatus.Text = selected.Count == 1
                        ? $"★ Đã thêm vào Yêu thích [Group: {groupLabel}]!"
                        : $"★ Đã thêm {selected.Count} mục vào Yêu thích [Group: {groupLabel}]!";
                }
            }
        }

        private void BtnToggleFav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardItem item)
            {
                PromptSelectGroupForSelectedItems(new[] { item });
            }
        }

        private void BtnToggleFavoriteAction_Click(object sender, RoutedEventArgs e)
        {
            PromptSelectGroupForSelectedItems();
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            int curIdx = LstClipboard.SelectedIndex;
            bool isFavView = (_currentMode == "FAVORITES");
            foreach (var item in selected)
            {
                _historyManager?.DeleteItem(item, isFavView);
            }
            _itemsView?.Refresh();
            if (LstClipboard.Items.Count > 0)
            {
                LstClipboard.SelectedIndex = Math.Min(curIdx, LstClipboard.Items.Count - 1);
            }
            else
            {
                ClearPreview();
            }
            if (TxtStatus != null)
            {
                TxtStatus.Text = $"✓ Đã xóa {selected.Count} mục khỏi {(isFavView ? "danh sách Yêu thích" : "Lịch sử")}!";
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == "FAVORITES")
            {
                var res = MessageBox.Show("Bạn có chắc chắn muốn xóa TẤT CẢ các mục trong danh sách Yêu thích không?",
                                          "Xác nhận xóa Yêu thích", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    _historyManager?.ClearFavorites();
                    _itemsView?.Refresh();
                    ClearPreview();
                    if (TxtStatus != null) TxtStatus.Text = "✓ Đã xóa sạch toàn bộ mục Yêu thích!";
                }
            }
            else
            {
                var res = MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ lịch sử clipboard không?\n(Lưu ý: Các mục trong danh sách Yêu thích sẽ được bảo tồn an toàn)",
                                          "Xác nhận xóa lịch sử", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _historyManager?.ClearHistory();
                    _itemsView?.Refresh();
                    ClearPreview();
                    if (TxtStatus != null) TxtStatus.Text = "✓ Đã xóa sạch lịch sử clipboard (Mục Yêu thích được giữ an toàn)!";
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        #region Context Menu Handlers (Comfort Keys Pro Standard)

        private void CtxMenuPaste_Click(object sender, RoutedEventArgs e)
        {
            ExecutePasteSelected(false, false);
        }

        private void CtxMenuToggleFav_Click(object sender, RoutedEventArgs e)
        {
            PromptSelectGroupForSelectedItems();
        }

        private void CtxMenuRemoveFav_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected != null && selected.Count > 0)
            {
                foreach (var it in selected)
                {
                    _historyManager?.RemoveFromFavorites(it);
                }
                _itemsView?.Refresh();
                UpdateGroupFilterButtonLabel();
                if (TxtStatus != null)
                {
                    TxtStatus.Text = selected.Count == 1
                        ? "☆ Đã bỏ khỏi mục Yêu thích!"
                        : $"☆ Đã bỏ {selected.Count} mục khỏi Yêu thích!";
                }
            }
        }

        private void CtxMenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if (LstClipboard.SelectedItem is ClipboardItem item)
            {
                if (item.ContentType != ClipboardContentType.Text)
                {
                    MessageBox.Show("Chỉ có thể chỉnh sửa nội dung văn bản.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (TxtPreview != null)
                {
                    TxtPreview.IsReadOnly = false;
                    TxtPreview.Focus();
                    TxtPreview.SelectAll();
                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = "✏ Đang chỉnh sửa văn bản. Nhấn Ctrl+S hoặc click ra ngoài để lưu!";
                    }
                }
            }
        }

        private void CtxMenuPreview_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPreview != null && TxtPreview.Visibility == Visibility.Visible)
            {
                TxtPreview.Focus();
            }
        }

        private void CtxMenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0)
            {
                if (LstClipboard.SelectedItem is ClipboardItem cur)
                    selected = new List<ClipboardItem> { cur };
                else
                    selected = (_currentMode == "FAVORITES" ? _historyManager.FavoriteItems : _historyManager.Items).ToList();
            }

            if (selected.Count == 0)
            {
                MessageBox.Show("Không có mục nào để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string backupDir = _historyManager.GetBackupDirectory();
            string defaultName = _historyManager.GenerateBackupFileName(backupDir);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = backupDir,
                FileName = defaultName,
                Filter = "Gói sao lưu Clipboard ZIP (*.zip)|*.zip|Văn bản thuần (*.txt)|*.txt|Ảnh PNG (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                    if (ext == ".txt" && selected.Count == 1 && selected[0].ContentType == ClipboardContentType.Text)
                    {
                        File.WriteAllText(sfd.FileName, selected[0].TextContent ?? "", Encoding.UTF8);
                        if (TxtStatus != null) TxtStatus.Text = $"✓ Đã lưu tệp văn bản: {Path.GetFileName(sfd.FileName)}";
                    }
                    else if (ext == ".png" && selected.Count == 1 && selected[0].ContentType == ClipboardContentType.Image)
                    {
                        string imgPath = selected[0].ImagePath;
                        if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
                        {
                            File.Copy(imgPath, sfd.FileName, true);
                            if (TxtStatus != null) TxtStatus.Text = $"✓ Đã lưu tệp ảnh: {Path.GetFileName(sfd.FileName)}";
                        }
                    }
                    else
                    {
                        var histToZip = (_currentMode == "HISTORY") ? selected : null;
                        var favToZip = (_currentMode == "FAVORITES") ? selected : null;
                        _historyManager.ExportToZip(sfd.FileName, histToZip, favToZip);
                        if (TxtStatus != null) TxtStatus.Text = $"✓ Đã lưu gói ZIP ({selected.Count} mục): {Path.GetFileName(sfd.FileName)}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi lưu tệp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CtxMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            BtnDeleteItem_Click(sender, e);
        }

        private void CtxMenuDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            BtnClearAll_Click(sender, e);
        }

        private void CtxMenuCopy_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCopySelectedToClipboard();
        }

        private void CtxMenuMerge_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected != null && selected.Count > 1)
            {
                var textParts = selected.Where(x => x.ContentType == ClipboardContentType.Text && !string.IsNullOrEmpty(x.TextContent))
                                        .Select(x => x.TextContent)
                                        .ToList();
                if (textParts.Count > 0)
                {
                    string merged = FormatMergedText(textParts);
                    _historyManager?.AddText(merged, "ModernKey Gộp", null);
                    _itemsView?.Refresh();
                    EnsureAppropriateSelection();
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã gộp {textParts.Count} đoạn văn bản ({GetMergeFormatSummary()}) thành 1 mục mới!";
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn từ 2 mục văn bản trở lên để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CtxMenuMergeImagesVertical_Click(object sender, RoutedEventArgs e)
        {
            MergeSelectedImages(true);
        }

        private void CtxMenuMergeImagesHorizontal_Click(object sender, RoutedEventArgs e)
        {
            MergeSelectedImages(false);
        }

        private void MergeSelectedImages(bool isVertical)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count < 2)
            {
                MessageBox.Show("Vui lòng chọn từ 2 hình ảnh trở lên để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Thu thập đường dẫn ảnh từ các mục đã chọn (hỗ trợ cả Image lẫn Files chứa tệp ảnh)
            var imagePaths = new List<string>();
            foreach (var it in selected)
            {
                if (it.ContentType == ClipboardContentType.Image)
                {
                    string p = ClipboardItem.ResolvePath(it.ImagePath);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        imagePaths.Add(p);
                    }
                }
                else if (it.ContentType == ClipboardContentType.Files)
                {
                    var files = it.FilePaths;
                    foreach (var f in files)
                    {
                        string ext = Path.GetExtension(f)?.ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp" || ext == ".gif")
                        {
                            if (File.Exists(f)) imagePaths.Add(f);
                        }
                    }
                }
            }

            if (imagePaths.Count < 2)
            {
                MessageBox.Show("Chỉ tìm thấy ít hơn 2 hình ảnh hợp lệ trong các mục đã chọn để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Nạp tất cả ảnh vào bộ nhớ an toàn (không lock file)
                var loadedBitmaps = new List<System.Drawing.Bitmap>();
                foreach (var path in imagePaths)
                {
                    try
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var orig = System.Drawing.Image.FromStream(fs))
                        {
                            var bmp = new System.Drawing.Bitmap(orig);
                            loadedBitmaps.Add(bmp);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Lỗi đọc file ảnh: " + ex.Message);
                    }
                }

                if (loadedBitmaps.Count < 2)
                {
                    foreach (var b in loadedBitmaps) b.Dispose();
                    MessageBox.Show("Không thể nạp đủ các tệp hình ảnh để thực hiện gộp.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int totalWidth = 0;
                int totalHeight = 0;
                int spacing = 0; // Ghép liền mạch (0px)

                if (isVertical)
                {
                    totalWidth = loadedBitmaps.Max(b => b.Width);
                    totalHeight = loadedBitmaps.Sum(b => b.Height) + spacing * (loadedBitmaps.Count - 1);
                }
                else
                {
                    totalWidth = loadedBitmaps.Sum(b => b.Width) + spacing * (loadedBitmaps.Count - 1);
                    totalHeight = loadedBitmaps.Max(b => b.Height);
                }

                if (totalWidth <= 0 || totalHeight <= 0)
                {
                    foreach (var b in loadedBitmaps) b.Dispose();
                    return;
                }

                byte[] mergedPngBytes = null;
                using (var canvas = new System.Drawing.Bitmap(totalWidth, totalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (var g = System.Drawing.Graphics.FromImage(canvas))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    int currentX = 0;
                    int currentY = 0;

                    foreach (var bmp in loadedBitmaps)
                    {
                        if (isVertical)
                        {
                            // Ghép dọc: căn giữa theo chiều ngang
                            int drawX = (totalWidth - bmp.Width) / 2;
                            g.DrawImage(bmp, drawX, currentY, bmp.Width, bmp.Height);
                            currentY += bmp.Height + spacing;
                        }
                        else
                        {
                            // Ghép ngang: căn giữa theo chiều dọc
                            int drawY = (totalHeight - bmp.Height) / 2;
                            g.DrawImage(bmp, currentX, drawY, bmp.Width, bmp.Height);
                            currentX += bmp.Width + spacing;
                        }
                    }

                    using (var ms = new MemoryStream())
                    {
                        canvas.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        mergedPngBytes = ms.ToArray();
                    }
                }

                // Giải phóng bộ nhớ ảnh tạm
                foreach (var b in loadedBitmaps) b.Dispose();
                loadedBitmaps.Clear();

                if (mergedPngBytes == null || mergedPngBytes.Length == 0) return;

                string dirText = isVertical ? "dọc" : "ngang";
                string previewLabel = $"[Gộp {imagePaths.Count} ảnh {dirText} {totalWidth}x{totalHeight}]";

                // Thêm vào HistoryManager
                var newItem = _historyManager?.AddImage(mergedPngBytes, totalWidth, totalHeight, "ModernKey Gộp Ảnh", null, previewLabel);
                _itemsView?.Refresh();

                if (newItem != null)
                {
                    LstClipboard.SelectedItem = newItem;
                    LstClipboard.ScrollIntoView(newItem);
                    _lastActiveItemId = newItem.Id;

                    // Sao chép ngay vào Clipboard hệ thống
                    try
                    {
                        KeySender.SuppressClipboardMonitoring = true;
                        SafeSetClipboardImage(newItem.ImagePath, newItem.ImageSource);
                    }
                    catch { }
                    finally
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            Thread.Sleep(500);
                            KeySender.SuppressClipboardMonitoring = false;
                        });
                    }
                }

                if (TxtStatus != null)
                {
                    TxtStatus.Text = $"✓ Đã gộp thành công {imagePaths.Count} ảnh ({dirText}) thành ảnh mới {totalWidth}x{totalHeight} và nạp vào Clipboard!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi gộp hình ảnh: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CtxMenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            LstClipboard?.SelectAll();
        }

        private void CtxMenuMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (LstClipboard.SelectedItem is ClipboardItem item)
            {
                var coll = (_currentMode == "FAVORITES") ? _historyManager.FavoriteItems : _historyManager.Items;
                int idx = coll.IndexOf(item);
                if (idx > 0)
                {
                    coll.Move(idx, idx - 1);
                    _lastActiveItemId = item.Id;
                    _itemsView?.Refresh();
                    LstClipboard.SelectedItem = item;
                    LstClipboard.ScrollIntoView(item);
                    _historyManager.SaveAllNow();
                }
            }
        }

        private void CtxMenuMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (LstClipboard.SelectedItem is ClipboardItem item)
            {
                var coll = (_currentMode == "FAVORITES") ? _historyManager.FavoriteItems : _historyManager.Items;
                int idx = coll.IndexOf(item);
                if (idx >= 0 && idx < coll.Count - 1)
                {
                    coll.Move(idx, idx + 1);
                    _lastActiveItemId = item.Id;
                    _itemsView?.Refresh();
                    LstClipboard.SelectedItem = item;
                    LstClipboard.ScrollIntoView(item);
                    _historyManager.SaveAllNow();
                }
            }
        }

        private void CtxMenuBackup_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            var itemsToBackup = (selected != null && selected.Count > 0)
                ? selected
                : (_currentMode == "FAVORITES" ? _historyManager.FavoriteItems.ToList() : _historyManager.Items.ToList());

            if (itemsToBackup.Count == 0)
            {
                MessageBox.Show("Không có mục nào để sao lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string backupDir = _historyManager.GetBackupDirectory();
            string defaultName = _historyManager.GenerateBackupFileName(backupDir);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = backupDir,
                FileName = defaultName,
                Filter = "Gói sao lưu Clipboard ZIP (*.zip)|*.zip|JSON Backup (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                    if (ext == ".zip")
                    {
                        var hist = (_currentMode == "HISTORY") ? itemsToBackup : null;
                        var fav = (_currentMode == "FAVORITES") ? itemsToBackup : null;
                        _historyManager.ExportToZip(sfd.FileName, hist, fav);
                    }
                    else
                    {
                        string json = _historyManager.SerializeItemsToJson(itemsToBackup);
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                    }
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã sao lưu {itemsToBackup.Count} mục vào {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi sao lưu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CtxMenuBackupAll_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = _historyManager.GetBackupDirectory();
            string defaultName = _historyManager.GenerateBackupFileName(backupDir);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = backupDir,
                FileName = defaultName,
                Filter = "Gói sao lưu toàn bộ Clipboard ZIP (*.zip)|*.zip|All Files (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _historyManager.ExportToZip(sfd.FileName, _historyManager.Items, _historyManager.FavoriteItems);
                    int total = _historyManager.Items.Count + _historyManager.FavoriteItems.Count;
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã sao lưu toàn bộ ({total} mục) vào {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi sao lưu toàn bộ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CtxMenuRestore_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = _historyManager.GetBackupDirectory();
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = backupDir,
                Filter = "Gói sao lưu Clipboard (*.zip;*.json)|*.zip;*.json|Tệp ZIP (*.zip)|*.zip|Tệp JSON (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
                    if (ext == ".zip")
                    {
                        var (histCount, favCount) = _historyManager.RestoreFromZip(ofd.FileName);
                        _itemsView?.Refresh();
                        EnsureAppropriateSelection();
                        if (TxtStatus != null) TxtStatus.Text = $"✓ Đã khôi phục thành công {histCount} lịch sử & {favCount} yêu thích từ {Path.GetFileName(ofd.FileName)}!";
                    }
                    else
                    {
                        string content = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        var items = _historyManager.ParseJsonItems(content);
                        int count = 0;
                        foreach (var it in items)
                        {
                            if (it.ContentType == ClipboardContentType.Text && !string.IsNullOrEmpty(it.TextContent))
                            {
                                _historyManager.AddText(it.TextContent, it.SourceApp, it.SourceIconPath);
                                count++;
                            }
                        }
                        _itemsView?.Refresh();
                        EnsureAppropriateSelection();
                        if (TxtStatus != null) TxtStatus.Text = $"✓ Đã khôi phục {count} mục từ {Path.GetFileName(ofd.FileName)}!";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi khôi phục: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Drag & Drop ra các ứng dụng khác (Images, Files/Folders, Text)

        private Point _dragStartPoint = new Point(-1, -1);
        private bool _isDragging = false;

        private void LstClipboard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Bỏ qua nếu click vào button (nút sao yêu thích) hoặc scrollbar
            DependencyObject original = e.OriginalSource as DependencyObject;
            while (original != null && original != LstClipboard)
            {
                if (original is Button || original is System.Windows.Controls.Primitives.ScrollBar)
                {
                    _dragStartPoint = new Point(-1, -1);
                    return;
                }
                original = VisualTreeHelper.GetParent(original);
            }

            _dragStartPoint = e.GetPosition(null);
        }

        private void LstClipboard_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint.X < 0 || _isDragging) return;

            Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDragDropFromSelection(LstClipboard);
            }
        }

        private void ImgPreview_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ImgPreview_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint.X < 0 || _isDragging) return;

            Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDragDropFromPreviewImage();
            }
        }

        private static ClipboardItem GetItemAtPoint(ListBox listBox, Point pt)
        {
            if (listBox == null || pt.X < 0 || pt.Y < 0) return null;
            try
            {
                var hit = VisualTreeHelper.HitTest(listBox, pt);
                if (hit == null) return null;

                DependencyObject current = hit.VisualHit;
                while (current != null && current != listBox)
                {
                    if (current is ListBoxItem lbi)
                    {
                        return lbi.DataContext as ClipboardItem;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch { }
            return null;
        }

        private void StartDragDropFromPreviewImage()
        {
            if (LstClipboard?.SelectedItem is ClipboardItem curItem && curItem.ContentType == ClipboardContentType.Image)
            {
                StartDragDropForItems(new List<ClipboardItem> { curItem }, ImgPreview);
            }
        }

        private void StartDragDropFromSelection(DependencyObject dragSource)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected == null || selected.Count == 0)
            {
                var hoveredItem = GetItemAtPoint(LstClipboard, _dragStartPoint);
                if (hoveredItem != null)
                {
                    selected = new List<ClipboardItem> { hoveredItem };
                }
            }

            if (selected == null || selected.Count == 0) return;

            StartDragDropForItems(selected, dragSource);
        }

        private void StartDragDropForItems(List<ClipboardItem> items, DependencyObject dragSource)
        {
            if (items == null || items.Count == 0 || dragSource == null) return;

            _isDragging = true;
            _dragStartPoint = new Point(-1, -1);

            try
            {
                var data = CreateDataObjectForItems(items, out DragDropEffects allowedEffects);
                if (data == null) return;

                if (TxtStatus != null)
                {
                    TxtStatus.Text = items.Count > 1
                        ? $"⚡ Đang kéo {items.Count} mục... (Thả vào ứng dụng khác để dán/chép)"
                        : $"⚡ Đang kéo: {items[0].PreviewText}... (Thả vào ứng dụng khác)";
                }

                // Cho phép đầy đủ Copy, Move, Link để tương thích 100% với AnyDesk, TeamViewer, RDP, Explorer
                DragDropEffects effectsToAllow = allowedEffects | DragDropEffects.Link | DragDropEffects.Copy | DragDropEffects.Move;
                DragDropEffects result = DragDrop.DoDragDrop(dragSource, data, effectsToAllow);

                if (result != DragDropEffects.None)
                {
                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = "✓ Đã kéo thả thành công vào ứng dụng khác!";
                    }

                    if (_settings != null && _settings.ClipboardAutoHide && !_settings.ClipboardAlwaysOnTop)
                    {
                        Hide();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DragDrop error: " + ex.Message);
            }
            finally
            {
                _isDragging = false;
                _dragStartPoint = new Point(-1, -1);
            }
        }

        private DataObject CreateDataObjectForItems(List<ClipboardItem> items, out DragDropEffects allowedEffects)
        {
            // Mặc định cho phép đầy đủ Copy, Move, Link
            allowedEffects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
            if (items == null || items.Count == 0) return null;

            var data = new DataObject();

            // 1. Nhóm tệp tin & thư mục (Files / Folders) hoặc ảnh (Images lưu file)
            var fileList = new List<string>();
            int dropEffect = 1; // 1 = Copy, 2 = Move

            // 2. Nhóm văn bản thuần túy (Text)
            var textList = new List<string>();

            // 3. Ảnh đầu tiên để hỗ trợ Bitmap (Photoshop, Paint, Word)
            BitmapSource firstBitmap = null;
            string firstImagePath = null;

            foreach (var it in items)
            {
                if (it.ContentType == ClipboardContentType.Files)
                {
                    if (it.FilePaths != null && it.FilePaths.Count > 0)
                    {
                        foreach (var fp in it.FilePaths)
                        {
                            if (!string.IsNullOrWhiteSpace(fp))
                            {
                                fileList.Add(fp.Trim());
                            }
                        }
                    }
                    if (it.DropEffect > 0) dropEffect = it.DropEffect;
                    // LƯU Ý QUAN TRỌNG CHO ANYDESK / TEAMVIEWER / REMOTE DESKTOP:
                    // Tuyệt đối KHÔNG gán it.TextContent vào textList khi kéo Files!
                    // Nếu gán Text, AnyDesk/TeamViewer sẽ nhận diện nhầm đây là Text Drag và từ chối
                    // nhận trên canvas remote desktop (gây hiện biểu tượng cấm 🚫).
                }
                else if (it.ContentType == ClipboardContentType.Image)
                {
                    string resolvedImg = ClipboardItem.ResolvePath(it.ImagePath);
                    if (!string.IsNullOrEmpty(resolvedImg) && File.Exists(resolvedImg))
                    {
                        fileList.Add(resolvedImg);
                        if (firstImagePath == null) firstImagePath = resolvedImg;
                    }
                    if (firstBitmap == null && it.ImageSource is BitmapSource bs)
                    {
                        firstBitmap = bs;
                    }
                    // Tương tự, không đưa preview text vào textList khi kéo ảnh
                }
                else // Văn bản thuần túy
                {
                    if (!string.IsNullOrEmpty(it.TextContent))
                    {
                        textList.Add(it.TextContent);
                    }
                }
            }

            // Gán FileDropList nếu có tệp/thư mục hoặc file ảnh
            if (fileList.Count > 0)
            {
                string[] fileArray = fileList.Where(f => !string.IsNullOrEmpty(f)).Distinct().ToArray();
                if (fileArray.Length > 0)
                {
                    // 1. Gán CF_HDROP chuẩn Win32 OLE với string[] (autoConvert = true) cho AnyDesk, TeamViewer, Explorer
                    data.SetData(DataFormats.FileDrop, fileArray, true);
                    data.SetData("FileDrop", fileArray, true);

                    // 2. Gán StringCollection cho các ứng dụng WPF / .NET
                    var sc = new System.Collections.Specialized.StringCollection();
                    sc.AddRange(fileArray);
                    data.SetFileDropList(sc);

                    // 3. Gán Shell format "FileNameW" và "FileName" cho các native Win32 drop targets
                    data.SetData("FileNameW", fileArray[0]);
                    data.SetData("FileName", fileArray[0]);

                    // 4. Preferred DropEffect: Giá trị chuẩn Shell của Windows Explorer khi drag file:
                    // 5 = DROPEFFECT_COPY | DROPEFFECT_LINK (hỗ trợ cả Copy và Remote Link Transfer)
                    // hoặc 2 = DROPEFFECT_MOVE nếu đang Cut/Move
                    int effVal = (dropEffect == 2) ? 2 : (1 | 4);
                    data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(effVal)));

                    // 5. InShellDragLoop: Báo cho Windows Shell và Remote hook biết đây là Shell drag
                    data.SetData("InShellDragLoop", new MemoryStream(BitConverter.GetBytes(1)));

                    allowedEffects = (dropEffect == 2)
                        ? (DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link)
                        : (DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
                }
            }

            // Gán Bitmap & DIB cho ứng dụng đồ họa/soạn thảo nếu có ảnh
            if (firstImagePath != null && File.Exists(firstImagePath))
            {
                try
                {
                    byte[] pngBytes = File.ReadAllBytes(firstImagePath);
                    data.SetData("PNG", new MemoryStream(pngBytes));

                    using (var ms = new MemoryStream(pngBytes))
                    using (var gdiImg = System.Drawing.Image.FromStream(ms))
                    using (var bmpMs = new MemoryStream())
                    {
                        gdiImg.Save(bmpMs, System.Drawing.Imaging.ImageFormat.Bmp);
                        byte[] bmpData = bmpMs.ToArray();
                        if (bmpData.Length > 14)
                        {
                            // Cắt bỏ 14 byte BITMAPFILEHEADER để tạo DIB stream chuẩn CF_DIB
                            var dibStream = new MemoryStream(bmpData, 14, bmpData.Length - 14);
                            data.SetData(DataFormats.Dib, dibStream);
                        }
                    }
                }
                catch { }
            }

            if (firstBitmap != null)
            {
                try { data.SetImage(firstBitmap); } catch { }
            }
            else if (firstImagePath != null && File.Exists(firstImagePath))
            {
                try
                {
                    var bmp = new BitmapImage(new Uri(firstImagePath));
                    data.SetImage(bmp);
                }
                catch { }
            }

            // Gán Văn bản (Text) CHỈ KHI không có File và không có Image (kéo thả văn bản thuần túy)
            if (fileList.Count == 0 && firstBitmap == null && firstImagePath == null && textList.Count > 0)
            {
                string combinedText = textList.Count == 1 ? textList[0] : FormatMergedText(textList);
                data.SetText(combinedText);
                data.SetData(DataFormats.UnicodeText, combinedText);
                data.SetData(DataFormats.Text, combinedText);
                data.SetData(DataFormats.StringFormat, combinedText);
                allowedEffects = DragDropEffects.Copy | DragDropEffects.Move;
            }

            return data;
        }

        #endregion

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                ExecutePasteSelected(false, false);
                e.Handled = true;
            }
            else if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
            {
                ExecutePasteSelected(false, false);
                e.Handled = true;
            }
            else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                BtnToggleFavoriteAction_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Insert && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                if (Application.Current is App app)
                {
                    app.ToggleClipboardFavorites();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Insert && (Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            {
                if (Application.Current is App app)
                {
                    app.ToggleClipboardHistory();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                CtxMenuEdit_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (TxtPreview != null && !TxtPreview.IsReadOnly && TxtPreview.IsFocused)
                {
                    if (LstClipboard.SelectedItem is ClipboardItem curItem && curItem.ContentType == ClipboardContentType.Text)
                    {
                        curItem.TextContent = TxtPreview.Text;
                        curItem.CharCount = curItem.TextContent.Length;
                        curItem.ByteSize = Encoding.UTF8.GetByteCount(curItem.TextContent);
                        string p = curItem.TextContent.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
                        curItem.PreviewText = p.Length > 120 ? p.Substring(0, 117) + "..." : p;
                        TxtPreview.IsReadOnly = true;
                        _historyManager?.SaveHistoryNow();
                        _itemsView?.Refresh();
                        if (TxtStatus != null) TxtStatus.Text = "✓ Đã lưu thay đổi nội dung văn bản!";
                    }
                    e.Handled = true;
                }
                else
                {
                    CtxMenuSaveAs_Click(sender, null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                CtxMenuMoveUp_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                CtxMenuMoveDown_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (TxtSearch != null && TxtSearch.IsFocused && TxtSearch.SelectionLength > 0)
                {
                    return;
                }
                if (TxtPreview != null && TxtPreview.IsFocused && TxtPreview.SelectionLength > 0)
                {
                    return;
                }

                ExecuteCopySelectedToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (!TxtSearch.IsFocused)
                {
                    ExecutePasteSelected(false, false);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (!TxtSearch.IsFocused)
                {
                    LstClipboard?.SelectAll();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (!TxtSearch.IsFocused)
                {
                    _isSortDescending = !_isSortDescending;
                    ApplySortOrder();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Delete && !TxtSearch.IsFocused)
            {
                BtnDeleteItem_Click(sender, null);
                e.Handled = true;
            }
            else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.D0 && !TxtSearch.IsFocused && Keyboard.Modifiers == ModifierKeys.None)
            {
                ExecutePasteSelected(true, false);
                e.Handled = true;
            }
            else if (e.Key == Key.D1 && !TxtSearch.IsFocused && Keyboard.Modifiers == ModifierKeys.None)
            {
                ExecutePasteSelected(false, true);
                e.Handled = true;
            }
            else if ((e.Key == Key.Down || e.Key == Key.Up) && TxtSearch.IsFocused)
            {
                if (LstClipboard.Items.Count > 0)
                {
                    int curIdx = LstClipboard.SelectedIndex;
                    if (e.Key == Key.Down)
                    {
                        if (curIdx < 0) curIdx = 0;
                        else if (curIdx < LstClipboard.Items.Count - 1) curIdx++;
                    }
                    else if (e.Key == Key.Up)
                    {
                        if (curIdx > 0) curIdx--;
                    }

                    LstClipboard.SelectedIndex = curIdx;
                    var selItem = LstClipboard.SelectedItem as ClipboardItem;
                    if (selItem != null)
                    {
                        LstClipboard.ScrollIntoView(selItem);
                        _lastActiveItemId = selItem.Id;
                    }

                    var container = LstClipboard.ItemContainerGenerator.ContainerFromIndex(curIdx) as ListBoxItem;
                    container?.Focus();
                    e.Handled = true;
                }
            }
        }
    }
}
