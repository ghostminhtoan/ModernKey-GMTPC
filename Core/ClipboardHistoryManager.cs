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
            EnsureDirectoriesAndMigrate();
            LoadHistory();
            LoadFavorites();
        }

        public string GetClipboardDirectory()
        {
            string dir = Path.Combine(SettingsManager.GetConfigDirectory(), "clipboard");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return dir;
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

                // 1. Tự động chuyển file clipboard_history.json cũ vào folder clipboard\ mới
                string oldHist = Path.Combine(cfgDir, "clipboard_history.json");
                string newHist = Path.Combine(clipDir, "clipboard_history.json");
                if (File.Exists(oldHist) && !File.Exists(newHist))
                {
                    try { File.Move(oldHist, newHist); } catch { }
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
                GetBackupDirectory();
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
                    IsFavorite = false
                };

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
                    IsFavorite = false
                };

                InsertItem(item);
            }
        }

        public void AddImage(byte[] pngBytes, int width, int height, string sourceApp = null, string sourceIconPath = null)
        {
            if (pngBytes == null || pngBytes.Length == 0) return;

            lock (_lock)
            {
                string id = Guid.NewGuid().ToString("N");
                string filename = id + ".png";
                string thumbFilename = id + "_thumb.png";
                string fullPath = Path.Combine(GetCacheDirectory(), filename);
                string thumbFullPath = Path.Combine(GetCacheDirectory(), thumbFilename);

                try
                {
                    File.WriteAllBytes(fullPath, pngBytes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error saving clipboard image: " + ex.Message);
                    return;
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
                    TextContent = "[Hình ảnh chụp / sao chép]",
                    PreviewText = $"[Hình ảnh {width}x{height}]",
                    ImagePath = fullPath,
                    ThumbPath = thumbFullPath,
                    SourceApp = sourceApp ?? string.Empty,
                    SourceIconPath = sourceIconPath ?? string.Empty,
                    Timestamp = DateTime.Now,
                    CharCount = 0,
                    ByteSize = pngBytes.Length,
                    IsFavorite = false
                };

                InsertItem(item);
            }
        }

        public void AddImage(byte[] pngBytes, BitmapSource bmpSource, string sourceApp = null, string sourceIconPath = null)
        {
            AddImage(pngBytes, bmpSource?.PixelWidth ?? 0, bmpSource?.PixelHeight ?? 0, sourceApp, sourceIconPath);
        }

        private static void DispatchSafe(Action action)
        {
            if (action == null) return;
            var app = Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(action);
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
                    var existing = FindMatchingItem(FavoriteItems, item);
                    if (existing != null)
                    {
                        DispatchSafe(() =>
                        {
                            FavoriteItems.Remove(existing);
                        });
                    }
                    var matchHist = FindMatchingItem(Items, item);
                    if (matchHist != null) matchHist.IsFavorite = false;
                }

                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void DeleteItem(ClipboardItem item, bool isFavoriteView = false)
        {
            if (item == null) return;
            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    if (isFavoriteView)
                    {
                        FavoriteItems.Remove(item);
                        var matchHist = FindMatchingItem(Items, item);
                        if (matchHist != null) matchHist.IsFavorite = false;
                        if (matchHist == null) DeleteCacheFile(item);
                    }
                    else
                    {
                        Items.Remove(item);
                        var matchFav = FindMatchingItem(FavoriteItems, item);
                        if (matchFav == null) DeleteCacheFile(item);
                    }
                });
                SaveHistoryAsync();
                SaveFavoritesAsync();
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    for (int i = Items.Count - 1; i >= 0; i--)
                    {
                        var it = Items[i];
                        if (FindMatchingItem(FavoriteItems, it) == null)
                        {
                            DeleteCacheFile(it);
                        }
                        Items.RemoveAt(i);
                    }
                });
                SaveHistoryAsync();
            }
        }

        public void ClearFavorites()
        {
            lock (_lock)
            {
                DispatchSafe(() =>
                {
                    for (int i = FavoriteItems.Count - 1; i >= 0; i--)
                    {
                        var it = FavoriteItems[i];
                        var matchHist = FindMatchingItem(Items, it);
                        if (matchHist != null) matchHist.IsFavorite = false;
                        if (matchHist == null) DeleteCacheFile(it);
                        FavoriteItems.RemoveAt(i);
                    }
                });
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

        private void DeleteCacheFile(ClipboardItem item)
        {
            if (item != null && item.IsImage)
            {
                try
                {
                    if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                    {
                        File.Delete(item.ImagePath);
                    }
                }
                catch { }
                try
                {
                    if (!string.IsNullOrEmpty(item.ThumbPath) && File.Exists(item.ThumbPath))
                    {
                        File.Delete(item.ThumbPath);
                    }
                }
                catch { }
            }
        }

        private bool _saveHistPending = false;
        private void SaveHistoryAsync()
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error saving clipboard favorites: " + ex.Message);
            }
        }

        public void LoadHistory()
        {
            string path = GetHistoryFilePath();
            if (!File.Exists(path)) return;

            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content)) return;

                var list = ParseJsonItems(content);
                DispatchSafe(() =>
                {
                    Items.Clear();
                    foreach (var item in list)
                    {
                        Items.Add(item);
                    }
                    TrimLimit();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error loading clipboard history: " + ex.Message);
            }
        }

        public void LoadFavorites()
        {
            string path = GetFavoritesFilePath();
            if (!File.Exists(path)) return;

            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content)) return;

                var list = ParseJsonItems(content);
                DispatchSafe(() =>
                {
                    FavoriteItems.Clear();
                    foreach (var item in list)
                    {
                        item.IsFavorite = true;
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

            string timeStr = DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss tt dddd", System.Globalization.CultureInfo.InvariantCulture);
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
