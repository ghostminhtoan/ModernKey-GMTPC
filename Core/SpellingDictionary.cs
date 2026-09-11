using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ModernKey.Config;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ từ điển chính tả tiếng Việt siêu nhẹ nạp từ vi_VN.dic (59,547 từ)
    /// Tra cứu O(1) qua HashSet không phân biệt hoa thường.
    /// </summary>
    public sealed class SpellingDictionary
    {
        private static readonly Lazy<SpellingDictionary> _lazyInstance =
            new Lazy<SpellingDictionary>(() => new SpellingDictionary());

        public static SpellingDictionary Instance => _lazyInstance.Value;

        private readonly HashSet<string> _words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private bool _isLoaded = false;

        public bool IsLoaded => _isLoaded;
        public int WordCount => _words.Count;

        private SpellingDictionary()
        {
            // Tải lười theo nhu cầu (On-demand Lazy Load) để tiết kiệm ~10MB RAM và I/O khi khởi động
        }

        private void LoadFromReader(TextReader reader)
        {
            string firstLine = reader.ReadLine(); // Bỏ qua dòng số lượng từ (59547)
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    _words.Add(trimmed);
                }
            }
        }

        public void LoadDictionary()
        {
            lock (_lock)
            {
                if (_isLoaded) return;

                try
                {
                    string dicPath = null;
                    string configDir = SettingsManager.GetConfigDirectory();
                    string appDir = SettingsManager.GetAppDirectory();

                    string[] candidatePaths = new[]
                    {
                        Path.Combine(configDir, "vi_VN.dic"),
                        Path.Combine(configDir, "Config", "vi_VN.dic"),
                        Path.Combine(appDir, "Config", "vi_VN.dic"),
                        Path.Combine(appDir, "vi_VN.dic"),
                        Path.Combine(Directory.GetCurrentDirectory(), "Config", "vi_VN.dic"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "vi_VN.dic")
                    };

                    foreach (var path in candidatePaths)
                    {
                        if (File.Exists(path))
                        {
                            dicPath = path;
                            break;
                        }
                    }

                    if (dicPath != null && File.Exists(dicPath))
                    {
                        using (var reader = new StreamReader(dicPath, Encoding.UTF8))
                        {
                            LoadFromReader(reader);
                        }
                    }
                    else
                    {
                        // Đọc từ EmbeddedResource nếu chạy file standalone độc lập
                        var asm = typeof(SpellingDictionary).Assembly;
                        using (var stream = asm.GetManifestResourceStream("ModernKey.Config.vi_VN.dic"))
                        {
                            if (stream != null)
                            {
                                using (var reader = new StreamReader(stream, Encoding.UTF8))
                                {
                                    LoadFromReader(reader);
                                }
                            }
                        }
                    }

                    _isLoaded = true;
                }
                catch
                {
                    // Fallback an toàn nếu có lỗi đọc file
                    _isLoaded = true;
                }
            }
        }

        /// <summary>
        /// Kiểm tra từ tiếng Việt có hợp lệ trong từ điển không.
        /// </summary>
        public bool IsValidWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return true;

            // Từ 1 ký tự luôn hợp lệ
            if (word.Length == 1) return true;

            // Bỏ qua kiểm tra nếu từ chứa số hoặc ký tự không phải chữ
            foreach (char c in word)
            {
                if (!char.IsLetter(c)) return true;
            }

            if (!_isLoaded)
            {
                LoadDictionary();
            }

            if (_words.Count == 0) return true;

            return _words.Contains(word);
        }
    }
}
