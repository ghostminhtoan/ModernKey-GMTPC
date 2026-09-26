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

        private bool _isUrl = false;
        public bool IsUrl
        {
            get => _isUrl;
            set
            {
                if (_isUrl != value)
                {
                    _isUrl = value;
                    OnPropertyChanged(nameof(IsUrl));
                    OnPropertyChanged(nameof(TypeBadge));
                }
            }
        }

        private bool _isCode = false;
        public bool IsCode
        {
            get => _isCode;
            set
            {
                if (_isCode != value)
                {
                    _isCode = value;
                    OnPropertyChanged(nameof(IsCode));
                    OnPropertyChanged(nameof(TypeBadge));
                }
            }
        }

        private int _slotNumber = 0;
        public int SlotNumber
        {
            get => _slotNumber;
            set
            {
                if (_slotNumber != value)
                {
                    _slotNumber = value;
                    OnPropertyChanged(nameof(SlotNumber));
                    OnPropertyChanged(nameof(SlotBadgeText));
                    OnPropertyChanged(nameof(SlotBadgeVisibility));
                }
            }
        }
        public string SlotBadgeText => _slotNumber >= 1 && _slotNumber <= 9 ? $"[{_slotNumber}]" : string.Empty;
        public Visibility SlotBadgeVisibility => _slotNumber >= 1 && _slotNumber <= 9 ? Visibility.Visible : Visibility.Collapsed;

        private bool _isCompactView = false;
        public bool IsCompactView
        {
            get => _isCompactView;
            set
            {
                if (_isCompactView != value)
                {
                    _isCompactView = value;
                    OnPropertyChanged(nameof(IsCompactView));
                    OnPropertyChanged(nameof(CompactItemPadding));
                    OnPropertyChanged(nameof(CompactSubInfoVisibility));
                }
            }
        }
        public Thickness CompactItemPadding => _isCompactView ? new Thickness(6, 2, 6, 2) : new Thickness(8, 6, 8, 6);
        public Visibility CompactSubInfoVisibility => _isCompactView ? Visibility.Collapsed : Visibility.Visible;

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
                if (IsColorCode) return "[COLOR]";
                if (IsUrl) return "[LINK]";
                if (IsCode) return "[CODE]";
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

        // Tính năng làm mờ riêng theo từng mục (Selective Item Blur)
        private bool _isBlurred = false;
        public bool IsBlurred
        {
            get => _isBlurred;
            set
            {
                if (_isBlurred != value)
                {
                    _isBlurred = value;
                    OnPropertyChanged(nameof(IsBlurred));
                    OnPropertyChanged(nameof(BlurBadgeVisibility));
                    OnPropertyChanged(nameof(BlurToggleText));
                }
            }
        }

        private string _blurMode = "None"; // "None", "Blur" hoặc "Pixelate"
        public string BlurMode
        {
            get => _blurMode;
            set
            {
                if (_blurMode != value)
                {
                    _blurMode = value ?? "None";
                    OnPropertyChanged(nameof(BlurMode));
                }
            }
        }

        private double _blurRadius = 0.0;
        public double BlurRadius
        {
            get => _blurRadius;
            set
            {
                if (Math.Abs(_blurRadius - value) > 0.01)
                {
                    _blurRadius = value;
                    OnPropertyChanged(nameof(BlurRadius));
                }
            }
        }

        private double _pixelateSize = 0.0;
        public double PixelateSize
        {
            get => _pixelateSize;
            set
            {
                if (Math.Abs(_pixelateSize - value) > 0.01)
                {
                    _pixelateSize = value;
                    OnPropertyChanged(nameof(PixelateSize));
                }
            }
        }

        public Visibility BlurBadgeVisibility => IsBlurred ? Visibility.Visible : Visibility.Collapsed;
        public string BlurToggleText => IsBlurred ? "Gỡ làm mờ mục này" : "Làm mờ mục này";

        // Feature 6: Dữ liệu nhạy cảm
        private bool _isSensitive = false;
        public bool IsSensitive
        {
            get => _isSensitive;
            set
            {
                if (_isSensitive != value)
                {
                    _isSensitive = value;
                    OnPropertyChanged(nameof(IsSensitive));
                    OnPropertyChanged(nameof(SensitiveBadgeVisibility));
                    OnPropertyChanged(nameof(DisplayTitle));
                    OnPropertyChanged(nameof(DisplayPreviewText));
                }
            }
        }

        private bool _isMasked = true;
        public bool IsMasked
        {
            get => _isMasked;
            set
            {
                if (_isMasked != value)
                {
                    _isMasked = value;
                    OnPropertyChanged(nameof(IsMasked));
                    OnPropertyChanged(nameof(DisplayTitle));
                    OnPropertyChanged(nameof(DisplayPreviewText));
                    OnPropertyChanged(nameof(MaskToggleIcon));
                }
            }
        }

        public string MaskToggleIcon => IsMasked ? "👁" : "🔒";
        public Visibility SensitiveBadgeVisibility => IsSensitive ? Visibility.Visible : Visibility.Collapsed;

        public string DisplayPreviewText
        {
            get
            {
                if (IsSensitive && IsMasked)
                {
                    return "•••••••••••••••• [DỮ LIỆU BẢO MẬT]";
                }
                return PreviewText;
            }
        }

        // Feature 9: Nhận diện mã màu
        private bool _isColorCode = false;
        public bool IsColorCode
        {
            get => _isColorCode;
            set
            {
                if (_isColorCode != value)
                {
                    _isColorCode = value;
                    OnPropertyChanged(nameof(IsColorCode));
                    OnPropertyChanged(nameof(ColorBadgeVisibility));
                    OnPropertyChanged(nameof(ColorSwatchBrush));
                }
            }
        }

        private string _colorHex = string.Empty;
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                if (_colorHex != value)
                {
                    _colorHex = value;
                    OnPropertyChanged(nameof(ColorHex));
                    OnPropertyChanged(nameof(ColorSwatchBrush));
                }
            }
        }

        public Visibility ColorBadgeVisibility => IsColorCode ? Visibility.Visible : Visibility.Collapsed;

        public System.Windows.Media.Brush ColorSwatchBrush
        {
            get
            {
                if (IsColorCode && !string.IsNullOrEmpty(ColorHex))
                {
                    try
                    {
                        var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ColorHex);
                        var brush = new System.Windows.Media.SolidColorBrush(col);
                        brush.Freeze();
                        return brush;
                    }
                    catch { }
                }
                return System.Windows.Media.Brushes.Transparent;
            }
        }

        public void DetectMetadata()
        {
            if (ContentType != ClipboardContentType.Text || string.IsNullOrWhiteSpace(TextContent)) return;

            string trimmed = TextContent.Trim();

            // 1. Nhận diện mã màu HEX (#FFF, #FFFFFF, 0xFFFFFF) và RGB / RGBA
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
            {
                IsColorCode = true;
                ColorHex = trimmed.Length == 4 ? $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}" : trimmed;
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^0x([0-9a-fA-F]{6})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                IsColorCode = true;
                ColorHex = "#" + trimmed.Substring(2);
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^rgba?\s*\(\s*\d+\s*,\s*\d+\s*,\s*\d+(?:\s*,\s*[0-9\.]+)?\s*\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                try
                {
                    var m = System.Text.RegularExpressions.Regex.Match(trimmed, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        int r = Math.Min(255, int.Parse(m.Groups[1].Value));
                        int g = Math.Min(255, int.Parse(m.Groups[2].Value));
                        int b = Math.Min(255, int.Parse(m.Groups[3].Value));
                        IsColorCode = true;
                        ColorHex = $"#{r:X2}{g:X2}{b:X2}";
                    }
                }
                catch { }
            }

            // 2. Nhận diện URL / Web Link
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && trimmed.Contains(".")) ||
                (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)))
            {
                IsUrl = true;
            }
            else
            {
                IsUrl = false;
            }

            // 3. Nhận diện Code Snippet (từ khóa ngôn ngữ, cú pháp ngoặc, indentation, tag HTML/XML)
            if (!IsUrl && !IsColorCode)
            {
                bool hasCodeKeywords = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b(function|class|const|let|var|def|return|public|private|protected|import|using|namespace|typeof|console\.log|printf|SELECT|FROM|WHERE|INSERT|UPDATE|DELETE)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                bool hasBracketsOrTags = (trimmed.Contains("{") && trimmed.Contains("}")) || (trimmed.Contains("</") || trimmed.Contains("/>")) || (trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
                bool isMultiLine = trimmed.IndexOf('\n') >= 0;

                if (hasCodeKeywords || (hasBracketsOrTags && (isMultiLine || trimmed.Length > 20)))
                {
                    IsCode = true;
                }
                else
                {
                    IsCode = false;
                }
            }
            else
            {
                IsCode = false;
            }

            // Tắt tính năng tự động mask dữ liệu (Feature 6 cũ đã bỏ)
            IsSensitive = false;
        }

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
                BackgroundColorHex = this.BackgroundColorHex,
                IsBlurred = this.IsBlurred,
                BlurMode = this.BlurMode,
                BlurRadius = this.BlurRadius,
                PixelateSize = this.PixelateSize,
                IsUrl = this.IsUrl,
                IsCode = this.IsCode,
                SlotNumber = this.SlotNumber,
                IsCompactView = this.IsCompactView
            };
        }

        /// <summary>
        /// Lấy đường dẫn file ảnh gốc chất lượng cao (KHÔNG CÓ hậu tố _t.png).
        /// Tự động tìm kiếm file .png tương ứng trong cache/thư mục lưu trữ.
        /// </summary>
        public static string GetFullImagePath(ClipboardItem item)
        {
            if (item == null) return string.Empty;

            // 1. Thử lấy từ ImagePath trước
            if (!string.IsNullOrWhiteSpace(item.ImagePath))
            {
                string path = GetFullImagePath(item.ImagePath);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }

            // 2. Thử lấy từ ThumbPath nếu ImagePath không hợp lệ hoặc thiếu
            if (!string.IsNullOrWhiteSpace(item.ThumbPath))
            {
                string path = GetFullImagePath(item.ThumbPath);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }

            return string.Empty;
        }

        /// <summary>
        /// Chuyển đổi đường dẫn ảnh bất kỳ (kể cả có _t.png) thành đường dẫn ảnh gốc không có _t.png
        /// </summary>
        public static string GetFullImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Replace("\r", "r").Replace("\n", "n").Trim();

            // 1. Tạo candidate không có _t.png
            string nonThumbCandidate = path;
            if (nonThumbCandidate.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase))
            {
                nonThumbCandidate = nonThumbCandidate.Substring(0, nonThumbCandidate.Length - 6) + ".png";
            }
            else if (nonThumbCandidate.EndsWith("_t.jpg", StringComparison.OrdinalIgnoreCase))
            {
                nonThumbCandidate = nonThumbCandidate.Substring(0, nonThumbCandidate.Length - 6) + ".jpg";
            }
            else if (nonThumbCandidate.EndsWith("_t.jpeg", StringComparison.OrdinalIgnoreCase))
            {
                nonThumbCandidate = nonThumbCandidate.Substring(0, nonThumbCandidate.Length - 7) + ".jpeg";
            }

            // Thử resolve nonThumbCandidate trước
            string resolvedNonThumb = ResolvePath(nonThumbCandidate);
            if (!string.IsNullOrEmpty(resolvedNonThumb) && File.Exists(resolvedNonThumb))
            {
                return resolvedNonThumb;
            }

            // 2. Thử resolve đường dẫn gốc ban đầu
            string resolvedOrig = ResolvePath(path);
            if (!string.IsNullOrEmpty(resolvedOrig) && File.Exists(resolvedOrig))
            {
                // Nếu file resolved vẫn có _t.png, kiểm tra xem file bỏ _t.png có cạnh nó không
                if (resolvedOrig.EndsWith("_t.png", StringComparison.OrdinalIgnoreCase))
                {
                    string siblingNonThumb = resolvedOrig.Substring(0, resolvedOrig.Length - 6) + ".png";
                    if (File.Exists(siblingNonThumb))
                    {
                        return siblingNonThumb;
                    }
                }
                else if (resolvedOrig.EndsWith("_t.jpg", StringComparison.OrdinalIgnoreCase))
                {
                    string siblingNonThumb = resolvedOrig.Substring(0, resolvedOrig.Length - 6) + ".jpg";
                    if (File.Exists(siblingNonThumb))
                    {
                        return siblingNonThumb;
                    }
                }
                return resolvedOrig;
            }

            return resolvedNonThumb ?? resolvedOrig ?? path;
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace("\r", "r").Replace("\n", "n").Trim();
            if (File.Exists(path) || Directory.Exists(path)) return path;

            try
            {
                string fn = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fn))
                {
                    string cfgDir = ModernKey.Config.SettingsManager.GetConfigDirectory();

                    // 1. Thư mục chuẩn mới: <ConfigDir>\clipboard\clipboard_cache\
                    string newCacheDir = Path.Combine(cfgDir, "clipboard", "clipboard_cache");
                    string candHist = Path.Combine(newCacheDir, "history", fn);
                    if (File.Exists(candHist)) return candHist;

                    string candFav = Path.Combine(newCacheDir, "favorites", fn);
                    if (File.Exists(candFav)) return candFav;

                    string candNew = Path.Combine(newCacheDir, fn);
                    if (File.Exists(candNew)) return candNew;

                    string candNewIcon = Path.Combine(newCacheDir, "icons", fn);
                    if (File.Exists(candNewIcon)) return candNewIcon;

                    // 2. Dự phòng thư mục cũ nếu chưa chuyển: <ConfigDir>\clipboard_cache\
                    string oldCacheDir = Path.Combine(cfgDir, "clipboard_cache");
                    string candOldHist = Path.Combine(oldCacheDir, "history", fn);
                    if (File.Exists(candOldHist)) return candOldHist;

                    string candOldFav = Path.Combine(oldCacheDir, "favorites", fn);
                    if (File.Exists(candOldFav)) return candOldFav;

                    string candOld = Path.Combine(oldCacheDir, fn);
                    if (File.Exists(candOld)) return candOld;

                    string candOldIcon = Path.Combine(oldCacheDir, "icons", fn);
                    if (File.Exists(candOldIcon)) return candOldIcon;

                    // 3. Thư mục .portable tại AppDomain.CurrentDomain.BaseDirectory
                    string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(baseAppDir))
                    {
                        string candBaseHist = Path.Combine(baseAppDir, ".portable", "clipboard", "clipboard_cache", "history", fn);
                        if (File.Exists(candBaseHist)) return candBaseHist;

                        string candBaseFav = Path.Combine(baseAppDir, ".portable", "clipboard", "clipboard_cache", "favorites", fn);
                        if (File.Exists(candBaseFav)) return candBaseFav;

                        string candBaseOldHist = Path.Combine(baseAppDir, ".portable", "clipboard_cache", "history", fn);
                        if (File.Exists(candBaseOldHist)) return candBaseOldHist;

                        string candBaseOldFav = Path.Combine(baseAppDir, ".portable", "clipboard_cache", "favorites", fn);
                        if (File.Exists(candBaseOldFav)) return candBaseOldFav;
                    }
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
                    string fullPath = GetFullImagePath(this);
                    string target = !string.IsNullOrEmpty(fullPath) ? fullPath : ImagePath;
                    _imageSource = LoadBitmapSafe(target, 480);
                }
                return _imageSource;
            }
            set
            {
                _imageSource = value;
                OnPropertyChanged(nameof(ImageSource));
            }
        }

        private BitmapSource _fullImageSource = null;
        public BitmapSource FullImageSource
        {
            get
            {
                if (_fullImageSource == null && IsImage)
                {
                    string fullPath = GetFullImagePath(this);
                    if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                    {
                        _fullImageSource = LoadBitmapSafe(fullPath, 0); // 0 = Full original resolution
                    }
                    if (_fullImageSource == null && !string.IsNullOrEmpty(ImagePath))
                    {
                        _fullImageSource = LoadBitmapSafe(ImagePath, 0);
                    }
                }
                return _fullImageSource ?? ImageSource;
            }
            set
            {
                _fullImageSource = value;
                OnPropertyChanged(nameof(FullImageSource));
            }
        }

        public void ReleaseVisualResources()
        {
            _thumbSource = null;
            _imageSource = null;
            _fullImageSource = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
