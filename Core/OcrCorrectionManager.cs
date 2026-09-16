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
    /// Quản lý bảng ánh xạ tự động sửa lỗi OCR người dùng (ocr_user_corrections.txt).
    /// Định dạng: tu_sai||tu_dung (Mỗi quy tắc trên 1 dòng).
    /// Cho phép người dùng tùy biến và bổ sung các cặp từ để áp dụng sau khi OCR.
    /// </summary>
    public static class OcrCorrectionManager
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, string> _correctionsCache;
        private static DateTime _lastWriteTimeUtc = DateTime.MinValue;

        /// <summary>
        /// Lấy đường dẫn tệp ocruser_corrections.txt trong thư mục cấu hình (.portable)
        /// </summary>
        public static string GetConfigFilePath()
        {
            string dir = SettingsManager.GetConfigDirectory();
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }

            string primaryTxt = Path.Combine(dir, "ocruser_corrections.txt");
            string altTxt = Path.Combine(dir, "ocr_user_corrections.txt");

            // Tự động chuyển đổi nếu trước đó đã tạo file có dấu gạch dưới
            if (!File.Exists(primaryTxt) && File.Exists(altTxt))
            {
                try
                {
                    File.Move(altTxt, primaryTxt);
                }
                catch
                {
                    return altTxt;
                }
            }

            return primaryTxt;
        }

        /// <summary>
        /// Nạp danh sách sửa lỗi từ tệp .txt (tự động tạo tệp mẫu hoặc migrate từ .json cũ nếu chưa có)
        /// </summary>
        public static Dictionary<string, string> LoadCorrections(bool forceReload = false)
        {
            lock (_lock)
            {
                string txtPath = GetConfigFilePath();
                string dir = Path.GetDirectoryName(txtPath) ?? string.Empty;
                string json1 = Path.Combine(dir, "ocruser_corrections.json");
                string json2 = Path.Combine(dir, "ocr_user_corrections.json");

                // Tự động chuyển đổi từ file json cũ nếu có
                if (!File.Exists(txtPath))
                {
                    if (File.Exists(json1)) MigrateFromJsonFile(json1, txtPath);
                    else if (File.Exists(json2)) MigrateFromJsonFile(json2, txtPath);
                }

                if (!File.Exists(txtPath))
                {
                    CreateDefaultConfigFile(txtPath);
                }

                try
                {
                    DateTime currentWriteTime = File.GetLastWriteTimeUtc(txtPath);
                    if (!forceReload && _correctionsCache != null && currentWriteTime == _lastWriteTimeUtc)
                    {
                        return new Dictionary<string, string>(_correctionsCache, StringComparer.OrdinalIgnoreCase);
                    }

                    string text = File.ReadAllText(txtPath, Encoding.UTF8);
                    _correctionsCache = ParseCorrectionsText(text);
                    _lastWriteTimeUtc = currentWriteTime;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi đọc ocr_user_corrections.txt: " + ex.Message);
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
        /// Thêm hoặc cập nhật một cặp từ sửa lỗi mới vào tệp .txt
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
                    Debug.WriteLine("Lỗi ghi ocr_user_corrections.txt: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Mở tệp ocruser_corrections.txt bằng trình soạn thảo mặc định của Windows (hoặc Notepad)
        /// </summary>
        public static bool OpenConfigFile()
        {
            try
            {
                string path = GetConfigFilePath();
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string json1 = Path.Combine(dir, "ocruser_corrections.json");
                string json2 = Path.Combine(dir, "ocr_user_corrections.json");

                if (!File.Exists(path))
                {
                    if (File.Exists(json1)) MigrateFromJsonFile(json1, path);
                    else if (File.Exists(json2)) MigrateFromJsonFile(json2, path);
                }

                if (!File.Exists(path))
                {
                    CreateDefaultConfigFile(path);
                }

                // Cách 1: Mở trực tiếp bằng trình gán mặc định cho file .txt của Windows (ví dụ Notepad++, VS Code, v.v.)
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return true;
                }
                catch (Exception exShell)
                {
                    Debug.WriteLine("[OcrCorrectionManager] Lỗi mở qua ShellExecute: " + exShell.Message);
                }

                // Cách 2: Mở qua Notepad với đường dẫn hệ thống chuẩn System32
                try
                {
                    string systemNotepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
                    string notepadPath = File.Exists(systemNotepad) ? systemNotepad : "notepad.exe";
                    var psiNotepad = new ProcessStartInfo
                    {
                        FileName = notepadPath,
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psiNotepad);
                    return true;
                }
                catch (Exception exNotepad)
                {
                    Debug.WriteLine("[OcrCorrectionManager] Lỗi mở qua Notepad: " + exNotepad.Message);
                }

                // Cách 3: Mở File Explorer và chọn sẵn tệp để người dùng mở bằng bất kỳ ứng dụng nào
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                    return true;
                }
                catch { }

                return false;
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
                    { "t6ng", "tổng" },
                    { "do A1", "do AI" },
                    { "qu6c té", "quốc tế" },
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
            sb.AppendLine("# Bảng ánh xạ tự động sửa lỗi OCR người dùng");
            sb.AppendLine("# Định dạng: tu_sai||tu_dung (Mỗi quy tắc trên một dòng)");
            sb.AppendLine("# Dòng bắt đầu bằng # hoặc // là dòng chú thích");
            sb.AppendLine();

            foreach (var kvp in dict)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                sb.AppendLine($"{kvp.Key.Trim()}||{kvp.Value?.Trim() ?? string.Empty}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            _correctionsCache = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
            if (File.Exists(path))
            {
                _lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            }
        }

        private static Dictionary<string, string> ParseCorrectionsText(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return dict;

            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    int sepIdx = line.IndexOf("||", StringComparison.Ordinal);
                    if (sepIdx > 0)
                    {
                        string wrong = line.Substring(0, sepIdx).Trim();
                        string right = line.Substring(sepIdx + 2).Trim();
                        if (!string.IsNullOrEmpty(wrong))
                        {
                            dict[wrong] = right;
                        }
                    }
                }
            }

            return dict;
        }

        private static void MigrateFromJsonFile(string jsonPath, string txtPath)
        {
            try
            {
                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var dict = ParseCorrectionsJson(json);
                if (dict.Count > 0)
                {
                    // Bổ sung các ví dụ mẫu nếu chưa có
                    if (!dict.ContainsKey("t6ng")) dict["t6ng"] = "tổng";
                    if (!dict.ContainsKey("do A1")) dict["do A1"] = "do AI";
                    if (!dict.ContainsKey("qu6c té")) dict["qu6c té"] = "quốc tế";

                    SaveCorrections(dict);

                    // Xóa file json cũ sau khi chuyển đổi thành công
                    try { File.Delete(jsonPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi migrate từ json sang txt: " + ex.Message);
            }
        }

        private static Dictionary<string, string> ParseCorrectionsJson(string json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            int corrIdx = json.IndexOf("\"corrections\"", StringComparison.OrdinalIgnoreCase);
            if (corrIdx < 0) return dict;

            int braceOpen = json.IndexOf('{', corrIdx);
            if (braceOpen < 0) return dict;

            int braceClose = json.LastIndexOf('}');
            if (braceClose <= braceOpen) return dict;

            string block = json.Substring(braceOpen + 1, braceClose - braceOpen - 1);

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
