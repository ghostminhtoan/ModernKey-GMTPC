using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using ModernKey.Config;
using ModernKey.Models;

namespace ModernKey.Core
{
    public class ClipboardHistoryManager
    {
        private readonly AppSettings _settings;
        private readonly object _lock = new object();

        // 1. Danh sách Lịch sử thông thường (History)
        public ObservableCollection<ClipboardItem> Items { get; } = new ObservableCollection<ClipboardItem>();

        // 2. Danh sách Mục Yêu thích độc lập (Favorites - chuẩn Comfort Keys Pro)
        public ObservableCollection<ClipboardItem> FavoriteItems { get; } = new ObservableCollection<ClipboardItem>();

        public ClipboardHistoryManager(AppSettings settings)
        {
            _settings = settings;
            try
            {
                System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Items, _lock);
                System.Windows.Data.BindingOperations.EnableCollectionSynchronization(FavoriteItems, _lock);
            }
            catch { }

            // Nạp dữ liệu lịch sử và di chuyển thư mục bất đồng bộ trên luồng nền
            // Tuyệt đối không đọc file/parse JSON trên Main/UI Thread để đảm bảo app mở lên tức thì (<10ms)
            // không gây bất kỳ độ trễ hay giật lag chuột/phím nào khi đang chơi game!
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                EnsureDirectoriesAndMigrate();
                LoadHistory();
                LoadFavorites();
            });
        }

        public string GetClipboardDirectory()
        {
            string dir = null;
            if (!string.IsNullOrEmpty(_settings?.ClipboardStorageFolder))
            {
                try
                {
                    if (Directory.Exists(_settings.ClipboardStorageFolder))
                    {
                        dir = _settings.ClipboardStorageFolder;
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(SettingsManager.GetConfigDirectory(), "clipboard");
            }

            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public bool ChangeStorageDirectory(string newPath, bool copyExistingData = true)
        {
            lock (_lock)
            {
                string oldDir = GetClipboardDirectory();
                string targetDir = string.IsNullOrWhiteSpace(newPath)
                    ? Path.Combine(SettingsManager.GetConfigDirectory(), "clipboard")
                    : newPath.Trim();

                string fullOld = Path.GetFullPath(oldDir).TrimEnd('\\', '/');
                string fullNew = Path.GetFullPath(targetDir).TrimEnd('\\', '/');

                if (string.Equals(fullOld, fullNew, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Lưu dữ liệu hiện tại trước khi chuyển đổi
                SaveHistoryNow();
                SaveFavoritesNow();

                if (copyExistingData && Directory.Exists(oldDir))
                {
                    CopyDirectoryRecursive(oldDir, targetDir);
                }

                if (_settings != null)
                {
                    _settings.ClipboardStorageFolder = string.IsNullOrWhiteSpace(newPath) ? string.Empty : targetDir;
                    SettingsManager.SaveSettings(_settings);
                }

                ReloadData();
                return true;
            }
        }

        public void ReloadData()
        {
            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    Items.Clear();
                    FavoriteItems.Clear();
                });
                LoadHistory();
                LoadFavorites();
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                try
                {
                    if (!File.Exists(dest))
                    {
                        File.Copy(file, dest, false);
                    }
                }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string folderName = Path.GetFileName(dir);
                string destSub = Path.Combine(targetDir, folderName);
                CopyDirectoryRecursive(dir, destSub);
            }
        }

        private string GetHistoryFilePath()
        {
            return Path.Combine(GetClipboardDirectory(), "clipboard_history.json");
        }

        private string GetFavoritesFilePath()
        {
            return Path.Combine(GetClipboardDirectory(), "clipboard_favorites.json");
        }

        public string GetCacheDirectory()
        {
            string dir = Path.Combine(GetClipboardDirectory(), "clipboard_cache");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public string GetHistoryCacheDirectory()
        {
            string dir = Path.Combine(GetCacheDirectory(), "history");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public string GetFavoritesCacheDirectory()
        {
            string dir = Path.Combine(GetCacheDirectory(), "favorites");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        public string GetBackupDirectory()
        {
            string dir = Path.Combine(GetClipboardDirectory(), "backups");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
        }

        private void EnsureDirectoriesAndMigrate()
        {
            try
            {
                string cfgDir = SettingsManager.GetConfigDirectory();
                string clipDir = GetClipboardDirectory();

                // 1. Tự động chuyển file clipboard_history.json & clipboard_favorites.json cũ vào folder clipboard\ mới
                string oldHist = Path.Combine(cfgDir, "clipboard_history.json");
                string newHist = Path.Combine(clipDir, "clipboard_history.json");
                if (File.Exists(oldHist) && !File.Exists(newHist))
                {
                    try { File.Move(oldHist, newHist); } catch { }
                }

                string oldFav = Path.Combine(cfgDir, "clipboard_favorites.json");
                string newFav = Path.Combine(clipDir, "clipboard_favorites.json");
                if (File.Exists(oldFav) && !File.Exists(newFav))
                {
                    try { File.Move(oldFav, newFav); } catch { }
                }

                string oldLegacyFav = Path.Combine(cfgDir, "favorites.json");
                if (File.Exists(oldLegacyFav) && !File.Exists(newFav))
                {
                    try { File.Move(oldLegacyFav, newFav); } catch { }
                }

                string clipLegacyFav = Path.Combine(clipDir, "favorites.json");
                if (File.Exists(clipLegacyFav) && !File.Exists(newFav))
                {
                    try { File.Move(clipLegacyFav, newFav); } catch { }
                }

                // 2. Tự động chuyển folder clipboard_cache cũ vào folder clipboard\clipboard_cache mới
                string oldCache = Path.Combine(cfgDir, "clipboard_cache");
                string newCache = Path.Combine(clipDir, "clipboard_cache");
                if (Directory.Exists(oldCache) && !Directory.Exists(newCache))
                {
                    try { Directory.Move(oldCache, newCache); }
                    catch
                    {
                        try
                        {
                            Directory.CreateDirectory(newCache);
                            foreach (var file in Directory.GetFiles(oldCache, "*", SearchOption.AllDirectories))
                            {
                                string rel = file.Substring(oldCache.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                string dest = Path.Combine(newCache, rel);
                                string destFolder = Path.GetDirectoryName(dest);
                                if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                                File.Copy(file, dest, true);
                            }
                        }
                        catch { }
                    }
                }

                GetCacheDirectory();
                string histCache = GetHistoryCacheDirectory();
                string favCache = GetFavoritesCacheDirectory();
                GetBackupDirectory();

                // 3. Tự động chuyển các file ảnh cũ còn nằm trực tiếp trong clipboard_cache\ vào history\
                if (Directory.Exists(newCache))
                {
                    foreach (var file in Directory.GetFiles(newCache, "*.png", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            string fn = Path.GetFileName(file);
                            string destHist = Path.Combine(histCache, fn);
                            if (!File.Exists(destHist))
                            {
                                File.Move(file, destHist);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public void AddText(string text, string sourceApp = null, string sourceIconPath = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Bỏ qua nếu chuỗi quá dài bất thường (> 2MB văn bản) để tránh tràn RAM
            if (text.Length > 2000000) return;

            lock (_lock)
            {
                // Kiểm tra trùng lặp với mục gần nhất
                if (_settings.ClipboardIgnoreDuplicates && Items.Count > 0)
                {
                    var first = Items[0];
                    if (first.ContentType == ClipboardContentType.Text && string.Equals(first.TextContent, text, StringComparison.Ordinal))
                    {
                        first.Timestamp = DateTime.Now;
                        MoveToTop(first);
                        SaveHistoryAsync();
                        return;
                    }
                }

                // Tạo preview ngắn gọn 1 dòng
                string preview = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
                if (preview.Length > 120)
                {
                    preview = preview.Substring(0, 117) + "...";
                }

                long byteSize = Encoding.UTF8.GetByteCount(text);

                var item = new ClipboardItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ContentType = ClipboardContentType.Text,
                    TextContent = text,
                    PreviewText = string.IsNullOrWhiteSpace(preview) ? "[Văn bản khoảng trắng]" : preview,
                    SourceApp = sourceApp ?? string.Empty,
                    SourceIconPath = sourceIconPath ?? string.Empty,
                    Timestamp = DateTime.Now,
                    CharCount = text.Length,
                    ByteSize = byteSize,
                    IsFavorite = false,
                    IsBlurred = false,
                    BlurMode = "None",
                    BlurRadius = 0.0,
                    PixelateSize = 0.0
                };

                item.DetectMetadata();
                InsertItem(item);
            }
        }

        public void AddFiles(List<string> filePaths, int dropEffect, string sourceApp = null, string sourceIconPath = null)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            lock (_lock)
            {
                string fullText = string.Join(Environment.NewLine, filePaths);

                // Kiểm tra trùng lặp với mục gần nhất
                if (_settings.ClipboardIgnoreDuplicates && Items.Count > 0)
                {
                    var first = Items[0];
                    if (first.ContentType == ClipboardContentType.Files && string.Equals(first.TextContent, fullText, StringComparison.OrdinalIgnoreCase))
                    {
                        first.Timestamp = DateTime.Now;
                        first.DropEffect = dropEffect > 0 ? dropEffect : 1;
                        MoveToTop(first);
                        SaveHistoryAsync();
                        return;
                    }
                }

                string preview;
                long totalByteSize = 0;

                foreach (var p in filePaths)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            var fi = new FileInfo(p);
                            totalByteSize += fi.Length;
                        }
                    }
                    catch { }
                }

                if (filePaths.Count == 1)
                {
                    string singlePath = filePaths[0];
                    bool isDir = false;
                    try { isDir = Directory.Exists(singlePath); } catch { }
                    string name = Path.GetFileName(singlePath);
                    if (string.IsNullOrEmpty(name)) name = singlePath;

                    preview = isDir ? $"[Thư mục] {name}" : $"[Tệp] {name}";
                }
                else
                {
                    var sampleNames = filePaths.Take(3).Select(p =>
                    {
                        string n = Path.GetFileName(p);
                        return string.IsNullOrEmpty(n) ? p : n;
                    });
                    preview = $"[{filePaths.Count} mục] {string.Join(", ", sampleNames)}" + (filePaths.Count > 3 ? "..." : "");
                }

                var item = new ClipboardItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ContentType = ClipboardContentType.Files,
                    DropEffect = dropEffect > 0 ? dropEffect : 1,
                    TextContent = fullText,
                    PreviewText = preview,
                    SourceApp = sourceApp ?? string.Empty,
                    SourceIconPath = sourceIconPath ?? string.Empty,
                    Timestamp = DateTime.Now,
                    CharCount = filePaths.Count,
                    ByteSize = totalByteSize,
                    IsFavorite = false,
                    IsBlurred = false,
                    BlurMode = "None",
                    BlurRadius = 0.0,
                    PixelateSize = 0.0
                };

                InsertItem(item);
            }
        }

        private string GenerateUniqueImageId(string cacheDir)
        {
            string baseId = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
            string id = baseId;
            int counter = 1;
            while (File.Exists(Path.Combine(cacheDir, id + ".png")))
            {
                id = $"{baseId} ({counter++})";
            }
            return id;
        }

        public ClipboardItem AddImage(byte[] pngBytes, int width, int height, string sourceApp = null, string sourceIconPath = null, string customPreviewText = null)
        {
            if (pngBytes == null || pngBytes.Length == 0) return null;

            lock (_lock)
            {
                string cacheDir = GetHistoryCacheDirectory();
                string id = GenerateUniqueImageId(cacheDir);
                string filename = id + ".png";
                string thumbFilename = id + "_t.png";
                string fullPath = Path.Combine(cacheDir, filename);
                string thumbFullPath = Path.Combine(cacheDir, thumbFilename);

                try
                {
                    File.WriteAllBytes(fullPath, pngBytes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error saving clipboard image: " + ex.Message);
                    return null;
                }

                // Tạo ảnh thumbnail siêu nhẹ giống Comfort Keys Pro (.bm2)
                try
                {
                    using (var ms = new MemoryStream(pngBytes))
                    using (var orig = System.Drawing.Image.FromStream(ms))
                    {
                        int thumbW = 88;
                        int thumbH = 48;
                        if (orig.Width > 0 && orig.Height > 0)
                        {
                            double ratio = (double)orig.Width / orig.Height;
                            if (ratio > (double)thumbW / thumbH)
                            {
                                thumbH = Math.Max(1, (int)(thumbW / ratio));
                            }
                            else
                            {
                                thumbW = Math.Max(1, (int)(thumbH * ratio));
                            }
                        }
                        using (var thumbBmp = new System.Drawing.Bitmap(thumbW, thumbH))
                        using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(orig, 0, 0, thumbW, thumbH);
                            thumbBmp.Save(thumbFullPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                catch
                {
                    thumbFullPath = fullPath;
                }

                var item = new ClipboardItem
                {
                    Id = id,
                    ContentType = ClipboardContentType.Image,
                    TextContent = customPreviewText ?? "[Hình ảnh chụp / sao chép]",
                    PreviewText = customPreviewText ?? $"[Hình ảnh {width}x{height}]",
                    ImagePath = fullPath,
                    ThumbPath = thumbFullPath,
                    SourceApp = sourceApp ?? string.Empty,
                    SourceIconPath = sourceIconPath ?? string.Empty,
                    Timestamp = DateTime.Now,
                    CharCount = 0,
                    ByteSize = pngBytes.Length,
                    IsFavorite = false,
                    IsBlurred = false,
                    BlurMode = "None",
                    BlurRadius = 0.0,
                    PixelateSize = 0.0
                };

                InsertItem(item);
                return item;
            }
        }

        public ClipboardItem AddImage(byte[] pngBytes, BitmapSource bmpSource, string sourceApp = null, string sourceIconPath = null)
        {
            return AddImage(pngBytes, bmpSource?.PixelWidth ?? 0, bmpSource?.PixelHeight ?? 0, sourceApp, sourceIconPath);
        }

        private static void DispatchSafe(Action action)
        {
            if (action == null) return;
            var app = Application.Current;
            if (app != null && app.Dispatcher != null)
            {
                if (app.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    // Dùng BeginInvoke bất đồng bộ để luồng nền không bao giờ bị khóa cứng (deadlock) khi UI thread bận
                    app.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, action);
                }
            }
            else
            {
                action();
            }
        }

        private void InsertItem(ClipboardItem item)
        {
            DispatchSafe(() =>
            {
                Items.Insert(0, item);
                TrimLimit();
            });
            SaveHistoryAsync();
        }

        private void MoveToTop(ClipboardItem item)
        {
            DispatchSafe(() =>
            {
                int idx = Items.IndexOf(item);
                if (idx > 0)
                {
                    Items.Move(idx, 0);
                }
            });
        }

        private void TrimLimit()
        {
            int max = (_settings.ClipboardMaxItems >= 5 && _settings.ClipboardMaxItems <= 99999) ? _settings.ClipboardMaxItems : 200;
            if (Items.Count <= max) return;

            // Xóa các mục cũ nhất từ cuối danh sách History
            for (int i = Items.Count - 1; i >= max; i--)
            {
                var it = Items[i];
                // Chỉ xóa cache nếu mục này không tồn tại trong danh sách Yêu thích
                if (FindMatchingItem(FavoriteItems, it) == null)
                {
                    DeleteCacheFile(it);
                }
                Items.RemoveAt(i);
            }
        }

        public void ApplyMaxLimit()
        {
            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    TrimLimit();
                    SaveHistoryAsync();
                });
            }
        }

        private void EnsureFavoriteImageInFavoritesFolder(ClipboardItem it)
        {
            if (it == null || !it.IsImage) return;
            string favDir = GetFavoritesCacheDirectory();
            try
            {
                if (!string.IsNullOrEmpty(it.ImagePath))
                {
                    string fn = Path.GetFileName(it.ImagePath);
                    string dest = Path.Combine(favDir, fn);
                    if (!File.Exists(dest))
                    {
                        string resolved = ClipboardItem.ResolvePath(it.ImagePath);
                        if (File.Exists(resolved) && !string.Equals(resolved, dest, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(resolved, dest, true);
                        }
                    }
                    if (File.Exists(dest))
                    {
                        it.ImagePath = dest;
                    }
                }
                if (!string.IsNullOrEmpty(it.ThumbPath))
                {
                    string fnThumb = Path.GetFileName(it.ThumbPath);
                    string destThumb = Path.Combine(favDir, fnThumb);
                    if (!File.Exists(destThumb))
                    {
                        string resolvedThumb = ClipboardItem.ResolvePath(it.ThumbPath);
                        if (File.Exists(resolvedThumb) && !string.Equals(resolvedThumb, destThumb, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(resolvedThumb, destThumb, true);
                        }
                    }
                    if (File.Exists(destThumb))
                    {
                        it.ThumbPath = destThumb;
                    }
                }
            }
            catch { }
        }

        private void EnsureHistoryImageInHistoryFolder(ClipboardItem it)
        {
            if (it == null || !it.IsImage) return;
            string histDir = GetHistoryCacheDirectory();
            try
            {
                if (!string.IsNullOrEmpty(it.ImagePath))
                {
                    string fn = Path.GetFileName(it.ImagePath);
                    string dest = Path.Combine(histDir, fn);
                    if (!File.Exists(dest))
                    {
                        string resolved = ClipboardItem.ResolvePath(it.ImagePath);
                        if (File.Exists(resolved) && !string.Equals(resolved, dest, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(resolved, dest, true);
                        }
                    }
                    if (File.Exists(dest))
                    {
                        it.ImagePath = dest;
                    }
                }
                if (!string.IsNullOrEmpty(it.ThumbPath))
                {
                    string fnThumb = Path.GetFileName(it.ThumbPath);
                    string destThumb = Path.Combine(histDir, fnThumb);
                    if (!File.Exists(destThumb))
                    {
                        string resolvedThumb = ClipboardItem.ResolvePath(it.ThumbPath);
                        if (File.Exists(resolvedThumb) && !string.Equals(resolvedThumb, destThumb, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(resolvedThumb, destThumb, true);
                        }
                    }
                    if (File.Exists(destThumb))
                    {
                        it.ThumbPath = destThumb;
                    }
                }
            }
            catch { }
        }

        public void ToggleFavorite(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                bool willBeFav = !item.IsFavorite;
                item.IsFavorite = willBeFav;

                if (willBeFav)
                {
                    var existing = FindMatchingItem(FavoriteItems, item);
                    if (existing == null)
                    {
                        var favClone = item.Clone();
                        favClone.IsFavorite = true;
                        EnsureFavoriteImageInFavoritesFolder(favClone);
                        DispatchSafe(() =>
                        {
                            FavoriteItems.Insert(0, favClone);
                        });
                    }
                    var matchHist = FindMatchingItem(Items, item);
                    if (matchHist != null) matchHist.IsFavorite = true;
                }
                else
                {
                    var matchHist = FindMatchingItem(Items, item);
                    var existing = FindMatchingItem(FavoriteItems, item);
                    if (existing != null)
                    {
                        if (matchHist == null || !string.Equals(matchHist.ImagePath, existing.ImagePath, StringComparison.OrdinalIgnoreCase))
                        {
                            DeleteCacheFile(existing);
                        }
                        DispatchSafe(() =>
                        {
                            FavoriteItems.Remove(existing);
                        });
                    }
                    if (matchHist != null) matchHist.IsFavorite = false;
                }

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void AddToFavoritesWithGroup(ClipboardItem item, string groupName)
        {
            if (item == null) return;
            string cleanGroup = (groupName ?? "").Trim();
            if (cleanGroup.Equals("-All-", StringComparison.OrdinalIgnoreCase) || cleanGroup.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                cleanGroup = string.Empty;
            }

            lock (_lock)
            {
                item.IsFavorite = true;
                item.GroupName = cleanGroup;

                var existing = FindMatchingItem(FavoriteItems, item);
                if (existing != null)
                {
                    existing.GroupName = cleanGroup;
                    existing.IsFavorite = true;
                }
                else
                {
                    var favClone = item.Clone();
                    favClone.IsFavorite = true;
                    favClone.GroupName = cleanGroup;
                    EnsureFavoriteImageInFavoritesFolder(favClone);
                    DispatchSafe(() =>
                    {
                        FavoriteItems.Insert(0, favClone);
                    });
                }

                var matchHist = FindMatchingItem(Items, item);
                if (matchHist != null)
                {
                    matchHist.IsFavorite = true;
                    matchHist.GroupName = cleanGroup;
                }

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void RemoveFromFavorites(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                item.IsFavorite = false;
                var matchHist = FindMatchingItem(Items, item);
                var existing = FindMatchingItem(FavoriteItems, item);
                if (existing != null)
                {
                    if (matchHist == null || !string.Equals(matchHist.ImagePath, existing.ImagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteCacheFile(existing);
                    }
                    DispatchSafe(() =>
                    {
                        FavoriteItems.Remove(existing);
                    });
                }
                if (matchHist != null)
                {
                    matchHist.IsFavorite = false;
                }

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void RemoveFromFavorites(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return;
            var list = items.Where(x => x != null).ToList();
            if (list.Count == 0) return;

            if (list.Count == 1)
            {
                RemoveFromFavorites(list[0]);
                return;
            }

            var targetIds = new HashSet<string>(list.Where(x => !string.IsNullOrEmpty(x.Id)).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            var targetItems = new HashSet<ClipboardItem>(list);

            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    for (int i = FavoriteItems.Count - 1; i >= 0; i--)
                    {
                        var it = FavoriteItems[i];
                        if (targetItems.Contains(it) || (!string.IsNullOrEmpty(it.Id) && targetIds.Contains(it.Id)))
                        {
                            it.IsFavorite = false;
                            FavoriteItems.RemoveAt(i);
                        }
                    }

                    foreach (var histItem in Items)
                    {
                        if (targetItems.Contains(histItem) || (!string.IsNullOrEmpty(histItem.Id) && targetIds.Contains(histItem.Id)))
                        {
                            histItem.IsFavorite = false;
                        }
                    }
                });

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public Dictionary<string, int> GetFavoriteGroupsWithCount()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            lock (_lock)
            {
                foreach (var it in FavoriteItems)
                {
                    string g = (it.GroupName ?? "").Trim();
                    if (string.IsNullOrEmpty(g)) continue;
                    if (dict.ContainsKey(g)) dict[g]++;
                    else dict[g] = 1;
                }
            }
            return dict;
        }

        public static List<string> GetAllCacheDirectories()
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string cfgDir = SettingsManager.GetConfigDirectory();
                if (!string.IsNullOrEmpty(cfgDir))
                {
                    dirs.Add(Path.Combine(cfgDir, "clipboard", "clipboard_cache", "history"));
                    dirs.Add(Path.Combine(cfgDir, "clipboard", "clipboard_cache", "favorites"));
                    dirs.Add(Path.Combine(cfgDir, "clipboard", "clipboard_cache"));
                    dirs.Add(Path.Combine(cfgDir, "clipboard_cache", "history"));
                    dirs.Add(Path.Combine(cfgDir, "clipboard_cache", "favorites"));
                }

                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(appDir))
                {
                    dirs.Add(Path.Combine(appDir, ".portable", "clipboard", "clipboard_cache", "history"));
                    dirs.Add(Path.Combine(appDir, ".portable", "clipboard", "clipboard_cache", "favorites"));
                    dirs.Add(Path.Combine(appDir, ".portable", "clipboard_cache", "history"));
                    dirs.Add(Path.Combine(appDir, ".portable", "clipboard_cache", "favorites"));

                    if (appDir.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\.."));
                        dirs.Add(Path.Combine(projectRoot, ".portable", "clipboard", "clipboard_cache", "history"));
                        dirs.Add(Path.Combine(projectRoot, ".portable", "clipboard", "clipboard_cache", "favorites"));
                        dirs.Add(Path.Combine(projectRoot, ".portable", "clipboard_cache", "history"));
                        dirs.Add(Path.Combine(projectRoot, ".portable", "clipboard_cache", "favorites"));

                        string projRoot2 = Path.GetFullPath(Path.Combine(appDir, @"..\.."));
                        dirs.Add(Path.Combine(projRoot2, ".portable", "clipboard", "clipboard_cache", "history"));
                        dirs.Add(Path.Combine(projRoot2, ".portable", "clipboard", "clipboard_cache", "favorites"));
                    }
                }
            }
            catch { }
            return dirs.Where(d => Directory.Exists(d)).ToList();
        }

        private static void TryDeleteFileWithRetry(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
            }
            catch (IOException)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        System.Threading.Thread.Sleep(50 * (i + 1));
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.SetAttributes(filePath, FileAttributes.Normal);
                                File.Delete(filePath);
                                break;
                            }
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }

        public static void DeleteImageFileAtAllLocations(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string fn = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fn)) return;

                string thumbFn = fn.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase)
                    ? fn
                    : Path.GetFileNameWithoutExtension(fn) + "_t.png";

                // 1. Thử xóa trực tiếp
                TryDeleteFileWithRetry(path);

                // 2. Thử xóa thumbnail cùng thư mục
                if (!string.Equals(fn, thumbFn, StringComparison.OrdinalIgnoreCase))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        TryDeleteFileWithRetry(Path.Combine(dir, thumbFn));
                    }
                }

                // 3. Quét xóa đúng tên file fn và thumbFn ở các thư mục cache đã biết
                var cacheDirs = GetAllCacheDirectories();
                foreach (var dir in cacheDirs)
                {
                    TryDeleteFileWithRetry(Path.Combine(dir, fn));
                    TryDeleteFileWithRetry(Path.Combine(dir, thumbFn));
                }
            }
            catch { }
        }

        private void DeleteCacheFile(ClipboardItem item)
        {
            if (item != null && item.IsImage)
            {
                item.ReleaseVisualResources();
                if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    DeleteImageFileAtAllLocations(item.ImagePath);
                }
                if (!string.IsNullOrEmpty(item.ThumbPath))
                {
                    DeleteImageFileAtAllLocations(item.ThumbPath);
                }
            }
        }

        public void DeleteItem(ClipboardItem item, bool isFavoriteView = false)
        {
            if (item == null) return;
            lock (_lock)
            {
                bool shouldDeleteCache = false;
                DispatchSafe(() =>
                {
                    if (isFavoriteView)
                    {
                        FavoriteItems.Remove(item);
                        var matchHist = FindMatchingItem(Items, item);
                        if (matchHist != null) matchHist.IsFavorite = false;
                        if (matchHist == null || !string.Equals(Path.GetFileName(matchHist.ImagePath), Path.GetFileName(item.ImagePath), StringComparison.OrdinalIgnoreCase))
                        {
                            shouldDeleteCache = true;
                        }
                    }
                    else
                    {
                        Items.Remove(item);
                        var matchFav = FindMatchingItem(FavoriteItems, item);
                        if (matchFav == null || !string.Equals(Path.GetFileName(matchFav.ImagePath), Path.GetFileName(item.ImagePath), StringComparison.OrdinalIgnoreCase))
                        {
                            shouldDeleteCache = true;
                        }
                    }
                });

                if (shouldDeleteCache)
                {
                    DeleteCacheFile(item);
                }

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void DeleteItems(IEnumerable<ClipboardItem> itemsToDelete, bool isFavoriteView = false)
        {
            if (itemsToDelete == null) return;
            var list = itemsToDelete.Where(x => x != null).ToList();
            if (list.Count == 0) return;

            if (list.Count == 1)
            {
                DeleteItem(list[0], isFavoriteView);
                return;
            }

            var targetIds = new HashSet<string>(list.Where(x => !string.IsNullOrEmpty(x.Id)).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            var targetItems = new HashSet<ClipboardItem>(list);
            var itemsForCacheDeletion = new List<ClipboardItem>();

            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    if (isFavoriteView)
                    {
                        var histIds = new HashSet<string>(Items.Where(h => !string.IsNullOrEmpty(h.Id)).Select(h => h.Id), StringComparer.OrdinalIgnoreCase);
                        var histPaths = new HashSet<string>(Items.Where(h => h.IsImage && !string.IsNullOrEmpty(h.ImagePath)).Select(h => Path.GetFileName(h.ImagePath)), StringComparer.OrdinalIgnoreCase);

                        for (int i = FavoriteItems.Count - 1; i >= 0; i--)
                        {
                            var it = FavoriteItems[i];
                            if (targetItems.Contains(it) || (!string.IsNullOrEmpty(it.Id) && targetIds.Contains(it.Id)))
                            {
                                string fn = (it.IsImage && !string.IsNullOrEmpty(it.ImagePath)) ? Path.GetFileName(it.ImagePath) : null;
                                bool existsInHistory = (!string.IsNullOrEmpty(it.Id) && histIds.Contains(it.Id)) ||
                                                       (fn != null && histPaths.Contains(fn));
                                if (!existsInHistory && it.IsImage)
                                {
                                    itemsForCacheDeletion.Add(it);
                                }
                                FavoriteItems.RemoveAt(i);
                            }
                        }

                        foreach (var histItem in Items)
                        {
                            if (targetItems.Contains(histItem) || (!string.IsNullOrEmpty(histItem.Id) && targetIds.Contains(histItem.Id)))
                            {
                                histItem.IsFavorite = false;
                            }
                        }
                    }
                    else
                    {
                        var favIds = new HashSet<string>(FavoriteItems.Where(f => !string.IsNullOrEmpty(f.Id)).Select(f => f.Id), StringComparer.OrdinalIgnoreCase);
                        var favPaths = new HashSet<string>(FavoriteItems.Where(f => f.IsImage && !string.IsNullOrEmpty(f.ImagePath)).Select(f => Path.GetFileName(f.ImagePath)), StringComparer.OrdinalIgnoreCase);

                        for (int i = Items.Count - 1; i >= 0; i--)
                        {
                            var it = Items[i];
                            if (targetItems.Contains(it) || (!string.IsNullOrEmpty(it.Id) && targetIds.Contains(it.Id)))
                            {
                                string fn = (it.IsImage && !string.IsNullOrEmpty(it.ImagePath)) ? Path.GetFileName(it.ImagePath) : null;
                                bool inFav = (!string.IsNullOrEmpty(it.Id) && favIds.Contains(it.Id)) ||
                                             (fn != null && favPaths.Contains(fn));
                                if (!inFav && it.IsImage)
                                {
                                    itemsForCacheDeletion.Add(it);
                                }
                                Items.RemoveAt(i);
                            }
                        }
                    }
                });

                foreach (var it in itemsForCacheDeletion)
                {
                    DeleteCacheFile(it);
                }

                SaveHistoryAsync();
                if (isFavoriteView) SaveFavoritesAsync();
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                var itemsForCacheDeletion = new List<ClipboardItem>();
                DispatchSafe(() =>
                {
                    var favIds = new HashSet<string>(FavoriteItems.Where(f => !string.IsNullOrEmpty(f.Id)).Select(f => f.Id), StringComparer.OrdinalIgnoreCase);
                    var favPaths = new HashSet<string>(FavoriteItems.Where(f => f.IsImage && !string.IsNullOrEmpty(f.ImagePath)).Select(f => Path.GetFileName(f.ImagePath)), StringComparer.OrdinalIgnoreCase);

                    for (int i = Items.Count - 1; i >= 0; i--)
                    {
                        var it = Items[i];
                        string fn = (it.IsImage && !string.IsNullOrEmpty(it.ImagePath)) ? Path.GetFileName(it.ImagePath) : null;
                        bool inFav = (!string.IsNullOrEmpty(it.Id) && favIds.Contains(it.Id)) ||
                                     (fn != null && favPaths.Contains(fn));
                        if (!inFav && it.IsImage)
                        {
                            itemsForCacheDeletion.Add(it);
                        }
                    }
                    Items.Clear();
                });

                foreach (var it in itemsForCacheDeletion)
                {
                    DeleteCacheFile(it);
                }

                SaveHistoryAsync();
            }
        }

        public void ClearFavorites()
        {
            lock (_lock)
            {
                var itemsForCacheDeletion = new List<ClipboardItem>();
                DispatchSafe(() =>
                {
                    var histIds = new HashSet<string>(Items.Where(h => !string.IsNullOrEmpty(h.Id)).Select(h => h.Id), StringComparer.OrdinalIgnoreCase);
                    var histPaths = new HashSet<string>(Items.Where(h => h.IsImage && !string.IsNullOrEmpty(h.ImagePath)).Select(h => Path.GetFileName(h.ImagePath)), StringComparer.OrdinalIgnoreCase);

                    for (int i = FavoriteItems.Count - 1; i >= 0; i--)
                    {
                        var it = FavoriteItems[i];
                        string fn = (it.IsImage && !string.IsNullOrEmpty(it.ImagePath)) ? Path.GetFileName(it.ImagePath) : null;
                        bool inHist = (!string.IsNullOrEmpty(it.Id) && histIds.Contains(it.Id)) ||
                                      (fn != null && histPaths.Contains(fn));
                        if (!inHist && it.IsImage)
                        {
                            itemsForCacheDeletion.Add(it);
                        }
                    }
                    foreach (var histItem in Items)
                    {
                        histItem.IsFavorite = false;
                    }
                    FavoriteItems.Clear();
                });

                foreach (var it in itemsForCacheDeletion)
                {
                    DeleteCacheFile(it);
                }

                SaveFavoritesAsync();
                SaveHistoryAsync();
            }
        }

        public void ClearAll(bool keepFavorites = true)
        {
            ClearHistory();
            if (!keepFavorites)
            {
                ClearFavorites();
            }
        }

        private bool _saveHistPending = false;
        public void SaveHistoryAsync()
        {
            if (_saveHistPending) return;
            _saveHistPending = true;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Threading.Thread.Sleep(300);
                lock (_lock)
                {
                    _saveHistPending = false;
                    SaveHistoryInternal();
                }
            });
        }

        private bool _saveFavPending = false;
        private void SaveFavoritesAsync()
        {
            if (_saveFavPending) return;
            _saveFavPending = true;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Threading.Thread.Sleep(300);
                lock (_lock)
                {
                    _saveFavPending = false;
                    SaveFavoritesInternal();
                }
            });
        }

        public void SaveHistoryNow()
        {
            lock (_lock)
            {
                SaveHistoryInternal();
            }
        }

        public void SaveFavoritesNow()
        {
            lock (_lock)
            {
                SaveFavoritesInternal();
            }
        }

        public void SaveAllNow()
        {
            lock (_lock)
            {
                SaveHistoryInternal();
                SaveFavoritesInternal();
            }
        }

        public string SerializeItemsToJson(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return "[]";
            var sb = new StringBuilder();
            sb.AppendLine("[");
            var list = items.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                sb.Append("  {");
                sb.Append($"\"Id\": \"{Escape(it.Id)}\", ");
                sb.Append($"\"ContentType\": {(int)it.ContentType}, ");
                sb.Append($"\"DropEffect\": {it.DropEffect}, ");
                sb.Append($"\"Timestamp\": \"{it.Timestamp:o}\", ");
                sb.Append($"\"CharCount\": {it.CharCount}, ");
                sb.Append($"\"ByteSize\": {it.ByteSize}, ");
                sb.Append($"\"IsFavorite\": {(it.IsFavorite ? "true" : "false")}, ");
                sb.Append($"\"IsBlurred\": {(it.IsBlurred ? "true" : "false")}, ");
                sb.Append($"\"BlurMode\": \"{Escape(it.BlurMode ?? "Blur")}\", ");
                sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\"BlurRadius\": {0:F1}, ", it.BlurRadius));
                sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\"PixelateSize\": {0:F1}, ", it.PixelateSize));
                sb.Append($"\"GroupName\": \"{Escape(it.GroupName ?? "")}\", ");
                sb.Append($"\"ImagePath\": \"{Escape(it.ImagePath ?? "")}\", ");
                sb.Append($"\"ThumbPath\": \"{Escape(it.ThumbPath ?? "")}\", ");
                sb.Append($"\"SourceApp\": \"{Escape(it.SourceApp ?? "")}\", ");
                sb.Append($"\"SourceIconPath\": \"{Escape(it.SourceIconPath ?? "")}\", ");
                sb.Append($"\"PreviewText\": \"{Escape(it.PreviewText ?? "")}\", ");
                sb.Append($"\"TextContent\": \"{Escape(it.TextContent ?? "")}\"");
                sb.Append("}");

                if (i < list.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private void SaveHistoryInternal()
        {
            try
            {
                string path = GetHistoryFilePath();
                List<ClipboardItem> snapshot = null;
                DispatchSafe(() =>
                {
                    snapshot = new List<ClipboardItem>(Items);
                });
                string json = SerializeItemsToJson(snapshot);
                File.WriteAllText(path, json, Encoding.UTF8);

                // Đồng bộ ngay sang project root .portable nếu đang chạy trong thư mục build \bin\
                try
                {
                    string appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                    if (appDir.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\.."));
                        string rootPortableHist = Path.Combine(projectRoot, ".portable", "clipboard", "clipboard_history.json");
                        string rootClipDir = Path.GetDirectoryName(rootPortableHist);
                        if (!Directory.Exists(rootClipDir)) Directory.CreateDirectory(rootClipDir);
                        File.WriteAllText(rootPortableHist, json, Encoding.UTF8);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error saving clipboard history: " + ex.Message);
            }
        }

        private void SaveFavoritesInternal()
        {
            try
            {
                string path = GetFavoritesFilePath();
                List<ClipboardItem> snapshot = null;
                DispatchSafe(() =>
                {
                    snapshot = new List<ClipboardItem>(FavoriteItems);
                });
                string json = SerializeItemsToJson(snapshot);
                File.WriteAllText(path, json, Encoding.UTF8);

                // Đồng bộ ngay sang project root .portable nếu đang chạy trong thư mục build \bin\
                try
                {
                    string appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                    if (appDir.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\.."));
                        string rootPortableFav = Path.Combine(projectRoot, ".portable", "clipboard", "clipboard_favorites.json");
                        string rootClipDir = Path.GetDirectoryName(rootPortableFav);
                        if (!Directory.Exists(rootClipDir)) Directory.CreateDirectory(rootClipDir);
                        File.WriteAllText(rootPortableFav, json, Encoding.UTF8);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error saving clipboard favorites: " + ex.Message);
            }
        }

        public void LoadHistory()
        {
            string path = GetHistoryFilePath();
            bool jsonExistsAndLoaded = false;
            if (File.Exists(path))
            {
                try
                {
                    string content = File.ReadAllText(path, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var list = ParseJsonItems(content);
                        DispatchSafe(() =>
                        {
                            Items.Clear();
                            foreach (var item in list)
                            {
                                item.DetectMetadata();
                                if (item.IsImage)
                                {
                                    EnsureHistoryImageInHistoryFolder(item);
                                }
                                Items.Add(item);
                            }
                        });
                        jsonExistsAndLoaded = (list != null && list.Count > 0);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading clipboard history: " + ex.Message);
                }
            }

            DispatchSafe(() =>
            {
                if (!jsonExistsAndLoaded && (!File.Exists(path) || new FileInfo(path).Length <= 2))
                {
                    AutoRecoverOrphanedCacheImages();
                }
                TrimLimit();
            });
        }

        private void AutoRecoverOrphanedCacheImages()
        {
            try
            {
                string histCacheDir = GetHistoryCacheDirectory();
                if (!Directory.Exists(histCacheDir)) return;

                var existingImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in Items)
                {
                    if (item.IsImage)
                    {
                        if (!string.IsNullOrEmpty(item.ImagePath)) existingImageFiles.Add(Path.GetFileName(item.ImagePath));
                        if (!string.IsNullOrEmpty(item.ThumbPath)) existingImageFiles.Add(Path.GetFileName(item.ThumbPath));
                    }
                }

                var pngFiles = Directory.GetFiles(histCacheDir, "*.png", SearchOption.TopDirectoryOnly);
                bool recoveredAny = false;

                foreach (var png in pngFiles)
                {
                    string fileName = Path.GetFileName(png);
                    if (fileName.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase)) continue;
                    if (existingImageFiles.Contains(fileName)) continue;

                    try
                    {
                        var fi = new FileInfo(png);
                        string thumbPath = Path.Combine(histCacheDir, Path.GetFileNameWithoutExtension(fileName) + "_t.png");
                        if (!File.Exists(thumbPath)) thumbPath = png;

                        var item = new ClipboardItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ContentType = ClipboardContentType.Image,
                            ImagePath = png,
                            ThumbPath = thumbPath,
                            PreviewText = $"[Hình ảnh] {fi.Name}",
                            Timestamp = fi.LastWriteTime,
                            ByteSize = fi.Length,
                            IsFavorite = false
                        };

                        item.DetectMetadata();
                        Items.Add(item);
                        recoveredAny = true;
                    }
                    catch { }
                }

                if (recoveredAny)
                {
                    var sorted = Items.OrderByDescending(x => x.Timestamp).ToList();
                    Items.Clear();
                    foreach (var it in sorted) Items.Add(it);
                    SaveHistoryAsync();
                }
            }
            catch { }
        }

        public void LoadFavorites()
        {
            string path = GetFavoritesFilePath();
            string content = null;

            if (File.Exists(path))
            {
                try { content = File.ReadAllText(path, Encoding.UTF8); } catch { }
            }

            // Nếu file chính rỗng hoặc chưa có, tìm ở các vị trí fallback / root .portable
            if (string.IsNullOrWhiteSpace(content) || content.Trim() == "[]")
            {
                string cfgDir = SettingsManager.GetConfigDirectory();
                string appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                string projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\.."));

                string[] fallbackPaths = new[]
                {
                    Path.Combine(GetClipboardDirectory(), "favorites.json"),
                    Path.Combine(cfgDir, "clipboard_favorites.json"),
                    Path.Combine(cfgDir, "favorites.json"),
                    Path.Combine(projectRoot, ".portable", "clipboard", "clipboard_favorites.json"),
                    Path.Combine(projectRoot, ".portable", "clipboard_favorites.json"),
                    Path.Combine(projectRoot, ".portable", "clipboard", "favorites.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModernKey", "clipboard", "clipboard_favorites.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModernKey", "clipboard_favorites.json")
                };

                foreach (var fb in fallbackPaths)
                {
                    try
                    {
                        if (File.Exists(fb))
                        {
                            string fbContent = File.ReadAllText(fb, Encoding.UTF8);
                            if (!string.IsNullOrWhiteSpace(fbContent) && fbContent.Trim() != "[]")
                            {
                                content = fbContent;
                                try
                                {
                                    string dir = Path.GetDirectoryName(path);
                                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                    File.WriteAllText(path, content, Encoding.UTF8);
                                }
                                catch { }
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrWhiteSpace(content)) return;

            try
            {
                var list = ParseJsonItems(content);
                if (list == null || list.Count == 0) return;

                DispatchSafe(() =>
                {
                    FavoriteItems.Clear();
                    foreach (var item in list)
                    {
                        item.DetectMetadata();
                        item.IsFavorite = true;
                        if (item.IsImage)
                        {
                            EnsureFavoriteImageInFavoritesFolder(item);
                        }
                        FavoriteItems.Add(item);
                    }
                    // Đồng bộ cờ IsFavorite cho các mục tương ứng trong Items
                    foreach (var histItem in Items)
                    {
                        if (FindMatchingItem(FavoriteItems, histItem) != null)
                        {
                            histItem.IsFavorite = true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error loading clipboard favorites: " + ex.Message);
            }
        }

        public static ClipboardItem FindMatchingItem(IEnumerable<ClipboardItem> collection, ClipboardItem target)
        {
            if (collection == null || target == null) return null;
            foreach (var it in collection)
            {
                if (string.Equals(it.Id, target.Id, StringComparison.OrdinalIgnoreCase)) return it;
                if (it.ContentType == target.ContentType)
                {
                    if (it.ContentType == ClipboardContentType.Text && string.Equals(it.TextContent, target.TextContent, StringComparison.Ordinal)) return it;
                    if (it.ContentType == ClipboardContentType.Files && string.Equals(it.TextContent, target.TextContent, StringComparison.OrdinalIgnoreCase)) return it;
                    if (it.ContentType == ClipboardContentType.Image && !string.IsNullOrEmpty(it.ImagePath) && !string.IsNullOrEmpty(target.ImagePath))
                    {
                        if (string.Equals(Path.GetFileName(it.ImagePath), Path.GetFileName(target.ImagePath), StringComparison.OrdinalIgnoreCase)) return it;
                    }
                }
            }
            return null;
        }

        public string GenerateBackupFileName(string targetFolder = null)
        {
            string folder = !string.IsNullOrEmpty(targetFolder) ? targetFolder : GetBackupDirectory();
            int nextIndex = 1;
            try
            {
                if (Directory.Exists(folder))
                {
                    var zipFiles = Directory.GetFiles(folder, "*.zip");
                    int maxIdx = 0;
                    foreach (var zf in zipFiles)
                    {
                        string fn = Path.GetFileNameWithoutExtension(zf);
                        var match = Regex.Match(fn, @"^(\d+)\.");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
                        {
                            if (idx > maxIdx) maxIdx = idx;
                        }
                    }
                    nextIndex = maxIdx + 1;
                }
            }
            catch { }

            string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
            return $"{nextIndex}. {timeStr}.zip";
        }

        public void ExportToZip(string zipFilePath, IEnumerable<ClipboardItem> historyItems, IEnumerable<ClipboardItem> favoriteItems)
        {
            string dir = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Ghi tệp clipboard_history.json
                if (historyItems != null)
                {
                    string histJson = SerializeItemsToJson(historyItems);
                    var entry = archive.CreateEntry("clipboard_history.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                    {
                        writer.Write(histJson);
                    }
                }

                // 2. Ghi tệp clipboard_favorites.json
                if (favoriteItems != null)
                {
                    string favJson = SerializeItemsToJson(favoriteItems);
                    var entry = archive.CreateEntry("clipboard_favorites.json", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                    {
                        writer.Write(favJson);
                    }
                }

                // 3. Ghi manifest thông tin gói
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                {
                    int histCount = historyItems != null ? historyItems.Count() : 0;
                    int favCount = favoriteItems != null ? favoriteItems.Count() : 0;
                    writer.Write($"{{\"App\":\"ModernKey\",\"Version\":\"1.0\",\"CreatedAt\":\"{DateTime.Now:o}\",\"HistoryCount\":{histCount},\"FavoritesCount\":{favCount}}}");
                }

                // 4. Đóng gói toàn bộ tệp hình ảnh & icons thuộc các item được export
                var allItems = (historyItems ?? Enumerable.Empty<ClipboardItem>())
                    .Concat(favoriteItems ?? Enumerable.Empty<ClipboardItem>());

                foreach (var it in allItems)
                {
                    AddMediaFileToZip(archive, it.ImagePath, "cache", addedFiles);
                    AddMediaFileToZip(archive, it.ThumbPath, "cache", addedFiles);
                    AddMediaFileToZip(archive, it.SourceIconPath, "cache/icons", addedFiles);
                }
            }
        }

        private static void AddMediaFileToZip(ZipArchive archive, string localPath, string zipFolder, HashSet<string> addedFiles)
        {
            if (string.IsNullOrEmpty(localPath)) return;
            string resolved = ClipboardItem.ResolvePath(localPath);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return;

            string fn = Path.GetFileName(resolved);
            string entryName = $"{zipFolder}/{fn}".Replace('\\', '/');

            if (addedFiles.Contains(entryName)) return;
            addedFiles.Add(entryName);

            try
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using (var entryStream = entry.Open())
                using (var fileStream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileStream.CopyTo(entryStream);
                }
            }
            catch { }
        }

        public (int historyCount, int favoritesCount) RestoreFromZip(string zipFilePath)
        {
            if (!File.Exists(zipFilePath)) return (0, 0);

            int histCount = 0;
            int favCount = 0;

            lock (_lock)
            {
                string cacheDir = GetCacheDirectory();
                string iconsDir = Path.Combine(cacheDir, "icons");
                if (!Directory.Exists(iconsDir)) Directory.CreateDirectory(iconsDir);

                using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    // 1. Trích xuất tất cả tệp cache vào thư mục clipboard_cache của máy hiện tại
                    foreach (var entry in archive.Entries)
                    {
                        string norm = entry.FullName.Replace('\\', '/');
                        if (norm.StartsWith("cache/", StringComparison.OrdinalIgnoreCase))
                        {
                            string sub = norm.Substring("cache/".Length).TrimStart('/');
                            if (string.IsNullOrEmpty(sub)) continue;

                            string dest = Path.Combine(cacheDir, sub.Replace('/', Path.DirectorySeparatorChar));
                            string destFolder = Path.GetDirectoryName(dest);
                            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

                            entry.ExtractToFile(dest, true);
                        }
                    }

                    // 2. Nạp các mục lịch sử
                    var histEntry = archive.GetEntry("clipboard_history.json") ?? archive.GetEntry("history.json") ?? archive.GetEntry("items.json");
                    if (histEntry != null)
                    {
                        using (var reader = new StreamReader(histEntry.Open(), Encoding.UTF8))
                        {
                            string json = reader.ReadToEnd();
                            var importedItems = ParseJsonItems(json);
                            DispatchSafe(() =>
                            {
                                foreach (var it in importedItems)
                                {
                                    FixLocalPaths(it, cacheDir);
                                    if (FindMatchingItem(Items, it) == null)
                                    {
                                        Items.Add(it);
                                        histCount++;
                                    }
                                }
                            });
                        }
                    }

                    // 3. Nạp các mục yêu thích
                    var favEntry = archive.GetEntry("clipboard_favorites.json") ?? archive.GetEntry("favorites.json");
                    if (favEntry != null)
                    {
                        using (var reader = new StreamReader(favEntry.Open(), Encoding.UTF8))
                        {
                            string json = reader.ReadToEnd();
                            var importedFavs = ParseJsonItems(json);
                            DispatchSafe(() =>
                            {
                                foreach (var it in importedFavs)
                                {
                                    FixLocalPaths(it, cacheDir);
                                    it.IsFavorite = true;
                                    if (FindMatchingItem(FavoriteItems, it) == null)
                                    {
                                        FavoriteItems.Add(it);
                                        favCount++;
                                    }
                                    var matchHist = FindMatchingItem(Items, it);
                                    if (matchHist != null) matchHist.IsFavorite = true;
                                }
                            });
                        }
                    }
                }

                SaveHistoryNow();
                SaveFavoritesNow();
            }

            return (histCount, favCount);
        }

        private static void FixLocalPaths(ClipboardItem it, string localCacheDir)
        {
            if (!string.IsNullOrEmpty(it.ImagePath))
            {
                string fn = Path.GetFileName(it.ImagePath);
                it.ImagePath = Path.Combine(localCacheDir, fn);
            }
            if (!string.IsNullOrEmpty(it.ThumbPath))
            {
                string fnThumb = Path.GetFileName(it.ThumbPath);
                it.ThumbPath = Path.Combine(localCacheDir, fnThumb);
            }
            if (!string.IsNullOrEmpty(it.SourceIconPath))
            {
                string fnIcon = Path.GetFileName(it.SourceIconPath);
                it.SourceIconPath = Path.Combine(localCacheDir, "icons", fnIcon);
            }
        }

        public string ResolveCachePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace("\r", "r").Replace("\n", "n");
            if (File.Exists(path)) return path;

            try
            {
                string fn = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fn))
                {
                    string cacheDir = GetCacheDirectory();
                    string candHist = Path.Combine(cacheDir, "history", fn);
                    if (File.Exists(candHist)) return candHist;

                    string candFav = Path.Combine(cacheDir, "favorites", fn);
                    if (File.Exists(candFav)) return candFav;

                    string cand = Path.Combine(cacheDir, fn);
                    if (File.Exists(cand)) return cand;

                    string candIcon = Path.Combine(cacheDir, "icons", fn);
                    if (File.Exists(candIcon)) return candIcon;
                }
            }
            catch { }

            return path;
        }

        public List<ClipboardItem> ParseJsonItems(string json)
        {
            var result = new List<ClipboardItem>();
            // Tách từng object {...}
            var matches = Regex.Matches(json, @"\{(?<obj>.*?)\}(?=\s*,\s*\{|\s*\])", RegexOptions.Singleline);
            foreach (Match m in matches)
            {
                string block = m.Groups["obj"].Value;
                var item = new ClipboardItem();

                item.Id = ExtractJsonString(block, "Id") ?? Guid.NewGuid().ToString("N");
                item.ContentType = (ClipboardContentType)ExtractJsonInt(block, "ContentType", 0);
                item.DropEffect = ExtractJsonInt(block, "DropEffect", 1);
                item.CharCount = ExtractJsonInt(block, "CharCount", 0);
                item.ByteSize = ExtractJsonLong(block, "ByteSize", 0);
                item.IsFavorite = ExtractJsonBool(block, "IsFavorite", false);
                item.IsBlurred = ExtractJsonBool(block, "IsBlurred", false);
                item.BlurMode = ExtractJsonString(block, "BlurMode") ?? "None";
                item.BlurRadius = ExtractJsonDouble(block, "BlurRadius", 0.0);
                item.PixelateSize = ExtractJsonDouble(block, "PixelateSize", 0.0);
                item.GroupName = ExtractJsonString(block, "GroupName") ?? string.Empty;
                item.ImagePath = ResolveCachePath(ExtractJsonString(block, "ImagePath"));
                item.ThumbPath = ResolveCachePath(ExtractJsonString(block, "ThumbPath"));
                item.SourceApp = ExtractJsonString(block, "SourceApp");
                item.SourceIconPath = ResolveCachePath(ExtractJsonString(block, "SourceIconPath"));
                item.PreviewText = ExtractJsonString(block, "PreviewText");
                item.TextContent = ExtractJsonString(block, "TextContent");

                string timeStr = ExtractJsonString(block, "Timestamp");
                if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out var dt))
                {
                    item.Timestamp = dt;
                }

                result.Add(item);
            }

            return result;
        }

        private static double ExtractJsonDouble(string block, string key, double defVal)
        {
            var match = Regex.Match(block, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<val>-?\\d+(?:\\.\\d+)?)");
            if (match.Success && double.TryParse(match.Groups["val"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return defVal;
        }

        private static string ExtractJsonString(string block, string key)
        {
            var match = Regex.Match(block, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<val>(?:\\\\\"|[^\"])*)\"");
            if (match.Success)
            {
                return Unescape(match.Groups["val"].Value);
            }
            return null;
        }

        private static int ExtractJsonInt(string block, string key, int defVal)
        {
            var match = Regex.Match(block, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<val>-?\\d+)");
            if (match.Success && int.TryParse(match.Groups["val"].Value, out int val))
            {
                return val;
            }
            return defVal;
        }

        private static long ExtractJsonLong(string block, string key, long defVal)
        {
            var match = Regex.Match(block, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<val>-?\\d+)");
            if (match.Success && long.TryParse(match.Groups["val"].Value, out long val))
            {
                return val;
            }
            return defVal;
        }

        private static bool ExtractJsonBool(string block, string key, bool defVal)
        {
            var match = Regex.Match(block, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<val>true|false)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return string.Equals(match.Groups["val"].Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            return defVal;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '\\': sb.Append('\\'); i++; break;
                        case '"': sb.Append('"'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        default: sb.Append(next); i++; break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }
    }
}
