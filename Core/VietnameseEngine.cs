using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ModernKey.Config;
using ModernKey.Models;

namespace ModernKey.Core
{
    public class VietnameseEngine
    {
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static bool _hookReportedCaps = false;

        public static bool IsCapsLockActive()
        {
            return _hookReportedCaps;
        }

        private readonly AppSettings _settings;
        private readonly MacroManager _macroManager;

        // Buffer lưu trữ các phím gốc đã gõ cho từ hiện tại
        private readonly List<char> _charBuffer = new List<char>();

        // Cờ trạng thái
        private bool _inNumberSequence = false;

        public VietnameseEngine(AppSettings settings, MacroManager macroManager)
        {
            _settings = settings ?? new AppSettings();
            _macroManager = macroManager ?? new MacroManager();
        }

        public void Reset()
        {
            _charBuffer.Clear();
            _inNumberSequence = false;
        }

        public bool HasPendingWord => _charBuffer.Count > 0;

        private static readonly HashSet<string> _codeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "const", "return", "class", "public", "private", "protected", "function", "static",
            "import", "export", "var", "let", "while", "yield", "async", "await", "struct",
            "interface", "namespace", "using", "case", "default", "false", "true", "null",
            "undefined", "string", "double", "float", "boolean", "package", "switch", "typeof"
        };

        private static bool IsCodeOrUrlPattern(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string lower = text.ToLowerInvariant();
            if (lower.StartsWith("http:") || lower.StartsWith("https:") || lower.StartsWith("git ") || lower.StartsWith("www.") || lower.StartsWith("//"))
                return true;
            return false;
        }

        private static bool IsCodeKeyword(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return _codeKeywords.Contains(text);
        }

        public bool HandleEscUndo(out int backspaceCount, out string newString)
        {
            backspaceCount = 0;
            newString = null;
            if (_settings.EscKeyUndo && _charBuffer.Count > 0)
            {
                string displayWord = GetDisplayWord(_charBuffer);
                string rawWord = new string(_charBuffer.ToArray());
                if (!string.IsNullOrEmpty(displayWord) && displayWord != rawWord)
                {
                    backspaceCount = displayWord.Length;
                    newString = rawWord;
                    Reset();
                    return true;
                }
            }
            Reset();
            return false;
        }

        public bool ProcessKey(char ch, int vkCode, bool isShift, bool isCaps, bool isCtrl, bool isAlt,
                               out int backspaceCount, out string newString)
        {
            return ProcessKey(ch, vkCode, isShift, isCaps, isCtrl, isAlt, out backspaceCount, out newString, out _);
        }

        public bool ProcessKey(char ch, int vkCode, bool isShift, bool isCaps, bool isCtrl, bool isAlt,
                               out int backspaceCount, out string newString, out int trailingVkCode)
        {
            _hookReportedCaps = isCaps;
            backspaceCount = 0;
            newString = null;
            trailingVkCode = 0;

            // 0. Phím ESC: hoàn tác từ tiếng Việt về ký tự gốc nếu bật EscKeyUndo, hoặc reset engine
            if (vkCode == 0x1B)
            {
                if (HandleEscUndo(out backspaceCount, out newString))
                {
                    return true;
                }
                return false;
            }

            // 1. Phím chuyển chế độ gõ (Ctrl+Shift hoặc Alt+Z)
            if (_settings.SwitchMode == SwitchKeyMode.CtrlShift)
            {
                if (isCtrl && isShift && (vkCode == 0xA0 || vkCode == 0xA1 || vkCode == 0x10))
                {
                    _settings.IsVietnamese = !_settings.IsVietnamese;
                    Reset();
                    return false;
                }
            }
            else if (_settings.SwitchMode == SwitchKeyMode.AltZ)
            {
                if (isAlt && (vkCode == 0x5A || ch == 'z' || ch == 'Z'))
                {
                    _settings.IsVietnamese = !_settings.IsVietnamese;
                    Reset();
                    return true;
                }
            }

            // Nếu đang giữ phím điều khiển (Ctrl hoặc Alt hoặc Win)
            if (isCtrl || isAlt)
            {
                Reset();
                return false;
            }

            // 2. Nếu chế độ Tiếng Anh: kiểm tra nếu cho phép gõ tắt cả khi tắt tiếng Việt
            if (!_settings.IsVietnamese)
            {
                if (_settings.UseMacro && _settings.UseMacroInEnglish)
                {
                    return ProcessEnglishMacroOnly(ch, vkCode, out backspaceCount, out newString, out trailingVkCode);
                }
                Reset();
                return false;
            }

            // 3. Phím Backspace
            if (vkCode == 0x08)
            {
                string currentDisplay = GetDisplayWord(_charBuffer);
                if (string.IsNullOrEmpty(currentDisplay) || currentDisplay.Length <= 1)
                {
                    _charBuffer.Clear();
                }
                else
                {
                    string remainingDisplay = currentDisplay.Substring(0, currentDisplay.Length - 1);
                    var newBuf = SynchronizeBufferWithDisplay(remainingDisplay, _settings.CurrentInputMethod, _settings.ModernToneRules);
                    _charBuffer.Clear();
                    if (newBuf != null && newBuf.Count > 0)
                    {
                        _charBuffer.AddRange(newBuf);
                    }
                }
                return false;
            }

            // 4. Ký tự ngắt từ (Space, Enter, Tab, Dấu câu, Dấu ngoặc, Gạch nối...)
            bool isSpace = (vkCode == 0x20 || ch == ' ');
            bool isReturn = (vkCode == 0x0D || ch == '\r' || ch == '\n');
            bool isTab = (vkCode == 0x09 || ch == '\t');

            bool isPunctuation = ch == '.' || ch == ',' || ch == ';' || ch == ':' ||
                                 ch == '!' || ch == '?' ||
                                 ch == '/' || ch == '\\' || ch == '|' ||
                                 ch == ')' || ch == '<' || ch == '>' ||
                                 ch == '\"' || ch == '\'' || ch == '`' ||
                                 ch == '-' || ch == '_' || ch == '+' || ch == '=' ||
                                 ch == '~' || ch == '@' || ch == '#' || ch == '$' || ch == '%';

            if (_settings.CurrentInputMethod == InputMethod.Custom)
            {
                bool isCustomKey = false;
                if (_settings.CustomRules != null)
                {
                    for (int r = 0; r < _settings.CustomRules.Count; r++)
                    {
                        if (_settings.CustomRules[r].Key == ch) { isCustomKey = true; break; }
                    }
                }
                if (!isCustomKey && (ch == '[' || ch == ']' || ch == '{' || ch == '}' || ch == '(' || ch == '^' || ch == '&' || ch == '*'))
                    isPunctuation = true;
            }
            else if (_settings.CurrentInputMethod != InputMethod.TuBinhTran)
            {
                if (ch == '[' || ch == ']' || ch == '{' || ch == '}' || ch == '(' || ch == '^' || ch == '&' || ch == '*')
                    isPunctuation = true;
            }

            bool isWordBreak = isSpace || isReturn || isTab || isPunctuation;

            if (isWordBreak)
            {
                // Kiểm tra Macro khi ấn phím ngắt theo MacroTriggerMask hoặc bất kỳ Punctuation nào
                if (_settings.UseMacro && _charBuffer.Count > 0)
                {
                    bool triggerAllowed = (isSpace && (_settings.MacroTriggerMask & 0x01) != 0) ||
                                          (isReturn && (_settings.MacroTriggerMask & 0x02) != 0) ||
                                          isPunctuation;

                    if (triggerAllowed)
                    {
                        string displayWord = GetDisplayWord(_charBuffer);
                        string rawWord = new string(_charBuffer.ToArray());

                        if (_macroManager.TryGetMacro(displayWord, _settings.AutoCapsMacro, out string replacement) ||
                            _macroManager.TryGetMacro(rawWord, _settings.AutoCapsMacro, out replacement))
                        {
                            backspaceCount = Math.Min(displayWord.Length, 15);
                            newString = replacement;
                            if (isSpace)
                            {
                                trailingVkCode = 0x20; // Phím vật lý VK_SPACE (chuẩn OpenKey C++)
                            }
                            else if (isReturn)
                            {
                                trailingVkCode = 0x0D; // Phím vật lý VK_RETURN (chuẩn OpenKey C++)
                            }
                            else if (isPunctuation && ch != '\0')
                            {
                                newString = replacement + ch;
                                trailingVkCode = 0;
                            }
                            else
                            {
                                trailingVkCode = 0;
                            }
                            Reset();
                            return true;
                        }
                    }
                }

                // Smart Code Passthrough: khôi phục từ khóa code khi ngắt từ
                if (_settings.SmartCodePassthrough && _charBuffer.Count > 0)
                {
                    string displayWord = GetDisplayWord(_charBuffer);
                    string rawWord = new string(_charBuffer.ToArray());

                    if (!string.IsNullOrEmpty(displayWord) && displayWord != rawWord && IsCodeKeyword(rawWord))
                    {
                        backspaceCount = displayWord.Length;
                        newString = rawWord;
                        if (isSpace)
                        {
                            trailingVkCode = 0x20;
                        }
                        else if (isReturn)
                        {
                            trailingVkCode = 0x0D;
                        }
                        else if (isPunctuation && ch != '\0')
                        {
                            newString = rawWord + ch;
                            trailingVkCode = 0;
                        }
                        Reset();
                        return true;
                    }
                }

                // Kiểm tra từ điển chính tả vi_VN.dic và tự động khôi phục nếu từ sai chính tả
                if (_settings.CheckSpelling && _settings.RestoreIfWrongSpelling && _charBuffer.Count > 0)
                {
                    string displayWord = GetDisplayWord(_charBuffer);
                    string rawWord = new string(_charBuffer.ToArray());

                    if (!string.IsNullOrEmpty(displayWord) && displayWord != rawWord && !SpellingDictionary.Instance.IsValidWord(displayWord))
                    {
                        backspaceCount = displayWord.Length;
                        newString = rawWord;
                        if (isSpace)
                        {
                            trailingVkCode = 0x20;
                        }
                        else if (isReturn)
                        {
                            trailingVkCode = 0x0D;
                        }
                        else if (isPunctuation && ch != '\0')
                        {
                            newString = rawWord + ch;
                            trailingVkCode = 0;
                        }
                        Reset();
                        return true;
                    }
                }

                Reset();
                return false;
            }

            // 5. Kiểm tra bảo vệ số thuần và từ chứa số (như hardcode, hentai2read)
            bool isDigit = char.IsDigit(ch);

            if (isDigit)
            {
                if (_settings.CurrentInputMethod == InputMethod.Telex || _settings.CurrentInputMethod == InputMethod.SimpleTelex)
                {
                    // Khi đang gõ Telex/SimpleTelex mà xuất hiện chữ số (VD: 2 trong hentai2read),
                    // đánh dấu _inNumberSequence = true và reset buffer để không ép rule tiếng Việt sau chữ số
                    _inNumberSequence = true;
                    _charBuffer.Clear();
                    return false;
                }
                else if (_settings.CurrentInputMethod == InputMethod.Vni)
                {
                    // Nếu buffer rỗng hoặc đang trong chuỗi số thuần
                    if (_charBuffer.Count == 0 || _inNumberSequence)
                    {
                        _inNumberSequence = true;
                        _charBuffer.Clear();
                        return false;
                    }
                }
                else if (_settings.CurrentInputMethod == InputMethod.TuBinhTran)
                {
                    // Trong Tư Bình Trần: 6=â, 7=ê, 8=ô, 9=ă có thể đứng đầu từ (Standalone)
                    // Nếu đang trong chuỗi số thuần hoặc bắt đầu bằng số 0..5: duy trì chuỗi số thuần
                    if (_inNumberSequence || (_charBuffer.Count == 0 && (ch >= '0' && ch <= '5')))
                    {
                        _inNumberSequence = true;
                        _charBuffer.Clear();
                        return false;
                    }
                }
                else if (_settings.CurrentInputMethod == InputMethod.Custom)
                {
                    bool isCustomDigitRule = false;
                    if (_settings.CustomRules != null)
                    {
                        for (int r = 0; r < _settings.CustomRules.Count; r++)
                        {
                            if (_settings.CustomRules[r].Key == ch) { isCustomDigitRule = true; break; }
                        }
                    }

                    if (!isCustomDigitRule || _inNumberSequence)
                    {
                        _inNumberSequence = true;
                        _charBuffer.Clear();
                        return false;
                    }
                }
            }
            else
            {
                if (char.IsLetter(ch))
                {
                    // Nếu trước đó vừa có chữ số (_inNumberSequence = true) như hentai2read ➔ read
                    // Xóa buffer phím trước để không biến đổi tiếng Việt ghép vào từ đằng trước có số
                    if (_inNumberSequence)
                    {
                        _charBuffer.Clear();
                        _inNumberSequence = false;
                    }
                }
            }

            // 5.5. Smart Code Passthrough: nếu chuỗi đang gõ là URL hoặc từ khóa code, không biến đổi tiếng Việt
            if (_settings.SmartCodePassthrough && _charBuffer.Count > 0)
            {
                string rawWord = new string(_charBuffer.ToArray()) + ch;
                if (IsCodeOrUrlPattern(rawWord))
                {
                    _charBuffer.Add(ch);
                    return false;
                }
            }

            // 6. Xử lý gõ tiếng Việt với cơ chế Delta-Change thông minh
            return TryTransformVietnamese(ch, out backspaceCount, out newString);
        }

        public bool TryTriggerMacroDirect(out int backspaceCount, out string newString)
        {
            backspaceCount = 0;
            newString = null;
            if (!_settings.UseMacro || _charBuffer.Count == 0) return false;

            string displayWord = GetDisplayWord(_charBuffer);
            string rawWord = new string(_charBuffer.ToArray());

            if (_macroManager.TryGetMacro(displayWord, _settings.AutoCapsMacro, out string replacement) ||
                _macroManager.TryGetMacro(rawWord, _settings.AutoCapsMacro, out replacement))
            {
                backspaceCount = Math.Min(displayWord.Length, 15);
                newString = replacement;
                Reset();
                return true;
            }
            return false;
        }

        private bool ProcessEnglishMacroOnly(char ch, int vkCode, out int backspaceCount, out string newString, out int trailingVkCode)
        {
            backspaceCount = 0;
            newString = null;
            trailingVkCode = 0;

            // 0. Phím ESC: dừng ngay gõ tắt và reset engine
            if (vkCode == 0x1B)
            {
                Reset();
                return false;
            }

            if (vkCode == 0x08)
            {
                if (_charBuffer.Count > 0) _charBuffer.RemoveAt(_charBuffer.Count - 1);
                return false;
            }

            bool isSpace = (vkCode == 0x20 || ch == ' ');
            bool isReturn = (vkCode == 0x0D || ch == '\r' || ch == '\n');
            bool isTab = (vkCode == 0x09 || ch == '\t');

            bool isPunctuation = !isSpace && !isReturn && !isTab && (
                char.IsPunctuation(ch) || char.IsSymbol(ch) ||
                ch == '.' || ch == ',' || ch == ';' || ch == ':' ||
                ch == '!' || ch == '?' ||
                ch == '/' || ch == '\\' || ch == '|' ||
                ch == ')' || ch == '(' || ch == '<' || ch == '>' ||
                ch == '\"' || ch == '\'' || ch == '`' ||
                ch == '-' || ch == '_' || ch == '+' || ch == '=' ||
                ch == '~' || ch == '@' || ch == '#' || ch == '$' || ch == '%' ||
                ch == '^' || ch == '&' || ch == '*' || ch == '[' || ch == ']' ||
                ch == '{' || ch == '}');

            bool isWordBreak = isSpace || isReturn || isTab || isPunctuation;

            if (isWordBreak)
            {
                if (_charBuffer.Count > 0)
                {
                    bool triggerAllowed = (isSpace && (_settings.MacroTriggerMask & 0x01) != 0) ||
                                          (isReturn && (_settings.MacroTriggerMask & 0x02) != 0) ||
                                          isPunctuation;

                    if (triggerAllowed)
                    {
                        string word = new string(_charBuffer.ToArray());
                        if (_macroManager.TryGetMacro(word, _settings.AutoCapsMacro, out string replacement))
                        {
                            backspaceCount = Math.Min(word.Length, 15);
                            newString = replacement;
                            if (isSpace)
                            {
                                trailingVkCode = 0x20; // Phím vật lý VK_SPACE (chuẩn OpenKey C++)
                            }
                            else if (isReturn)
                            {
                                trailingVkCode = 0x0D; // Phím vật lý VK_RETURN (chuẩn OpenKey C++)
                            }
                            else if (isPunctuation && ch != '\0')
                            {
                                newString = replacement + ch;
                                trailingVkCode = 0;
                            }
                            else
                            {
                                trailingVkCode = 0;
                            }
                            Reset();
                            return true;
                        }
                    }
                }
                Reset();
                return false;
            }

            if (char.IsLetterOrDigit(ch))
            {
                if (_charBuffer.Count < 30) _charBuffer.Add(ch);
            }
            else
            {
                Reset();
            }

            return false;
        }

        private string GetDisplayWord(List<char> buffer)
        {
            if (buffer == null || buffer.Count == 0) return string.Empty;
            string transformed = TransformWord(buffer, _settings.CurrentInputMethod, _settings.ModernToneRules, _settings.CustomRules);
            return transformed ?? new string(buffer.ToArray());
        }

        private bool TryTransformVietnamese(char ch, out int backspaceCount, out string newString)
        {
            backspaceCount = 0;
            newString = null;

            // 1. Lấy từ đang hiển thị trên màn hình TRƯỚC KHI gõ phím ch
            string prevDisplayWord = GetDisplayWord(_charBuffer);

            // 2. Kỳ vọng chuỗi nếu là gõ phím thông thường (không biến đổi dấu)
            string expectedNormal = prevDisplayWord + ch;

            // 3. Thử thêm ký tự ch vào buffer và phân tích
            List<char> testBuffer = new List<char>(_charBuffer) { ch };
            string transformed = TransformWord(testBuffer, _settings.CurrentInputMethod, _settings.ModernToneRules, _settings.CustomRules);
            string actualDisplayWord = transformed ?? new string(testBuffer.ToArray());

            // 4. SO SÁNH DELTA-CHANGE:
            // Nếu actualDisplayWord GIỐNG HỆT expectedNormal:
            // Phím ch KHÔNG làm biến đổi dấu hay mũ nào! Để Windows in ký tự tự nhiên!
            if (string.Equals(actualDisplayWord, expectedNormal, StringComparison.Ordinal))
            {
                _charBuffer.Add(ch);
                return false; // KHÔNG GỬI BACKSPACE, KHÔNG NUỐT PHÍM!
            }

            // 5. NẾU KHÁC NHAU: THỰC SỰ CÓ BIẾN ĐỔI DẤU HOẶC MŨ TIẾNG VIỆT!
            // Số lượng ký tự cần xóa đúng bằng độ dài của từ trước đó đang hiển thị trên màn hình
            backspaceCount = Math.Min(prevDisplayWord.Length, 15); // Bảo vệ không xóa lấn sang từ trước

            // Chuyển sang bảng mã đích
            newString = CharsetConverter.FromUnicode(actualDisplayWord, _settings.CurrentCharset);

            _charBuffer.Add(ch);
            return true;
        }

        public static string TransformWord(List<char> keys, InputMethod method, bool modernTone, List<CustomInputRule> customRules = null)
        {
            if (keys == null || keys.Count == 0) return null;

            string result = null;
            if (method == InputMethod.TuBinhTran)
            {
                result = ProcessTuBinhTran(keys, modernTone);
            }
            else if (method == InputMethod.Custom)
            {
                result = ProcessCustomInputMethod(keys, customRules, modernTone);
            }
            else
            {
                result = ProcessTelexOrVni(keys, method, modernTone);
            }

            if (!string.IsNullOrEmpty(result))
            {
                return EnforceCasingConsistency(result, keys);
            }

            return null;
        }

        public static string TransformWord(List<char> keys, InputMethod method, bool modernTone)
        {
            return TransformWord(keys, method, modernTone, null);
        }

        private static string EnforceCasingConsistency(string transformedWord, List<char> originalKeys)
        {
            if (string.IsNullOrEmpty(transformedWord) || originalKeys == null || originalKeys.Count == 0)
                return transformedWord;

            // Nếu hệ thống đang bật CapsLock: Toàn bộ từ tiếng Việt luôn được viết HOA đồng bộ
            if (IsCapsLockActive())
            {
                return transformedWord.ToUpper();
            }

            // 1. Đếm số lượng chữ cái và số chữ cái viết hoa trong keystrokes gốc
            int letterCount = 0;
            int upperCount = 0;
            for (int i = 0; i < originalKeys.Count; i++)
            {
                char k = originalKeys[i];
                if (char.IsLetter(k))
                {
                    letterCount++;
                    if (char.IsUpper(k)) upperCount++;
                }
                else if (k == '^' || k == '&' || k == '*' || k == '(' || k == '{' || k == '}')
                {
                    // Trong Tư Bình Trần, các phím ký hiệu Shift đại diện cho nguyên âm viết HOA (^=Â, &=Ê, *=Ô, (=Ă, {=Ư, }=Ơ)
                    letterCount++;
                    upperCount++;
                }
            }

            // 2. Kiểm tra xem chữ cái đầu tiên có phải là chữ HOA không (cho TitleCase)
            bool firstLetterIsUpper = false;
            for (int i = 0; i < originalKeys.Count; i++)
            {
                char k = originalKeys[i];
                if (char.IsLetter(k))
                {
                    firstLetterIsUpper = char.IsUpper(k);
                    break;
                }
                else if (k == '^' || k == '&' || k == '*' || k == '(' || k == '{' || k == '}')
                {
                    firstLetterIsUpper = true;
                    break;
                }
            }

            // 3. Nếu người dùng gõ dạng TitleCase: CHỈ CÓ 1 chữ cái HOA duy nhất và đó là chữ cái đầu tiên
            // (ví dụ: Tr7n, Duowng, Tieets, T81 -> Tốt, T8 -> Tô, Dd[]ng, *n2 -> Ồn, (n -> Ăn, ^n -> Ân):
            if (firstLetterIsUpper && upperCount == 1)
            {
                if (transformedWord.Length <= 1) return transformedWord.ToUpper();
                return char.ToUpper(transformedWord[0]) + transformedWord.Substring(1).ToLower();
            }

            // 4. Nếu người dùng gõ toàn bộ chữ cái HOA (ví dụ: TR7N, DUOWNG, C6N, TIEETS, DD[]NG):
            // Kết quả BẮT BUỘC phải là VIẾT HOA TOÀN BỘ (All-Caps)!
            if (letterCount > 0 && letterCount == upperCount)
            {
                return transformedWord.ToUpper();
            }

            // 5. Nếu người dùng gõ toàn bộ chữ cái thường:
            // Kết quả BẮT BUỘC là viết thường toàn bộ!
            if (letterCount > 0 && upperCount == 0)
            {
                return transformedWord.ToLower();
            }

            return transformedWord;
        }

        private static bool IsAllLettersUpper(StringBuilder sb)
        {
            if (sb == null || sb.Length == 0) return false;
            int letterCount = 0;
            for (int i = 0; i < sb.Length; i++)
            {
                char ch = sb[i];
                if (char.IsLetter(ch))
                {
                    letterCount++;
                    if (!char.IsUpper(ch)) return false;
                }
            }
            return letterCount > 0;
        }

        private static string ProcessTuBinhTran(List<char> keys, bool modernTone)
        {
            if (keys.Count == 0) return null;

            bool modified = false;
            StringBuilder sb = new StringBuilder();
            int tone = 0; // 0: không dấu, 1: sắc, 2: huyền, 3: hỏi, 4: ngã, 5: nặng
            char lastRawKey = '\0';
            bool wasStandaloneAtStart = false;

            for (int i = 0; i < keys.Count; i++)
            {
                char c = keys[i];
                char lower = char.ToLower(c);

                // 1. Toggle phím số / ký hiệu: nếu gõ lặp lại cùng phím (66->6, 77->7, 88->8, 99->9, [[->[, ]]->]), phục hồi thành đúng 1 ký tự gốc chuẩn OpenKey C++!
                if (c == lastRawKey && (c == '6' || c == '7' || c == '8' || c == '9' || c == '[' || c == ']' ||
                                        c == '^' || c == '&' || c == '*' || c == '(' || c == '{' || c == '}'))
                {
                    if (sb.Length > 0)
                    {
                        sb[sb.Length - 1] = c;
                        lastRawKey = '\0';
                        wasStandaloneAtStart = false;
                        modified = true;
                        continue;
                    }
                }

                // 2. Dấu thanh: 1..5 (chỉ khi sb đã có nguyên âm)
                bool hasVowelSoFar = HasAnyVowel(sb.ToString());

                if (hasVowelSoFar && (c == '1' || c == '2' || c == '3' || c == '4' || c == '5'))
                {
                    int targetTone = c - '0';
                    // Toggle dấu: gõ lặp lại cùng phím dấu thì xóa dấu
                    tone = (tone == targetTone ? 0 : targetTone);
                    lastRawKey = c;
                    wasStandaloneAtStart = false;
                    modified = true;
                    continue;
                }

                // 3. Nếu ký tự trước được chèn dạng Standalone (6->â, 7->ê, 8->ô, 9->ă) và ký tự hiện tại là một số khác (ví dụ gõ 67, 60, 78)
                // Phục hồi ký tự standalone đó thành số gốc và thêm số thứ hai (mô phỏng restoreTBTStandaloneThenInsertRaw của OpenKey C++)
                if (wasStandaloneAtStart && char.IsDigit(c))
                {
                    if (sb.Length > 0)
                    {
                        sb[sb.Length - 1] = lastRawKey;
                        sb.Append(c);
                        wasStandaloneAtStart = false;
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                }

                // Kiểm tra xem ký tự trước đó có phải là phụ âm tiếng Việt không (Consonant Context)
                // C++ OpenKey: IS_CONSONANT || (key == KEY_I && prev == KEY_G) || (key == KEY_U && prev == KEY_Q)
                bool prevIsConsonant = sb.Length > 0 && IsConsonant(sb[sb.Length - 1]);
                bool prevIsGi = sb.Length >= 2 && char.ToLower(sb[sb.Length - 1]) == 'i' && char.ToLower(sb[sb.Length - 2]) == 'g';
                bool prevIsQu = sb.Length >= 2 && char.ToLower(sb[sb.Length - 1]) == 'u' && char.ToLower(sb[sb.Length - 2]) == 'q';
                bool isConsonantContext = prevIsConsonant || prevIsGi || prevIsQu;

                // Chỉ tính là số thuần nếu phím trước là chữ số NHƯNG KHÔNG PHẢI phím dấu thanh (1..5)
                bool prevWasToneKey = i > 0 && (keys[i - 1] >= '1' && keys[i - 1] <= '5');
                bool prevWasPureDigit = i > 0 && char.IsDigit(keys[i - 1]) && !prevWasToneKey;

                if (c == '0' || lower == 'z')
                {
                    if (tone > 0)
                    {
                        tone = 0;
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        modified = true;
                        continue;
                    }
                }

                // Xử lý phím 'd' / 'D' tạo chữ 'đ' / 'Đ' trong Tư Bình Trần:
                // 1) dd -> đ ở đầu từ hoặc liền sau 'd' (ví dụ: "dd" -> "đ", "ddo1" -> "đó", "dd[]2ng" -> "đường")
                // 2) d ở cuối từ / sau nguyên âm: khi từ đã có 'd'/'D' chưa có gạch ngang (ví dụ: "do1d" -> "đó", "d[]2ngd" -> "đường", "d6nd" -> "đân")
                // 3) Phục hồi đ -> dd khi lặp lại (toggle)
                if (lower == 'd' && sb.Length > 0)
                {
                    if (char.ToLower(sb[sb.Length - 1]) == 'd')
                    {
                        bool isUpper = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(c) || IsCapsLockActive();
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append(isUpper ? 'Đ' : 'đ');
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        modified = true;
                        continue;
                    }

                    int dIdx = -1;
                    for (int j = 0; j < sb.Length; j++)
                    {
                        if (sb[j] == 'd' || sb[j] == 'D')
                        {
                            dIdx = j;
                            break;
                        }
                    }

                    if (dIdx >= 0 && hasVowelSoFar)
                    {
                        bool isUpper = (sb[dIdx] == 'D') || char.IsUpper(c) || IsCapsLockActive();
                        sb[dIdx] = isUpper ? 'Đ' : 'đ';
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        modified = true;
                        continue;
                    }

                    int dDauIdx = -1;
                    for (int j = 0; j < sb.Length; j++)
                    {
                        if (sb[j] == 'đ' || sb[j] == 'Đ')
                        {
                            dDauIdx = j;
                            break;
                        }
                    }

                    if (dDauIdx >= 0 && hasVowelSoFar)
                    {
                        sb[dDauIdx] = (sb[dDauIdx] == 'Đ') ? 'D' : 'd';
                        sb.Append(c);
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        modified = true;
                        continue;
                    }
                }

                // [ -> ư, { -> Ư
                if (c == '[' || c == '{')
                {
                    bool isUpper = (c == '{') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0 && (sb[sb.Length - 1] == 'ơ' || sb[sb.Length - 1] == 'Ơ'))
                    {
                        bool wasUpper = char.IsUpper(sb[sb.Length - 1]) || isUpper;
                        sb.Remove(sb.Length - 1, 1);
                        bool allUpper = IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                        if (allUpper) sb.Append("ƯƠ");
                        else if (wasUpper) sb.Append("Ươ");
                        else sb.Append("ươ");
                    }
                    else if (sb.Length > 0 && (sb[sb.Length - 1] == 'u' || sb[sb.Length - 1] == 'U'))
                    {
                        // u + [ -> ư
                        bool wasUpper = char.IsUpper(sb[sb.Length - 1]) || isUpper;
                        sb[sb.Length - 1] = wasUpper ? 'Ư' : 'ư';
                    }
                    else
                    {
                        sb.Append(isUpper ? 'Ư' : 'ư');
                    }
                    lastRawKey = c;
                    wasStandaloneAtStart = false;
                    modified = true;
                    continue;
                }

                // ] -> ơ, } -> Ơ
                if (c == ']' || c == '}')
                {
                    bool isUpper = (c == '}') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0 && (sb[sb.Length - 1] == 'ư' || sb[sb.Length - 1] == 'Ư'))
                    {
                        bool wasUpper = char.IsUpper(sb[sb.Length - 1]) || isUpper;
                        sb.Remove(sb.Length - 1, 1);
                        bool allUpper = IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                        if (allUpper) sb.Append("ƯƠ");
                        else if (wasUpper) sb.Append("Ươ");
                        else sb.Append("ươ");
                    }
                    else if (sb.Length > 0 && (sb[sb.Length - 1] == 'u' || sb[sb.Length - 1] == 'U'))
                    {
                        // u + ] -> ươ (ví dụ thu]ng -> thương, ngu]i -> người)
                        bool wasUpper = char.IsUpper(sb[sb.Length - 1]);
                        sb.Remove(sb.Length - 1, 1);
                        bool allUpper = IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                        if (allUpper) sb.Append("ƯƠ");
                        else if (wasUpper) sb.Append("Ươ");
                        else sb.Append("ươ");
                    }
                    else if (sb.Length > 0 && (sb[sb.Length - 1] == 'o' || sb[sb.Length - 1] == 'O'))
                    {
                        // o + ] -> ơ
                        bool wasUpper = char.IsUpper(sb[sb.Length - 1]) || isUpper;
                        sb[sb.Length - 1] = wasUpper ? 'Ơ' : 'ơ';
                    }
                    else
                    {
                        sb.Append(isUpper ? 'Ơ' : 'ơ');
                    }
                    lastRawKey = c;
                    wasStandaloneAtStart = false;
                    modified = true;
                    continue;
                }

                // 6 hoặc ^ (Shift+6): â / Â (ví dụ "a6" -> "â", "u6" -> "uâ", "c6n" -> "cân", "6n" -> "ân", "^n" -> "Ân", "gi6c" -> "giấc")
                if (c == '6' || c == '^')
                {
                    if (prevWasPureDigit)
                    {
                        sb.Append(c);
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        continue;
                    }

                    bool isUpperTarget = (c == '^') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0)
                    {
                        char pChar = sb[sb.Length - 1];
                        char pLower = char.ToLower(pChar);
                        bool pUpper = char.IsUpper(pChar);

                        // a + 6 -> â
                        if (pLower == 'a')
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append((pUpper || isUpperTarget) ? 'Â' : 'â');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                        // u + 6 -> uâ (chu6n -> chuẩn, xu6n -> xuân, qu6n -> quân, hu6n -> huân, khu6n -> khuân)
                        if (pLower == 'u')
                        {
                            sb.Append((pUpper || isUpperTarget) ? 'Â' : 'â');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                    }

                    if (sb.Length == 0 || isConsonantContext)
                    {
                        bool isStart = (sb.Length == 0);
                        sb.Append(isUpperTarget ? 'Â' : 'â');
                        lastRawKey = c;
                        wasStandaloneAtStart = isStart;
                        modified = true;
                        continue;
                    }
                }

                // 7 hoặc & (Shift+7): ê / Ê (ví dụ "i7" -> "iê", "y7" -> "yê", "e7" -> "ê", "tr7n" -> "trên", "7m" -> "êm", "&-đê" -> "Ê-đê")
                if (c == '7' || c == '&')
                {
                    if (prevWasPureDigit)
                    {
                        sb.Append(c);
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        continue;
                    }

                    bool isUpperTarget = (c == '&') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0)
                    {
                        char pChar = sb[sb.Length - 1];
                        char pLower = char.ToLower(pChar);
                        bool pUpper = char.IsUpper(pChar);

                        // e + 7 -> ê
                        if (pLower == 'e')
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append((pUpper || isUpperTarget) ? 'Ê' : 'ê');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                        // i + 7 -> iê (ti7t -> tiết, si7ng -> siêng, chi7c -> chiếc, ki7m -> kiệm)
                        if (pLower == 'i' && !prevIsGi)
                        {
                            sb.Append((pUpper || isUpperTarget) ? 'Ê' : 'ê');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                        // y + 7 -> yê (y7u -> yêu, chuy7n -> chuyện, khuy7n -> khuyên)
                        if (pLower == 'y')
                        {
                            sb.Append((pUpper || isUpperTarget) ? 'Ê' : 'ê');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                    }

                    if (sb.Length == 0 || isConsonantContext)
                    {
                        bool isStart = (sb.Length == 0);
                        sb.Append(isUpperTarget ? 'Ê' : 'ê');
                        lastRawKey = c;
                        wasStandaloneAtStart = isStart;
                        modified = true;
                        continue;
                    }
                }

                // 8 hoặc * (Shift+8): ô / Ô (ví dụ "u8" -> "uô", "o8" -> "ô", "c8ng" -> "công", "8m" -> "ôm", "*n" -> "Ôn", "*n2" -> "Ồn")
                if (c == '8' || c == '*')
                {
                    if (prevWasPureDigit)
                    {
                        sb.Append(c);
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        continue;
                    }

                    bool isUpperTarget = (c == '*') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0)
                    {
                        char pChar = sb[sb.Length - 1];
                        char pLower = char.ToLower(pChar);
                        bool pUpper = char.IsUpper(pChar);

                        // o + 8 -> ô
                        if (pLower == 'o')
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append((pUpper || isUpperTarget) ? 'Ô' : 'ô');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                        // u + 8 -> uô (u8ng -> uống, tu8i -> tuổi, thu8c -> thuộc, gu8c -> guốc, lu8n -> luôn)
                        if (pLower == 'u' && !prevIsQu)
                        {
                            sb.Append((pUpper || isUpperTarget) ? 'Ô' : 'ô');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                    }

                    if (sb.Length == 0 || isConsonantContext)
                    {
                        bool isStart = (sb.Length == 0);
                        sb.Append(isUpperTarget ? 'Ô' : 'ô');
                        lastRawKey = c;
                        wasStandaloneAtStart = isStart;
                        modified = true;
                        continue;
                    }
                }

                // 9 hoặc ( (Shift+9): ă / Ă (ví dụ "o9" -> "oă", "u9" -> "uă", "a9" -> "ă", "c9n" -> "căn", "9n" -> "ăn", "(n" -> "Ăn")
                if (c == '9' || c == '(')
                {
                    if (prevWasPureDigit)
                    {
                        sb.Append(c);
                        lastRawKey = c;
                        wasStandaloneAtStart = false;
                        continue;
                    }

                    bool isUpperTarget = (c == '(') || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                    if (sb.Length > 0)
                    {
                        char pChar = sb[sb.Length - 1];
                        char pLower = char.ToLower(pChar);
                        bool pUpper = char.IsUpper(pChar);

                        // a + 9 -> ă
                        if (pLower == 'a')
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append((pUpper || isUpperTarget) ? 'Ă' : 'ă');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                        // o + 9 -> oă (ho9c -> hoặc), u + 9 -> uă
                        if (pLower == 'o' || (pLower == 'u' && !prevIsQu))
                        {
                            sb.Append((pUpper || isUpperTarget) ? 'Ă' : 'ă');
                            lastRawKey = c;
                            wasStandaloneAtStart = false;
                            modified = true;
                            continue;
                        }
                    }

                    if (sb.Length == 0 || isConsonantContext)
                    {
                        bool isStart = (sb.Length == 0);
                        sb.Append(isUpperTarget ? 'Ă' : 'ă');
                        lastRawKey = c;
                        wasStandaloneAtStart = isStart;
                        modified = true;
                        continue;
                    }
                }

                sb.Append(c);
                wasStandaloneAtStart = false;
                lastRawKey = c;
            }

            // Tự động chuẩn hóa 'ưo' thành 'ươ'
            FixUoToUo(sb);

            if (tone > 0)
            {
                string withTone = ApplyToneMark(sb.ToString(), tone, modernTone);
                if (withTone != sb.ToString())
                {
                    return withTone;
                }
            }

            return modified ? sb.ToString() : null;
        }

        private static void FixUoToUo(StringBuilder sb)
        {
            if (sb == null || sb.Length < 2) return;
            for (int k = 0; k < sb.Length - 1; k++)
            {
                char c1 = sb[k];
                char c2 = sb[k + 1];
                if ((c1 == 'ư' || c1 == 'Ư') && (c2 == 'o' || c2 == 'O'))
                {
                    bool isUpper2 = char.IsUpper(c2);
                    sb[k + 1] = isUpper2 ? 'Ơ' : 'ơ';
                }
            }
        }

        private static string ProcessCustomInputMethod(List<char> keys, List<CustomInputRule> rules, bool modernTone)
        {
            if (keys == null || keys.Count == 0) return null;
            if (rules == null || rules.Count == 0) return null;

            bool modified = false;
            StringBuilder sb = new StringBuilder();
            int tone = 0;
            char lastRawKey = '\0';

            for (int i = 0; i < keys.Count; i++)
            {
                char c = keys[i];
                char lower = char.ToLower(c);

                CustomInputRule matched = null;
                for (int r = 0; r < rules.Count; r++)
                {
                    if (rules[r].Key == c)
                    {
                        matched = rules[r];
                        break;
                    }
                }
                if (matched == null)
                {
                    for (int r = 0; r < rules.Count; r++)
                    {
                        if (char.ToLower(rules[r].Key) == lower)
                        {
                            matched = rules[r];
                            break;
                        }
                    }
                }

                if (matched == null)
                {
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                int action = matched.Action;

                if (c == lastRawKey && action >= 19 && action <= 32)
                {
                    if (sb.Length > 0)
                    {
                        sb[sb.Length - 1] = c;
                        lastRawKey = '\0';
                        modified = true;
                        continue;
                    }
                }

                bool hasVowelSoFar = HasAnyVowel(sb.ToString());
                bool isCaps = IsCapsLockActive() || char.IsUpper(c) || (sb.Length > 1 && IsAllLettersUpper(sb));

                if (action == 0)
                {
                    if (tone > 0)
                    {
                        tone = 0;
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action >= 1 && action <= 5)
                {
                    if (hasVowelSoFar)
                    {
                        int targetTone = action;
                        tone = (tone == targetTone ? 0 : targetTone);
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 6)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'a' || kLower == 'e' || kLower == 'o' || kLower == 'â' || kLower == 'ê' || kLower == 'ô')
                        {
                            vIdx = k;
                            break;
                        }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        char prevLower = char.ToLower(prev);
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        if (prevLower == 'a') { sb[vIdx] = prevUpper ? 'Â' : 'â'; modified = true; }
                        else if (prevLower == 'e') { sb[vIdx] = prevUpper ? 'Ê' : 'ê'; modified = true; }
                        else if (prevLower == 'o') { sb[vIdx] = prevUpper ? 'Ô' : 'ô'; modified = true; }
                        else if (prevLower == 'â') { sb[vIdx] = prevUpper ? 'A' : 'a'; modified = true; }
                        else if (prevLower == 'ê') { sb[vIdx] = prevUpper ? 'E' : 'e'; modified = true; }
                        else if (prevLower == 'ô') { sb[vIdx] = prevUpper ? 'O' : 'o'; modified = true; }
                        lastRawKey = c;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 7)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'a' || kLower == 'â') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'a') ? (prevUpper ? 'Â' : 'â') : (prevUpper ? 'A' : 'a');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 8)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'e' || kLower == 'ê') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'e') ? (prevUpper ? 'Ê' : 'ê') : (prevUpper ? 'E' : 'e');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 9)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'o' || kLower == 'ô') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'o') ? (prevUpper ? 'Ô' : 'ô') : (prevUpper ? 'O' : 'o');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 10 || action == 11)
                {
                    bool applied = false;
                    if (sb.Length >= 2)
                    {
                        char c1 = sb[sb.Length - 2];
                        char c2 = sb[sb.Length - 1];
                        if (char.ToLower(c1) == 'u' && char.ToLower(c2) == 'o')
                        {
                            bool uUp = char.IsUpper(c1) || isCaps;
                            bool oUp = char.IsUpper(c2) || isCaps;
                            sb[sb.Length - 2] = uUp ? 'Ư' : 'ư';
                            sb[sb.Length - 1] = oUp ? 'Ơ' : 'ơ';
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }
                    }
                    if (action == 10 && sb.Length > 0)
                    {
                        for (int k = sb.Length - 1; k >= 0; k--)
                        {
                            char kLower = char.ToLower(sb[k]);
                            bool kUp = char.IsUpper(sb[k]) || isCaps;
                            if (kLower == 'a') { sb[k] = kUp ? 'Ă' : 'ă'; applied = true; break; }
                            if (kLower == 'u') { sb[k] = kUp ? 'Ư' : 'ư'; applied = true; break; }
                            if (kLower == 'o') { sb[k] = kUp ? 'Ơ' : 'ơ'; applied = true; break; }
                        }
                    }
                    if (applied)
                    {
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 12)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'u' || kLower == 'ư') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'u') ? (prevUpper ? 'Ư' : 'ư') : (prevUpper ? 'U' : 'u');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 13)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'o' || kLower == 'ơ') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'o') ? (prevUpper ? 'Ơ' : 'ơ') : (prevUpper ? 'O' : 'o');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 14)
                {
                    int vIdx = -1;
                    for (int k = sb.Length - 1; k >= 0; k--)
                    {
                        char kLower = char.ToLower(sb[k]);
                        if (kLower == 'a' || kLower == 'ă') { vIdx = k; break; }
                    }
                    if (vIdx >= 0)
                    {
                        char prev = sb[vIdx];
                        bool prevUpper = char.IsUpper(prev) || isCaps;
                        sb[vIdx] = (char.ToLower(prev) == 'a') ? (prevUpper ? 'Ă' : 'ă') : (prevUpper ? 'A' : 'a');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 15)
                {
                    if (sb.Length > 0)
                    {
                        if (char.ToLower(sb[sb.Length - 1]) == 'd')
                        {
                            bool isUp = char.IsUpper(sb[sb.Length - 1]) || isCaps;
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(isUp ? 'Đ' : 'đ');
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }

                        int dIdx = -1;
                        for (int j = 0; j < sb.Length; j++)
                        {
                            if (sb[j] == 'd' || sb[j] == 'D') { dIdx = j; break; }
                        }
                        if (dIdx >= 0 && hasVowelSoFar)
                        {
                            bool isUp = (sb[dIdx] == 'D') || isCaps;
                            sb[dIdx] = isUp ? 'Đ' : 'đ';
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }

                        int dDauIdx = -1;
                        for (int j = 0; j < sb.Length; j++)
                        {
                            if (sb[j] == 'đ' || sb[j] == 'Đ') { dDauIdx = j; break; }
                        }
                        if (dDauIdx >= 0 && hasVowelSoFar)
                        {
                            sb[dDauIdx] = (sb[dDauIdx] == 'Đ') ? 'D' : 'd';
                            sb.Append(c);
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }
                    }
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                if (action == 16 || action == 17)
                {
                    if (action == 17 && sb.Length == 0)
                    {
                        sb.Append(c);
                        lastRawKey = c;
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        char prev = sb[sb.Length - 1];
                        char prevLower = char.ToLower(prev);
                        bool prevUp = char.IsUpper(prev) || isCaps;

                        if (prevLower == 'a')
                        {
                            if (sb.Length >= 2 && char.ToLower(sb[sb.Length - 2]) == 'u')
                            {
                                bool uUp = char.IsUpper(sb[sb.Length - 2]) || isCaps;
                                sb[sb.Length - 2] = uUp ? 'Ư' : 'ư';
                                sb[sb.Length - 1] = prevUp ? 'A' : 'a';
                                lastRawKey = c;
                                modified = true;
                                continue;
                            }
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(prevUp ? 'Ă' : 'ă');
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }
                        if (prevLower == 'o')
                        {
                            if (sb.Length >= 2 && char.ToLower(sb[sb.Length - 2]) == 'u')
                            {
                                bool uUp = char.IsUpper(sb[sb.Length - 2]) || isCaps;
                                sb[sb.Length - 2] = uUp ? 'Ư' : 'ư';
                                sb.Remove(sb.Length - 1, 1);
                                sb.Append((uUp && prevUp) ? 'Ơ' : 'ơ');
                                lastRawKey = c;
                                modified = true;
                                continue;
                            }
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(prevUp ? 'Ơ' : 'ơ');
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }
                        if (prevLower == 'u')
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(prevUp ? 'Ư' : 'ư');
                            lastRawKey = c;
                            modified = true;
                            continue;
                        }
                    }

                    sb.Append(isCaps ? 'Ư' : 'ư');
                    lastRawKey = c;
                    modified = true;
                    continue;
                }

                if (action == 18)
                {
                    sb.Append(c);
                    lastRawKey = c;
                    continue;
                }

                char charToAdd = '\0';
                switch (action)
                {
                    case 19: charToAdd = 'ă'; break;
                    case 20: charToAdd = 'Ă'; break;
                    case 21: charToAdd = 'â'; break;
                    case 22: charToAdd = 'Â'; break;
                    case 23: charToAdd = 'đ'; break;
                    case 24: charToAdd = 'Đ'; break;
                    case 25: charToAdd = 'ê'; break;
                    case 26: charToAdd = 'Ê'; break;
                    case 27: charToAdd = 'ô'; break;
                    case 28: charToAdd = 'Ô'; break;
                    case 29: charToAdd = 'ơ'; break;
                    case 30: charToAdd = 'Ơ'; break;
                    case 31: charToAdd = 'ư'; break;
                    case 32: charToAdd = 'Ư'; break;
                }

                if (charToAdd != '\0')
                {
                    if ((charToAdd == 'ơ' || charToAdd == 'Ơ') && sb.Length > 0 && (sb[sb.Length - 1] == 'ư' || sb[sb.Length - 1] == 'Ư'))
                    {
                        bool uUp = char.IsUpper(sb[sb.Length - 1]);
                        bool oUp = char.IsUpper(charToAdd) || isCaps;
                        sb.Remove(sb.Length - 1, 1);
                        if (uUp && oUp) sb.Append("ƯƠ");
                        else if (uUp) sb.Append("Ươ");
                        else sb.Append("ươ");
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if ((charToAdd == 'ư' || charToAdd == 'Ư') && sb.Length > 0 && (sb[sb.Length - 1] == 'ơ' || sb[sb.Length - 1] == 'Ơ'))
                    {
                        bool oUp = char.IsUpper(sb[sb.Length - 1]);
                        bool uUp = char.IsUpper(charToAdd) || isCaps;
                        sb.Remove(sb.Length - 1, 1);
                        if (uUp && oUp) sb.Append("ƯƠ");
                        else if (uUp) sb.Append("Ươ");
                        else sb.Append("ươ");
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if ((charToAdd == 'ư' || charToAdd == 'Ư') && sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'u')
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = wasUp ? 'Ư' : 'ư';
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if ((charToAdd == 'ơ' || charToAdd == 'Ơ') && sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'o')
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = wasUp ? 'Ơ' : 'ơ';
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if (sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'a' && (charToAdd == 'ă' || charToAdd == 'â'))
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = (charToAdd == 'â') ? (wasUp ? 'Â' : 'â') : (wasUp ? 'Ă' : 'ă');
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if (sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'e' && (charToAdd == 'ê'))
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = wasUp ? 'Ê' : 'ê';
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if (sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'o' && (charToAdd == 'ô'))
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = wasUp ? 'Ô' : 'ô';
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }
                    else if (sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'd' && (charToAdd == 'đ' || charToAdd == 'Đ'))
                    {
                        bool wasUp = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(charToAdd) || isCaps;
                        sb[sb.Length - 1] = wasUp ? 'Đ' : 'đ';
                        lastRawKey = c;
                        modified = true;
                        continue;
                    }

                    sb.Append(charToAdd);
                    lastRawKey = c;
                    modified = true;
                    continue;
                }

                sb.Append(c);
                lastRawKey = c;
            }

            FixUoToUo(sb);

            if (tone > 0)
            {
                string withTone = ApplyToneMark(sb.ToString(), tone, modernTone);
                if (withTone != sb.ToString())
                {
                    return withTone;
                }
            }

            return modified ? sb.ToString() : null;
        }

        private static bool IsConsonant(char c)
        {
            return char.IsLetter(c) && !IsVowel(c);
        }

        private static string ProcessTelexOrVni(List<char> keys, InputMethod method, bool modernTone)
        {
            if (keys.Count == 0) return null;

            bool modified = false;
            StringBuilder sb = new StringBuilder();
            int tone = 0;

            for (int i = 0; i < keys.Count; i++)
            {
                char c = keys[i];
                char lower = char.ToLower(c);
                bool hasVowelSoFar = HasAnyVowel(sb.ToString());

                if (method == InputMethod.Telex || method == InputMethod.SimpleTelex)
                {
                    // FIX TRIỆT ĐỂ: Dấu thanh (s, f, r, x, j) CHỈ có hiệu lực khi đã có NGUYÊN ÂM!
                    if (hasVowelSoFar)
                    {
                        if (lower == 's') { tone = (tone == 1 ? 0 : 1); modified = true; continue; }
                        if (lower == 'f') { tone = (tone == 2 ? 0 : 2); modified = true; continue; }
                        if (lower == 'r') { tone = (tone == 3 ? 0 : 3); modified = true; continue; }
                        if (lower == 'x') { tone = (tone == 4 ? 0 : 4); modified = true; continue; }
                        if (lower == 'j') { tone = (tone == 5 ? 0 : 5); modified = true; continue; }
                        if (lower == 'z') { tone = 0; modified = true; continue; }
                    }

                    // Xử lý phím 'd' / 'D' tạo chữ 'đ' / 'Đ' trong Telex:
                    // 1) dd -> đ ở đầu từ hoặc liền sau 'd'
                    // 2) d ở cuối từ / sau nguyên âm: khi từ đã có 'd'/'D' (ví dụ: "dường" + 'd' -> "đường", "dó" + 'd' -> "đó")
                    // 3) Phục hồi đ -> dd khi lặp lại (toggle)
                    if (lower == 'd' && sb.Length > 0)
                    {
                        if (char.ToLower(sb[sb.Length - 1]) == 'd')
                        {
                            bool isUpper = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(c) || IsCapsLockActive();
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(isUpper ? 'Đ' : 'đ');
                            modified = true;
                            continue;
                        }

                        int dIdx = -1;
                        for (int j = 0; j < sb.Length; j++)
                        {
                            if (sb[j] == 'd' || sb[j] == 'D')
                            {
                                dIdx = j;
                                break;
                            }
                        }

                        if (dIdx >= 0 && hasVowelSoFar)
                        {
                            bool isUpper = (sb[dIdx] == 'D') || char.IsUpper(c) || IsCapsLockActive();
                            sb[dIdx] = isUpper ? 'Đ' : 'đ';
                            modified = true;
                            continue;
                        }

                        int dDauIdx = -1;
                        for (int j = 0; j < sb.Length; j++)
                        {
                            if (sb[j] == 'đ' || sb[j] == 'Đ')
                            {
                                dDauIdx = j;
                                break;
                            }
                        }

                        if (dDauIdx >= 0 && hasVowelSoFar)
                        {
                            sb[dDauIdx] = (sb[dDauIdx] == 'Đ') ? 'D' : 'd';
                            sb.Append(c);
                            modified = true;
                            continue;
                        }
                    }
                    // aa -> â
                    if (lower == 'a' && sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'a')
                    {
                        bool isUpper = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(c) || IsCapsLockActive();
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append(isUpper ? 'Â' : 'â');
                        modified = true;
                        continue;
                    }
                    // ee -> ê
                    if (lower == 'e' && sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'e')
                    {
                        bool isUpper = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(c) || IsCapsLockActive();
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append(isUpper ? 'Ê' : 'ê');
                        modified = true;
                        continue;
                    }
                    // oo -> ô
                    if (lower == 'o' && sb.Length > 0 && char.ToLower(sb[sb.Length - 1]) == 'o')
                    {
                        bool isUpper = char.IsUpper(sb[sb.Length - 1]) || char.IsUpper(c) || IsCapsLockActive();
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append(isUpper ? 'Ô' : 'ô');
                        modified = true;
                        continue;
                    }
                    // w -> ư / aw -> ă / ow -> ơ
                    if (lower == 'w')
                    {
                        bool isCaps = IsCapsLockActive();
                        if (sb.Length > 0)
                        {
                            char prev = sb[sb.Length - 1];
                            char prevLower = char.ToLower(prev);
                            bool isUpper = char.IsUpper(prev) || char.IsUpper(c) || isCaps;
                            if (prevLower == 'a')
                            {
                                // Nếu trước 'a' là 'u' (ví dụ "ua" + w -> "ưa")
                                if (sb.Length >= 2 && char.ToLower(sb[sb.Length - 2]) == 'u')
                                {
                                    bool uUpper = char.IsUpper(sb[sb.Length - 2]) || isCaps;
                                    sb[sb.Length - 2] = uUpper ? 'Ư' : 'ư';
                                    sb[sb.Length - 1] = isUpper ? 'A' : 'a';
                                    modified = true;
                                    continue;
                                }
                                sb.Remove(sb.Length - 1, 1);
                                sb.Append(isUpper ? 'Ă' : 'ă');
                                modified = true;
                                continue;
                            }
                            if (prevLower == 'o')
                            {
                                // Nếu trước 'o' là 'u' (ví dụ "uo" + w -> "ươ")
                                if (sb.Length >= 2 && char.ToLower(sb[sb.Length - 2]) == 'u')
                                {
                                    bool uUpper = char.IsUpper(sb[sb.Length - 2]) || isCaps;
                                    bool allUpper = isCaps || (sb.Length > 2 && IsAllLettersUpper(sb));
                                    sb[sb.Length - 2] = uUpper ? 'Ư' : 'ư';
                                    sb.Remove(sb.Length - 1, 1);
                                    sb.Append((allUpper || (uUpper && isUpper)) ? 'Ơ' : 'ơ');
                                    modified = true;
                                    continue;
                                }
                                sb.Remove(sb.Length - 1, 1);
                                sb.Append(isUpper ? 'Ơ' : 'ơ');
                                modified = true;
                                continue;
                            }
                            if (prevLower == 'u')
                            {
                                sb.Remove(sb.Length - 1, 1);
                                sb.Append(isUpper ? 'Ư' : 'ư');
                                modified = true;
                                continue;
                            }
                        }
                        sb.Append((char.IsUpper(c) || isCaps) ? 'Ư' : 'ư');
                        modified = true;
                        continue;
                    }
                }
                else if (method == InputMethod.Vni)
                {
                    // VNI: 1..5 dấu thanh (chỉ khi đã có nguyên âm)
                    if (hasVowelSoFar)
                    {
                        if (c == '1') { tone = (tone == 1 ? 0 : 1); modified = true; continue; }
                        if (c == '2') { tone = (tone == 2 ? 0 : 2); modified = true; continue; }
                        if (c == '3') { tone = (tone == 3 ? 0 : 3); modified = true; continue; }
                        if (c == '4') { tone = (tone == 4 ? 0 : 4); modified = true; continue; }
                        if (c == '5') { tone = (tone == 5 ? 0 : 5); modified = true; continue; }
                        if (c == '0') { tone = 0; modified = true; continue; }
                    }

                    // 6: mũ a, e, o
                    if (c == '6' && sb.Length > 0)
                    {
                        char prev = sb[sb.Length - 1];
                        char prevLower = char.ToLower(prev);
                        bool isUpper = char.IsUpper(prev) || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                        if (prevLower == 'a') { sb.Remove(sb.Length - 1, 1); sb.Append(isUpper ? 'Â' : 'â'); modified = true; continue; }
                        if (prevLower == 'e') { sb.Remove(sb.Length - 1, 1); sb.Append(isUpper ? 'Ê' : 'ê'); modified = true; continue; }
                        if (prevLower == 'o') { sb.Remove(sb.Length - 1, 1); sb.Append(isUpper ? 'Ô' : 'ô'); modified = true; continue; }
                    }
                    // 7: móc o, u
                    if (c == '7' && sb.Length > 0)
                    {
                        char prev = sb[sb.Length - 1];
                        char prevLower = char.ToLower(prev);
                        bool isUpper = char.IsUpper(prev) || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                        if (prevLower == 'o')
                        {
                            if (sb.Length >= 2 && char.ToLower(sb[sb.Length - 2]) == 'u')
                            {
                                bool uUpper = char.IsUpper(sb[sb.Length - 2]) || IsCapsLockActive();
                                bool allUpper = IsCapsLockActive() || (sb.Length > 2 && IsAllLettersUpper(sb));
                                sb[sb.Length - 2] = uUpper ? 'Ư' : 'ư';
                                sb.Remove(sb.Length - 1, 1);
                                sb.Append((allUpper || (uUpper && isUpper)) ? 'Ơ' : 'ơ');
                                modified = true;
                                continue;
                            }
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(isUpper ? 'Ơ' : 'ơ');
                            modified = true;
                            continue;
                        }
                        if (prevLower == 'u') { sb.Remove(sb.Length - 1, 1); sb.Append(isUpper ? 'Ư' : 'ư'); modified = true; continue; }
                    }
                    // 8: trăng a -> ă
                    if (c == '8' && sb.Length > 0)
                    {
                        char prev = sb[sb.Length - 1];
                        if (char.ToLower(prev) == 'a')
                        {
                            bool isUpper = char.IsUpper(prev) || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(isUpper ? 'Ă' : 'ă');
                            modified = true;
                            continue;
                        }
                    }
                    // 9: đ
                    if (c == '9' && sb.Length > 0)
                    {
                        char prev = sb[sb.Length - 1];
                        if (char.ToLower(prev) == 'd')
                        {
                            bool isUpper = char.IsUpper(prev) || IsCapsLockActive() || (sb.Length > 1 && IsAllLettersUpper(sb));
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(isUpper ? 'Đ' : 'đ');
                            modified = true;
                            continue;
                        }
                    }
                }

                sb.Append(c);
            }

            // Tự động chuẩn hóa 'ưo' thành 'ươ'
            FixUoToUo(sb);

            if (tone > 0)
            {
                string wordWithTone = ApplyToneMark(sb.ToString(), tone, modernTone);
                if (wordWithTone != sb.ToString())
                {
                    return wordWithTone;
                }
            }

            return modified ? sb.ToString() : null;
        }

        private static bool HasAnyVowel(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            foreach (char c in str)
            {
                if (IsVowel(c)) return true;
            }
            return false;
        }

        private static string ApplyToneMark(string word, int tone, bool modern)
        {
            if (string.IsNullOrEmpty(word) || tone == 0) return word;

            int targetIdx = FindMainVowelIndex(word, modern);
            if (targetIdx < 0) return word;

            char targetChar = word[targetIdx];
            char tonedChar = AddToneToChar(targetChar, tone);

            var sb = new StringBuilder(word);
            sb[targetIdx] = tonedChar;
            return sb.ToString();
        }

        private static int FindMainVowelIndex(string word, bool modern)
        {
            List<int> vowelIndices = new List<int>();
            for (int i = 0; i < word.Length; i++)
            {
                if (IsVowel(word[i])) vowelIndices.Add(i);
            }

            if (vowelIndices.Count == 0) return -1;
            if (vowelIndices.Count == 1) return vowelIndices[0];

            string lowerWord = word.ToLower();

            // 1. Phụ âm đầu đặc biệt: "qu" và "gi"
            // Nếu từ bắt đầu bằng "qu" và sau 'u' còn nguyên âm khác -> loại bỏ 'u' khỏi danh sách nguyên âm
            if (lowerWord.StartsWith("qu") && vowelIndices.Count > 1 && vowelIndices[0] == 1)
            {
                vowelIndices.RemoveAt(0);
            }
            // Nếu từ bắt đầu bằng "gi" và sau 'i' còn nguyên âm khác -> loại bỏ 'i' khỏi danh sách nguyên âm
            else if (lowerWord.StartsWith("gi") && vowelIndices.Count > 1 && vowelIndices[0] == 1)
            {
                vowelIndices.RemoveAt(0);
            }

            if (vowelIndices.Count == 1) return vowelIndices[0];

            // 2. Xét xem có phụ âm cuối không
            bool hasEndingConsonant = vowelIndices[vowelIndices.Count - 1] < word.Length - 1;

            // 3. Nếu cụm có 3 nguyên âm (ví dụ: oai, oay, oeo, uôi, ươi, ươu, uya, uyê, iêu, yêu)
            if (vowelIndices.Count >= 3)
            {
                // Cụm uyê có phụ âm cuối (chuyện, thuyền, quyết, tuyến, duyệt, khuyên...)
                // Âm chính luôn là 'ê' (nguyên âm thứ 3 trong cụm uyê)
                char c0 = char.ToLower(word[vowelIndices[0]]);
                char c1_char = char.ToLower(word[vowelIndices[1]]);
                char c2_char = char.ToLower(word[vowelIndices[2]]);
                if (c0 == 'u' && c1_char == 'y' && c2_char == 'ê')
                {
                    return vowelIndices[2];
                }

                // Các cụm 3 nguyên âm khác (oai, oay, oeo, uôi, ươi, ươu, uya, iêu, yêu): luôn đặt ở nguyên âm thứ 2
                return vowelIndices[1];
            }

            // 4. Nếu cụm có 2 nguyên âm (v1, v2)
            int v1Idx = vowelIndices[0];
            int v2Idx = vowelIndices[1];
            char c1 = char.ToLower(word[v1Idx]);
            char c2 = char.ToLower(word[v2Idx]);

            // TH 4.1: CÓ PHỤ ÂM CUỐI (ví dụ: tiết, kiệm, siêng, năng, uống, đường, phường, viện, toán, hoàng, luận, quýt...)
            if (hasEndingConsonant)
            {
                // Khi có phụ âm cuối, dấu luôn đặt ở nguyên âm thứ hai
                return v2Idx;
            }

            // TH 4.2: KHÔNG CÓ PHỤ ÂM CUỐI (từ kết thúc sau nguyên âm thứ 2)
            // Cặp ưa (ngựa, cửa, lửa, mưa, chữa, xưa) -> dấu luôn đặt ở ư (nguyên âm thứ 1)
            if ((c1 == 'ư' || c1 == 'u') && (c2 == 'a'))
            {
                return v1Idx;
            }

            // Cặp ia (mía, kìa, tía, chĩa, đĩa) -> dấu luôn đặt ở i (nguyên âm thứ 1)
            if (c1 == 'i' && c2 == 'a')
            {
                return v1Idx;
            }

            // Cặp ua (của, múa, rùa, lúa, lụa) -> dấu luôn đặt ở u (nguyên âm thứ 1)
            if (c1 == 'u' && c2 == 'a')
            {
                return v1Idx;
            }

            // Nếu nguyên âm thứ hai là bán âm cuối (i, y, o, u):
            // ai (ái, bài), ao (ào, Đạo), au (sáu, màu), ay (ngày, chạy), âu (cẩu), ây (cấy, thầy),
            // eo (kéo, mèo), êu (đều, nếu), oi (nói), ôi (tối), ơi (mới), ui (Bùi, túi), ưi (ngửi), ưu (cứu), iu (xíu)
            if (c2 == 'i' || c2 == 'y' || c2 == 'o' || c2 == 'u')
            {
                return v1Idx;
            }

            // Các cặp âm đệm: oa, oe, uy
            if ((c1 == 'o' && (c2 == 'a' || c2 == 'e')) || (c1 == 'u' && c2 == 'y'))
            {
                // Kiểu mới: đặt ở nguyên âm thứ 2 (hoà, hoè, thuý)
                // Kiểu cũ: đặt ở nguyên âm thứ 1 (hòa, hòe, thúy)
                return modern ? v2Idx : v1Idx;
            }

            // Mặc định: nếu có nguyên âm mang mũ/móc (â, ă, ê, ô, ơ, ư) thì ưu tiên
            if (c1 == 'â' || c1 == 'ă' || c1 == 'ê' || c1 == 'ô' || c1 == 'ơ' || c1 == 'ư') return v1Idx;
            if (c2 == 'â' || c2 == 'ă' || c2 == 'ê' || c2 == 'ô' || c2 == 'ơ' || c2 == 'ư') return v2Idx;

                        return modern ? v2Idx : v1Idx;
        }

        private static bool IsVowel(char c)
        {
            char l = RemoveToneFromChar(char.ToLower(c));
            return l == 'a' || l == 'ă' || l == 'â' ||
                   l == 'e' || l == 'ê' ||
                   l == 'i' || l == 'y' ||
                   l == 'o' || l == 'ô' || l == 'ơ' ||
                   l == 'u' || l == 'ư';
        }

        public static char RemoveToneFromChar(char c, out int tone)
        {
            tone = 0;
            char l = char.ToLower(c);
            bool isUpper = char.IsUpper(c);
            char baseChar = l;

            switch (l)
            {
                case 'á': baseChar = 'a'; tone = 1; break;
                case 'à': baseChar = 'a'; tone = 2; break;
                case 'ả': baseChar = 'a'; tone = 3; break;
                case 'ã': baseChar = 'a'; tone = 4; break;
                case 'ạ': baseChar = 'a'; tone = 5; break;

                case 'ắ': baseChar = 'ă'; tone = 1; break;
                case 'ằ': baseChar = 'ă'; tone = 2; break;
                case 'ẳ': baseChar = 'ă'; tone = 3; break;
                case 'ẵ': baseChar = 'ă'; tone = 4; break;
                case 'ặ': baseChar = 'ă'; tone = 5; break;

                case 'ấ': baseChar = 'â'; tone = 1; break;
                case 'ầ': baseChar = 'â'; tone = 2; break;
                case 'ẩ': baseChar = 'â'; tone = 3; break;
                case 'ẫ': baseChar = 'â'; tone = 4; break;
                case 'ậ': baseChar = 'â'; tone = 5; break;

                case 'é': baseChar = 'e'; tone = 1; break;
                case 'è': baseChar = 'e'; tone = 2; break;
                case 'ẻ': baseChar = 'e'; tone = 3; break;
                case 'ẽ': baseChar = 'e'; tone = 4; break;
                case 'ẹ': baseChar = 'e'; tone = 5; break;

                case 'ế': baseChar = 'ê'; tone = 1; break;
                case 'ề': baseChar = 'ê'; tone = 2; break;
                case 'ể': baseChar = 'ê'; tone = 3; break;
                case 'ễ': baseChar = 'ê'; tone = 4; break;
                case 'ệ': baseChar = 'ê'; tone = 5; break;

                case 'í': baseChar = 'i'; tone = 1; break;
                case 'ì': baseChar = 'i'; tone = 2; break;
                case 'ỉ': baseChar = 'i'; tone = 3; break;
                case 'ĩ': baseChar = 'i'; tone = 4; break;
                case 'ị': baseChar = 'i'; tone = 5; break;

                case 'ó': baseChar = 'o'; tone = 1; break;
                case 'ò': baseChar = 'o'; tone = 2; break;
                case 'ỏ': baseChar = 'o'; tone = 3; break;
                case 'õ': baseChar = 'o'; tone = 4; break;
                case 'ọ': baseChar = 'o'; tone = 5; break;

                case 'ố': baseChar = 'ô'; tone = 1; break;
                case 'ồ': baseChar = 'ô'; tone = 2; break;
                case 'ổ': baseChar = 'ô'; tone = 3; break;
                case 'ỗ': baseChar = 'ô'; tone = 4; break;
                case 'ộ': baseChar = 'ô'; tone = 5; break;

                case 'ớ': baseChar = 'ơ'; tone = 1; break;
                case 'ờ': baseChar = 'ơ'; tone = 2; break;
                case 'ở': baseChar = 'ơ'; tone = 3; break;
                case 'ỡ': baseChar = 'ơ'; tone = 4; break;
                case 'ợ': baseChar = 'ơ'; tone = 5; break;

                case 'ú': baseChar = 'u'; tone = 1; break;
                case 'ù': baseChar = 'u'; tone = 2; break;
                case 'ủ': baseChar = 'u'; tone = 3; break;
                case 'ũ': baseChar = 'u'; tone = 4; break;
                case 'ụ': baseChar = 'u'; tone = 5; break;

                case 'ứ': baseChar = 'ư'; tone = 1; break;
                case 'ừ': baseChar = 'ư'; tone = 2; break;
                case 'ử': baseChar = 'ư'; tone = 3; break;
                case 'ữ': baseChar = 'ư'; tone = 4; break;
                case 'ự': baseChar = 'ư'; tone = 5; break;

                case 'ý': baseChar = 'y'; tone = 1; break;
                case 'ỳ': baseChar = 'y'; tone = 2; break;
                case 'ỷ': baseChar = 'y'; tone = 3; break;
                case 'ỹ': baseChar = 'y'; tone = 4; break;
                case 'ỵ': baseChar = 'y'; tone = 5; break;
            }

            return isUpper ? char.ToUpper(baseChar) : baseChar;
        }

        public static char RemoveToneFromChar(char c)
        {
            return RemoveToneFromChar(c, out _);
        }

        public static List<char> SynchronizeBufferWithDisplay(string displayWord, InputMethod method, bool modernTone, List<CustomInputRule> customRules = null)
        {
            if (string.IsNullOrEmpty(displayWord)) return new List<char>();

            // 1. Tách dấu thanh từ displayWord
            int tone = 0;
            var baseChars = new List<char>();
            for (int i = 0; i < displayWord.Length; i++)
            {
                char norm = RemoveToneFromChar(displayWord[i], out int t);
                if (t > 0) tone = t;
                baseChars.Add(norm);
            }

            // 2. Chuyển đổi baseChars thành raw keystrokes theo kiểu gõ
            var keys = new List<char>();
            for (int i = 0; i < baseChars.Count; i++)
            {
                char c = baseChars[i];
                char lower = char.ToLower(c);
                bool isUpper = char.IsUpper(c);

                if (method == InputMethod.TuBinhTran)
                {
                    if (lower == 'đ')
                    {
                        keys.Add(isUpper ? 'D' : 'd');
                        keys.Add('d');
                    }
                    else if (lower == 'â')
                    {
                        keys.Add(isUpper ? '^' : '6');
                    }
                    else if (lower == 'ê')
                    {
                        keys.Add(isUpper ? '&' : '7');
                    }
                    else if (lower == 'ô')
                    {
                        keys.Add(isUpper ? '*' : '8');
                    }
                    else if (lower == 'ă')
                    {
                        keys.Add(isUpper ? '(' : '9');
                    }
                    else if (lower == 'ư')
                    {
                        keys.Add(isUpper ? '{' : '[');
                    }
                    else if (lower == 'ơ')
                    {
                        keys.Add(isUpper ? '}' : ']');
                    }
                    else
                    {
                        keys.Add(c);
                    }
                }
                else if (method == InputMethod.Telex || method == InputMethod.SimpleTelex)
                {
                    if (lower == 'đ') { keys.Add(isUpper ? 'D' : 'd'); keys.Add('d'); }
                    else if (lower == 'â') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('a'); }
                    else if (lower == 'ê') { keys.Add(isUpper ? 'E' : 'e'); keys.Add('e'); }
                    else if (lower == 'ô') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('o'); }
                    else if (lower == 'ă') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('w'); }
                    else if (lower == 'ư') { keys.Add(isUpper ? 'U' : 'u'); keys.Add('w'); }
                    else if (lower == 'ơ') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('w'); }
                    else { keys.Add(c); }
                }
                else if (method == InputMethod.Vni)
                {
                    if (lower == 'đ') { keys.Add(isUpper ? 'D' : 'd'); keys.Add('9'); }
                    else if (lower == 'â') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('6'); }
                    else if (lower == 'ê') { keys.Add(isUpper ? 'E' : 'e'); keys.Add('6'); }
                    else if (lower == 'ô') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('6'); }
                    else if (lower == 'ă') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('8'); }
                    else if (lower == 'ư') { keys.Add(isUpper ? 'U' : 'u'); keys.Add('7'); }
                    else if (lower == 'ơ') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('7'); }
                    else { keys.Add(c); }
                }
                else if (method == InputMethod.Custom)
                {
                    if (lower == 'đ') { keys.Add(isUpper ? 'D' : 'd'); keys.Add('d'); }
                    else if (lower == 'â') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('a'); }
                    else if (lower == 'ê') { keys.Add(isUpper ? 'E' : 'e'); keys.Add('e'); }
                    else if (lower == 'ô') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('o'); }
                    else if (lower == 'ă') { keys.Add(isUpper ? 'A' : 'a'); keys.Add('w'); }
                    else if (lower == 'ư') { keys.Add(isUpper ? 'U' : 'u'); keys.Add('w'); }
                    else if (lower == 'ơ') { keys.Add(isUpper ? 'O' : 'o'); keys.Add('w'); }
                    else { keys.Add(c); }
                }
                else
                {
                    keys.Add(c);
                }
            }

            // 3. Thêm dấu thanh vào cuối nếu có
            if (tone > 0)
            {
                if (method == InputMethod.TuBinhTran || method == InputMethod.Vni)
                {
                    keys.Add((char)('0' + tone));
                }
                else if (method == InputMethod.Telex || method == InputMethod.SimpleTelex)
                {
                    char[] telexTones = { '\0', 's', 'f', 'r', 'x', 'j' };
                    if (tone >= 1 && tone <= 5) keys.Add(telexTones[tone]);
                }
            }

            // 4. Tự kiểm tra tính nhất quán (Self-Verification)
            string testDisplay = TransformWord(keys, method, modernTone);
            if (string.Equals(testDisplay, displayWord, StringComparison.Ordinal))
            {
                return keys;
            }

            // Fallback an toàn: Trả về trực tiếp displayWord để độ dài buffer khớp 100% với màn hình
            return new List<char>(displayWord);
        }

        private static char AddToneToChar(char c, int tone)
        {
            char l = char.ToLower(c);
            bool isUpper = char.IsUpper(c);

            char res = c;
            switch (l)
            {
                case 'a':
                    res = tone == 1 ? 'á' : tone == 2 ? 'à' : tone == 3 ? 'ả' : tone == 4 ? 'ã' : 'ạ';
                    break;
                case 'ă':
                    res = tone == 1 ? 'ắ' : tone == 2 ? 'ằ' : tone == 3 ? 'ẳ' : tone == 4 ? 'ẵ' : 'ặ';
                    break;
                case 'â':
                    res = tone == 1 ? 'ấ' : tone == 2 ? 'ầ' : tone == 3 ? 'ẩ' : tone == 4 ? 'ẫ' : 'ậ';
                    break;
                case 'e':
                    res = tone == 1 ? 'é' : tone == 2 ? 'è' : tone == 3 ? 'ẻ' : tone == 4 ? 'ẽ' : 'ẹ';
                    break;
                case 'ê':
                    res = tone == 1 ? 'ế' : tone == 2 ? 'ề' : tone == 3 ? 'ể' : tone == 4 ? 'ễ' : 'ệ';
                    break;
                case 'i':
                    res = tone == 1 ? 'í' : tone == 2 ? 'ì' : tone == 3 ? 'ỉ' : tone == 4 ? 'ĩ' : 'ị';
                    break;
                case 'o':
                    res = tone == 1 ? 'ó' : tone == 2 ? 'ò' : tone == 3 ? 'ỏ' : tone == 4 ? 'õ' : 'ọ';
                    break;
                case 'ô':
                    res = tone == 1 ? 'ố' : tone == 2 ? 'ồ' : tone == 3 ? 'ổ' : tone == 4 ? 'ỗ' : 'ộ';
                    break;
                case 'ơ':
                    res = tone == 1 ? 'ớ' : tone == 2 ? 'ờ' : tone == 3 ? 'ở' : tone == 4 ? 'ỡ' : 'ợ';
                    break;
                case 'u':
                    res = tone == 1 ? 'ú' : tone == 2 ? 'ù' : tone == 3 ? 'ủ' : tone == 4 ? 'ũ' : 'ụ';
                    break;
                case 'ư':
                    res = tone == 1 ? 'ứ' : tone == 2 ? 'ừ' : tone == 3 ? 'ử' : tone == 4 ? 'ữ' : 'ự';
                    break;
                case 'y':
                    res = tone == 1 ? 'ý' : tone == 2 ? 'ỳ' : tone == 3 ? 'ỷ' : tone == 4 ? 'ỹ' : 'ỵ';
                    break;
            }

            return isUpper ? char.ToUpper(res) : res;
        }
    }
}
