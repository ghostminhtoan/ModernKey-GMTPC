using System;
using System.Collections.Generic;
using System.Text;

namespace ModernKey.Core
{
    public static class CharsetConverter
    {
        private static readonly string[] UnicodeChars = new string[]
        {
            "à","á","ả","ã","ạ","â","ầ","ấ","ẩ","ẫ","ậ","ă","ằ","ắ","ẳ","ẵ","ặ",
            "è","é","ẻ","ẽ","ẹ","ê","ề","ế","ể","ễ","ệ",
            "ì","í","ỉ","ĩ","ị",
            "ò","ó","ỏ","õ","ọ","ô","ồ","ố","ổ","ỗ","ộ","ơ","ờ","ớ","ở","ỡ","ợ",
            "ù","ú","ủ","ũ","ụ","ư","ừ","ứ","ử","ữ","ự",
            "ỳ","ý","ỷ","ỹ","ỵ",
            "đ",
            "À","Á","Ả","Ã","Ạ","Â","Ầ","Ấ","Ẩ","Ẫ","Ậ","Ă","Ằ","Ắ","Ẳ","Ẵ","Ặ",
            "È","É","Ẻ","Ẽ","Ẹ","Ê","Ề","Ế","Ể","Ễ","Ệ",
            "Ì","Í","Ỉ","Ĩ","Ị",
            "Ò","Ó","Ỏ","Õ","Ọ","Ô","Ồ","Ố","Ổ","Ỗ","Ộ","Ơ","Ờ","Ớ","Ở","Ỡ","Ợ",
            "Ù","Ú","Ủ","Ũ","Ụ","Ư","Ừ","Ứ","Ử","Ữ","Ự",
            "Ỳ","Ý","Ỷ","Ỹ","Ỵ",
            "Đ"
        };

        private static readonly string[] Tcvn3Chars = new string[]
        {
            "\u00B5","\u00B8","\u00B6","\u00B7","\u00B9","\u00A2","\u00C7","\u00CA","\u00C8","\u00C9","\u00CB","\u00A1","\u00BB","\u00BE","\u00BC","\u00BD","\u00C6",
            "\u00CC","\u00CE","\u00CD","\u00CF","\u00D0","\u00A3","\u00D1","\u00D5","\u00D2","\u00D3","\u00D6",
            "\u00D7","\u00DD","\u00D8","\u00DC","\u00DE",
            "\u00DF","\u00E3","\u00E1","\u00E2","\u00E4","\u00A4","\u00E5","\u00E8","\u00E6","\u00E7","\u00E9","\u00A5","\u00EA","\u00ED","\u00EB","\u00EC","\u00EE",
            "\u00EF","\u00F3","\u00F1","\u00F2","\u00F4","\u00A6","\u00F5","\u00F8","\u00F6","\u00F7","\u00F9",
            "\u00FA","\u00FD","\u00FB","\u00FC","\u00FE",
            "\u00A7",
            "A","A","A","A","A","\u00A2","\u00C7","\u00CA","\u00C8","\u00C9","\u00CB","\u00A1","\u00BB","\u00BE","\u00BC","\u00BD","\u00C6",
            "E","E","E","E","E","\u00A3","\u00D1","\u00D5","\u00D2","\u00D3","\u00D6",
            "I","I","I","I","I",
            "O","O","O","O","O","\u00A4","\u00E5","\u00E8","\u00E6","\u00E7","\u00E9","\u00A5","\u00EA","\u00ED","\u00EB","\u00EC","\u00EE",
            "U","U","U","U","U","\u00A6","\u00F5","\u00F8","\u00F6","\u00F7","\u00F9",
            "Y","Y","Y","Y","Y",
            "\u00A7"
        };

        private static readonly string[] VniChars = new string[]
        {
            "a\u0300","a\u0301","a\u0309","a\u0303","a\u0323","a\u0302","a\u0302\u0300","a\u0302\u0301","a\u0302\u0309","a\u0302\u0303","a\u0302\u0323","a\u0306","a\u0306\u0300","a\u0306\u0301","a\u0306\u0309","a\u0306\u0303","a\u0306\u0323",
            "e\u0300","e\u0301","e\u0309","e\u0303","e\u0323","e\u0302","e\u0302\u0300","e\u0302\u0301","e\u0302\u0309","e\u0302\u0303","e\u0302\u0323",
            "i\u0300","i\u0301","i\u0309","i\u0303","i\u0323",
            "o\u0300","o\u0301","o\u0309","o\u0303","o\u0323","o\u0302","o\u0302\u0300","o\u0302\u0301","o\u0302\u0309","o\u0302\u0303","o\u0302\u0323","o\u031B","o\u031B\u0300","o\u031B\u0301","o\u031B\u0309","o\u031B\u0303","o\u031B\u0323",
            "u\u0300","u\u0301","u\u0309","u\u0303","u\u0323","u\u031B","u\u031B\u0300","u\u031B\u0301","u\u031B\u0309","u\u031B\u0303","u\u031B\u0323",
            "y\u0300","y\u0301","y\u0309","y\u0303","y\u0323",
            "d\u0335",
            "A\u0300","A\u0301","A\u0309","A\u0303","A\u0323","A\u0302","A\u0302\u0300","A\u0302\u0301","A\u0302\u0309","A\u0302\u0303","A\u0302\u0323","A\u0306","A\u0306\u0300","A\u0306\u0301","A\u0306\u0309","A\u0306\u0303","A\u0306\u0323",
            "E\u0300","E\u0301","E\u0309","E\u0303","E\u0323","E\u0302","E\u0302\u0300","E\u0302\u0301","E\u0302\u0309","E\u0302\u0303","E\u0302\u0323",
            "I\u0300","I\u0301","I\u0309","I\u0303","I\u0323",
            "O\u0300","O\u0301","O\u0309","O\u0303","O\u0323","O\u0302","O\u0302\u0300","O\u0302\u0301","O\u0302\u0309","O\u0302\u0303","O\u0302\u0323","O\u031B","O\u031B\u0300","O\u031B\u0301","O\u031B\u0309","O\u031B\u0303","O\u031B\u0323",
            "U\u0300","U\u0301","U\u0309","U\u0303","U\u0323","U\u031B","U\u031B\u0300","U\u031B\u0301","U\u031B\u0309","U\u031B\u0303","U\u031B\u0323",
            "Y\u0300","Y\u0301","Y\u0309","Y\u0303","Y\u0323",
            "D\u0335"
        };

        public static string Convert(string text, Charset source, Charset target)
        {
            if (string.IsNullOrEmpty(text) || source == target) return text;

            // 1. Chuyển từ Source sang Unicode dựng sẵn
            string unicode = ToUnicode(text, source);

            // 2. Chuyển từ Unicode dựng sẵn sang Target
            return FromUnicode(unicode, target);
        }

        public static string ToUnicode(string text, Charset source)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (source == Charset.Unicode) return text;

            if (source == Charset.UnicodeCompound)
            {
                return text.Normalize(NormalizationForm.FormC);
            }

            string[] sourceArr = source == Charset.TCVN3 ? Tcvn3Chars : VniChars;
            var sb = new StringBuilder(text);

            for (int i = 0; i < sourceArr.Length; i++)
            {
                sb.Replace(sourceArr[i], UnicodeChars[i]);
            }
            return sb.ToString();
        }

        public static string FromUnicode(string unicodeText, Charset target)
        {
            if (string.IsNullOrEmpty(unicodeText)) return unicodeText;
            if (target == Charset.Unicode) return unicodeText;

            if (target == Charset.UnicodeCompound)
            {
                return unicodeText.Normalize(NormalizationForm.FormD);
            }

            string[] targetArr = target == Charset.TCVN3 ? Tcvn3Chars : VniChars;
            var sb = new StringBuilder(unicodeText);

            for (int i = 0; i < UnicodeChars.Length; i++)
            {
                sb.Replace(UnicodeChars[i], targetArr[i]);
            }
            return sb.ToString();
        }

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    if (c == 'đ') sb.Append('d');
                    else if (c == 'Đ') sb.Append('D');
                    else sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
