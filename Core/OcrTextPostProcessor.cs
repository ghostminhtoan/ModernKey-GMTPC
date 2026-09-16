using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ hậu xử lý văn bản OCR chuyên biệt cho ngữ cảnh Mạng xã hội, Video và Phim ảnh:
    /// - Nối dòng thông minh cho phụ đề video (Subtitle Auto-unbreak): Ghép câu liền mạch khi bị ngắt ngẫu nhiên bởi khung hình video.
    /// - Chuẩn hóa chữ hoa (All-caps / Title case): Làm mềm các tiêu đề thumbnail YouTube giật tít viết hoa toàn bộ.
    /// - Lọc trùng lặp phụ đề (Deduplication): Loại bỏ các dòng thoại trùng lặp khi chụp liên tục nhiều frame video.
    /// </summary>
    public static class OcrTextPostProcessor
    {
        public static string Process(string rawText, OcrPreset preset, bool preserveLineBreaks)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            string text = rawText.Trim();

            switch (preset)
            {
                case OcrPreset.Subtitle:
                    return ProcessSubtitle(text, preserveLineBreaks);

                case OcrPreset.Thumbnail:
                    return ProcessThumbnail(text, preserveLineBreaks);

                case OcrPreset.SocialPost:
                    return ProcessSocialPost(text, preserveLineBreaks);

                case OcrPreset.Auto:
                default:
                    return text;
            }
        }

        /// <summary>
        /// Xử lý chuyên sâu cho phụ đề Video / Phim:
        /// - Lọc trùng lặp các dòng phụ đề liền kề.
        /// - Ghép các dòng bị ngắt vụn thành câu hoàn chỉnh nếu preserveLineBreaks = false.
        /// </summary>
        private static string ProcessSubtitle(string text, bool preserveLineBreaks)
        {
            var lines = SplitLines(text);
            if (lines.Count == 0) return string.Empty;

            // 1. Khử trùng lặp dòng liền kề (do chụp nhiều frame của cùng 1 subtitle)
            var dedupLines = DeduplicateAdjacentLines(lines);

            if (preserveLineBreaks)
            {
                return string.Join(Environment.NewLine, dedupLines);
            }

            // 2. Nối dòng thông minh (Smart Unbreak)
            var sb = new StringBuilder();
            for (int i = 0; i < dedupLines.Count; i++)
            {
                string line = dedupLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (sb.Length == 0)
                {
                    sb.Append(line);
                    continue;
                }

                // Nếu dòng mới bắt đầu bằng dấu gạch đầu dòng (thoại của 2 nhân vật khác nhau) -> Xuống dòng
                if (line.StartsWith("-") || line.StartsWith("–") || line.StartsWith("—"))
                {
                    sb.AppendLine();
                    sb.Append(line);
                    continue;
                }

                // Kiểm tra xem dòng trước có kết thúc bằng dấu chấm ngắt câu hay không
                string currentContent = sb.ToString().TrimEnd();
                char lastChar = currentContent.Length > 0 ? currentContent[currentContent.Length - 1] : '\0';

                bool isSentenceEnd = lastChar == '.' || lastChar == '!' || lastChar == '?' || lastChar == ':' || currentContent.EndsWith("...");

                if (isSentenceEnd)
                {
                    sb.AppendLine();
                    sb.Append(line);
                }
                else
                {
                    // Nối tiếp câu (cách nhau 1 khoảng trắng)
                    sb.Append(" ");
                    sb.Append(line);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Xử lý chuyên sâu cho Thumbnail / Tiêu đề YouTube:
        /// - Chuẩn hóa các tiêu đề viết hoa toàn bộ (ALL-CAPS) về dạng Title Case tự nhiên.
        /// </summary>
        private static string ProcessThumbnail(string text, bool preserveLineBreaks)
        {
            var lines = SplitLines(text);
            if (lines.Count == 0) return string.Empty;

            var resultLines = new List<string>();
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Nếu dòng chữ in hoa toàn bộ và có độ dài trên 8 ký tự -> Chuẩn hóa Title Case
                if (IsAllUpper(line) && line.Length > 8)
                {
                    line = ToTitleCaseVietnamese(line);
                }

                resultLines.Add(line);
            }

            return string.Join(preserveLineBreaks ? Environment.NewLine : " ", resultLines);
        }

        /// <summary>
        /// Xử lý bài đăng mạng xã hội / Meme
        /// </summary>
        private static string ProcessSocialPost(string text, bool preserveLineBreaks)
        {
            // Bảo toàn định dạng bài viết hoặc chuẩn hóa ngắt đoạn hợp lý
            var lines = SplitLines(text);
            return string.Join(preserveLineBreaks ? Environment.NewLine : " ", lines);
        }

        private static List<string> SplitLines(string text)
        {
            var list = new List<string>();
            using (var reader = new StringReader(text))
            {
                string l;
                while ((l = reader.ReadLine()) != null)
                {
                    string trimmed = l.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        list.Add(trimmed);
                    }
                }
            }
            return list;
        }

        private static List<string> DeduplicateAdjacentLines(List<string> lines)
        {
            var result = new List<string>();
            string prev = null;

            foreach (var line in lines)
            {
                if (prev == null || !string.Equals(line, prev, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(line);
                    prev = line;
                }
            }

            return result;
        }

        private static bool IsAllUpper(string s)
        {
            int letterCount = 0;
            int upperCount = 0;

            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    letterCount++;
                    if (char.IsUpper(c)) upperCount++;
                }
            }

            return letterCount >= 4 && letterCount == upperCount;
        }

        private static string ToTitleCaseVietnamese(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            // Chuyển toàn bộ về chữ thường trước
            string lower = s.ToLower(new CultureInfo("vi-VN"));

            // Viết hoa chữ cái đầu mỗi từ
            var words = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length > 0)
                {
                    words[i] = char.ToUpper(w[0], new CultureInfo("vi-VN")) + (w.Length > 1 ? w.Substring(1) : string.Empty);
                }
            }

            return string.Join(" ", words);
        }
    }
}
