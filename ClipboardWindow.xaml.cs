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
using ModernKey.Config;
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

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SIZE = 0xF000;
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

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
        private double _textZoom = 1.0;
        private volatile bool _isBatchUpdating = false;
        private readonly DateTime _sessionStartTime = DateTime.Now;
        private ClipboardItem _quickLookItem = null;
        private DateTime _showTime = DateTime.MinValue;

        public ClipboardWindow(ClipboardHistoryManager historyManager, AppSettings settings)
        {
            _historyManager = historyManager;
            _settings = settings;
            InitializeComponent();

            if (_settings != null)
            {
                _isSequentialPaste = _settings.ClipboardSequentialPaste;
                _isCompactMode = _settings.ClipboardViewModeCompact;
                _isSortDescending = _settings.ClipboardSortDescending;
                _activeFilter = (!string.IsNullOrEmpty(_settings.ClipboardActiveFilter) && _settings.ClipboardActiveFilter != "SESSION" && _settings.ClipboardActiveFilter != "TODAY") ? _settings.ClipboardActiveFilter : "ALL";
                _lastActiveItemId = _settings.ClipboardLastSelectedItemId;
                _textZoom = _settings.ClipboardTextZoom > 0.5 ? _settings.ClipboardTextZoom : 1.0;
            }

            InitCollectionView();
            ApplyTextZoom(_textZoom);

            if (_historyManager != null)
            {
                _historyManager.Items.CollectionChanged += HistoryItems_CollectionChanged;
                _historyManager.FavoriteItems.CollectionChanged += FavoriteItems_CollectionChanged;
            }



            Loaded += ClipboardWindow_Loaded;
            Deactivated += ClipboardWindow_Deactivated;
            IsVisibleChanged += ClipboardWindow_IsVisibleChanged;
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
            if (_isBatchUpdating) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isBatchUpdating) return;
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
            if (_isBatchUpdating) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isBatchUpdating) return;
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
            UpdateSequentialPasteUI();
            UpdateCompactModeUI();
            ApplyCompactModeToItems();
            HighlightActiveTab(_activeFilter);
            ApplyTextZoom(_textZoom);
            if (LstClipboard != null && (LstClipboard.ItemsSource == null || LstClipboard.ItemsSource != _itemsView || _itemsView == null))
            {
                InitCollectionView();
            }
            else
            {
                _itemsView?.Refresh();
            }

            EnsureAppropriateSelection();
            UpdateSlotNumbers();
            ApplyBlurModeUI();
            UpdatePinAlwaysOnTopUI();
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

        private void ClipboardWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                // Khi HUD bị ẩn hoặc đóng: clear preview để giải phóng bitmap lớn
                ClearPreview();
                // Hẹn giờ dọn dẹp nhàn rỗi sau 2 giây
                App.RequestMemoryCleanup(2);
            }
        }

        public void ShowHud(IntPtr targetHwnd, string targetMode = null)
        {
            _showTime = DateTime.Now;
            _lastTargetHwnd = (targetHwnd != IntPtr.Zero && targetHwnd != new System.Windows.Interop.WindowInteropHelper(this).Handle)
                ? targetHwnd
                : GetForegroundWindow();

            Topmost = _settings != null && _settings.ClipboardAlwaysOnTop;
            UpdatePinAlwaysOnTopUI();
            UpdateMergeOptionsButtonLabel();
            ApplyBlurModeUI();

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
            UpdateSlotNumbers();

            // Gợi ý 1: Smart Anchoring - Neo thông minh theo vị trí con trỏ chuột/caret và chống tràn viền màn hình
            try
            {
                var mousePos = System.Windows.Forms.Cursor.Position;
                var currentScreen = System.Windows.Forms.Screen.FromPoint(mousePos);
                var workArea = currentScreen.WorkingArea;

                if (WindowState != WindowState.Maximized)
                {
                    double w = ActualWidth > 0 ? ActualWidth : Width;
                    double h = ActualHeight > 0 ? ActualHeight : Height;

                    double targetLeft = mousePos.X + 16;
                    double targetTop = mousePos.Y + 16;

                    // Lật sang trái nếu vượt quá mép phải
                    if (targetLeft + w > workArea.Right)
                    {
                        targetLeft = mousePos.X - w - 16;
                    }
                    // Lật lên trên nếu vượt quá mép dưới
                    if (targetTop + h > workArea.Bottom)
                    {
                        targetTop = mousePos.Y - h - 16;
                    }

                    // Đảm bảo không bị khuất ra ngoài cạnh trái / trên
                    if (targetLeft < workArea.Left) targetLeft = workArea.Left + 8;
                    if (targetTop < workArea.Top) targetTop = workArea.Top + 8;

                    Left = targetLeft;
                    Top = targetTop;
                }
            }
            catch { }

            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
            // Focus vào clipboard danh sách thay vì ô search
            LstClipboard?.Focus();
            if (LstClipboard?.SelectedItem != null)
            {
                LstClipboard.ScrollIntoView(LstClipboard.SelectedItem);
                var container = LstClipboard.ItemContainerGenerator.ContainerFromItem(LstClipboard.SelectedItem) as ListBoxItem;
                container?.Focus();
            }
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
                // Chọn item đầu tiên hiển thị theo chiều sắp xếp hiện tại (không ép reset về newest)
                var firstItem = LstClipboard.Items[0] as ClipboardItem;
                LstClipboard.SelectedItem = firstItem;
                if (firstItem != null)
                {
                    LstClipboard.ScrollIntoView(firstItem);
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

            // 1. Lọc theo tab danh mục & thời gian (Chuẩn Comfort Keys Pro & Tab Pills)
            if (_activeFilter == "FAV" && !item.IsFavorite) return false;
            if (_activeFilter == "TXT" && item.ContentType != ClipboardContentType.Text) return false;
            if (_activeFilter == "IMG" && item.ContentType != ClipboardContentType.Image) return false;
            if (_activeFilter == "FILES" && item.ContentType != ClipboardContentType.Files) return false;
            if (_activeFilter == "URL" && !item.IsUrl) return false;
            if (_activeFilter == "CODE" && !item.IsCode) return false;

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

            // 2. Lọc theo từ khóa tìm kiếm (Gợi ý 7: Fuzzy match hỗ trợ gõ tắt và không dấu)
            string query = TxtSearch.Text?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                if (IsFuzzyMatch(item.TextContent, query)) return true;
                if (IsFuzzyMatch(item.PreviewText, query)) return true;
                if (IsFuzzyMatch(item.CustomTitle, query)) return true;
                if (IsFuzzyMatch(item.GroupName, query)) return true;
                return false;
            }

            return true;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LstClipboard == null) return;
            _itemsView?.Refresh();
            UpdateSlotNumbers();
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
                HighlightActiveTab("FAV");
            }
            else
            {
                _currentMode = "HISTORY";
                if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ LỊCH SỬ";
                if (_activeFilter == "FAV") _activeFilter = "ALL";
                HighlightActiveTab(_activeFilter);
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
            int urlCount = targetItems.Count(x => x.IsUrl);
            int codeCount = targetItems.Count(x => x.IsCode);

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
            AddFilterItem("Tệp tin & Thư mục (Files)", "FILES", filesCount, "• ");
            AddFilterItem("Hình ảnh (Picture)", "IMG", imgCount, "• ");
            AddFilterItem("Đường dẫn web (URL)", "URL", urlCount, "• ");
            AddFilterItem("Mã lập trình (Code)", "CODE", codeCount, "• ");

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
            if (_settings != null)
            {
                _settings.ClipboardSortDescending = _isSortDescending;
                Config.SettingsManager.SaveSettings(_settings);
            }
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
            if (_settings != null)
            {
                _settings.ClipboardLastSelectedItemId = _lastActiveItemId;
            }

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
                if (PnlWebAiImageActions != null) PnlWebAiImageActions.Visibility = Visibility.Visible;
                if (TxtMetaInfo != null) TxtMetaInfo.Text = $"[HÌNH ẢNH]{appPart} • Dung lượng: {item.ByteSize / 1024} KB • Thời gian: {item.TimeDisplay}";
            }
            else if (item.ContentType == ClipboardContentType.Files)
            {
                if (PnlWebAiImageActions != null) PnlWebAiImageActions.Visibility = Visibility.Collapsed;
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
                if (PnlWebAiImageActions != null) PnlWebAiImageActions.Visibility = Visibility.Collapsed;
                if (ImgPreview != null) ImgPreview.Visibility = Visibility.Collapsed;
                if (TxtPreview != null)
                {
                    TxtPreview.Visibility = Visibility.Visible;
                    if (item.IsSensitive && item.IsMasked)
                    {
                        TxtPreview.Text = "••••••••••••••••••••••••••••••••\r\n[NỘI DUNG ĐÃ ĐƯỢC BẢO MẬT]\r\n(Nhấp vào biểu tượng 👁 trên danh sách để xem nội dung đầy đủ)";
                    }
                    else
                    {
                        TxtPreview.Text = item.TextContent ?? string.Empty;
                    }
                }
                int lineCount = (item.TextContent ?? "").Split('\n').Length;
                if (TxtMetaInfo != null) TxtMetaInfo.Text = $"Văn bản{appPart} • {item.CharCount} ký tự • {lineCount} dòng • {item.ByteSize} B • {item.TimeDisplay}";
            }

            // Đồng bộ hiệu ứng làm mờ lên khung Preview: nếu không mờ thì Effect = null (100% rõ nét)
            if (item != null && item.IsBlurred)
            {
                ApplyBlurToHost(ImgPreview, item);
                ApplyBlurToHost(TxtPreview, item);
            }
            else
            {
                if (ImgPreview != null) ImgPreview.Effect = null;
                if (TxtPreview != null) TxtPreview.Effect = null;
            }
        }

        private void ClearPreview()
        {
            if (PnlWebAiImageActions != null)
            {
                PnlWebAiImageActions.Visibility = Visibility.Collapsed;
            }
            if (TxtPreview != null)
            {
                TxtPreview.Visibility = Visibility.Visible;
                TxtPreview.Text = string.Empty;
                TxtPreview.Effect = null;
            }
            if (ImgPreview != null)
            {
                ImgPreview.Visibility = Visibility.Collapsed;
                ImgPreview.Source = null;
                ImgPreview.Effect = null;
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

        private void LstClipboard_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && ((Keyboard.Modifiers & ModifierKeys.Control) != 0 || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                var element = e.OriginalSource as DependencyObject;
                while (element != null && !(element is ListBoxItem) && element != LstClipboard)
                {
                    element = VisualTreeHelper.GetParent(element);
                }

                ClipboardItem item = null;
                if (element is ListBoxItem lbi)
                {
                    item = lbi.DataContext as ClipboardItem;
                }
                if (item == null)
                {
                    item = LstClipboard?.SelectedItem as ClipboardItem;
                }

                if (item != null)
                {
                    e.Handled = true;
                    OpenClipboardItemFile(item);
                }
            }
        }

        private void OpenClipboardItemFile(ClipboardItem item)
        {
            if (item == null) return;
            try
            {
                if (item.ContentType == ClipboardContentType.Image)
                {
                    OpenImageInDefaultViewer(item);
                    return;
                }
                else if (item.ContentType == ClipboardContentType.Files && item.FilePaths != null && item.FilePaths.Count > 0)
                {
                    foreach (var p in item.FilePaths)
                    {
                        string resolved = ClipboardItem.ResolvePath(p);
                        if (File.Exists(resolved) || Directory.Exists(resolved))
                        {
                            Process.Start(new ProcessStartInfo(resolved) { UseShellExecute = true });
                            if (TxtStatus != null) TxtStatus.Text = $"✓ Đã mở tệp bằng ứng dụng mặc định: {Path.GetFileName(resolved)}";
                        }
                    }
                    return;
                }

                string txt = item.TextContent ?? string.Empty;
                string trimmed = txt.Trim();
                string resolvedTxtPath = ClipboardItem.ResolvePath(trimmed);

                if (!string.IsNullOrWhiteSpace(trimmed) && (File.Exists(resolvedTxtPath) || Directory.Exists(resolvedTxtPath)))
                {
                    Process.Start(new ProcessStartInfo(resolvedTxtPath) { UseShellExecute = true });
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã mở tệp bằng ứng dụng mặc định: {Path.GetFileName(resolvedTxtPath)}";
                }
                else if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    Process.Start(new ProcessStartInfo(uriResult.AbsoluteUri) { UseShellExecute = true });
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã mở liên kết qua trình duyệt: {uriResult.Host}";
                }
                else if (!string.IsNullOrWhiteSpace(txt))
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "ModernKey");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                    string tempFile = Path.Combine(tempDir, $"clip_{item.Id ?? Guid.NewGuid().ToString("N")}.txt");
                    File.WriteAllText(tempFile, txt, Encoding.UTF8);
                    Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã mở văn bản bằng ứng dụng mặc định: {Path.GetFileName(tempFile)}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening clipboard item file: " + ex.Message);
                if (TxtStatus != null) TxtStatus.Text = "❌ Lỗi khi mở tệp: " + ex.Message;
            }
        }

        private void CtxMenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItem as ClipboardItem;
            if (selected != null)
            {
                OpenClipboardItemFile(selected);
            }
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
                        if (fileItems.Count > 1)
                        {
                            _historyManager?.AddFiles(allFiles, effect, "ModernKey Gộp Tệp", null);
                            _itemsView?.Refresh();
                        }
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

            bool keepHudOpen = _isSequentialPaste || (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            // Ẩn HUD ngay lập tức nếu bật AutoHide và không ở chế độ dán liên tiếp
            if (!keepHudOpen && _settings != null && _settings.ClipboardAutoHide)
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
                            string fullImgPath = ClipboardItem.GetFullImagePath(item);
                            if (!string.IsNullOrEmpty(fullImgPath) && System.IO.File.Exists(fullImgPath))
                            {
                                using (var fullImg = System.Drawing.Image.FromFile(fullImgPath))
                                {
                                    System.Windows.Forms.Clipboard.SetImage(fullImg);
                                }
                            }
                            else if (item.FullImageSource != null || item.ImageSource != null)
                            {
                                Clipboard.SetImage(item.FullImageSource ?? item.ImageSource);
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

                if (keepHudOpen)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (LstClipboard != null && LstClipboard.Items.Count > 0)
                        {
                            int nextIdx = LstClipboard.SelectedIndex + 1;
                            if (nextIdx < LstClipboard.Items.Count)
                            {
                                LstClipboard.SelectedIndex = nextIdx;
                                var nextItem = LstClipboard.SelectedItem as ClipboardItem;
                                if (nextItem != null) LstClipboard.ScrollIntoView(nextItem);
                            }
                        }
                        if (TxtStatus != null)
                        {
                            TxtStatus.Text = "✓ [DÁN LIÊN TIẾP] Đã dán mục, sẵn sàng dán mục tiếp theo!";
                        }
                    }));
                }

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

            try
            {
                _isBatchUpdating = true;
                // 1. Giải phóng Image Preview và visual resources trước khi xóa để file lock không bị giữ
                ClearPreview();
                foreach (var it in selected)
                {
                    it?.ReleaseVisualResources();
                }

                // Bỏ chọn tất cả trước để WPF ListBox không phải tính toán lại Selection sau mỗi mục xóa
                LstClipboard?.UnselectAll();

                _historyManager?.DeleteItems(selected, isFavView);
            }
            finally
            {
                _isBatchUpdating = false;
            }

            _itemsView?.Refresh();
            if (LstClipboard.Items.Count > 0)
            {
                int newIdx = Math.Min(curIdx, LstClipboard.Items.Count - 1);
                if (newIdx >= 0)
                {
                    LstClipboard.SelectedIndex = newIdx;
                    if (LstClipboard.SelectedItem != null)
                    {
                        LstClipboard.ScrollIntoView(LstClipboard.SelectedItem);
                    }
                }
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
                    try
                    {
                        _isBatchUpdating = true;
                        ClearPreview();
                        if (LstClipboard?.Items != null)
                        {
                            foreach (var it in LstClipboard.Items.OfType<ClipboardItem>())
                            {
                                it?.ReleaseVisualResources();
                            }
                        }
                        LstClipboard?.UnselectAll();
                        _historyManager?.ClearFavorites();
                    }
                    finally
                    {
                        _isBatchUpdating = false;
                    }
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
                    try
                    {
                        _isBatchUpdating = true;
                        ClearPreview();
                        if (LstClipboard?.Items != null)
                        {
                            foreach (var it in LstClipboard.Items.OfType<ClipboardItem>())
                            {
                                it?.ReleaseVisualResources();
                            }
                        }
                        LstClipboard?.UnselectAll();
                        _historyManager?.ClearHistory();
                    }
                    finally
                    {
                        _isBatchUpdating = false;
                    }
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
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        #region Window Control & Resizing (Chuẩn Window, Maximize, Resize)

        private void ClipboardWindow_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
            source?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            try
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                IntPtr monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
                if (monitor != IntPtr.Zero)
                {
                    MONITORINFO mi = new MONITORINFO();
                    if (GetMonitorInfo(monitor, mi))
                    {
                        RECT rcWork = mi.rcWork;
                        RECT rcMonitor = mi.rcMonitor;
                        mmi.ptMaxPosition.x = Math.Abs(rcWork.left - rcMonitor.left);
                        mmi.ptMaxPosition.y = Math.Abs(rcWork.top - rcMonitor.top);
                        mmi.ptMaxSize.x = Math.Abs(rcWork.right - rcWork.left);
                        mmi.ptMaxSize.y = Math.Abs(rcWork.bottom - rcWork.top);
                        mmi.ptMinTrackSize.x = (int)MinWidth;
                        mmi.ptMinTrackSize.y = (int)MinHeight;
                    }
                }
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            catch { }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                if (MainContainerBorder != null)
                {
                    MainContainerBorder.Margin = new Thickness(0);
                    MainContainerBorder.CornerRadius = new CornerRadius(0);
                }
                if (TxtMaximizeIcon != null) TxtMaximizeIcon.Text = "🗗";
                if (BtnMaximize != null) BtnMaximize.ToolTip = "Khôi phục kích thước cửa sổ";
            }
            else
            {
                if (MainContainerBorder != null)
                {
                    MainContainerBorder.Margin = new Thickness(8);
                    MainContainerBorder.CornerRadius = new CornerRadius(4);
                }
                if (TxtMaximizeIcon != null) TxtMaximizeIcon.Text = "🗖";
                if (BtnMaximize != null) BtnMaximize.ToolTip = "Phóng to (Maximize)";
            }
        }

        public void UpdatePinAlwaysOnTopUI()
        {
            if (BtnPinAlwaysOnTop == null || TxtPinIcon == null) return;
            bool isPinned = _settings != null && _settings.ClipboardAlwaysOnTop;
            Topmost = isPinned;
            if (isPinned)
            {
                BtnPinAlwaysOnTop.Background = (Brush)new BrushConverter().ConvertFromString("#142B3E");
                BtnPinAlwaysOnTop.BorderBrush = (Brush)FindResource("CyberNeonCyan");
                TxtPinIcon.Foreground = (Brush)FindResource("CyberNeonCyan");
                BtnPinAlwaysOnTop.ToolTip = "Ghim cửa sổ luôn nổi trên cùng (Always on top): ĐANG BẬT";
            }
            else
            {
                BtnPinAlwaysOnTop.Background = (Brush)new BrushConverter().ConvertFromString("#15212C");
                BtnPinAlwaysOnTop.BorderBrush = (Brush)FindResource("CyberBorderDim");
                TxtPinIcon.Foreground = (Brush)FindResource("CyberTextSecondary");
                BtnPinAlwaysOnTop.ToolTip = "Ghim cửa sổ luôn nổi trên cùng (Always on top): ĐANG TẮT";
            }
        }

        private void BtnPinAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.ClipboardAlwaysOnTop = !_settings.ClipboardAlwaysOnTop;
                Config.SettingsManager.SaveSettings(_settings);
            }
            UpdatePinAlwaysOnTopUI();
            if (TxtStatus != null)
            {
                bool isPinned = _settings != null && _settings.ClipboardAlwaysOnTop;
                TxtStatus.Text = isPinned ? "✓ Đã BẬT chế độ Ghim luôn nổi trên cùng (Always On Top)!" : "✓ Đã TẮT chế độ Ghim luôn nổi trên cùng (Always On Top)!";
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        public void ToggleMaximize()
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe && fe.Tag is string tag)
            {
                int direction = 0;
                switch (tag)
                {
                    case "Left": direction = 1; break;
                    case "Right": direction = 2; break;
                    case "Top": direction = 3; break;
                    case "TopLeft": direction = 4; break;
                    case "TopRight": direction = 5; break;
                    case "Bottom": direction = 6; break;
                    case "BottomLeft": direction = 7; break;
                    case "BottomRight": direction = 8; break;
                }
                if (direction > 0)
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    ReleaseCapture();
                    SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + direction), IntPtr.Zero);
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Dán Liên Tiếp & Giao Diện Thu Gọn (Sequential Paste & Compact Mode)

        private bool _isSequentialPaste = false;
        public bool IsSequentialPaste
        {
            get => _isSequentialPaste;
            set
            {
                _isSequentialPaste = value;
                if (_settings != null) _settings.ClipboardSequentialPaste = value;
                UpdateSequentialPasteUI();
            }
        }

        private void BtnSequentialPasteToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleSequentialPaste();
        }

        public void ToggleSequentialPaste()
        {
            IsSequentialPaste = !IsSequentialPaste;
            if (TxtStatus != null)
            {
                TxtStatus.Text = _isSequentialPaste
                    ? "✓ Chế độ Dán Liên Tiếp: ĐÃ BẬT (HUD sẽ giữ mở sau khi dán)"
                    : "✓ Chế độ Dán Liên Tiếp: ĐÃ TẮT";
            }
        }

        private void UpdateSequentialPasteUI()
        {
            if (BtnSequentialPasteToggle == null || TxtSequentialLabel == null || TxtSequentialIcon == null) return;
            if (_isSequentialPaste)
            {
                BtnSequentialPasteToggle.Background = (Brush)new BrushConverter().ConvertFromString("#183526");
                BtnSequentialPasteToggle.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#00FF66");
                TxtSequentialLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#00FF66");
                TxtSequentialLabel.Text = "DÁN LIÊN TIẾP: BẬT";
                TxtSequentialIcon.Foreground = (Brush)new BrushConverter().ConvertFromString("#00FF66");
            }
            else
            {
                BtnSequentialPasteToggle.Background = (Brush)new BrushConverter().ConvertFromString("#101B24");
                BtnSequentialPasteToggle.BorderBrush = (Brush)FindResource("CyberBorderDim");
                TxtSequentialLabel.Foreground = (Brush)FindResource("CyberTextSecondary");
                TxtSequentialLabel.Text = "DÁN LIÊN TIẾP: TẮT";
                TxtSequentialIcon.Foreground = (Brush)FindResource("CyberTextSecondary");
            }
        }

        private bool _isCompactMode = false;
        public bool IsCompactMode
        {
            get => _isCompactMode;
            set
            {
                _isCompactMode = value;
                if (_settings != null) _settings.ClipboardViewModeCompact = value;
                UpdateCompactModeUI();
                ApplyCompactModeToItems();
            }
        }

        private void BtnCompactToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleCompactMode();
        }

        public void ToggleCompactMode()
        {
            IsCompactMode = !IsCompactMode;
            if (TxtStatus != null)
            {
                TxtStatus.Text = _isCompactMode ? "✓ Chế độ xem: THU GỌN (1 dòng)" : "✓ Chế độ xem: ĐẦY ĐỦ (2 dòng)";
            }
        }

        private void UpdateCompactModeUI()
        {
            if (BtnCompactToggle == null || TxtCompactLabel == null) return;
            if (_isCompactMode)
            {
                BtnCompactToggle.Background = (Brush)new BrushConverter().ConvertFromString("#1F3325");
                BtnCompactToggle.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#00FF66");
                TxtCompactLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#00FF66");
                TxtCompactLabel.Text = "☰ GỌN (BẬT)";
            }
            else
            {
                BtnCompactToggle.Background = (Brush)new BrushConverter().ConvertFromString("#10141E");
                BtnCompactToggle.BorderBrush = (Brush)FindResource("CyberNeonCyan");
                TxtCompactLabel.Foreground = (Brush)FindResource("CyberNeonCyan");
                TxtCompactLabel.Text = "☰ GỌN";
            }
        }

        private void ApplyCompactModeToItems()
        {
            if (_historyManager != null)
            {
                foreach (var item in _historyManager.Items) item.IsCompactView = _isCompactMode;
                foreach (var item in _historyManager.FavoriteItems) item.IsCompactView = _isCompactMode;
            }
            _itemsView?.Refresh();
        }

        private void UpdateSlotNumbers()
        {
            if (LstClipboard == null) return;
            int slot = 1;
            foreach (var obj in LstClipboard.Items)
            {
                if (obj is ClipboardItem item)
                {
                    item.SlotNumber = (slot <= 9) ? slot : 0;
                    slot++;
                }
            }
        }

        #endregion

        #region Tab Filter & Quick Look Inspector & In-Line Actions

        private void FilterTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (string.Equals(tag, "FAV", StringComparison.OrdinalIgnoreCase))
                {
                    if (RbModeFavorites != null && RbModeFavorites.IsChecked != true)
                    {
                        RbModeFavorites.IsChecked = true;
                    }
                }
                _activeFilter = tag;
                if (_settings != null)
                {
                    _settings.ClipboardActiveFilter = _activeFilter;
                    Config.SettingsManager.SaveSettings(_settings);
                }
                HighlightActiveTab(tag);
                _itemsView?.Refresh();
                UpdateSlotNumbers();
                EnsureAppropriateSelection();
            }
        }

        private void HighlightActiveTab(string activeTag)
        {
            var tabs = new[] { BtnTabAll, BtnTabFav, BtnTabTxt, BtnTabFiles, BtnTabImg, BtnTabUrl, BtnTabCode };
            foreach (var t in tabs)
            {
                if (t == null) continue;
                bool isActive = string.Equals(t.Tag as string, activeTag, StringComparison.OrdinalIgnoreCase);
                t.Background = isActive ? (Brush)new BrushConverter().ConvertFromString("#142B3E") : (Brush)new BrushConverter().ConvertFromString("#10141E");
                t.BorderBrush = isActive ? (Brush)FindResource("CyberNeonCyan") : (Brush)FindResource("CyberBorderDim");
                t.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        public void ApplyTextZoom(double zoom)
        {
            _textZoom = Math.Max(0.7, Math.Min(2.0, Math.Round(zoom, 2)));
            if (ClipboardListScale != null)
            {
                ClipboardListScale.ScaleX = _textZoom;
                ClipboardListScale.ScaleY = _textZoom;
            }
            if (ClipboardPreviewScale != null)
            {
                ClipboardPreviewScale.ScaleX = _textZoom;
                ClipboardPreviewScale.ScaleY = _textZoom;
            }
            if (TxtStatus != null)
            {
                TxtStatus.Text = $"Cỡ chữ / Thu phóng: {(int)(_textZoom * 100)}% (Ctrl + - / = / 0)";
            }
            if (_settings != null)
            {
                _settings.ClipboardTextZoom = _textZoom;
                Config.SettingsManager.SaveSettings(_settings);
            }
        }

        public void OpenImageInDefaultViewer(ClipboardItem item)
        {
            if (item == null) return;
            try
            {
                // 1. Định vị đường dẫn file ảnh gốc chất lượng cao (không có hậu tố _t.png)
                string sourceFilePath = ClipboardItem.GetFullImagePath(item);
                if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                {
                    sourceFilePath = ClipboardItem.ResolvePath(item.ImagePath);
                }

                if (!string.IsNullOrEmpty(sourceFilePath) && sourceFilePath.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase))
                {
                    string nonThumb = sourceFilePath.Substring(0, sourceFilePath.Length - 6) + ".png";
                    if (File.Exists(nonThumb))
                    {
                        sourceFilePath = nonThumb;
                    }
                }

                // 2. BƯỚC TRUNG GIAN (Intermediate Step):
                // Sao chép hoặc xuất ảnh gốc ra một thư mục tạm chuẩn (%TEMP%\ModernKey_ImageViewer)
                // Điều này giải quyết triệt để lỗi của Windows Photos App và các app UWP không thể truy cập
                // file nằm trong thư mục ẩn .portable hoặc đường dẫn NTFS Symlink.
                string tempDir = Path.Combine(Path.GetTempPath(), "ModernKey_ImageViewer");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string destFileName = !string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath)
                    ? Path.GetFileName(sourceFilePath)
                    : $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                if (destFileName.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase))
                {
                    destFileName = destFileName.Substring(0, destFileName.Length - 6) + ".png";
                }

                string targetTempFile = Path.Combine(tempDir, destFileName);

                if (!string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath))
                {
                    File.Copy(sourceFilePath, targetTempFile, true);
                    try
                    {
                        File.SetAttributes(targetTempFile, FileAttributes.Normal);
                    }
                    catch { }
                }
                else
                {
                    // Dự phòng: Nếu file không tồn tại trên đĩa, xuất trực tiếp từ Bitmap gốc trong RAM
                    var fullBmp = item.FullImageSource ?? item.ImageSource;
                    if (fullBmp is BitmapSource bs)
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bs));
                        using (var fs = new FileStream(targetTempFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                        {
                            encoder.Save(fs);
                        }
                    }
                }

                if (File.Exists(targetTempFile))
                {
                    bool launched = false;
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = targetTempFile,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        launched = true;
                    }
                    catch (Exception exLaunch)
                    {
                        Debug.WriteLine("Process.Start failed, trying fallback: " + exLaunch.Message);
                    }

                    // Fallback 1: Thử mở qua explorer.exe
                    if (!launched)
                    {
                        try
                        {
                            Process.Start("explorer.exe", $"\"{targetTempFile}\"");
                            launched = true;
                        }
                        catch { }
                    }

                    // Fallback 2: Thử mở qua cmd start
                    if (!launched)
                    {
                        try
                        {
                            var psiCmd = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c start \"\" \"{targetTempFile}\"",
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                UseShellExecute = false
                            };
                            Process.Start(psiCmd);
                            launched = true;
                        }
                        catch { }
                    }

                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = $"✓ Đã mở ảnh gốc qua trình xem ảnh mặc định: {Path.GetFileName(targetTempFile)}";
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                if (TxtStatus != null) TxtStatus.Text = $"Lỗi mở ảnh: {ex.Message}";
            }
        }

        public void ExecutePreviewItem(ClipboardItem item)
        {
            if (item == null) return;
            OpenQuickLook(item);
        }

        private void ImgPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = _quickLookItem ?? LstClipboard?.SelectedItem as ClipboardItem;
            if (item != null && item.ContentType == ClipboardContentType.Image)
            {
                OpenImageInDefaultViewer(item);
            }
        }

        private void ImgQuickLookBody_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = _quickLookItem ?? LstClipboard?.SelectedItem as ClipboardItem;
            if (item != null && item.ContentType == ClipboardContentType.Image)
            {
                OpenImageInDefaultViewer(item);
            }
        }

        private void BtnQuickLookOpenExternal_Click(object sender, RoutedEventArgs e)
        {
            var item = _quickLookItem ?? LstClipboard?.SelectedItem as ClipboardItem;
            if (item != null && item.ContentType == ClipboardContentType.Image)
            {
                OpenImageInDefaultViewer(item);
            }
        }

        public void ToggleQuickLook()
        {
            if (PnlQuickLook != null && PnlQuickLook.Visibility == Visibility.Visible)
            {
                CloseQuickLook();
            }
            else
            {
                if (LstClipboard?.SelectedItem is ClipboardItem item)
                {
                    OpenQuickLook(item);
                }
            }
        }

        public void OpenQuickLook(ClipboardItem item)
        {
            if (item == null || PnlQuickLook == null) return;
            _quickLookItem = item;

            if (TxtQuickLookTitle != null)
            {
                string typeLabel = item.TypeBadge;
                string sub = item.HasCustomTitle ? item.CustomTitle : (item.ContentType == ClipboardContentType.Image ? "[ẢNH]" : (item.ContentType == ClipboardContentType.Files ? $"[{item.CharCount} tệp/thư mục]" : $"{item.CharCount} ký tự"));
                TxtQuickLookTitle.Text = $"{typeLabel} - {sub}";
            }

            if (item.ContentType == ClipboardContentType.Image)
            {
                if (TxtQuickLookBody != null) TxtQuickLookBody.Visibility = Visibility.Collapsed;
                if (ImgQuickLookBody != null)
                {
                    ImgQuickLookBody.Visibility = Visibility.Visible;
                    // Sử dụng FullImageSource để hiển thị ảnh gốc độ phân giải đầy đủ, không bị mờ 480px
                    var fullSource = item.FullImageSource ?? item.ImageSource;
                    ImgQuickLookBody.Source = fullSource;

                    if (fullSource != null && TxtQuickLookTitle != null)
                    {
                        string sizeInfo = item.ByteSize > 0 ? $" • {item.ByteSize / 1024} KB" : "";
                        TxtQuickLookTitle.Text = $"[ẢNH {fullSource.PixelWidth}x{fullSource.PixelHeight}{sizeInfo}] - Nhấn F hoặc click ảnh để mở trình xem ngoài";
                    }
                }
                if (BtnQuickLookOpenExternal != null) BtnQuickLookOpenExternal.Visibility = Visibility.Visible;
            }
            else
            {
                if (BtnQuickLookOpenExternal != null) BtnQuickLookOpenExternal.Visibility = Visibility.Collapsed;
                if (ImgQuickLookBody != null)
                {
                    ImgQuickLookBody.Visibility = Visibility.Collapsed;
                    ImgQuickLookBody.Source = null;
                }
                if (TxtQuickLookBody != null)
                {
                    TxtQuickLookBody.Visibility = Visibility.Visible;
                    if (item.ContentType == ClipboardContentType.Files)
                    {
                        var sb = new StringBuilder();
                        string mode = item.DropEffect == 2 ? "CUT (Di chuyển)" : "COPY (Sao chép)";
                        sb.AppendLine($"=== DANH SÁCH TỆP / THƯ MỤC ({item.CharCount} mục - {mode}) ===");
                        sb.AppendLine();
                        var paths = item.FilePaths;
                        for (int i = 0; i < paths.Count; i++)
                        {
                            string p = paths[i];
                            sb.AppendLine($"[{i + 1}] {p}");
                        }
                        TxtQuickLookBody.Text = sb.ToString();
                    }
                    else
                    {
                        TxtQuickLookBody.Text = item.TextContent ?? string.Empty;
                    }
                }
            }

            PnlQuickLook.Visibility = Visibility.Visible;
        }

        public void CloseQuickLook()
        {
            if (PnlQuickLook != null)
            {
                PnlQuickLook.Visibility = Visibility.Collapsed;
                if (ImgQuickLookBody != null) ImgQuickLookBody.Source = null;
            }
            if (_quickLookItem != null)
            {
                _quickLookItem.ReleaseVisualResources();
                _quickLookItem = null;
            }
        }

        private void BtnCloseQuickLook_Click(object sender, RoutedEventArgs e)
        {
            CloseQuickLook();
        }

        private void BtnQuickLookPaste_Click(object sender, RoutedEventArgs e)
        {
            CloseQuickLook();
            ExecutePasteSelected(false, false);
        }

        private void BtnQuickLookCopy_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCopySelectedToClipboard();
            if (TxtStatus != null) TxtStatus.Text = "✓ Đã sao chép nội dung Quick Look vào Clipboard!";
        }

        private void BtnItemQuickCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
            {
                LstClipboard.SelectedItem = item;
                ExecuteCopySelectedToClipboard();
            }
        }

        private void BtnItemQuickLook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
            {
                LstClipboard.SelectedItem = item;
                ExecutePreviewItem(item);
            }
        }

        private void BtnItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
            {
                LstClipboard.SelectedItem = item;
                BtnDeleteItem_Click(sender, e);
            }
        }

        private static bool IsFuzzyMatch(string source, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (string.IsNullOrEmpty(source)) return false;

            // 1. So khớp trực tiếp (nhanh)
            if (source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // 2. So khớp không dấu tiếng Việt
            string sClean = RemoveDiacritics(source);
            string qClean = RemoveDiacritics(query);
            if (sClean.IndexOf(qClean, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // 3. So khớp dãy ký tự con (Subsequence match - gõ tắt)
            if (qClean.Length > 1 && qClean.Length <= 8)
            {
                int qIdx = 0;
                for (int sIdx = 0; sIdx < sClean.Length && qIdx < qClean.Length; sIdx++)
                {
                    if (char.ToLowerInvariant(sClean[sIdx]) == char.ToLowerInvariant(qClean[qIdx]))
                    {
                        qIdx++;
                    }
                }
                if (qIdx == qClean.Length) return true;
            }

            return false;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        #endregion

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
                try
                {
                    _isBatchUpdating = true;
                    ClearPreview();
                    foreach (var it in selected)
                    {
                        it?.ReleaseVisualResources();
                    }
                    LstClipboard?.UnselectAll();
                    _historyManager?.RemoveFromFavorites(selected);
                }
                finally
                {
                    _isBatchUpdating = false;
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
                var dlg = new EditClipboardDialog(item)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    _historyManager?.SaveHistoryNow();
                    _historyManager?.SaveFavoritesNow();
                    _itemsView?.Refresh();
                    UpdatePreview(item);

                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = $"✓ Đã cập nhật mục: {item.DisplayTitle}";
                    }
                }
            }
        }

        private void CtxMenuPreview_Click(object sender, RoutedEventArgs e)
        {
            if (LstClipboard?.SelectedItem is ClipboardItem item)
            {
                ExecutePreviewItem(item);
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
                        string imgPath = ClipboardItem.GetFullImagePath(selected[0]);
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
            if (selected == null || selected.Count < 2)
            {
                MessageBox.Show("Vui lòng chọn từ 2 mục trở lên để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var fileItems = selected.Where(x => x.ContentType == ClipboardContentType.Files).ToList();
            if (fileItems.Count > 0 && selected.All(x => x.ContentType == ClipboardContentType.Files))
            {
                CtxMenuMergeFiles_Click(sender, e);
                return;
            }

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

        private void CtxMenuMergeFiles_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected != null && selected.Count > 1)
            {
                var fileItems = selected.Where(x => x.ContentType == ClipboardContentType.Files).ToList();
                if (fileItems.Count > 0)
                {
                    var allFiles = fileItems.SelectMany(x => x.FilePaths).Distinct().ToList();
                    int effect = fileItems[0].DropEffect;
                    SetClipboardDataForFiles(allFiles, effect);
                    _historyManager?.AddFiles(allFiles, effect, "ModernKey Gộp Tệp", null);
                    _itemsView?.Refresh();
                    EnsureAppropriateSelection();
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã gộp {fileItems.Count} mục tệp ({allFiles.Count} đường dẫn) thành 1 mục tệp mới!";
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn từ 2 mục tệp trở lên để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
            // Nếu khung Quick Look Inspector đang mở
            if (PnlQuickLook != null && PnlQuickLook.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape || e.Key == Key.Space)
                {
                    CloseQuickLook();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.F)
                {
                    var quickImg = _quickLookItem ?? LstClipboard?.SelectedItem as ClipboardItem;
                    if (quickImg != null && quickImg.ContentType == ClipboardContentType.Image)
                    {
                        OpenImageInDefaultViewer(quickImg);
                        e.Handled = true;
                        return;
                    }
                }
                if (e.Key == Key.Enter)
                {
                    CloseQuickLook();
                    ExecutePasteSelected(false, false);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    ExecuteCopySelectedToClipboard();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                // Shift + Enter: Dán Plain Text (Văn bản thuần không định dạng)
                ExecutePasteSelected(true, false);
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
            else if (e.Key == Key.F && (TxtSearch == null || !TxtSearch.IsFocused))
            {
                // Phím F: Mở ảnh full bằng ứng dụng xem ảnh mặc định
                if (LstClipboard?.SelectedItem is ClipboardItem item && item.ContentType == ClipboardContentType.Image)
                {
                    OpenImageInDefaultViewer(item);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Space && (TxtSearch == null || !TxtSearch.IsFocused))
            {
                // Phím Space: Xem trước (Mở Quick Look với ảnh full nét hoặc văn bản chi tiết)
                if (LstClipboard?.SelectedItem is ClipboardItem item)
                {
                    ExecutePreviewItem(item);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Q && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                // Ctrl + Q: Bật/Tắt chế độ Dán liên tiếp
                ToggleSequentialPaste();
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
            // Thu nhỏ cỡ chữ clipboard (Ctrl + -)
            else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                ApplyTextZoom(_textZoom - 0.1);
                e.Handled = true;
            }
            // Phóng to cỡ chữ clipboard (Ctrl + = / Ctrl + +)
            else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                ApplyTextZoom(_textZoom + 0.1);
                e.Handled = true;
            }
            // Reset cỡ chữ clipboard về 100% (Ctrl + 0)
            else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                ApplyTextZoom(1.0);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 && !TxtSearch.IsFocused && Keyboard.Modifiers == ModifierKeys.None)
            {
                ExecutePasteSelected(true, false);
                e.Handled = true;
            }
            // Gợi ý 2: Bấm phím 1-9 dán tức thì slot 1-9 (hỗ trợ phím số chính, numpad, và Alt+1..9)
            else if (((Keyboard.Modifiers == ModifierKeys.None && (TxtSearch == null || !TxtSearch.IsFocused) && (TxtPreview == null || !TxtPreview.IsFocused)) || (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                     && ((e.Key >= Key.D1 && e.Key <= Key.D9) || (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)))
            {
                int digit = -1;
                if (e.Key >= Key.D1 && e.Key <= Key.D9) digit = (int)(e.Key - Key.D1) + 1;
                else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) digit = (int)(e.Key - Key.NumPad1) + 1;

                if (digit >= 1 && digit <= 9 && LstClipboard != null)
                {
                    var items = LstClipboard.Items.Cast<ClipboardItem>().ToList();
                    if (items.Count >= digit)
                    {
                        var target = items[digit - 1];
                        LstClipboard.SelectedItem = target;
                        ExecutePaste(target, false, false);
                        e.Handled = true;
                        return;
                    }
                }
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

        private void BtnToggleMask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
            {
                item.IsMasked = !item.IsMasked;
                if (LstClipboard?.SelectedItem == item)
                {
                    UpdatePreview(item);
                }
            }
        }

        private void ColorSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
            {
                if (!string.IsNullOrEmpty(item.ColorHex))
                {
                    try
                    {
                        Clipboard.SetText(item.ColorHex);
                        if (TxtStatus != null) TxtStatus.Text = $"✓ Đã sao chép mã màu: {item.ColorHex}";
                    }
                    catch { }
                }
            }
        }

        private void CtxMenuStickyNote_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedAsStickyNote();
        }

        private void BtnStickyNoteAction_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedAsStickyNote();
        }

        private void OpenSelectedAsStickyNote()
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().FirstOrDefault();
            if (selected != null)
            {
                string text = selected.ContentType == ClipboardContentType.Text ? selected.TextContent : selected.PreviewText;
                var sticky = new StickyNoteWindow(text);
                sticky.Show();
                if (TxtStatus != null) TxtStatus.Text = "✓ Đã ghim thẻ Sticky Note lên màn hình!";
            }
        }

        private void BtnWebAiImage_Click(object sender, RoutedEventArgs e)
        {
            if (BtnWebAiImage != null && BtnWebAiImage.ContextMenu != null)
            {
                BtnWebAiImage.ContextMenu.PlacementTarget = BtnWebAiImage;
                BtnWebAiImage.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                BtnWebAiImage.ContextMenu.IsOpen = true;
            }
        }

        private void MnuWebAiTranslate_Click(object sender, RoutedEventArgs e)
        {
            LaunchWebAiService("https://translate.google.com/?op=images", "Google Dịch Ảnh");
        }

        private void MnuWebAiLens_Click(object sender, RoutedEventArgs e)
        {
            LaunchWebAiService("https://lens.google.com/", "Google Lens");
        }

        private void MnuWebAiPictureToText_Click(object sender, RoutedEventArgs e)
        {
            LaunchWebAiService("https://www.picturetotext.org/", "PictureToText.org");
        }

        private void LaunchWebAiService(string targetUrl, string serviceName)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().FirstOrDefault();
            if (selected == null || selected.ContentType != ClipboardContentType.Image || string.IsNullOrEmpty(selected.ImagePath))
            {
                if (TxtStatus != null) TxtStatus.Text = "⚠ Vui lòng chọn một hình ảnh để trích xuất chữ qua Web AI!";
                return;
            }

            try
            {
                // 1. Nạp ảnh vào Windows Clipboard hệ thống (hỗ trợ cả Bitmap lẫn FileDrop List để tương thích 100% web dropzone)
                if (File.Exists(selected.ImagePath))
                {
                    var dataObj = new DataObject();
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(selected.ImagePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    dataObj.SetImage(bmp);

                    var files = new System.Collections.Specialized.StringCollection();
                    files.Add(selected.ImagePath);
                    dataObj.SetFileDropList(files);

                    Clipboard.SetDataObject(dataObj, true);
                }

                // 2. Mở trình duyệt mặc định với URL của dịch vụ
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });

                if (TxtStatus != null)
                {
                    TxtStatus.Text = $"✓ Đã mở {serviceName} & nạp ảnh vào Clipboard! Nhấn Ctrl+V để trích xuất.";
                }

                // 3. Hiển thị lời nhắc Toast thông minh 5s (không cướp focus trình duyệt, tự tắt sau 5 giây)
                WebAiToastWindow.ShowToast(serviceName);

                // 4. Đợi trình duyệt mở ra và tự động thực hiện Ctrl + V (sau ~2.5 giây)
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(2500);
                    KeySender.SendCtrlVPaste();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi mở dịch vụ Web AI: " + ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnStorageFolderFooter_Click(object sender, RoutedEventArgs e)
        {
            if (BtnStorageFolderFooter != null && BtnStorageFolderFooter.ContextMenu != null)
            {
                BtnStorageFolderFooter.ContextMenu.PlacementTarget = BtnStorageFolderFooter;
                BtnStorageFolderFooter.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                BtnStorageFolderFooter.ContextMenu.IsOpen = true;
            }
        }

        private void MnuChangeStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            string currentDir = _historyManager.GetClipboardDirectory();
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Chọn thư mục lưu trữ dữ liệu Clipboard (Lịch sử, Yêu thích, Ảnh cache):";
                dlg.ShowNewFolderButton = true;
                if (System.IO.Directory.Exists(currentDir))
                {
                    dlg.SelectedPath = currentDir;
                }

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    string target = dlg.SelectedPath.Trim();
                    if (string.Equals(System.IO.Path.GetFullPath(target).TrimEnd('\\', '/'), System.IO.Path.GetFullPath(currentDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    var result = MessageBox.Show(
                        $"Bạn có muốn sao chép toàn bộ dữ liệu lịch sử và ảnh hiện tại sang thư mục mới không?\n\nThư mục mới:\n{target}",
                        "Xác nhận chuyển thư mục lưu trữ",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;

                    bool copyExisting = (result == MessageBoxResult.Yes);
                    try
                    {
                        _historyManager.ChangeStorageDirectory(target, copyExisting);
                        _itemsView?.Refresh();
                        EnsureAppropriateSelection();

                        if (TxtStatus != null)
                        {
                            TxtStatus.Text = $"✓ Đã chuyển thư mục lưu trữ sang: {target}";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi chuyển thư mục lưu trữ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void MnuOpenStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            string dir = _historyManager.GetClipboardDirectory();
            if (!System.IO.Directory.Exists(dir))
            {
                try { System.IO.Directory.CreateDirectory(dir); } catch { }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể mở thư mục: " + ex.Message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MnuResetStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            var defaultDir = System.IO.Path.Combine(SettingsManager.GetConfigDirectory(), "clipboard");
            string currentDir = _historyManager.GetClipboardDirectory();

            if (string.Equals(System.IO.Path.GetFullPath(currentDir).TrimEnd('\\', '/'), System.IO.Path.GetFullPath(defaultDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Thư mục lưu trữ hiện tại đã là thư mục mặc định!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Bạn có muốn khôi phục về thư mục lưu trữ mặc định (clipboard\\) không?",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _historyManager.ChangeStorageDirectory(string.Empty, false);
                    _itemsView?.Refresh();
                    EnsureAppropriateSelection();

                    if (TxtStatus != null)
                    {
                        TxtStatus.Text = "✓ Đã khôi phục về thư mục lưu trữ mặc định.";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnItemStickyNote_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ClipboardItem item)
            {
                string text = item.ContentType == ClipboardContentType.Text ? item.TextContent : item.PreviewText;
                var sticky = new StickyNoteWindow(text);
                sticky.Show();
                if (TxtStatus != null) TxtStatus.Text = "✓ Đã ghim thẻ Sticky Note lên màn hình!";
            }
        }

        private bool _isSyncingBlurSliders = false;

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var selectedList = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selectedList == null || selectedList.Count == 0)
            {
                if (MnuBlurRoot != null) MnuBlurRoot.IsEnabled = false;
                if (CtxMenuWebAi != null) CtxMenuWebAi.IsEnabled = false;
                return;
            }

            if (MnuBlurRoot != null) MnuBlurRoot.IsEnabled = true;
            var primary = selectedList[0];

            bool isImage = primary != null && primary.ContentType == ClipboardContentType.Image;
            if (CtxMenuWebAi != null)
            {
                CtxMenuWebAi.IsEnabled = isImage;
            }

            int fileSelCount = selectedList.Count(x => x.ContentType == ClipboardContentType.Files);
            if (CtxMenuMergeFiles != null)
            {
                CtxMenuMergeFiles.IsEnabled = fileSelCount >= 2;
                CtxMenuMergeFiles.Visibility = fileSelCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
            }

            _isSyncingBlurSliders = true;
            try
            {
                if (MnuToggleBlur != null)
                {
                    MnuToggleBlur.Header = primary.IsBlurred ? "✓ Tắt làm mờ mục này" : "Bật làm mờ mục này";
                }
                if (SliderBlurRadius != null)
                {
                    SliderBlurRadius.Value = primary.IsBlurred ? primary.BlurRadius : 0;
                    if (TxtBlurPercent != null) TxtBlurPercent.Text = $"{(int)SliderBlurRadius.Value}%";
                }
                if (SliderPixelSize != null)
                {
                    SliderPixelSize.Value = primary.IsBlurred ? primary.PixelateSize : 0;
                    if (TxtPixelPercent != null) TxtPixelPercent.Text = $"{(int)SliderPixelSize.Value}%";
                }
            }
            finally
            {
                _isSyncingBlurSliders = false;
            }
        }

        private void MnuToggleBlur_Click(object sender, RoutedEventArgs e)
        {
            var selectedList = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selectedList == null || selectedList.Count == 0) return;

            var primary = selectedList[0];
            bool newBlurred = !primary.IsBlurred;

            foreach (var it in selectedList)
            {
                it.IsBlurred = newBlurred;
                if (newBlurred)
                {
                    if (it.BlurRadius <= 0.5 && it.PixelateSize <= 0.5)
                    {
                        it.BlurRadius = 30.0;
                        it.BlurMode = "Blur";
                    }
                }
                else
                {
                    it.BlurRadius = 0.0;
                    it.PixelateSize = 0.0;
                    it.BlurMode = "None";
                }
            }

            if (MnuToggleBlur != null)
            {
                MnuToggleBlur.Header = newBlurred ? "✓ Tắt làm mờ mục này" : "Bật làm mờ mục này";
            }
            if (SliderBlurRadius != null)
            {
                SliderBlurRadius.Value = newBlurred ? selectedList[0].BlurRadius : 0;
                if (TxtBlurPercent != null) TxtBlurPercent.Text = $"{(int)SliderBlurRadius.Value}%";
            }
            if (SliderPixelSize != null)
            {
                SliderPixelSize.Value = newBlurred ? selectedList[0].PixelateSize : 0;
                if (TxtPixelPercent != null) TxtPixelPercent.Text = $"{(int)SliderPixelSize.Value}%";
            }

            _historyManager.SaveHistoryAsync();
            _itemsView?.Refresh();
            if (LstClipboard.SelectedItem is ClipboardItem curItem && selectedList.Contains(curItem))
            {
                UpdatePreview(curItem);
            }
            if (TxtStatus != null)
            {
                TxtStatus.Text = newBlurred ? $"✓ Đã bật làm mờ bảo vệ cho {selectedList.Count} mục đã chọn!" : $"✓ Đã gỡ làm mờ cho {selectedList.Count} mục đã chọn (0%).";
            }
        }

        public void ApplyBlurModeUI()
        {
            _itemsView?.Refresh();
        }

        private void SliderBlurRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBlurSliders) return;
            var selectedList = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selectedList == null || selectedList.Count == 0) return;

            double radius = e.NewValue;
            if (TxtBlurPercent != null) TxtBlurPercent.Text = $"{(int)radius}%";

            foreach (var it in selectedList)
            {
                it.BlurRadius = radius;
                it.BlurMode = (radius > 0.5) ? "Blur" : "None";
                it.IsBlurred = (radius > 0.5);
            }

            if (MnuToggleBlur != null && selectedList.Count > 0)
            {
                MnuToggleBlur.Header = selectedList[0].IsBlurred ? "✓ Tắt làm mờ mục này" : "Bật làm mờ mục này";
            }

            _historyManager.SaveHistoryAsync();
            _itemsView?.Refresh();
            if (LstClipboard.SelectedItem is ClipboardItem curItem && selectedList.Contains(curItem))
            {
                UpdatePreview(curItem);
            }
        }

        private void SliderPixelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingBlurSliders) return;
            var selectedList = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selectedList == null || selectedList.Count == 0) return;

            double size = e.NewValue;
            if (TxtPixelPercent != null) TxtPixelPercent.Text = $"{(int)size}%";

            foreach (var it in selectedList)
            {
                it.PixelateSize = size;
                it.BlurMode = (size > 0.5) ? "Pixelate" : "None";
                it.IsBlurred = (size > 0.5);
            }

            if (MnuToggleBlur != null && selectedList.Count > 0)
            {
                MnuToggleBlur.Header = selectedList[0].IsBlurred ? "✓ Tắt làm mờ mục này" : "Bật làm mờ mục này";
            }

            _historyManager.SaveHistoryAsync();
            _itemsView?.Refresh();
            if (LstClipboard.SelectedItem is ClipboardItem curItem && selectedList.Contains(curItem))
            {
                UpdatePreview(curItem);
            }
        }

        private void ItemContent_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement host)
            {
                ApplyBlurToHost(host, host.DataContext as ClipboardItem);
            }
        }

        private void ItemContent_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement host)
            {
                // Tạm thời bỏ mờ khi hover để xem nhanh
                host.Effect = null;
            }
        }

        private void ItemContent_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement host)
            {
                // Mờ lại nếu item đó được đánh dấu làm mờ
                ApplyBlurToHost(host, host.DataContext as ClipboardItem);
            }
        }

        private void ApplyBlurToHost(FrameworkElement host, ClipboardItem item)
        {
            if (host == null) return;
            if (item != null && item.IsBlurred)
            {
                if (item.BlurMode == "Blur" && item.BlurRadius > 0.5)
                {
                    host.Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = Math.Max(1.0, (item.BlurRadius / 100.0) * 35.0),
                        KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                    };
                }
                else if (item.BlurMode == "Pixelate" && item.PixelateSize > 0.5)
                {
                    host.Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = Math.Max(2.0, (item.PixelateSize / 100.0) * 30.0),
                        KernelType = System.Windows.Media.Effects.KernelType.Box
                    };
                }
                else
                {
                    host.Effect = null;
                }
            }
            else
            {
                host.Effect = null;
            }
        }
    }
}
