using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using ModernKey.Config;
using ModernKey.Models;

namespace ModernKey.Core
{
    public class MacroManager
    {
        public ObservableCollection<MacroEntry> MacroList { get; } = new ObservableCollection<MacroEntry>();

        public MacroManager()
        {
            Load();
        }

        public void Load()
        {
            MacroList.Clear();
            string path = SettingsManager.GetMacroFilePath();

            // Nếu file cấu hình macro chưa tồn tại, thử tìm nạp từ file C++ release
            if (!File.Exists(path))
            {
                string cppPath = @"R:\HDD R\ZC SYMLINK\USERS\source\repos\ghostminhtoan\openkey\Sources\OpenKey\win32\OpenKey\x64\Release\openkeymacro.txt";
                if (File.Exists(cppPath))
                {
                    ImportFromFile(cppPath, false);
                    Save();
                    return;
                }

                // Ví dụ mặc định
                MacroList.Add(new MacroEntry("vn", "Việt Nam"));
                MacroList.Add(new MacroEntry("mk", "ModernKey GMTPC"));
                Save();
                return;
            }

            try
            {
                string content = ReadMacroText(path);
                var items = ParseMacroContent(content);
                foreach (var item in items)
                {
                    AddOrUpdate(item.Shortcut, item.Replacement);
                }

                // Nếu file đang có nhưng bị rỗng (0 mục), thử nạp từ C++ nếu có
                if (MacroList.Count == 0)
                {
                    string cppPath = @"R:\HDD R\ZC SYMLINK\USERS\source\repos\ghostminhtoan\openkey\Sources\OpenKey\win32\OpenKey\x64\Release\openkeymacro.txt";
                    if (File.Exists(cppPath))
                    {
                        ImportFromFile(cppPath, false);
                        Save();
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        public void Save()
        {
            string path = SettingsManager.GetMacroFilePath();
            try
            {
                string content = FormatOpenKeyMacro(MacroList);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, content, new UTF8Encoding(true));
            }
            catch
            {
                // Ignore
            }
        }

        public bool TryGetMacro(string word, bool autoCaps, out string replacement)
        {
            replacement = null;
            if (string.IsNullOrEmpty(word)) return false;

            foreach (var item in MacroList)
            {
                if (string.Equals(item.Shortcut, word, StringComparison.OrdinalIgnoreCase))
                {
                    string rep = item.Replacement;

                    if (autoCaps && rep.Length > 0)
                    {
                        // Kiểm tra nếu word viết hoa toàn bộ: ví dụ "VN" -> "VIỆT NAM"
                        bool allUpper = true;
                        bool hasLetter = false;
                        for (int i = 0; i < word.Length; i++)
                        {
                            if (char.IsLetter(word[i]))
                            {
                                hasLetter = true;
                                if (!char.IsUpper(word[i]))
                                {
                                    allUpper = false;
                                    break;
                                }
                            }
                        }

                        if (allUpper && hasLetter && word.Length > 1)
                        {
                            rep = rep.ToUpper();
                        }
                        else if (char.IsUpper(word[0]))
                        {
                            rep = char.ToUpper(rep[0]) + (rep.Length > 1 ? rep.Substring(1) : string.Empty);
                        }
                    }

                    replacement = rep;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetMacro(string word, out string replacement)
        {
            return TryGetMacro(word, true, out replacement);
        }

        public int ImportFromFile(string filePath, bool append)
        {
            if (!File.Exists(filePath)) return 0;

            if (!append)
            {
                MacroList.Clear();
            }

            int count = 0;
            try
            {
                string content = ReadMacroText(filePath);
                var items = ParseMacroContent(content);
                foreach (var item in items)
                {
                    AddOrUpdate(item.Shortcut, item.Replacement);
                    count++;
                }

                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Import Macro error: " + ex.Message);
            }

            return count;
        }

        public int ConvertEvKeyFromFile(string filePath, bool append)
        {
            if (!File.Exists(filePath)) return 0;

            if (!append)
            {
                MacroList.Clear();
            }

            int count = 0;
            try
            {
                string content = ReadMacroText(filePath);
                var items = ParseMacroContent(content);
                foreach (var item in items)
                {
                    AddOrUpdate(item.Shortcut, item.Replacement);
                    count++;
                }

                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Convert EVKey error: " + ex.Message);
            }

            return count;
        }

        public bool ExportToFile(string filePath)
        {
            try
            {
                string content = FormatOpenKeyMacro(MacroList);
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, content, new UTF8Encoding(true));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void AddOrUpdate(string shortcut, string replacement)
        {
            if (string.IsNullOrEmpty(shortcut)) return;

            foreach (var item in MacroList)
            {
                if (string.Equals(item.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase))
                {
                    item.Replacement = replacement;
                    return;
                }
            }
            MacroList.Add(new MacroEntry(shortcut, replacement));
        }

        public static string ReadMacroText(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            byte[] raw = File.ReadAllBytes(filePath);
            return DecodeMacroBytes(raw);
        }

        public static string DecodeMacroBytes(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return string.Empty;

            // 1. Kiểm tra UTF-16LE / BE BOM
            if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
            }
            if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
            }

            // 2. Bỏ qua UTF-8 BOM nếu có
            int startIdx = 0;
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                startIdx = 3;
            }

            // 3. Chuyển đổi CESU-8 surrogate pairs (đặc thù từ OpenKey C++ Windows) sang UTF-8 chuẩn 4-byte
            // High surrogate: \xED\xA0..\xAF \x80..\xBF \x80..\xBF (0xD800..0xDBFF)
            // Low surrogate:  \xED\xB0..\xBF \x80..\xBF \x80..\xBF (0xDC00..0xDFFF)
            var outBytes = new List<byte>(raw.Length);
            int i = startIdx;
            while (i < raw.Length)
            {
                if (raw[i] == 0xED && i + 5 < raw.Length && raw[i + 3] == 0xED)
                {
                    byte b0 = raw[i];
                    byte b1 = raw[i + 1];
                    byte b2 = raw[i + 2];
                    byte b3 = raw[i + 3];
                    byte b4 = raw[i + 4];
                    byte b5 = raw[i + 5];

                    int cp1 = ((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
                    int cp2 = ((b3 & 0x0F) << 12) | ((b4 & 0x3F) << 6) | (b5 & 0x3F);

                    if (cp1 >= 0xD800 && cp1 <= 0xDBFF && cp2 >= 0xDC00 && cp2 <= 0xDFFF)
                    {
                        int fullCp = 0x10000 + ((cp1 - 0xD800) << 10) + (cp2 - 0xDC00);
                        outBytes.Add((byte)(0xF0 | (fullCp >> 18)));
                        outBytes.Add((byte)(0x80 | ((fullCp >> 12) & 0x3F)));
                        outBytes.Add((byte)(0x80 | ((fullCp >> 6) & 0x3F)));
                        outBytes.Add((byte)(0x80 | (fullCp & 0x3F)));
                        i += 6;
                        continue;
                    }
                }
                outBytes.Add(raw[i]);
                i++;
            }

            return Encoding.UTF8.GetString(outBytes.ToArray());
        }

        public static List<MacroEntry> ParseMacroContent(string content)
        {
            var list = new List<MacroEntry>();
            if (string.IsNullOrEmpty(content)) return list;

            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inBlock = false;
            string blockName = null;
            var blockContent = new StringBuilder();

            foreach (var rawLine in lines)
            {
                string trimmed = rawLine.Trim();

                // 1. Kiểm tra block OpenKey: ::name ... ::end
                if (!inBlock && trimmed.StartsWith("::") && !trimmed.Equals("::end", StringComparison.OrdinalIgnoreCase))
                {
                    blockName = trimmed.Substring(2).Trim();
                    blockContent.Clear();
                    inBlock = true;
                    continue;
                }

                if (inBlock)
                {
                    if (trimmed.Equals("::end", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(blockName))
                        {
                            list.Add(new MacroEntry(blockName, blockContent.ToString()));
                        }
                        inBlock = false;
                        continue;
                    }

                    if (blockContent.Length > 0) blockContent.Append("\r\n");
                    blockContent.Append(rawLine);
                    continue;
                }

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                // Bỏ qua marker header EVKey
                if (trimmed.Contains("<<") && trimmed.Contains(">>"))
                    continue;

                // 2. Format EVKey: name||content
                int evSep = rawLine.IndexOf("||");
                if (evSep > 0)
                {
                    string k = rawLine.Substring(0, evSep).Trim();
                    string v = rawLine.Substring(evSep + 2);
                    v = v.Replace("\\n", "\r\n");
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    {
                        list.Add(new MacroEntry(k, v));
                    }
                    continue;
                }

                // 3. Format đơn dòng khác: k:v, k=v, k\tv
                int sep = rawLine.IndexOf(':');
                if (sep <= 0) sep = rawLine.IndexOf('=');
                if (sep <= 0) sep = rawLine.IndexOf('\t');
                if (sep > 0)
                {
                    string k = rawLine.Substring(0, sep).Trim();
                    string v = rawLine.Substring(sep + 1);
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    {
                        v = v.Replace("\\n", "\r\n");
                        list.Add(new MacroEntry(k, v));
                    }
                }
            }

            return list;
        }

        public static string FormatOpenKeyMacro(IEnumerable<MacroEntry> list)
        {
            var sb = new StringBuilder();
            sb.AppendLine(";OpenKey Macro Text Data version=2");
            foreach (var item in list)
            {
                if (string.IsNullOrEmpty(item.Shortcut) || string.IsNullOrEmpty(item.Replacement))
                    continue;

                sb.AppendLine($"::{item.Shortcut}");
                sb.AppendLine(item.Replacement);
                sb.AppendLine("::end");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
