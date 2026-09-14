using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ModernKey.Core
{
    /// <summary>
    /// Quản lý từ điển sửa lỗi chính tả tiếng Anh thông dụng (pót:post, cáe:case...).
    /// Tự động bung file spelling_correction.txt (UTF-8 without BOM) cùng thư mục với file exe.
    /// </summary>
    public class SpellingCorrectionManager
    {
        private static readonly Lazy<SpellingCorrectionManager> _instance =
            new Lazy<SpellingCorrectionManager>(() => new SpellingCorrectionManager());

        public static SpellingCorrectionManager Instance => _instance.Value;

        private const string DictionaryFileName = "spelling_correction.txt";
        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private FileSystemWatcher _fileWatcher;
        private string _dictionaryFilePath;

        public const string DefaultContent =
@"pót:post
cáe:case
pát:past
clóe:close
clúe:cluse
ríe:rise
róe:rose
wíe:wise
fêt:feet
mêt:meet
nêd:need
sêd:seed
kêp:keep
dêp:deep
wêk:week
fêd:feed
bôk:book
lôk:look
côk:cook
gôd:good
tôl:tool
tôt:toot
rôt:root
shơ:show
dơn:down
tơn:town";

        public SpellingCorrectionManager()
        {
            InitializeDictionary();
        }

        public string GetDictionaryFilePath()
        {
            if (string.IsNullOrEmpty(_dictionaryFilePath))
            {
                string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
                {
                    baseDir = AppDomain.CurrentDomain.BaseDirectory;
                }
                _dictionaryFilePath = Path.Combine(baseDir, DictionaryFileName);
            }
            return _dictionaryFilePath;
        }

        public void InitializeDictionary()
        {
            try
            {
                string filePath = GetDictionaryFilePath();

                // 1. Nếu file chưa tồn tại trong cùng thư mục với exe, tự động tạo file từ điển mặc định (UTF-8 without BOM)
                if (!File.Exists(filePath))
                {
                    var utf8WithoutBom = new UTF8Encoding(false);
                    File.WriteAllText(filePath, DefaultContent, utf8WithoutBom);
                }

                // 2. Nạp nội dung từ điển từ file
                LoadFromFile(filePath);

                // 3. Khởi tạo FileSystemWatcher để tự động reload khi người dùng chỉnh sửa file
                SetupWatcher(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpellingCorrectionManager] Error initializing: " + ex.Message);
                // Nạp từ điển mặc định trong bộ nhớ nếu không thể thao tác file
                LoadFromString(DefaultContent);
            }
        }

        private void SetupWatcher(string filePath)
        {
            try
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }

                string dir = Path.GetDirectoryName(filePath);
                if (Directory.Exists(dir))
                {
                    _fileWatcher = new FileSystemWatcher(dir, DictionaryFileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };

                    DateTime lastTrigger = DateTime.MinValue;
                    _fileWatcher.Changed += (s, e) =>
                    {
                        if ((DateTime.Now - lastTrigger).TotalMilliseconds > 500)
                        {
                            lastTrigger = DateTime.Now;
                            LoadFromFile(filePath);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpellingCorrectionManager] Watcher error: " + ex.Message);
            }
        }

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                            continue;

                        int colonIdx = line.IndexOf(':');
                        if (colonIdx > 0 && colonIdx < line.Length - 1)
                        {
                            string wrong = line.Substring(0, colonIdx).Trim();
                            string correct = line.Substring(colonIdx + 1).Trim();
                            if (!string.IsNullOrEmpty(wrong) && !string.IsNullOrEmpty(correct))
                            {
                                dict[wrong] = correct;
                            }
                        }
                    }
                }

                lock (_lock)
                {
                    _corrections.Clear();
                    foreach (var kvp in dict)
                    {
                        _corrections[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpellingCorrectionManager] Error reading file: " + ex.Message);
            }
        }

        public void LoadFromString(string content)
        {
            if (string.IsNullOrEmpty(content)) return;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    int colonIdx = line.IndexOf(':');
                    if (colonIdx > 0 && colonIdx < line.Length - 1)
                    {
                        string wrong = line.Substring(0, colonIdx).Trim();
                        string correct = line.Substring(colonIdx + 1).Trim();
                        if (!string.IsNullOrEmpty(wrong) && !string.IsNullOrEmpty(correct))
                        {
                            dict[wrong] = correct;
                        }
                    }
                }
            }

            lock (_lock)
            {
                _corrections.Clear();
                foreach (var kvp in dict)
                {
                    _corrections[kvp.Key] = kvp.Value;
                }
            }
        }

        public bool TryCorrect(string word, out string replacement)
        {
            replacement = null;
            if (string.IsNullOrEmpty(word)) return false;

            string target;
            lock (_lock)
            {
                if (!_corrections.TryGetValue(word, out target))
                {
                    return false;
                }
            }

            // Bảo toàn casing tương thích với từ người dùng vừa gõ
            replacement = PreserveCasing(word, target);
            return true;
        }

        private static string PreserveCasing(string original, string target)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(target))
                return target;

            bool allUpper = true;
            bool firstUpper = char.IsUpper(original[0]);

            for (int i = 0; i < original.Length; i++)
            {
                if (char.IsLetter(original[i]) && !char.IsUpper(original[i]))
                {
                    allUpper = false;
                    break;
                }
            }

            if (allUpper && original.Length > 1)
            {
                return target.ToUpper();
            }

            if (firstUpper)
            {
                if (target.Length == 1) return target.ToUpper();
                return char.ToUpper(target[0]) + target.Substring(1);
            }

            return target.ToLower();
        }

        public void OpenDictionaryFile()
        {
            try
            {
                string filePath = GetDictionaryFilePath();
                if (!File.Exists(filePath))
                {
                    var utf8WithoutBom = new UTF8Encoding(false);
                    File.WriteAllText(filePath, DefaultContent, utf8WithoutBom);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpellingCorrectionManager] Cannot open file: " + ex.Message);
            }
        }
    }
}
