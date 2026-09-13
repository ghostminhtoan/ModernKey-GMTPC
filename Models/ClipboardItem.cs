using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ModernKey.Models
{
    public enum ClipboardContentType
    {
        Text = 0,
        Image = 1,
        Files = 2
    }

    public class ClipboardItem : INotifyPropertyChanged
    {
        private bool _isFavorite = false;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ClipboardContentType ContentType { get; set; } = ClipboardContentType.Text;
        public int DropEffect { get; set; } = 1; // 1 = Copy (DROPEFFECT_COPY), 2 = Move/Cut (DROPEFFECT_MOVE)
        public string TextContent { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string ThumbPath { get; set; } = string.Empty;
        public string SourceApp { get; set; } = string.Empty;
        public string SourceIconPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int CharCount { get; set; } = 0;
        public long ByteSize { get; set; } = 0;

        public List<string> FilePaths
        {
            get
            {
                if (ContentType != ClipboardContentType.Files || string.IsNullOrEmpty(TextContent))
                    return new List<string>();
                return TextContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(p => p.Trim())
                                  .Where(p => !string.IsNullOrEmpty(p))
                                  .ToList();
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                    OnPropertyChanged(nameof(FavoriteIcon));
                    OnPropertyChanged(nameof(FavoriteBrush));
                }
            }
        }

        private string _groupName = string.Empty;
        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged(nameof(GroupName));
                    OnPropertyChanged(nameof(GroupDisplay));
                    OnPropertyChanged(nameof(GroupBadgeVisibility));
                    OnPropertyChanged(nameof(GroupBadgeText));
                }
            }
        }

        public string GroupDisplay => string.IsNullOrEmpty(_groupName) ? "-All-" : _groupName;
        public Visibility GroupBadgeVisibility => !string.IsNullOrEmpty(_groupName) ? Visibility.Visible : Visibility.Collapsed;
        public string GroupBadgeText => !string.IsNullOrEmpty(_groupName) ? $"[{_groupName}]" : string.Empty;

        public string TimeDisplay => Timestamp.ToString("HH:mm:ss dd/MM");

        public string InfoDisplay
        {
            get
            {
                string appPrefix = !string.IsNullOrEmpty(SourceApp) ? $"{SourceApp} • " : "";
                if (ContentType == ClipboardContentType.Image)
                {
                    return $"{appPrefix}[ẢNH] {ByteSize / 1024} KB";
                }
                if (ContentType == ClipboardContentType.Files)
                {
                    string effectText = DropEffect == 2 ? "Di chuyển" : "Sao chép";
                    string sizeStr = ByteSize > 0 ? (ByteSize >= 1024 * 1024 ? $"{ByteSize / (1024 * 1024)} MB" : $"{Math.Max(1, ByteSize / 1024)} KB") : "";
                    string sizePart = !string.IsNullOrEmpty(sizeStr) ? $" • {sizeStr}" : "";
                    return $"{appPrefix}[{effectText}] {CharCount} tệp/thư mục{sizePart}";
                }
                return $"{appPrefix}{CharCount} ký tự • {ByteSize} B";
            }
        }

        public string FavoriteIcon => IsFavorite ? "★" : "☆";
        public string FavoriteBrush => IsFavorite ? "#FFE600" : "#55657E";

        public string TypeBadge
        {
            get
            {
                if (ContentType == ClipboardContentType.Image) return "[IMG]";
                if (ContentType == ClipboardContentType.Files)
                {
                    if (DropEffect == 2) return "[CUT]";
                    var paths = FilePaths;
                    if (paths.Count == 1)
                    {
                        try
                        {
                            if (Directory.Exists(paths[0])) return "[DIR]";
                        }
                        catch { }
                        return "[FILE]";
                    }
                    return "[FILES]";
                }
                return (TextContent != null && TextContent.IndexOf('\n') >= 0 ? "[TXT+]" : "[TXT]");
            }
        }

        public bool IsImage => ContentType == ClipboardContentType.Image;
        public bool IsFiles => ContentType == ClipboardContentType.Files;
        public Visibility ImageThumbnailVisibility => IsImage ? Visibility.Visible : Visibility.Collapsed;

        public Visibility AppIconVisibility => !string.IsNullOrEmpty(SourceIconPath) && File.Exists(SourceIconPath) ? Visibility.Visible : Visibility.Collapsed;

        private string _customTitle = string.Empty;
        public string CustomTitle
        {
            get => _customTitle;
            set
            {
                if (_customTitle != value)
                {
                    _customTitle = value ?? string.Empty;
                    OnPropertyChanged(nameof(CustomTitle));
                    OnPropertyChanged(nameof(HasCustomTitle));
                    OnPropertyChanged(nameof(CustomTitleVisibility));
                    OnPropertyChanged(nameof(DefaultTitleVisibility));
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }
        public bool HasCustomTitle => !string.IsNullOrWhiteSpace(_customTitle);
        public Visibility CustomTitleVisibility => HasCustomTitle ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DefaultTitleVisibility => HasCustomTitle ? Visibility.Collapsed : Visibility.Visible;
        public string DisplayTitle => HasCustomTitle ? _customTitle : PreviewText;

        private string _shortcutKey = string.Empty;
        public string ShortcutKey
        {
            get => _shortcutKey;
            set
            {
                if (_shortcutKey != value)
                {
                    _shortcutKey = value ?? string.Empty;
                    OnPropertyChanged(nameof(ShortcutKey));
                    OnPropertyChanged(nameof(ShortcutText));
                    OnPropertyChanged(nameof(HasShortcut));
                    OnPropertyChanged(nameof(ShortcutVisibility));
                }
            }
        }

        private uint _shortcutVk = 0;
        public uint ShortcutVk
        {
            get => _shortcutVk;
            set
            {
                if (_shortcutVk != value)
                {
                    _shortcutVk = value;
                    OnPropertyChanged(nameof(ShortcutVk));
                }
            }
        }

        private int _shortcutModifiers = 0; // 1 = Alt, 2 = Ctrl, 4 = Shift, 8 = Win
        public int ShortcutModifiers
        {
            get => _shortcutModifiers;
            set
            {
                if (_shortcutModifiers != value)
                {
                    _shortcutModifiers = value;
                    OnPropertyChanged(nameof(ShortcutModifiers));
                    OnPropertyChanged(nameof(ShortcutText));
                    OnPropertyChanged(nameof(HasShortcut));
                    OnPropertyChanged(nameof(ShortcutVisibility));
                }
            }
        }

        public string ShortcutText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_shortcutKey)) return string.Empty;
                var parts = new List<string>();
                if ((_shortcutModifiers & 2) != 0) parts.Add("Ctrl");
                if ((_shortcutModifiers & 1) != 0) parts.Add("Alt");
                if ((_shortcutModifiers & 4) != 0) parts.Add("Shift");
                if ((_shortcutModifiers & 8) != 0) parts.Add("Win");
                parts.Add(_shortcutKey.ToUpperInvariant());
                return string.Join("+", parts);
            }
        }
        public bool HasShortcut => !string.IsNullOrEmpty(ShortcutText);
        public Visibility ShortcutVisibility => HasShortcut ? Visibility.Visible : Visibility.Collapsed;

        private string _backgroundColorHex = string.Empty;
        public string BackgroundColorHex
        {
            get => _backgroundColorHex;
            set
            {
                if (_backgroundColorHex != value)
                {
                    _backgroundColorHex = value ?? string.Empty;
                    OnPropertyChanged(nameof(BackgroundColorHex));
                    OnPropertyChanged(nameof(HasCustomBackground));
                    OnPropertyChanged(nameof(ItemBackgroundBrush));
                }
            }
        }
        public bool HasCustomBackground => !string.IsNullOrWhiteSpace(_backgroundColorHex) && !_backgroundColorHex.Equals("Default", StringComparison.OrdinalIgnoreCase);

        public System.Windows.Media.Brush ItemBackgroundBrush
        {
            get
            {
                if (HasCustomBackground)
                {
                    try
                    {
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_backgroundColorHex);
                        // Đảm bảo alpha để hòa trộn mượt mà với giao diện
                        if (color.A == 255)
                        {
                            color = System.Windows.Media.Color.FromArgb(200, color.R, color.G, color.B);
                        }
                        var brush = new System.Windows.Media.SolidColorBrush(color);
                        brush.Freeze();
                        return brush;
                    }
                    catch { }
                }
                return System.Windows.Media.Brushes.Transparent;
            }
        }

        public ClipboardItem Clone()
        {
            return new ClipboardItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ContentType = this.ContentType,
                DropEffect = this.DropEffect,
                TextContent = this.TextContent,
                PreviewText = this.PreviewText,
                ImagePath = this.ImagePath,
                ThumbPath = this.ThumbPath,
                SourceApp = this.SourceApp,
                SourceIconPath = this.SourceIconPath,
                Timestamp = this.Timestamp,
                CharCount = this.CharCount,
                ByteSize = this.ByteSize,
                IsFavorite = this.IsFavorite,
                GroupName = this.GroupName,
                CustomTitle = this.CustomTitle,
                ShortcutKey = this.ShortcutKey,
                ShortcutVk = this.ShortcutVk,
                ShortcutModifiers = this.ShortcutModifiers,
                BackgroundColorHex = this.BackgroundColorHex
            };
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace("\r", "r").Replace("\n", "n");
            if (File.Exists(path)) return path;

            try
            {
                string fn = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fn))
                {
                    string cfgDir = ModernKey.Config.SettingsManager.GetConfigDirectory();

                    // 1. Thư mục chuẩn mới: <ConfigDir>\clipboard\clipboard_cache\
                    string newCacheDir = Path.Combine(cfgDir, "clipboard", "clipboard_cache");
                    string candNew = Path.Combine(newCacheDir, fn);
                    if (File.Exists(candNew)) return candNew;

                    string candNewIcon = Path.Combine(newCacheDir, "icons", fn);
                    if (File.Exists(candNewIcon)) return candNewIcon;

                    // 2. Dự phòng thư mục cũ nếu chưa chuyển: <ConfigDir>\clipboard_cache\
                    string oldCacheDir = Path.Combine(cfgDir, "clipboard_cache");
                    string candOld = Path.Combine(oldCacheDir, fn);
                    if (File.Exists(candOld)) return candOld;

                    string candOldIcon = Path.Combine(oldCacheDir, "icons", fn);
                    if (File.Exists(candOldIcon)) return candOldIcon;
                }
            }
            catch { }

            return path;
        }

        private static BitmapSource LoadBitmapSafe(string path, int decodeWidth)
        {
            string resolved = ResolvePath(path);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return null;

            try
            {
                using (var fs = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = fs;
                    if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly Dictionary<string, BitmapSource> _appIconCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _appIconCacheLock = new object();

        public static BitmapSource GetCachedAppIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return null;
            string resolved = ResolvePath(iconPath);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return null;

            lock (_appIconCacheLock)
            {
                if (_appIconCache.TryGetValue(resolved, out var cached)) return cached;
                var bmp = LoadBitmapSafe(resolved, 16);
                if (bmp != null)
                {
                    _appIconCache[resolved] = bmp;
                }
                return bmp;
            }
        }

        public BitmapSource AppIconSource => GetCachedAppIcon(SourceIconPath);

        private BitmapSource _thumbSource = null;
        public BitmapSource ThumbSource
        {
            get
            {
                if (_thumbSource == null && IsImage)
                {
                    string target = !string.IsNullOrEmpty(ThumbPath) ? ThumbPath : ImagePath;
                    // Decode đúng 48px khớp với khung hiển thị 44x32 trên UI để tiết kiệm 75% RAM
                    _thumbSource = LoadBitmapSafe(target, 48);
                    if (_thumbSource == null && !string.IsNullOrEmpty(ImagePath))
                    {
                        _thumbSource = LoadBitmapSafe(ImagePath, 48);
                    }
                }
                return _thumbSource;
            }
        }

        private BitmapSource _imageSource = null;
        public BitmapSource ImageSource
        {
            get
            {
                if (_imageSource == null && IsImage)
                {
                    _imageSource = LoadBitmapSafe(ImagePath, 480);
                }
                return _imageSource;
            }
            set
            {
                _imageSource = value;
                OnPropertyChanged(nameof(ImageSource));
            }
        }

        public void ReleaseVisualResources()
        {
            _thumbSource = null;
            _imageSource = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
