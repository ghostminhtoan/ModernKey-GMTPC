using System;
using System.Collections.Generic;
using System.Text;
using ModernKey.Models;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ máy kiểm tra chính tả tiếng Việt thuật toán chuẩn OpenKey C++.
    /// Port 1:1 từ Engine.cpp và Vietnamese.cpp của tác giả Mai Vũ Tuyên (OpenKey).
    /// Hoạt động thuần túy bằng bảng luật âm tiết (consonantTable, endConsonantTable, vowelCombine),
    /// không cần nạp file từ điển .dic ngoại vi, tốc độ tra cứu O(1) và tiết kiệm RAM tối đa.
    /// </summary>
    public static class OpenKeySpelling
    {
        // ==========================================
        // HẰNG SỐ MÃ PHÍM & MASKS CHUẨN OPENKEY C++
        // ==========================================
        public const ushort KEY_A = 0x41;
        public const ushort KEY_B = 0x42;
        public const ushort KEY_C = 0x43;
        public const ushort KEY_D = 0x44;
        public const ushort KEY_E = 0x45;
        public const ushort KEY_F = 0x46;
        public const ushort KEY_G = 0x47;
        public const ushort KEY_H = 0x48;
        public const ushort KEY_I = 0x49;
        public const ushort KEY_J = 0x4A;
        public const ushort KEY_K = 0x4B;
        public const ushort KEY_L = 0x4C;
        public const ushort KEY_M = 0x4D;
        public const ushort KEY_N = 0x4E;
        public const ushort KEY_O = 0x4F;
        public const ushort KEY_P = 0x50;
        public const ushort KEY_Q = 0x51;
        public const ushort KEY_R = 0x52;
        public const ushort KEY_S = 0x53;
        public const ushort KEY_T = 0x54;
        public const ushort KEY_U = 0x55;
        public const ushort KEY_V = 0x56;
        public const ushort KEY_W = 0x57;
        public const ushort KEY_X = 0x58;
        public const ushort KEY_Y = 0x59;
        public const ushort KEY_Z = 0x5A;

        public const ushort KEY_LEFT_BRACKET = 0xDB;
        public const ushort KEY_RIGHT_BRACKET = 0xDD;

        public const uint CAPS_MASK = 0x10000;
        public const uint TONE_MASK = 0x20000;   // Dấu mũ (^: Â, Ê, Ô) hoặc chữ Đ
        public const uint TONEW_MASK = 0x40000;  // Dấu râu/trăng (w: Ă, Ơ, Ư)

        public const uint MARK1_MASK = 0x80000;  // Dấu Sắc (á)
        public const uint MARK2_MASK = 0x100000; // Dấu Huyền (à)
        public const uint MARK3_MASK = 0x200000; // Dấu Hỏi (ả)
        public const uint MARK4_MASK = 0x400000; // Dấu Ngã (ã)
        public const uint MARK5_MASK = 0x800000; // Dấu Nặng (ạ)

        public const uint MARK_MASK = 0xF80000;  // Bất kỳ dấu thanh nào
        public const uint CHAR_MASK = 0xFFFF;    // Lấy 16-bit key code

        public const ushort END_CONSONANT_MASK = 0x4000;
        public const ushort CONSONANT_ALLOW_MASK = 0x8000;

        // ==========================================
        // BẢNG BẮT ĐẦU PHỤ ÂM (_consonantTable)
        // ==========================================
        private static readonly ushort[][] _consonantTable = new ushort[][]
        {
            new ushort[] { KEY_N, KEY_G, KEY_H }, // ngh
            new ushort[] { KEY_P, KEY_H },        // ph
            new ushort[] { KEY_T, KEY_H },        // th
            new ushort[] { KEY_T, KEY_R },        // tr
            new ushort[] { KEY_G, KEY_I },        // gi
            new ushort[] { KEY_C, KEY_H },        // ch
            new ushort[] { KEY_N, KEY_H },        // nh
            new ushort[] { KEY_N, KEY_G },        // ng
            new ushort[] { KEY_K, KEY_H },        // kh
            new ushort[] { KEY_G, KEY_H },        // gh
            new ushort[] { KEY_G },               // g
            new ushort[] { KEY_C },               // c
            new ushort[] { KEY_Q },               // q
            new ushort[] { KEY_K },               // k
            new ushort[] { KEY_T },               // t
            new ushort[] { KEY_R },               // r
            new ushort[] { KEY_H },               // h
            new ushort[] { KEY_B },               // b
            new ushort[] { KEY_M },               // m
            new ushort[] { KEY_V },               // v
            new ushort[] { KEY_N },               // n
            new ushort[] { KEY_L },               // l
            new ushort[] { KEY_X },               // x
            new ushort[] { KEY_P },               // p
            new ushort[] { KEY_S },               // s
            new ushort[] { KEY_D },               // d hoặc đ
            new ushort[] { (ushort)(KEY_F | CONSONANT_ALLOW_MASK) },
            new ushort[] { (ushort)(KEY_W | CONSONANT_ALLOW_MASK) },
            new ushort[] { (ushort)(KEY_Z | CONSONANT_ALLOW_MASK) },
            new ushort[] { (ushort)(KEY_J | CONSONANT_ALLOW_MASK) },
            new ushort[] { (ushort)(KEY_F | END_CONSONANT_MASK) },
            new ushort[] { (ushort)(KEY_W | END_CONSONANT_MASK) },
            new ushort[] { (ushort)(KEY_J | END_CONSONANT_MASK) },
        };

        // Bảng phụ âm dành riêng cho kiểu gõ Tư Bình Trần
        private static readonly ushort[][] _consonantTableTBT = new ushort[][]
        {
            new ushort[] { KEY_N, KEY_G, KEY_H },
            new ushort[] { KEY_P, KEY_H },
            new ushort[] { KEY_T, KEY_H },
            new ushort[] { KEY_T, KEY_R },
            new ushort[] { KEY_G, KEY_I },
            new ushort[] { KEY_C, KEY_H },
            new ushort[] { KEY_N, KEY_H },
            new ushort[] { KEY_N, KEY_G },
            new ushort[] { KEY_K, KEY_H },
            new ushort[] { KEY_G, KEY_H },
            new ushort[] { KEY_G },
            new ushort[] { KEY_C },
            new ushort[] { KEY_Q },
            new ushort[] { KEY_K },
            new ushort[] { KEY_T },
            new ushort[] { KEY_R },
            new ushort[] { KEY_H },
            new ushort[] { KEY_B },
            new ushort[] { KEY_M },
            new ushort[] { KEY_V },
            new ushort[] { KEY_N },
            new ushort[] { KEY_L },
            new ushort[] { KEY_X },
            new ushort[] { KEY_P },
            new ushort[] { KEY_S },
            new ushort[] { KEY_D },
        };

        // ==========================================
        // BẢNG KẾT THÚC PHỤ ÂM (_endConsonantTable)
        // ==========================================
        private static readonly ushort[][] _endConsonantTable = new ushort[][]
        {
            new ushort[] { KEY_T },
            new ushort[] { KEY_P },
            new ushort[] { KEY_C },
            new ushort[] { KEY_N },
            new ushort[] { KEY_M },
            new ushort[] { (ushort)(KEY_G | END_CONSONANT_MASK) },
            new ushort[] { (ushort)(KEY_K | END_CONSONANT_MASK) },
            new ushort[] { (ushort)(KEY_H | END_CONSONANT_MASK) },
            new ushort[] { KEY_C, KEY_H }, // ch
            new ushort[] { KEY_N, KEY_H }, // nh
            new ushort[] { KEY_N, KEY_G }, // ng
        };

        // ==========================================
        // BẢNG GHÉP NGUYÊN ÂM ĐÔI/BA (_vowelCombine)
        // Cột 0: 1 = Có thể có phụ âm cuối, 0 = Không được có phụ âm cuối
        // ==========================================
        private static readonly Dictionary<ushort, uint[][]> _vowelCombine = new Dictionary<ushort, uint[][]>()
        {
            {
                KEY_A, new uint[][]
                {
                    new uint[] { 0, KEY_A, KEY_I },
                    new uint[] { 0, KEY_A, KEY_O },
                    new uint[] { 0, KEY_A, KEY_U },
                    new uint[] { 0, KEY_A | TONE_MASK, KEY_U },
                    new uint[] { 0, KEY_A, KEY_Y },
                    new uint[] { 0, KEY_A | TONE_MASK, KEY_Y },
                }
            },
            {
                KEY_E, new uint[][]
                {
                    new uint[] { 0, KEY_E, KEY_O },
                    new uint[] { 0, KEY_E | TONE_MASK, KEY_U },
                }
            },
            {
                KEY_I, new uint[][]
                {
                    new uint[] { 1, KEY_I, KEY_E | TONE_MASK, KEY_U },
                    new uint[] { 0, KEY_I, KEY_A },
                    new uint[] { 1, KEY_I, KEY_E | TONE_MASK },
                    new uint[] { 0, KEY_I, KEY_U },
                }
            },
            {
                KEY_O, new uint[][]
                {
                    new uint[] { 0, KEY_O, KEY_A, KEY_I },
                    new uint[] { 0, KEY_O, KEY_A, KEY_O },
                    new uint[] { 0, KEY_O, KEY_A, KEY_Y },
                    new uint[] { 0, KEY_O, KEY_E, KEY_O },
                    new uint[] { 1, KEY_O, KEY_A },
                    new uint[] { 1, KEY_O, KEY_A | TONEW_MASK },
                    new uint[] { 1, KEY_O, KEY_E },
                    new uint[] { 0, KEY_O, KEY_I },
                    new uint[] { 0, KEY_O | TONE_MASK, KEY_I },
                    new uint[] { 0, KEY_O | TONEW_MASK, KEY_I },
                    new uint[] { 1, KEY_O, KEY_O },
                    new uint[] { 1, KEY_O | TONE_MASK, KEY_O | TONE_MASK },
                }
            },
            {
                KEY_U, new uint[][]
                {
                    new uint[] { 0, KEY_U, KEY_Y, KEY_U },
                    new uint[] { 1, KEY_U, KEY_Y, KEY_E | TONE_MASK },
                    new uint[] { 0, KEY_U, KEY_Y, KEY_A },
                    new uint[] { 0, KEY_U | TONEW_MASK, KEY_O | TONEW_MASK, KEY_U },
                    new uint[] { 0, KEY_U | TONEW_MASK, KEY_O | TONEW_MASK, KEY_I },
                    new uint[] { 0, KEY_U, KEY_O | TONE_MASK, KEY_I },
                    new uint[] { 0, KEY_U, KEY_A | TONE_MASK, KEY_Y },
                    new uint[] { 1, KEY_U, KEY_A, KEY_O },
                    new uint[] { 1, KEY_U, KEY_A },
                    new uint[] { 1, KEY_U, KEY_A | TONEW_MASK },
                    new uint[] { 1, KEY_U, KEY_A | TONE_MASK },
                    new uint[] { 0, KEY_U | TONEW_MASK, KEY_A },
                    new uint[] { 1, KEY_U, KEY_E | TONE_MASK },
                    new uint[] { 0, KEY_U, KEY_I },
                    new uint[] { 0, KEY_U | TONEW_MASK, KEY_I },
                    new uint[] { 1, KEY_U, KEY_O },
                    new uint[] { 1, KEY_U, KEY_O | TONE_MASK },
                    new uint[] { 0, KEY_U, KEY_O | TONEW_MASK },
                    new uint[] { 1, KEY_U | TONEW_MASK, KEY_O | TONEW_MASK },
                    new uint[] { 0, KEY_U | TONEW_MASK, KEY_U },
                    new uint[] { 1, KEY_U, KEY_Y },
                }
            },
            {
                KEY_Y, new uint[][]
                {
                    new uint[] { 0, KEY_Y, KEY_E | TONE_MASK, KEY_U },
                    new uint[] { 1, KEY_Y, KEY_E | TONE_MASK },
                }
            }
        };

        // ==========================================
        // BẢNG ÁNH XẠ KÝ TỰ UNICODE TIẾNG VIỆT -> UINT
        // ==========================================
        private static readonly Dictionary<char, uint> _charToTypingWord = InitCharToTypingWord();

        private static Dictionary<char, uint> InitCharToTypingWord()
        {
            var map = new Dictionary<char, uint>(256);

            // Chữ cái ASCII cơ bản
            for (char c = 'a'; c <= 'z'; c++)
                map[c] = (uint)char.ToUpperInvariant(c);
            for (char c = 'A'; c <= 'Z'; c++)
                map[c] = (uint)c;

            map['['] = KEY_LEFT_BRACKET;
            map[']'] = KEY_RIGHT_BRACKET;

            // A variants
            AddVariant(map, "aA", KEY_A, 0);
            AddVariant(map, "áÁ", KEY_A, MARK1_MASK);
            AddVariant(map, "àÀ", KEY_A, MARK2_MASK);
            AddVariant(map, "ảẢ", KEY_A, MARK3_MASK);
            AddVariant(map, "ãÃ", KEY_A, MARK4_MASK);
            AddVariant(map, "ạẠ", KEY_A, MARK5_MASK);

            AddVariant(map, "âÂ", KEY_A, TONE_MASK);
            AddVariant(map, "ấẤ", KEY_A, TONE_MASK | MARK1_MASK);
            AddVariant(map, "ầẦ", KEY_A, TONE_MASK | MARK2_MASK);
            AddVariant(map, "ẩẨ", KEY_A, TONE_MASK | MARK3_MASK);
            AddVariant(map, "ẫẪ", KEY_A, TONE_MASK | MARK4_MASK);
            AddVariant(map, "ậẬ", KEY_A, TONE_MASK | MARK5_MASK);

            AddVariant(map, "ăĂ", KEY_A, TONEW_MASK);
            AddVariant(map, "ắẮ", KEY_A, TONEW_MASK | MARK1_MASK);
            AddVariant(map, "ằẰ", KEY_A, TONEW_MASK | MARK2_MASK);
            AddVariant(map, "ẳẲ", KEY_A, TONEW_MASK | MARK3_MASK);
            AddVariant(map, "ẵẴ", KEY_A, TONEW_MASK | MARK4_MASK);
            AddVariant(map, "ặẶ", KEY_A, TONEW_MASK | MARK5_MASK);

            // D variant (đ/Đ)
            AddVariant(map, "dD", KEY_D, 0);
            AddVariant(map, "đĐ", KEY_D, TONE_MASK);

            // E variants
            AddVariant(map, "eE", KEY_E, 0);
            AddVariant(map, "éÉ", KEY_E, MARK1_MASK);
            AddVariant(map, "èÈ", KEY_E, MARK2_MASK);
            AddVariant(map, "ẻẺ", KEY_E, MARK3_MASK);
            AddVariant(map, "ẽẼ", KEY_E, MARK4_MASK);
            AddVariant(map, "ẹẸ", KEY_E, MARK5_MASK);

            AddVariant(map, "êÊ", KEY_E, TONE_MASK);
            AddVariant(map, "ếẾ", KEY_E, TONE_MASK | MARK1_MASK);
            AddVariant(map, "ềỀ", KEY_E, TONE_MASK | MARK2_MASK);
            AddVariant(map, "ểỂ", KEY_E, TONE_MASK | MARK3_MASK);
            AddVariant(map, "ễỄ", KEY_E, TONE_MASK | MARK4_MASK);
            AddVariant(map, "ệỆ", KEY_E, TONE_MASK | MARK5_MASK);

            // I variants
            AddVariant(map, "iI", KEY_I, 0);
            AddVariant(map, "íÍ", KEY_I, MARK1_MASK);
            AddVariant(map, "ìÌ", KEY_I, MARK2_MASK);
            AddVariant(map, "ỉỈ", KEY_I, MARK3_MASK);
            AddVariant(map, "ĩĨ", KEY_I, MARK4_MASK);
            AddVariant(map, "ịỊ", KEY_I, MARK5_MASK);

            // O variants
            AddVariant(map, "oO", KEY_O, 0);
            AddVariant(map, "óÓ", KEY_O, MARK1_MASK);
            AddVariant(map, "òÒ", KEY_O, MARK2_MASK);
            AddVariant(map, "ỏỎ", KEY_O, MARK3_MASK);
            AddVariant(map, "õÕ", KEY_O, MARK4_MASK);
            AddVariant(map, "ọỌ", KEY_O, MARK5_MASK);

            AddVariant(map, "ôÔ", KEY_O, TONE_MASK);
            AddVariant(map, "ốỐ", KEY_O, TONE_MASK | MARK1_MASK);
            AddVariant(map, "ồỒ", KEY_O, TONE_MASK | MARK2_MASK);
            AddVariant(map, "ổỔ", KEY_O, TONE_MASK | MARK3_MASK);
            AddVariant(map, "ỗỖ", KEY_O, TONE_MASK | MARK4_MASK);
            AddVariant(map, "ộỘ", KEY_O, TONE_MASK | MARK5_MASK);

            AddVariant(map, "ơƠ", KEY_O, TONEW_MASK);
            AddVariant(map, "ớỚ", KEY_O, TONEW_MASK | MARK1_MASK);
            AddVariant(map, "ờỜ", KEY_O, TONEW_MASK | MARK2_MASK);
            AddVariant(map, "ởỞ", KEY_O, TONEW_MASK | MARK3_MASK);
            AddVariant(map, "ỡỠ", KEY_O, TONEW_MASK | MARK4_MASK);
            AddVariant(map, "ợỢ", KEY_O, TONEW_MASK | MARK5_MASK);

            // U variants
            AddVariant(map, "uU", KEY_U, 0);
            AddVariant(map, "úÚ", KEY_U, MARK1_MASK);
            AddVariant(map, "ùÙ", KEY_U, MARK2_MASK);
            AddVariant(map, "ủỦ", KEY_U, MARK3_MASK);
            AddVariant(map, "ũŨ", KEY_U, MARK4_MASK);
            AddVariant(map, "ụỤ", KEY_U, MARK5_MASK);

            AddVariant(map, "ưƯ", KEY_U, TONEW_MASK);
            AddVariant(map, "ứỨ", KEY_U, TONEW_MASK | MARK1_MASK);
            AddVariant(map, "ừỪ", KEY_U, TONEW_MASK | MARK2_MASK);
            AddVariant(map, "ửỬ", KEY_U, TONEW_MASK | MARK3_MASK);
            AddVariant(map, "ữỮ", KEY_U, TONEW_MASK | MARK4_MASK);
            AddVariant(map, "ựỰ", KEY_U, TONEW_MASK | MARK5_MASK);

            // Y variants
            AddVariant(map, "yY", KEY_Y, 0);
            AddVariant(map, "ýÝ", KEY_Y, MARK1_MASK);
            AddVariant(map, "ỳỲ", KEY_Y, MARK2_MASK);
            AddVariant(map, "ỷỶ", KEY_Y, MARK3_MASK);
            AddVariant(map, "ỹỸ", KEY_Y, MARK4_MASK);
            AddVariant(map, "ỵỴ", KEY_Y, MARK5_MASK);

            return map;
        }

        private static void AddVariant(Dictionary<char, uint> map, string chars, ushort baseKey, uint flags)
        {
            uint val = baseKey | flags;
            for (int i = 0; i < chars.Length; i++)
            {
                map[chars[i]] = val;
            }
        }

        // ==========================================
        // CÁC HÀM XÁC ĐỊNH PHỤ ÂM
        // ==========================================
        public static bool IsConsonant(ushort keyCode)
        {
            return !(keyCode == KEY_A || keyCode == KEY_E || keyCode == KEY_U || keyCode == KEY_Y || keyCode == KEY_I || keyCode == KEY_O);
        }

        public static bool IsSpellingConsonant(ushort keyCode, bool isTuBinhTran)
        {
            if (isTuBinhTran)
            {
                return (keyCode >= KEY_A && keyCode <= KEY_Z) && IsConsonant(keyCode);
            }
            return IsConsonant(keyCode);
        }

        // ==========================================
        // THUẬT TOÁN KIỂM TRA CHÍNH TẢ CHUẨN OPENKEY C++
        // ==========================================
        /// <summary>
        /// Thuật toán checkSpelling() ported 1:1 từ OpenKey C++.
        /// </summary>
        /// <param name="typingWord">Mảng chứa các ký tự kèm cờ (TONE, TONEW, MARK)</param>
        /// <param name="length">Độ dài từ hiện tại</param>
        /// <param name="forceCheckVowel">True khi ngắt từ (Space/Enter/Punctuation), False khi đang gõ từng phím</param>
        /// <param name="isTuBinhTran">Chế độ kiểu gõ Tư Bình Trần</param>
        /// <param name="allowConsonantZFWJ">Cho phép phụ âm Z, F, W, J</param>
        /// <param name="quickStartConsonant">Cho phép gõ nhanh phụ âm đầu (f->ph, w->qu, j->gi)</param>
        /// <param name="quickEndConsonant">Cho phép gõ nhanh phụ âm cuối (g->ng, h->nh, k->ch)</param>
        public static bool CheckSpelling(
            uint[] typingWord,
            int length,
            bool forceCheckVowel,
            bool isTuBinhTran = false,
            bool allowConsonantZFWJ = false,
            bool quickStartConsonant = false,
            bool quickEndConsonant = false)
        {
            if (length <= 0) return true;

            bool spellingOK = false;
            bool spellingVowelOK = true;
            int spellingEndIndex = length;

            if (length > 0 && (typingWord[length - 1] & CHAR_MASK) == KEY_RIGHT_BRACKET)
            {
                spellingEndIndex = length - 1;
            }

            if (spellingEndIndex > 0)
            {
                int j = 0;
                var consonantTable = isTuBinhTran ? _consonantTableTBT : _consonantTable;

                // 1. Kiểm tra phụ âm đầu (Check first consonant)
                ushort firstChar = (ushort)(typingWord[0] & CHAR_MASK);
                if (IsSpellingConsonant(firstChar, isTuBinhTran))
                {
                    bool matched = false;
                    for (int i = 0; i < consonantTable.Length; i++)
                    {
                        bool spellingFlag = false;
                        if (spellingEndIndex < consonantTable[i].Length)
                            spellingFlag = true;

                        for (j = 0; j < consonantTable[i].Length; j++)
                        {
                            if (spellingEndIndex > j &&
                                (consonantTable[i][j] & ~(quickStartConsonant ? END_CONSONANT_MASK : 0)) != (typingWord[j] & CHAR_MASK) &&
                                (consonantTable[i][j] & ~(allowConsonantZFWJ ? CONSONANT_ALLOW_MASK : 0)) != (typingWord[j] & CHAR_MASK))
                            {
                                spellingFlag = true;
                                break;
                            }
                        }

                        if (spellingFlag)
                            continue;

                        matched = true;
                        j = consonantTable[i].Length;
                        break;
                    }

                    if (!matched)
                    {
                        // Phụ âm đầu không tồn tại trong tiếng Việt (ví dụ "zx", "qm"...)
                        return false;
                    }
                }

                if (j == spellingEndIndex) // Chỉ mới gõ phụ âm đầu (ví dụ "d", "tr", "ngh")
                {
                    spellingOK = true;
                }

                // 2. Kiểm tra nguyên âm kế tiếp (Check next vowel)
                int k = j;
                int vsi = k;

                // Fix case "que't" (qu + u + e)
                if (vsi < spellingEndIndex && (typingWord[vsi] & CHAR_MASK) == KEY_U && k > 0 && k < spellingEndIndex - 1 && (typingWord[vsi - 1] & CHAR_MASK) == KEY_Q)
                {
                    k = k + 1;
                    j = k;
                    vsi = k;
                }
                else if (length >= 2 && (typingWord[0] & CHAR_MASK) == KEY_G && (typingWord[1] & CHAR_MASK) == KEY_I &&
                         (spellingEndIndex == 2 || IsSpellingConsonant((ushort)(typingWord[2] & CHAR_MASK), isTuBinhTran)))
                {
                    vsi = k = j = 1; // fix 'gì', 'gìn'
                }

                for (int l = 0; l < 3; l++)
                {
                    if (k < spellingEndIndex && !IsSpellingConsonant((ushort)(typingWord[k] & CHAR_MASK), isTuBinhTran))
                    {
                        k++;
                    }
                }

                if (k > j) // Có nguyên âm
                {
                    spellingVowelOK = false;

                    // Kiểm tra kết hợp nguyên âm đôi/ba hợp lệ
                    if (k - j > 1 && forceCheckVowel)
                    {
                        ushort vowelKey = (ushort)(typingWord[j] & CHAR_MASK);
                        if (_vowelCombine.TryGetValue(vowelKey, out var vowelSet))
                        {
                            for (int l = 0; l < vowelSet.Length; l++)
                            {
                                bool spellingFlag = false;
                                int ii = 1;
                                for (ii = 1; ii < vowelSet[l].Length; ii++)
                                {
                                    if (j + ii - 1 < spellingEndIndex &&
                                        vowelSet[l][ii] != ((typingWord[j + ii - 1] & CHAR_MASK) | (typingWord[j + ii - 1] & TONEW_MASK) | (typingWord[j + ii - 1] & TONE_MASK)))
                                    {
                                        spellingFlag = true;
                                        break;
                                    }
                                }

                                if (spellingFlag || (k < spellingEndIndex && vowelSet[l][0] == 0) ||
                                    (j + ii - 1 < spellingEndIndex && !IsSpellingConsonant((ushort)(typingWord[j + ii - 1] & CHAR_MASK), isTuBinhTran)))
                                {
                                    continue;
                                }

                                spellingVowelOK = true;
                                break;
                            }
                        }
                    }
                    else if (!IsSpellingConsonant((ushort)(typingWord[j] & CHAR_MASK), isTuBinhTran))
                    {
                        spellingVowelOK = true;
                    }

                    // 3. Tiếp tục kiểm tra phụ âm cuối (Continue check last consonant)
                    for (int ii = 0; ii < _endConsonantTable.Length; ii++)
                    {
                        bool spellingFlag = false;
                        int ej = 0;

                        for (ej = 0; ej < _endConsonantTable[ii].Length; ej++)
                        {
                            if (spellingEndIndex > k + ej &&
                                (_endConsonantTable[ii][ej] & ~(quickEndConsonant ? END_CONSONANT_MASK : 0)) != (typingWord[k + ej] & CHAR_MASK))
                            {
                                spellingFlag = true;
                                break;
                            }
                        }

                        if (spellingFlag)
                            continue;

                        if (k + ej >= spellingEndIndex)
                        {
                            spellingOK = true;
                            break;
                        }
                    }

                    // 4. Giới hạn quy tắc thanh điệu: Phụ âm cuối "ch", "t" (và p, c) không thể đi với dấu Huyền, Hỏi, Ngã
                    if (spellingOK)
                    {
                        if (length >= 3 && (typingWord[length - 1] & CHAR_MASK) == KEY_H && (typingWord[length - 2] & CHAR_MASK) == KEY_C &&
                            !((typingWord[length - 3] & MARK1_MASK) != 0 || (typingWord[length - 3] & MARK5_MASK) != 0 || (typingWord[length - 3] & MARK_MASK) == 0))
                        {
                            spellingOK = false;
                        }
                        else if (length >= 2 && (typingWord[length - 1] & CHAR_MASK) == KEY_T &&
                            !((typingWord[length - 2] & MARK1_MASK) != 0 || (typingWord[length - 2] & MARK5_MASK) != 0 || (typingWord[length - 2] & MARK_MASK) == 0))
                        {
                            spellingOK = false;
                        }
                    }
                }
            }
            else
            {
                spellingOK = true;
            }

            return spellingOK && spellingVowelOK;
        }

        /// <summary>
        /// Kiểm tra xem từ có chứa dấu thanh/mũ trên nguyên âm không (chuẩn checkRestoreIfWrongSpelling).
        /// </summary>
        public static bool HasToneMarkOnVowel(uint[] typingWord, int length)
        {
            for (int i = 0; i < length; i++)
            {
                ushort key = (ushort)(typingWord[i] & CHAR_MASK);
                if (!IsConsonant(key) &&
                    ((typingWord[i] & MARK_MASK) != 0 || (typingWord[i] & TONE_MASK) != 0 || (typingWord[i] & TONEW_MASK) != 0))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra xem từ chuỗi Unicode có chứa nguyên âm mang dấu thanh/mũ tiếng Việt hay không (chuẩn OpenKey C++).
        /// Từ thuần phụ âm (như ddr, ctrl, html) sẽ trả về false và không bao giờ bị khôi phục sai chính tả khi kết thúc từ.
        /// </summary>
        public static bool HasToneMarkOnVowel(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            string norm = word.Normalize(NormalizationForm.FormC);
            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                if (_charToTypingWord.TryGetValue(ch, out uint val))
                {
                    ushort key = (ushort)(val & CHAR_MASK);
                    if (!IsConsonant(key) &&
                        ((val & MARK_MASK) != 0 || (val & TONE_MASK) != 0 || (val & TONEW_MASK) != 0))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra một từ chuỗi (Unicode UTF-16) có đúng quy tắc chính tả OpenKey C++ hay không.
        /// </summary>
        public static bool IsValidWord(string word, bool forceCheckVowel = true, AppSettings settings = null)
        {
            if (string.IsNullOrEmpty(word)) return true;
            if (word.Length == 1) return true;

            string norm = word.Normalize(NormalizationForm.FormC);
            uint[] typingWord = new uint[norm.Length];
            int len = 0;

            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                if (_charToTypingWord.TryGetValue(ch, out uint val))
                {
                    typingWord[len++] = val;
                }
                else if (char.IsLetter(ch))
                {
                    typingWord[len++] = (uint)char.ToUpperInvariant(ch);
                }
                else
                {
                    // Chứa ký tự không phải chữ (số, ký hiệu): không ép rule chính tả
                    return true;
                }
            }

            if (len == 0) return true;

            bool isTuBinhTran = settings?.CurrentInputMethod == InputMethod.TuBinhTran;
            bool allowConsonantZFWJ = settings?.AllowConsonantZFWJ ?? false;

            return CheckSpelling(typingWord, len, forceCheckVowel, isTuBinhTran, allowConsonantZFWJ, false, false);
        }

        /// <summary>
        /// Overload đơn giản kiểm tra chính tả tiếng Việt.
        /// </summary>
        public static bool IsValidWord(string word)
        {
            return IsValidWord(word, true, null);
        }
    }
}
