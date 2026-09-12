using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModernKey.Config;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ từ điển chính tả tiếng Việt & tiếng Anh nạp từ vi_VN.dic và en_US.dic.
    /// Tra cứu O(1) qua HashSet không phân biệt hoa thường.
    /// Hỗ trợ nạp file tùy biến (.dic custom) để ngay cạnh file exe hoặc trong thư mục Config.
    /// Tự động làm sạch định dạng Hunspell (cắt bỏ /affix flags và \t metadata).
    /// </summary>
    public sealed class SpellingDictionary
    {
        private static readonly Lazy<SpellingDictionary> _lazyInstance =
            new Lazy<SpellingDictionary>(() => new SpellingDictionary());

        public static SpellingDictionary Instance => _lazyInstance.Value;

        private readonly HashSet<string> _vietnameseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _englishWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _allWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();
        private bool _isLoaded = false;

        public bool IsLoaded => _isLoaded;
        public int WordCount => _allWords.Count;
        public int VietnameseWordCount => _vietnameseWords.Count;
        public int EnglishWordCount => _englishWords.Count;

        private SpellingDictionary()
        {
            // Tải lười theo nhu cầu (On-demand Lazy Load)
        }

        public void ReloadDictionaries()
        {
            lock (_lock)
            {
                _isLoaded = false;
                _vietnameseWords.Clear();
                _englishWords.Clear();
                _allWords.Clear();
                LoadDictionary();
            }
        }

        public void LoadDictionary()
        {
            lock (_lock)
            {
                if (_isLoaded) return;

                try
                {
                    // 1. Nạp từ điển tiếng Việt: vi_VN.dic
                    LoadSingleDictionary("vi_VN.dic", _vietnameseWords);

                    // 2. Nạp từ điển tiếng Anh: en_US.dic
                    LoadSingleDictionary("en_US.dic", _englishWords);

                    // 3. Hợp nhất vào _allWords để tra cứu O(1) siêu tốc
                    _allWords.UnionWith(_vietnameseWords);
                    _allWords.UnionWith(_englishWords);

                    _isLoaded = true;
                }
                catch
                {
                    // Fallback an toàn nếu có lỗi
                    _isLoaded = true;
                }
            }
        }

        private void LoadSingleDictionary(string fileName, HashSet<string> targetSet)
        {
            string cfgDir = SettingsManager.GetConfigDirectory();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string dicPath = null;
            string[] candidatePaths = new[]
            {
                // Ưu tiên 1: Cạnh file exe
                Path.Combine(baseDir, fileName),
                // Ưu tiên 2: Trong thư mục Config cạnh exe
                Path.Combine(baseDir, "Config", fileName),
                // Ưu tiên 3: Trong thư mục AppData
                Path.Combine(cfgDir, fileName),
                Path.Combine(cfgDir, "Config", fileName),
                // Ưu tiên 4: Thư mục làm việc hiện tại
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Config", fileName)
            };

            foreach (var p in candidatePaths)
            {
                if (File.Exists(p))
                {
                    dicPath = p;
                    break;
                }
            }

            Stream stream = null;
            if (dicPath != null)
            {
                try
                {
                    stream = new FileStream(dicPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch
                {
                    stream = null;
                }
            }

            if (stream == null)
            {
                // File exe Standalone hoàn toàn: nạp trực tiếp từ Embedded Resource trong assembly
                stream = typeof(SpellingDictionary).Assembly.GetManifestResourceStream("ModernKey.Config." + fileName);
            }

            if (stream != null)
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    ParseDictionaryStream(reader, targetSet);
                }
            }
        }

        private void ParseDictionaryStream(StreamReader reader, HashSet<string> targetSet)
        {
            string firstLine = reader.ReadLine();
            if (firstLine != null)
            {
                string firstTrimmed = firstLine.Trim();
                // Nếu dòng đầu là số đếm (ví dụ 6657 hoặc 50688) thì bỏ qua
                if (!int.TryParse(firstTrimmed, out _))
                {
                    AddCleanWord(firstTrimmed, targetSet);
                }
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                AddCleanWord(line, targetSet);
            }
        }

        private void AddCleanWord(string line, HashSet<string> targetSet)
        {
            if (string.IsNullOrEmpty(line)) return;

            string trimmed = line.Trim();
            if (trimmed.Length == 0) return;

            // Bỏ qua comment
            if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) return;

            // Cắt bỏ phần tab metadata
            int tabIdx = trimmed.IndexOf('\t');
            if (tabIdx >= 0)
            {
                trimmed = trimmed.Substring(0, tabIdx).Trim();
            }

            // Cắt bỏ phần /affix flags của Hunspell
            int slashIdx = trimmed.IndexOf('/');
            if (slashIdx >= 0)
            {
                trimmed = trimmed.Substring(0, slashIdx).Trim();
            }

            if (trimmed.Length > 0)
            {
                targetSet.Add(trimmed);

                // Nếu có đuôi 's (sở hữu cách) thì thêm cả từ gốc
                if (trimmed.EndsWith("'s", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
                {
                    string baseWord = trimmed.Substring(0, trimmed.Length - 2);
                    if (baseWord.Length > 0)
                    {
                        targetSet.Add(baseWord);
                    }
                }
            }
        }

        /// <summary>
        /// Kiểm tra từ có phải là từ tiếng Việt hợp lệ trong vi_VN.dic không.
        /// </summary>
        public bool IsValidVietnameseWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return true;
            if (word.Length == 1) return true;

            EnsureLoaded();
            return _vietnameseWords.Contains(word);
        }

        /// <summary>
        /// Kiểm tra từ có phải là từ tiếng Anh hợp lệ trong en_US.dic không.
        /// </summary>
        public bool IsEnglishWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            EnsureLoaded();
            return _englishWords.Contains(word);
        }

        /// <summary>
        /// Kiểm tra từ có hợp lệ trong từ điển (tiếng Việt HOẶC tiếng Anh) không.
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

            EnsureLoaded();

            if (_allWords.Count == 0) return true;

            return _allWords.Contains(word);
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                LoadDictionary();
            }
        }
    }
}
