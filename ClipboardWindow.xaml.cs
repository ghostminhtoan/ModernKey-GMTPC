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
        private bool _isSortDescending = true;
        private string _lastActiveItemId = null;
        private readonly DateTime _sessionStartTime = DateTime.Now;

        private DateTime _showTime = DateTime.MinValue;

        public ClipboardWindow(ClipboardHistoryManager historyManager, AppSettings settings)
        {
            _historyManager = historyManager;
            _settings = settings;
            InitializeComponent();

            Loaded += ClipboardWindow_Loaded;
            Deactivated += ClipboardWindow_Deactivated;
            if (TxtPreview != null)
            {
                TxtPreview.LostFocus += TxtPreview_LostFocus;
            }
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
            if (_historyManager != null)
            {
                if (_itemsView == null)
                {
                    _itemsView = CollectionViewSource.GetDefaultView(_currentMode == "FAVORITES" ? _historyManager.FavoriteItems : _historyManager.Items);
                    if (_itemsView != null)
                    {
                        _itemsView.Filter = FilterClipboardItem;
                        ApplySortOrder();
                    }
                    if (LstClipboard != null) LstClipboard.ItemsSource = _itemsView;
                }

                EnsureAppropriateSelection();
            }
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

        public void ShowHud(IntPtr targetHwnd)
        {
            _showTime = DateTime.Now;
            _lastTargetHwnd = (targetHwnd != IntPtr.Zero && targetHwnd != new System.Windows.Interop.WindowInteropHelper(this).Handle)
                ? targetHwnd
                : GetForegroundWindow();

            Topmost = _settings != null && _settings.ClipboardAlwaysOnTop;

            if (_itemsView == null && _historyManager != null)
            {
                _itemsView = CollectionViewSource.GetDefaultView(_currentMode == "FAVORITES" ? _historyManager.FavoriteItems : _historyManager.Items);
                if (_itemsView != null)
                {
                    _itemsView.Filter = FilterClipboardItem;
                    ApplySortOrder();
                }
                if (LstClipboard != null) LstClipboard.ItemsSource = _itemsView;
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
                _itemsView = CollectionViewSource.GetDefaultView(_historyManager.FavoriteItems);
            }
            else
            {
                _currentMode = "HISTORY";
                if (BtnClearAllFooter != null) BtnClearAllFooter.Content = "XÓA TOÀN BỘ LỊCH SỬ";
                _itemsView = CollectionViewSource.GetDefaultView(_historyManager.Items);
            }

            if (_itemsView != null)
            {
                _itemsView.Filter = FilterClipboardItem;
                ApplySortOrder();
            }
            if (LstClipboard != null)
            {
                LstClipboard.ItemsSource = _itemsView;
            }
            EnsureAppropriateSelection();
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
                sb.AppendLine($"• [Enter] hoặc [Ctrl+V]: Dán gộp vào ứng dụng đích");
                sb.AppendLine($"• [Ctrl+C]: Sao chép gộp vào Clipboard hệ thống");
                sb.AppendLine($"• [Del]: Xóa toàn bộ {items.Count} mục đã chọn");
                sb.AppendLine();
                sb.AppendLine("--- NỘI DUNG TỔNG HỢP ---");
                int idx = 1;
                foreach (var it in items)
                {
                    string content;
                    if (it.ContentType == ClipboardContentType.Text)
                    {
                        content = it.TextContent;
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
                    sb.AppendLine();
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
                        if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                        {
                            using (var img = System.Drawing.Image.FromFile(item.ImagePath))
                            {
                                System.Windows.Forms.Clipboard.SetImage(img);
                            }
                        }
                        else if (item.ImageSource != null)
                        {
                            Clipboard.SetImage(item.ImageSource);
                        }
                    }
                    else
                    {
                        Clipboard.SetText(item.TextContent ?? string.Empty);
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
                            string combined = string.Join(Environment.NewLine, textItems);
                            Clipboard.SetText(combined);
                        }
                    }
                }

                if (selected.Count > 0)
                {
                    _lastActiveItemId = selected.Last().Id;
                }

                if (TxtStatus != null)
                {
                    TxtStatus.Text = $"✓ Đã sao chép {selected.Count} mục vào Clipboard hệ thống!";
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

            string combinedText = string.Join(Environment.NewLine, textItems);
            var virtualItemText = new ClipboardItem
            {
                ContentType = ClipboardContentType.Text,
                TextContent = combinedText,
                PreviewText = $"[Gộp {textItems.Count} mục]",
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

        private void BtnToggleFav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardItem item)
            {
                _historyManager?.ToggleFavorite(item);
                _itemsView?.Refresh();
                if (TxtStatus != null)
                {
                    TxtStatus.Text = item.IsFavorite ? "★ Đã thêm vào mục Yêu thích!" : "☆ Đã bỏ khỏi mục Yêu thích!";
                }
            }
        }

        private void BtnToggleFavoriteAction_Click(object sender, RoutedEventArgs e)
        {
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected != null && selected.Count > 0)
            {
                foreach (var item in selected)
                {
                    _historyManager?.ToggleFavorite(item);
                }
                _itemsView?.Refresh();
                if (TxtStatus != null)
                {
                    TxtStatus.Text = selected.Count == 1
                        ? (selected[0].IsFavorite ? "★ Đã thêm vào mục Yêu thích!" : "☆ Đã bỏ khỏi mục Yêu thích!")
                        : $"✓ Đã cập nhật trạng thái Yêu thích cho {selected.Count} mục!";
                }
            }
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
            var selected = LstClipboard?.SelectedItems?.Cast<ClipboardItem>().ToList();
            if (selected != null && selected.Count > 0)
            {
                foreach (var it in selected)
                {
                    _historyManager?.ToggleFavorite(it);
                }
                _itemsView?.Refresh();
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
                    string merged = string.Join(Environment.NewLine + "---" + Environment.NewLine, textParts);
                    _historyManager?.AddText(merged, "ModernKey Gộp", null);
                    _itemsView?.Refresh();
                    EnsureAppropriateSelection();
                    if (TxtStatus != null) TxtStatus.Text = $"✓ Đã gộp {textParts.Count} đoạn văn bản thành 1 mục mới!";
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn từ 2 mục văn bản trở lên để gộp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (!TxtSearch.IsFocused)
                {
                    BtnToggleFavoriteAction_Click(sender, null);
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
                if (!TxtSearch.IsFocused || TxtSearch.SelectionLength == 0)
                {
                    ExecuteCopySelectedToClipboard();
                    e.Handled = true;
                }
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
