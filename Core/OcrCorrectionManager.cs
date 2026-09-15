using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ModernKey.Config;

namespace ModernKey.Core
{
    /// <summary>
    /// Quản lý bảng ánh xạ tự động sửa lỗi OCR người dùng (ocr_user_corrections.json).
    /// Cho phép người dùng tùy biến và bổ sung các cặp từ [từ_sai -> từ_đúng] để áp dụng sau khi OCR.
    /// </summary>
    public static class OcrCorrectionManager
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, string> _correctionsCache;
        private static DateTime _lastWriteTimeUtc = DateTime.MinValue;

        /// <summary>
        /// Lấy đường dẫn tệp ocr_user_corrections.json trong thư mục cấu hình (.portable hoặc Roaming)
        /// </summary>
        public static string GetConfigFilePath()
        {
            string dir = SettingsManager.GetConfigDirectory();
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return Path.Combine(dir, "ocr_user_corrections.json");
        }

        /// <summary>
        /// Nạp danh sách sửa lỗi từ tệp JSON (tự động tạo tệp mẫu nếu chưa có)
        /// </summary>
        public static Dictionary<string, string> LoadCorrections(bool forceReload = false)
        {
            lock (_lock)
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    CreateDefaultConfigFile(path);
                }

                try
                {
                    DateTime currentWriteTime = File.GetLastWriteTimeUtc(path);
                    if (!forceReload && _correctionsCache != null && currentWriteTime == _lastWriteTimeUtc)
                    {
                        return new Dictionary<string, string>(_correctionsCache, StringComparer.OrdinalIgnoreCase);
                    }

                    string json = File.ReadAllText(path, Encoding.UTF8);
                    _correctionsCache = ParseCorrectionsJson(json);
                    _lastWriteTimeUtc = currentWriteTime;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi đọc ocr_user_corrections.json: " + ex.Message);
                    if (_correctionsCache == null)
                    {
                        _correctionsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                return new Dictionary<string, string>(_correctionsCache, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Áp dụng các quy tắc sửa lỗi người dùng lên văn bản kết quả OCR
        /// </summary>
        public static string ApplyCorrections(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var corrections = LoadCorrections();
            if (corrections == null || corrections.Count == 0) return text;

            string result = text;
            foreach (var kvp in corrections)
            {
                string wrong = kvp.Key?.Trim();
                string right = kvp.Value;
                if (string.IsNullOrEmpty(wrong) || right == null) continue;

                // Nếu cụm từ chứa khoảng trắng -> Thay thế trực tiếp không phân biệt hoa thường
                if (wrong.Contains(" "))
                {
                    string pattern = Regex.Escape(wrong);
                    result = Regex.Replace(result, pattern, right, RegexOptions.IgnoreCase);
                }
                else
                {
                    // Nếu là từ đơn -> Thay thế với ranh giới từ (\b) để tránh thay nhầm từ con
                    string pattern = $@"\b{Regex.Escape(wrong)}\b";
                    result = Regex.Replace(result, pattern, right, RegexOptions.IgnoreCase);
                }
            }

            return result;
        }

        /// <summary>
        /// Thêm hoặc cập nhật một cặp từ sửa lỗi mới vào tệp JSON
        /// </summary>
        public static bool AddOrUpdateCorrection(string wrongWord, string rightWord)
        {
            if (string.IsNullOrWhiteSpace(wrongWord) || rightWord == null) return false;

            lock (_lock)
            {
                try
                {
                    var dict = LoadCorrections(true);
                    dict[wrongWord.Trim()] = rightWord.Trim();

                    SaveCorrections(dict);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi ghi ocr_user_corrections.json: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Mở tệp ocr_user_corrections.json bằng Notepad hoặc trình soạn thảo mặc định của Windows
        /// </summary>
        public static bool OpenConfigFile()
        {
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    CreateDefaultConfigFile(path);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi mở file cấu hình: " + ex.Message);
                return false;
            }
        }

        private static void CreateDefaultConfigFile(string path)
        {
            try
            {
                var defaultDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ModemKey", "ModernKey" },
                    { "tiẽng", "tiếng" },
                    { "thực hiên", "thực hiện" },
                    { "tai san", "tải sẵn" },
                    { "chay nhe", "chạy nhẹ" },
                    { "ban quyen", "bản quyền" },
                    { "cudi", "cười" },
                    { "chum ycongnghe", "chùm ycongnghe" },
                    { "thq sta 6ng nudc", "thợ sửa ống nước" },
                    { "thq sta", "thợ sửa" },
                    { "sta 6ng", "sửa ống" },
                    { "6ng nudc", "ống nước" },
                    { "scra 6ng", "sửa ống" },
                    { "nt_rdc", "nước" }
                };
                SaveCorrections(defaultDict);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi tạo tệp mặc định: " + ex.Message);
            }
        }

        private static void SaveCorrections(Dictionary<string, string> dict)
        {
            string path = GetConfigFilePath();
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine("  \"description\": \"Bảng ánh xạ tự động sửa lỗi OCR người dùng (Từ sai -> Từ đúng). Chỉnh sửa tệp này bằng bất kỳ trình soạn thảo nào.\",");
            sb.AppendLine("  \"corrections\": {");

            int index = 0;
            int count = dict.Count;
            foreach (var kvp in dict)
            {
                index++;
                string comma = (index < count) ? "," : "";
                string safeKey = EscapeJsonString(kvp.Key);
                string safeVal = EscapeJsonString(kvp.Value);
                sb.AppendLine($"    \"{safeKey}\": \"{safeVal}\"{comma}");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _correctionsCache = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
            if (File.Exists(path))
            {
                _lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            }
        }

        private static Dictionary<string, string> ParseCorrectionsJson(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            // Tìm khối "corrections": { ... }
            int corrIdx = json.IndexOf("\"corrections\"", StringComparison.OrdinalIgnoreCase);
            if (corrIdx < 0) return dict;

            int braceOpen = json.IndexOf('{', corrIdx);
            if (braceOpen < 0) return dict;

            int braceClose = json.LastIndexOf('}');
            if (braceClose <= braceOpen) return dict;

            string block = json.Substring(braceOpen + 1, braceClose - braceOpen - 1);

            // Phân tích các cặp "key": "value"
            var regex = new Regex("\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\"");
            var matches = regex.Matches(block);
            foreach (Match m in matches)
            {
                if (m.Groups.Count >= 3)
                {
                    string k = UnescapeJsonString(m.Groups[1].Value);
                    string v = UnescapeJsonString(m.Groups[2].Value);
                    if (!string.IsNullOrEmpty(k))
                    {
                        dict[k] = v;
                    }
                }
            }

            return dict;
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        private static string UnescapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\r", "\r")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t");
        }
    }
}
