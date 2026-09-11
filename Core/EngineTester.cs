using System;
using System.Collections.Generic;
using System.Text;
using ModernKey.Config;
using ModernKey.Models;

namespace ModernKey.Core
{
    public static class EngineTester
    {
        public static bool RunAllTests(out string report)
        {
            var sb = new StringBuilder();
            bool allPassed = true;

            sb.AppendLine("=== BAT DAU KIEM TRA TU DONG ENGINE TIENG VIET ===");

            // 1. Test Telex voi van ban mau cua nguoi dung
            var telexSettings = new AppSettings
            {
                IsVietnamese = true,
                CurrentInputMethod = InputMethod.Telex,
                ModernToneRules = true
            };
            var telexEngine = new VietnameseEngine(telexSettings, new MacroManager());

            var testCasesTelex = new (string input, string expected)[]
            {
                // Câu 1: êm ái, ầm ĩ, ồn ào, ăn dặm.
                ("eem", "êm"),
                ("ais", "ái"),
                ("aafm", "ầm"),
                ("ix", "ĩ"),
                ("oofn", "ồn"),
                ("aof", "ào"),
                ("awn", "ăn"),
                ("dawjm", "dặm"),

                // Câu 2: Ân sư. Ê-đê. Ôm ấp. Ăn uống.
                ("AAn", "Ân"),
                ("suw", "sư"),
                ("EE", "Ê"),
                ("ddee", "đê"),
                ("OOm", "Ôm"),
                ("aaps", "ấp"),
                ("AWn", "Ăn"),
                ("uoongs", "uống"),

                // Câu 3: Tiết kiệm, siêng năng. Trần Hưng Đạo, Bùi Viện (đường), (phường), (tiên) (học) (lễ).
                ("Tieets", "Tiết"),
                ("kieemj", "kiệm"),
                ("sieeng", "siêng"),
                ("nawng", "năng"),
                ("Traafn", "Trần"),
                ("Huwng", "Hưng"),
                ("Ddaoj", "Đạo"),
                ("Buif", "Bùi"),
                ("Vieenj", "Viện"),
                ("dduowfng", "đường"),
                ("phuowfng", "phường"),
                ("tieen", "tiên"),
                ("hocj", "học"),
                ("leex", "lễ"),
                ("duowngfd", "đường"),
                ("dood", "đô")
            };

            sb.AppendLine("[TEST TELEX - TUNG TU]");
            foreach (var tc in testCasesTelex)
            {
                string result = SimulateTypingWord(telexEngine, tc.input);
                if (result == tc.expected)
                {
                    sb.AppendLine($"  PASS: '{tc.input}' -> '{result}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: '{tc.input}' -> '{result}' (Mong doi: '{tc.expected}')");
                }
            }

            // 2. Test toan bo doan van ban lien tuc (Sentence Simulation) Telex
            sb.AppendLine("[TEST TELEX - TOAN BO DOAN VAN BAN]");
            string sample1 = "eem ais, aafm ix, oofn aof, awn dawjm.";
            string expected1 = "êm ái, ầm ĩ, ồn ào, ăn dặm.";
            string out1 = SimulateTypingSentence(telexEngine, sample1);
            if (out1 == expected1)
            {
                sb.AppendLine($"  PASS Doan 1: '{out1}'");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Doan 1:\n    Ket qua : '{out1}'\n    Mong doi: '{expected1}'");
            }

            string sample2 = "AAn suw. EE-ddee. OOm aaps. AWn uoongs.";
            string expected2 = "Ân sư. Ê-đê. Ôm ấp. Ăn uống.";
            string out2 = SimulateTypingSentence(telexEngine, sample2);
            if (out2 == expected2)
            {
                sb.AppendLine($"  PASS Doan 2: '{out2}'");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Doan 2:\n    Ket qua : '{out2}'\n    Mong doi: '{expected2}'");
            }

            string sample3 = "Tieets kieemj, sieeng nawng. Traafn Huwng Ddaoj, Buif Vieenj (dduowfng), (phuowfng), (tieen) (hocj) (leex).";
            string expected3 = "Tiết kiệm, siêng năng. Trần Hưng Đạo, Bùi Viện (đường), (phường), (tiên) (học) (lễ).";
            string out3 = SimulateTypingSentence(telexEngine, sample3);
            if (out3 == expected3)
            {
                sb.AppendLine($"  PASS Doan 3: '{out3}'");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Doan 3:\n    Ket qua : '{out3}'\n    Mong doi: '{expected3}'");
            }

            // 3. Test Tu Binh Tran
            sb.AppendLine("[TEST TU BINH TRAN]");
            var tbtSettings = new AppSettings
            {
                IsVietnamese = true,
                CurrentInputMethod = InputMethod.TuBinhTran,
                ModernToneRules = true
            };
            var tbtEngine = new VietnameseEngine(tbtSettings, new MacroManager());

            var testCasesTbt = new (string input, string expected)[]
            {
                ("tr7n", "trên"),
                ("c6n", "cân"),
                ("c8ng", "công"),
                ("c9n", "căn"),
                ("a62m", "ầm"),
                ("ai1", "ái"),
                ("s[", "sư"),
                ("dd[]ng2", "đường"),
                ("u8ng1", "uống"),
                ("ti7t1", "tiết"),
                ("ki7m5", "kiệm"),
                ("si7ng", "siêng"),
                ("tu8i3", "tuổi"),
                ("chuy7n5", "chuyện"),
                ("gi[]ng2", "giường"),
                ("ng[]i2", "người"),
                ("c[u", "cưu"),
                ("dd[a1", "đứa"),
                ("l[a3", "lửa"),
                ("gi6c1", "giấc"),
                ("chu6n3", "chuẩn"),
                ("chu36n", "chuẩn"),
                ("^n", "Ân"),
                ("&-dde7", "Ê-đê"),
                ("*n2", "Ồn"),
                ("(n", "Ăn"),
                ("e7m", "êm"),
                ("7m", "êm"),
                ("o8m", "ôm"),
                ("8m", "ôm"),
                ("a6p1", "ấp"),
                ("a6m2", "ầm"),
                ("o8n2", "ồn"),
                ("a6y1", "ấy"),
                ("[3", "ử"),
                ("{3", "Ử"),
                ("]3", "ở"),
                ("}3", "Ở"),
                ("[1", "ứ"),
                ("]1", "ớ"),
                ("T8", "Tô"),
                ("T81", "Tố"),
                ("T81t", "Tốt"),
                ("TR7N", "TRÊN"),
                ("d[]2ngd", "đường"),
                ("do1d", "đó"),
                ("D[]2ngd", "Đường"),
                ("Do1d", "Đó"),
                ("d6nd", "đân"),
                ("d8ngd", "đông"),
                ("d7d", "đê"),
                ("do1dd", "dód")
            };

            foreach (var tc in testCasesTbt)
            {
                string res = SimulateTypingWord(tbtEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS TBT: '{tc.input}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL TBT: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            // Test toggle số lặp lại trong TBT (66->6, 77->7, 88->8, 99->9)
            (string numInput, string numExpected)[] toggleCases = new[]
            {
                ("66", "6"),
                ("77", "7"),
                ("88", "8"),
                ("99", "9"),
                ("36", "36"),
                ("27", "27"),
                ("18", "18"),
                ("09", "09"),
                ("667", "67")
            };

            foreach (var numCase in toggleCases)
            {
                string numRes = SimulateTypingSentence(tbtEngine, numCase.numInput);
                if (numRes == numCase.numExpected)
                {
                    sb.AppendLine($"  PASS TBT: '{numCase.numInput}' -> '{numRes}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL TBT: '{numCase.numInput}' -> '{numRes}' (Mong doi: '{numCase.numExpected}')");
                }
            }

            // Test toan bo doan van mau moi nhat cua nguoi dung
            sb.AppendLine("[TEST TU BINH TRAN - TOAN BO DOAN VAN]");
            string paragraphExpected = "Ồn ào là một ngày mà tôi không mong muốn nhất. Ăn uống xong, mệt mỏi, tôi chuẩn bị đi ngủ. Mỗi khi mệt mỏi, tôi lại nhớ về căn nhà sàn nhỏ của người Ân sư vùng cao gốc Ê-đê, nơi tràn ngập sự êm ái và tình yêu thương ôm ấp lấy tuổi thơ tôi. Trái ngược với sự ầm ĩ, ồn ào của phố thị ngoài kia, gian nhà của thầy luôn bình yên với tiếng guốc gỗ lộc cộc quen thuộc trên sàn nhà. Ngày ấy, tôi chỉ là một đứa trẻ mới qua thời ăn dặm, được thầy cưu mang, dạy dỗ từng cách ăn uống đi đứng, cho đến đức tính tiết kiệm và siêng năng học tập. Đêm về, bên ánh lửa bập bùng, tôi lại nằm trên chiếc giường tre mộc mạc, lắng nghe thầy kể chuyện rồi chìm vào giấc ngủ an lành.";
            string paragraphInput = "*n2 ao2 la2 m8t5 ngay2 ma2 t8i kh8ng mong mu8n1 nh6t1. (n u8ng1 xong, m7t5 moi3, t8i chu6n3 bi5 ddi ngu3. M8i4 khi m7t5 moi3, t8i lai5 nh]1 v72 c9n nha2 san2 nho3 cua3 ng[]i2 ^n s[ vung2 cao g8c1 &-dde7, n]i tran2 ng6p5 s[5 e7m ai1 va2 tinh2 y7u th[]ng o8m a6p1 l6y1 tu8i3 th] t8i. Trai1 ng[]c5 v]i1 s[5 a6m2 i4, o8n2 ao2 cua3 ph81 thi5 ngoai2 kia, gian nha2 cua3 th6y2 lu8n binh2 y7n v]i1 ti7ng1 gu8c1 g84 l8c5 c8c5 quen thu8c5 tr7n san2 nha2. Ngay2 a6y1, t8i chi3 la2 m8t5 dd[a1 tre3 m]i1 qua th]i2 a9n da9m5, dd[]c5 th6y2 c[u mang, day5 d84 t[ng2 cach1 a9n u8ng1 ddi dd[ng1, cho dd7n1 dd[c1 tinh1 ti7t1 ki7m5 va2 si7ng na9ng hoc5 t6p5. Dd7m v72, b7n anh1 l[a3 b6p5 bung2, t8i lai5 na9m2 tr7n chi7c1 gi[]ng2 tre m8c5 mac5, la9ng1 nghe th6y2 k73 chuy7n5 r8i2 chim2 vao2 gi6c1 ngu3 an lanh2.";

            string paragraphActual = SimulateTypingSentence(tbtEngine, paragraphInput);
            if (paragraphActual == paragraphExpected)
            {
                sb.AppendLine("  PASS TBT: Toan bo doan van moi KHOP 100%!");
            }
            else
            {
                allPassed = false;
                sb.AppendLine("  FAIL TBT Toan bo doan van:");
                sb.AppendLine($"    Ket qua : '{paragraphActual}'");
                sb.AppendLine($"    Mong doi: '{paragraphExpected}'");

                // Tim diem khac nhau dau tien de debug
                int minL = Math.Min(paragraphActual.Length, paragraphExpected.Length);
                for (int d = 0; d < minL; d++)
                {
                    if (paragraphActual[d] != paragraphExpected[d])
                    {
                        sb.AppendLine($"    Khac biet tai vi tri {d}: thuc te '{paragraphActual.Substring(Math.Max(0, d - 10), Math.Min(25, paragraphActual.Length - Math.Max(0, d - 10)))}' vs mong doi '{paragraphExpected.Substring(Math.Max(0, d - 10), Math.Min(25, paragraphExpected.Length - Math.Max(0, d - 10)))}'");
                        break;
                    }
                }
            }

            // 4. Test VNI
            sb.AppendLine("[TEST VNI]");
            var vniSettings = new AppSettings
            {
                IsVietnamese = true,
                CurrentInputMethod = InputMethod.Vni,
                ModernToneRules = true
            };
            var vniEngine = new VietnameseEngine(vniSettings, new MacroManager());

            var testCasesVni = new (string input, string expected)[]
            {
                ("e6m", "êm"),
                ("ai1", "ái"),
                ("a62m", "ầm"),
                ("ao2", "ào"),
                ("a8n", "ăn"),
                ("da85m", "dặm"),
                ("Tie6t1", "Tiết"),
                ("D9ao5", "Đạo"),
                ("Bui2", "Bùi")
            };

            foreach (var tc in testCasesVni)
            {
                string res = SimulateTypingWord(vniEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS VNI: '{tc.input}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL VNI: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            // 4.5 TEST CASING CONSISTENCY (All-Caps và TitleCase tiếng Việt)
            sb.AppendLine("[TEST CASING CONSISTENCY - ALL-CAPS VA TITLECASE]");
            var testCasesCasingTBT = new (string input, string expected)[]
            {
                // All-Caps
                ("TR7N", "TRÊN"),
                ("C6N", "CÂN"),
                ("C8NG", "CÔNG"),
                ("DD[]NG2", "ĐƯỜNG"),
                ("TI7T1", "TIẾT"),
                ("^N", "ÂN"),
                // TitleCase
                ("Tr7n", "Trên"),
                ("C6n", "Cân"),
                ("Dd[]ng2", "Đường"),
                ("Ti7t1", "Tiết"),
                ("Th[]ng", "Thương")
            };

            foreach (var tc in testCasesCasingTBT)
            {
                string res = SimulateTypingWord(tbtEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS TBT Casing: '{tc.input}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL TBT Casing: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            var testCasesCasingTelex = new (string input, string expected)[]
            {
                ("TRAAFN", "TRẦN"),
                ("TIEETS", "TIẾT"),
                ("DDUOWNGF", "ĐƯỜNG"),
                ("Traafn", "Trần"),
                ("Tieets", "Tiết"),
                ("Dduowngf", "Đường")
            };

            foreach (var tc in testCasesCasingTelex)
            {
                string res = SimulateTypingWord(telexEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS Telex Casing: '{tc.input}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL Telex Casing: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            // 5. TEST CHUYÊN SÂU: Gõ sai rồi Backspace, gõ lại cho đúng chính tả (Kiểm tra lỗi hoàn trả phím)
            sb.AppendLine("\n--- TEST GO SAI -> BACKSPACE -> GO LAI CHO DUNG (HOAN TRA PHIM) ---");
            var testCasesBackspaceTBT = new (string input, string expected)[]
            {
                ("ti7tp\b1", "tiết"),        // Gõ "tiêt" nhầm 'p', xóa 'p', gõ '1' ra "tiết"
                ("chu6m\bn3", "chuẩn"),      // Gõ "chum", xóa 'm', gõ "n3" ra "chuẩn"
                ("tr7nk\bg", "trêng"),       // Gõ "trên" nhầm 'k', xóa 'k', gõ 'g' ra "trêng"
                ("dd\ba", "a"),              // Gõ "dd" ra "đ", xóa "đ", gõ 'a' ra "a" (không bị xóa lẹm hay nuốt phím)
                ("d9m1\bt", "dắt")           // Gõ "dắm", xóa 'm' còn "dắ", gõ 't' ra "dắt"
            };

            foreach (var tc in testCasesBackspaceTBT)
            {
                string res = SimulateTypingWord(tbtEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS TBT Backspace: '{tc.input.Replace("\b", "[BS]")}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL TBT Backspace: '{tc.input.Replace("\b", "[BS]")}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            var testCasesBackspaceTelex = new (string input, string expected)[]
            {
                ("tieetp\bs", "tiết"),       // Gõ "tiêt" nhầm 'p', xóa 'p', gõ 's' ra "tiết"
                ("dd\ba", "a"),              // Gõ "dd" ra "đ", xóa "đ", gõ 'a' ra "a"
                ("hoanf\bh", "hoàh"),        // Gõ "hoàn", xóa 'n', gõ 'h' ra "hoàh"
                ("cas\ba", "ca")             // Gõ "cá", xóa, gõ 'a' ra "ca"
            };

            foreach (var tc in testCasesBackspaceTelex)
            {
                string res = SimulateTypingWord(telexEngine, tc.input);
                if (res == tc.expected)
                {
                    sb.AppendLine($"  PASS Telex Backspace: '{tc.input.Replace("\b", "[BS]")}' -> '{res}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL Telex Backspace: '{tc.input.Replace("\b", "[BS]")}' -> '{res}' (Mong doi: '{tc.expected}')");
                }
            }

            // 5. Test tinh nang Go tat (Macro): Auto-Caps, Trigger Mask, Convert EVKey, Direct Trigger
            sb.AppendLine("[TEST MACRO - TOAN DIEN]");
            var macroMgr = new MacroManager();
            macroMgr.MacroList.Clear();
            macroMgr.MacroList.Add(new MacroEntry("vn", "việt nam"));
            macroMgr.MacroList.Add(new MacroEntry("hn", "hà nội"));

            // Test Auto-Caps
            if (macroMgr.TryGetMacro("vn", true, out string r1) && r1 == "việt nam")
                sb.AppendLine("  PASS: Macro 'vn' (autoCaps) -> 'việt nam'");
            else { allPassed = false; sb.AppendLine($"  FAIL: Macro 'vn' -> '{r1}'"); }

            if (macroMgr.TryGetMacro("VN", true, out string r2) && r2 == "VIỆT NAM")
                sb.AppendLine("  PASS: Macro 'VN' (autoCaps) -> 'VIỆT NAM'");
            else { allPassed = false; sb.AppendLine($"  FAIL: Macro 'VN' -> '{r2}'"); }

            if (macroMgr.TryGetMacro("Vn", true, out string r3) && r3 == "Việt nam")
                sb.AppendLine("  PASS: Macro 'Vn' (autoCaps) -> 'Việt nam'");
            else { allPassed = false; sb.AppendLine($"  FAIL: Macro 'Vn' -> '{r3}'"); }

            // Test EVKey convert text
            string tempEvKeyFile = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(tempEvKeyFile, "tp||thành phố\\nhồ chí minh\r\nbd||bình dương\r\n;comment\r\n#comment");
            int evCount = macroMgr.ConvertEvKeyFromFile(tempEvKeyFile, true);
            if (evCount == 2 && macroMgr.TryGetMacro("tp", out string rEv) && rEv == "thành phố\r\nhồ chí minh")
                sb.AppendLine("  PASS: Convert EVKey macro ho tro multiline \\n");
            else { allPassed = false; sb.AppendLine("  FAIL: Convert EVKey macro"); }
            try { System.IO.File.Delete(tempEvKeyFile); } catch { }

            // Test Macro Trigger qua VietnameseEngine (Space vs Enter)
            var macroSettings = new AppSettings
            {
                IsVietnamese = true,
                UseMacro = true,
                AutoCapsMacro = true,
                MacroTriggerMask = 0x01 // Chỉ Space
            };
            var macroEngine = new VietnameseEngine(macroSettings, macroMgr);

            // Gõ "vn " -> ra "việt nam "
            string mOut1 = SimulateTypingSentence(macroEngine, "vn ");
            if (mOut1 == "việt nam ")
                sb.AppendLine("  PASS: Engine trigger macro với phím Space");
            else { allPassed = false; sb.AppendLine($"  FAIL: Engine trigger macro Space: '{mOut1}'"); }

            // Gõ "VN " -> ra "VIỆT NAM "
            string mOut2 = SimulateTypingSentence(macroEngine, "VN ");
            if (mOut2 == "VIỆT NAM ")
                sb.AppendLine("  PASS: Engine trigger macro Auto-Caps với phím Space");
            else { allPassed = false; sb.AppendLine($"  FAIL: Engine trigger macro Auto-Caps Space: '{mOut2}'"); }

            // Test Direct Trigger (Double Shift)
            macroEngine.Reset();
            foreach (char c in "vn") macroEngine.ProcessKey(c, (int)c, false, false, false, false, out _, out _);
            if (macroEngine.TryTriggerMacroDirect(out int bc, out string rep) && bc == 2 && rep == "việt nam")
                sb.AppendLine("  PASS: TryTriggerMacroDirect (Double Shift) thành công");
            else { allPassed = false; sb.AppendLine($"  FAIL: TryTriggerMacroDirect: bc={bc}, rep='{rep}'"); }

            // Test Trailing Virtual Key (VK_SPACE và VK_RETURN chuẩn OpenKey C++ cho Browser Address Bar Navigation)
            macroMgr.MacroList.Add(new MacroEntry("ggg", "google.com"));
            macroEngine.Reset();
            foreach (char c in "ggg") macroEngine.ProcessKey(c, (int)c, false, false, false, false, out _, out _);
            bool isGggSpace = macroEngine.ProcessKey(' ', 0x20, false, false, false, false, out int gBc, out string gRep, out int gTrailing);
            if (isGggSpace && gRep == "google.com" && gTrailing == 0x20)
                sb.AppendLine("  PASS: Macro Space trả về replacement thuần + trailingVkCode VK_SPACE (0x20)");
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL: Macro Space trailingVkCode: rep='{gRep}', trailing=0x{gTrailing:X2}");
            }

            macroSettings.MacroTriggerMask = 0x03; // Bật cả Space và Enter
            macroEngine.Reset();
            foreach (char c in "ggg") macroEngine.ProcessKey(c, (int)c, false, false, false, false, out _, out _);
            bool isGggEnter = macroEngine.ProcessKey('\r', 0x0D, false, false, false, false, out int geBc, out string geRep, out int geTrailing);
            if (isGggEnter && geRep == "google.com" && geTrailing == 0x0D)
                sb.AppendLine("  PASS: Macro Enter trả về replacement thuần + trailingVkCode VK_RETURN (0x0D)");
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL: Macro Enter trailingVkCode: rep='{geRep}', trailing=0x{geTrailing:X2}");
            }

            // 6. Test Unicode Emoji Macro ("emogrin" -> "😏😏😏")
            macroMgr.MacroList.Add(new MacroEntry("emogrin", "😏😏😏"));
            string emojiOut = SimulateTypingSentence(macroEngine, "emogrin ");
            if (emojiOut == "😏😏😏 ")
                sb.AppendLine("  PASS: Emoji Macro 'emogrin ' -> '😏😏😏 '");
            else { allPassed = false; sb.AppendLine($"  FAIL: Emoji Macro: '{emojiOut}' (Mong đợi: '😏😏😏 ')"); }

            // 7. Test Nguong Sequence vs Clipboard (KeySender.ShouldUseClipboard)
            bool clipEmoji = ModernKey.Hook.KeySender.ShouldUseClipboard("😏😏😏", false);
            bool clipLong = ModernKey.Hook.KeySender.ShouldUseClipboard(new string('a', 65), false);
            bool clipNewline = ModernKey.Hook.KeySender.ShouldUseClipboard("a\nb", false);
            bool clipShort = ModernKey.Hook.KeySender.ShouldUseClipboard("ngan", false);
            bool clipForce = ModernKey.Hook.KeySender.ShouldUseClipboard("ngan", true);

            if (clipEmoji && clipLong && clipNewline && !clipShort && clipForce)
                sb.AppendLine("  PASS: Kiểm tra ngưỡng Sequence vs Clipboard (Emoji surrogate, >=60 chars, newline, force)");
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Clipboard Threshold: Emoji={clipEmoji}, Long={clipLong}, NL={clipNewline}, Short={clipShort}, Force={clipForce}");
            }

            // 8. Test Punctuation Triggers (. , ! ? ; - /)
            string punctDot = SimulateTypingSentence(macroEngine, "emogrin.");
            string punctExcl = SimulateTypingSentence(macroEngine, "emogrin!");
            string punctComma = SimulateTypingSentence(macroEngine, "vn,");
            string punctQuest = SimulateTypingSentence(macroEngine, "vn?");
            string punctSemi = SimulateTypingSentence(macroEngine, "vn;");
            string punctHyphen = SimulateTypingSentence(macroEngine, "vn-");

            if (punctDot == "😏😏😏." && punctExcl == "😏😏😏!" && punctComma == "việt nam," &&
                punctQuest == "việt nam?" && punctSemi == "việt nam;" && punctHyphen == "việt nam-")
            {
                sb.AppendLine("  PASS: Punctuation trigger (. , ! ? ; -) giữ lại dấu câu phía sau macro");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Punctuation Trigger: dot='{punctDot}', excl='{punctExcl}', comma='{punctComma}', quest='{punctQuest}', semi='{punctSemi}', hyphen='{punctHyphen}'");
            }

            // 9. Test phim ESC dung ngay go tat
            macroEngine.Reset();
            // Go 'emogr'
            foreach (char c in "emogr") macroEngine.ProcessKey(c, (int)c, false, false, false, false, out _, out _);
            // Bam ESC (vkCode = 0x1B)
            macroEngine.ProcessKey('\0', 0x1B, false, false, false, false, out _, out _);
            // Go tiep 'in.'
            string escOut = "";
            foreach (char c in "in.")
            {
                if (macroEngine.ProcessKey(c, (int)c, false, false, false, false, out int escBc, out string escStr))
                {
                    if (escBc > 0 && escOut.Length >= escBc) escOut = escOut.Substring(0, escOut.Length - escBc);
                    escOut += escStr;
                }
                else
                {
                    escOut += c;
                }
            }
            if (escOut == "in.")
            {
                sb.AppendLine("  PASS: Bấm phím ESC dừng ngay phiên gõ tắt (không bị bung 'emogrin.')");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL ESC cancel macro: out='{escOut}' (Mong đợi: 'in.')");
            }

            // 10. Test Giai ma CESU-8 Macro File va Go Emotion tu file OpenKey C++
            string cppMacroPath = @"R:\HDD R\ZC SYMLINK\USERS\source\repos\ghostminhtoan\openkey\Sources\OpenKey\win32\OpenKey\x64\Release\openkeymacro.txt";
            if (System.IO.File.Exists(cppMacroPath))
            {
                var fullMgr = new MacroManager();
                int imported = fullMgr.ImportFromFile(cppMacroPath, false);
                string emoGrin = null;
                string emoHaha = null;
                bool okGrin = fullMgr.TryGetMacro("emogrin", out emoGrin);
                bool okHaha = fullMgr.TryGetMacro("emohaha", out emoHaha);

                if (imported > 1000 && okGrin && emoGrin == "😏😏😏" && okHaha && emoHaha == "😆😆")
                {
                    sb.AppendLine($"  PASS: Giải mã CESU-8 từ file OpenKey C++ thành công ({imported} mục, emotion 'emogrin' = '{emoGrin}', 'emohaha' = '{emoHaha}')");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Nạp file macro C++ thất bại (imported={imported}, emogrin='{emoGrin}')");
                }
            }

            // 11. Test Clipboard Snapshot Backup / Restore Flow với Hình Ảnh
            try
            {
                // Đặt 1 bitmap test vào clipboard
                using (var bmp = new System.Drawing.Bitmap(32, 32))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Red);
                    }
                    System.Windows.Forms.Clipboard.SetImage(bmp);
                }

                bool hasImageBefore = System.Windows.Forms.Clipboard.ContainsImage();
                sb.AppendLine($"  [TEST CLIP] Trước khi gọi macro: ContainsImage = {hasImageBefore}");

                // Kích hoạt SendViaClipboardPaste với chuỗi test
                ModernKey.Hook.KeySender.SendViaClipboardPaste("TestMacroExpansionText");

                bool hasTextDuring = System.Windows.Forms.Clipboard.ContainsText();
                string textDuring = hasTextDuring ? System.Windows.Forms.Clipboard.GetText() : "NO_TEXT";
                sb.AppendLine($"  [TEST CLIP] Trong lúc dán: ContainsText = {hasTextDuring}, Text = '{textDuring}'");

                // Đợi 1700ms để Timer phục hồi ảnh (1500ms) chạy xong hoàn tất
                System.Threading.Thread.Sleep(1700);

                bool hasImageAfter = System.Windows.Forms.Clipboard.ContainsImage();
                bool hasTextAfter = System.Windows.Forms.Clipboard.ContainsText();
                sb.AppendLine($"  [TEST CLIP] Sau phục hồi ảnh: ContainsImage = {hasImageAfter}, ContainsText = {hasTextAfter}");

                if (hasImageBefore && hasTextDuring && textDuring == "TestMacroExpansionText" && hasImageAfter && !hasTextAfter)
                {
                    sb.AppendLine("  PASS: Kiểm tra Clipboard Image Backup -> Macro Paste -> Restore Image thành công 100%!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Clipboard Image Flow thất bại: BeforeImg={hasImageBefore}, DuringTxt={hasTextDuring}('{textDuring}'), AfterImg={hasImageAfter}, AfterTxt={hasTextAfter}");
                }
            }
            catch (Exception ex)
            {
                allPassed = false;
                sb.AppendLine($"  FAIL KeySender Clipboard Image: {ex.Message}\n{ex.StackTrace}");
            }

            // 12. Test Clipboard Snapshot Backup / Restore Flow với Văn Bản (Text - an toàn 800ms)
            try
            {
                const string originalText = "VanBanGocTruocKhiGoTat_12345";
                System.Windows.Forms.Clipboard.SetText(originalText);
                System.Threading.Thread.Sleep(20);

                ModernKey.Hook.KeySender.SendViaClipboardPaste("ChuoiMacroThayThe_67890");

                // Đợi 1000ms để Timer phục hồi văn bản an toàn (800ms) hoàn tất trọn vẹn
                System.Threading.Thread.Sleep(1000);

                bool hasTextAfter = System.Windows.Forms.Clipboard.ContainsText();
                string textAfter = hasTextAfter ? System.Windows.Forms.Clipboard.GetText() : null;

                if (textAfter == originalText)
                {
                    sb.AppendLine("  PASS: Kiểm tra Clipboard Text Backup -> Macro Paste -> Restore Text nhanh thành công 100%!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Clipboard Text Flow thất bại: Expected '{originalText}', Actual '{textAfter}'");
                }
            }
            catch (Exception ex)
            {
                allPassed = false;
                sb.AppendLine($"  FAIL KeySender Clipboard Text: {ex.Message}\n{ex.StackTrace}");
            }

            // 13. Test Cấu hình Tab Phím tắt chuẩn OpenKey C++
            try
            {
                var shortcutSettings = new AppSettings();
                bool defaultModOk = shortcutSettings.ShortcutModifier == 0x05; // Ctrl (1) + Alt (4) = 5
                bool defaultMaskOk = shortcutSettings.ShortcutEnableMask == 0x1FFF;
                bool defaultF4Ok = shortcutSettings.ShortcutF4Charset == Charset.VniWindows;

                bool f1Ok = (shortcutSettings.ShortcutEnableMask & (1 << 1)) != 0;
                bool f2Ok = (shortcutSettings.ShortcutEnableMask & (1 << 2)) != 0;
                bool f3Ok = (shortcutSettings.ShortcutEnableMask & (1 << 3)) != 0;
                bool f4Ok = (shortcutSettings.ShortcutEnableMask & (1 << 4)) != 0;
                bool f5Ok = (shortcutSettings.ShortcutEnableMask & (1 << 5)) != 0;
                bool f8Ok = (shortcutSettings.ShortcutEnableMask & (1 << 8)) != 0;
                bool f9Ok = (shortcutSettings.ShortcutEnableMask & (1 << 9)) != 0;
                bool f11Ok = (shortcutSettings.ShortcutEnableMask & (1 << 11)) != 0;
                bool f12Ok = (shortcutSettings.ShortcutEnableMask & (1 << 12)) != 0;

                if (defaultModOk && defaultMaskOk && defaultF4Ok && f1Ok && f2Ok && f3Ok && f4Ok && f5Ok && f8Ok && f9Ok && f11Ok && f12Ok)
                {
                    sb.AppendLine("  PASS: Kiểm tra Cấu hình và Bitmask Tab Phím tắt chuẩn OpenKey C++ thành công 100%!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Cấu hình Phím tắt thất bại: Mod={defaultModOk}, Mask={defaultMaskOk}, F4={defaultF4Ok}");
                }
            }
            catch (Exception ex)
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Cấu hình Tab Phím tắt: {ex.Message}\n{ex.StackTrace}");
            }

            // 14. Test Kiểu gõ Tự định nghĩa (Custom Input Method - 33 Actions & Presets)
            try
            {
                sb.AppendLine("[TEST CUSTOM INPUT METHOD - PRESETS & RULES]");

                // 14.1 Preset Telex
                var customTelexSettings = new AppSettings
                {
                    IsVietnamese = true,
                    CurrentInputMethod = InputMethod.Custom,
                    ModernToneRules = true,
                    CustomRules = CustomInputRule.GetPreset(0) // Telex
                };
                var customTelexEngine = new VietnameseEngine(customTelexSettings, new MacroManager());
                var testCasesCustomTelex = new (string input, string expected)[]
                {
                    ("tieets", "tiết"),
                    ("vieetj", "việt"),
                    ("dduwowngf", "đường"),
                    ("toanf", "toàn")
                };
                foreach (var tc in testCasesCustomTelex)
                {
                    string res = SimulateTypingWord(customTelexEngine, tc.input);
                    if (res == tc.expected)
                    {
                        sb.AppendLine($"  PASS Custom Telex: '{tc.input}' -> '{res}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL Custom Telex: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                    }
                }

                // 14.2 Preset VNI
                var customVniSettings = new AppSettings
                {
                    IsVietnamese = true,
                    CurrentInputMethod = InputMethod.Custom,
                    ModernToneRules = true,
                    CustomRules = CustomInputRule.GetPreset(1) // VNI
                };
                var customVniEngine = new VietnameseEngine(customVniSettings, new MacroManager());
                var testCasesCustomVni = new (string input, string expected)[]
                {
                    ("e6m", "êm"),
                    ("Tie6t1", "Tiết"),
                    ("D9ao5", "Đạo"),
                    ("toan2", "toàn")
                };
                foreach (var tc in testCasesCustomVni)
                {
                    string res = SimulateTypingWord(customVniEngine, tc.input);
                    if (res == tc.expected)
                    {
                        sb.AppendLine($"  PASS Custom VNI: '{tc.input}' -> '{res}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL Custom VNI: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                    }
                }

                // 14.3 Preset Tư Bình Trần đơn giản
                var customTbtSettings = new AppSettings
                {
                    IsVietnamese = true,
                    CurrentInputMethod = InputMethod.Custom,
                    ModernToneRules = true,
                    CustomRules = CustomInputRule.GetPreset(3) // TBT đơn giản
                };
                var customTbtEngine = new VietnameseEngine(customTbtSettings, new MacroManager());
                var testCasesCustomTbt = new (string input, string expected)[]
                {
                    ("tr7n", "trên"),
                    ("c6n", "cân"),
                    ("dd[]2ng", "đường"),
                    ("66", "6") // Phục hồi phím số gốc khi gõ lặp lại
                };
                foreach (var tc in testCasesCustomTbt)
                {
                    string res = SimulateTypingWord(customTbtEngine, tc.input);
                    if (res == tc.expected)
                    {
                        sb.AppendLine($"  PASS Custom TBT: '{tc.input}' -> '{res}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL Custom TBT: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                    }
                }

                // 14.4 Custom User Rule tự tạo (ví dụ: 'q' làm dấu sắc, 'j' làm dấu nặng, 'w' làm dấu móc)
                var userRules = new List<CustomInputRule>
                {
                    new CustomInputRule('q', 1), // sắc
                    new CustomInputRule('f', 2), // huyền
                    new CustomInputRule('j', 5), // nặng
                    new CustomInputRule('w', 16), // móc ă, ư, ơ
                    new CustomInputRule('d', 15)  // đ
                };
                var userCustomSettings = new AppSettings
                {
                    IsVietnamese = true,
                    CurrentInputMethod = InputMethod.Custom,
                    ModernToneRules = true,
                    CustomRules = userRules
                };
                var userCustomEngine = new VietnameseEngine(userCustomSettings, new MacroManager());
                string userRes1 = SimulateTypingWord(userCustomEngine, "toanq");
                string userRes2 = SimulateTypingWord(userCustomEngine, "dduwngj");
                if (userRes1 == "toán" && userRes2 == "đựng")
                {
                    sb.AppendLine("  PASS: Custom User Rule tự tạo ('q' làm dấu sắc, 'duwngj' -> 'đựng') thành công!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Custom User Rule: toanq='{userRes1}', duwngj='{userRes2}'");
                }

                // 14.5 Cấu hình Serialize / Deserialize CustomRules
                // 14.5 Cấu hình Serialize / Deserialize CustomRules
                var originalSettings = SettingsManager.LoadSettings();
                try
                {
                    var testSettings = new AppSettings
                    {
                        CurrentInputMethod = InputMethod.Custom,
                        CustomRules = new List<CustomInputRule>
                        {
                            new CustomInputRule('a', 7),
                            new CustomInputRule('w', 16)
                        }
                    };
                    SettingsManager.SaveSettings(testSettings);
                    var loadedSettings = SettingsManager.LoadSettings();
                    if (loadedSettings.CurrentInputMethod == InputMethod.Custom &&
                        loadedSettings.CustomRules != null &&
                        loadedSettings.CustomRules.Count == 2 &&
                        loadedSettings.CustomRules[0].Key == 'a' && loadedSettings.CustomRules[0].Action == 7 &&
                        loadedSettings.CustomRules[1].Key == 'w' && loadedSettings.CustomRules[1].Action == 16)
                    {
                        sb.AppendLine("  PASS: Serialize / Deserialize CustomRules trong SettingsManager hoàn hảo 100%!");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL: Serialize / Deserialize CustomRules thất bại (Count={loadedSettings.CustomRules?.Count})");
                    }
                }
                finally
                {
                    SettingsManager.SaveSettings(originalSettings);
                }
                // 15. Kiểm tra từ điển chính tả vi_VN.dic
                SpellingDictionary.Instance.LoadDictionary();
                bool dicValid1 = SpellingDictionary.Instance.IsValidWord("tiếng");
                bool dicValid2 = SpellingDictionary.Instance.IsValidWord("việt");
                bool dicValid3 = SpellingDictionary.Instance.IsValidWord("trên");
                bool dicInvalid = SpellingDictionary.Instance.IsValidWord("asdfzxcv");
                if (dicValid1 && dicValid2 && dicValid3 && !dicInvalid)
                {
                    sb.AppendLine($"  PASS: Từ điển vi_VN.dic nạp thành công {SpellingDictionary.Instance.WordCount:N0} từ và tra cứu O(1) chính xác!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Từ điển vi_VN.dic tra cứu sai ('tiếng'={dicValid1}, 'asdfzxcv'={dicInvalid})");
                }

                // 16. Kiểm tra EscKeyUndo
                var escSettings = new AppSettings { IsVietnamese = true, CurrentInputMethod = InputMethod.Telex, EscKeyUndo = true };
                var escEngine = new VietnameseEngine(escSettings, new MacroManager());
                SimulateTypingWord(escEngine, "tieets");
                if (escEngine.HandleEscUndo(out int undoBc, out string undoStr) && undoBc == 4 && undoStr == "tieets")
                {
                    sb.AppendLine("  PASS: EscKeyUndo hoàn tác từ tiếng Việt 'tiết' về chuỗi thô 'tieets' thành công!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: EscKeyUndo thất bại (undoBc={undoBc}, undoStr='{undoStr}')");
                }

                // 17. Kiểm tra SmartCodePassthrough
                var codeSettings = new AppSettings { IsVietnamese = true, CurrentInputMethod = InputMethod.Telex, SmartCodePassthrough = true };
                var codeEngine = new VietnameseEngine(codeSettings, new MacroManager());
                string codeRes1 = SimulateTypingWord(codeEngine, "const ");
                string codeRes2 = SimulateTypingWord(codeEngine, "https://");
                if (codeRes1 == "const " && codeRes2 == "https://")
                {
                    sb.AppendLine("  PASS: SmartCodePassthrough giữ nguyên từ khóa code 'const' và 'https://' không bị ép dấu!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: SmartCodePassthrough thất bại ('{codeRes1}', '{codeRes2}')");
                }

                // 18. Kiểm tra Profile Application
                var profSettings = new AppSettings();
                profSettings.ApplyProfile("Gaming");
                bool gOk = (!profSettings.UseMacro && !profSettings.CheckSpelling && profSettings.AutoExcludeEnabled);
                profSettings.ApplyProfile("Coding");
                bool cOk = (profSettings.SmartCodePassthrough && profSettings.EscKeyUndo && profSettings.AllowConsonantZFWJ);
                if (gOk && cOk)
                {
                    sb.AppendLine("  PASS: Quản lý Profile (Gaming, Coding, Office) áp dụng cấu hình chính xác!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Profile application thất bại (gOk={gOk}, cOk={cOk})");
                }

                // 19. Kiểm tra JSON Profile Export / Import
                string tempJsonPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "modernkey_test_profile.json");
                try
                {
                    var expSettings = new AppSettings { ActiveProfile = "Coding", SmartCodePassthrough = true, EscKeyUndo = true };
                    bool expOk = SettingsManager.ExportProfileJson(expSettings, tempJsonPath);
                    var impSettings = new AppSettings();
                    bool impOk = SettingsManager.ImportProfileJson(tempJsonPath, impSettings);
                    if (expOk && impOk && impSettings.ActiveProfile == "Coding" && impSettings.SmartCodePassthrough && impSettings.EscKeyUndo)
                    {
                        sb.AppendLine("  PASS: Xuất và nhập cấu hình Profile JSON độc lập thành công 100%!");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL: JSON Export/Import thất bại (exp={expOk}, imp={impOk})");
                    }
                }
                finally
                {
                    if (System.IO.File.Exists(tempJsonPath)) System.IO.File.Delete(tempJsonPath);
                }
            }
            catch (Exception ex)
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Custom Input Method: {ex.Message}\n{ex.StackTrace}");
            }

            sb.AppendLine(allPassed ? "=== TAT CA CAC TEST DEU PASS 100% ===" : "=== CO TEST THAT BAI ===");
            report = sb.ToString();
            return allPassed;
        }

        private static string SimulateTypingWord(VietnameseEngine engine, string input)
        {
            engine.Reset();
            var screen = new StringBuilder();

            foreach (char ch in input)
            {
                int vk = (int)ch;
                if (ch == '\b') vk = 0x08;
                bool isShift = char.IsUpper(ch);

                if (engine.ProcessKey(ch, vk, isShift, false, false, false,
                                      out int backspaceCount, out string newString, out int trailingVkCode))
                {
                    if (backspaceCount > 0)
                    {
                        int removeLen = Math.Min(backspaceCount, screen.Length);
                        screen.Remove(screen.Length - removeLen, removeLen);
                    }
                    if (!string.IsNullOrEmpty(newString))
                    {
                        screen.Append(newString);
                    }
                    if (trailingVkCode == 0x20)
                    {
                        screen.Append(' ');
                    }
                    else if (trailingVkCode == 0x0D)
                    {
                        screen.Append('\n');
                    }
                }
                else
                {
                    if (vk == 0x08)
                    {
                        // Phím Backspace khi không bị engine nuốt thì ứng dụng đích tự xóa 1 ký tự
                        if (screen.Length > 0)
                        {
                            screen.Remove(screen.Length - 1, 1);
                        }
                    }
                    else
                    {
                        screen.Append(ch);
                    }
                }
            }

            return screen.ToString();
        }

        private static string SimulateTypingSentence(VietnameseEngine engine, string sentence)
        {
            engine.Reset();
            var screen = new StringBuilder();

            foreach (char ch in sentence)
            {
                int vk = (int)ch;
                if (ch == ' ') vk = 0x20;
                else if (ch == '\r' || ch == '\n') vk = 0x0D;

                bool isShift = char.IsUpper(ch);

                if (engine.ProcessKey(ch, vk, isShift, false, false, false,
                                      out int backspaceCount, out string newString, out int trailingVkCode))
                {
                    if (backspaceCount > 0)
                    {
                        int removeLen = Math.Min(backspaceCount, screen.Length);
                        screen.Remove(screen.Length - removeLen, removeLen);
                    }
                    if (!string.IsNullOrEmpty(newString))
                    {
                        screen.Append(newString);
                    }
                    if (trailingVkCode == 0x20)
                    {
                        screen.Append(' ');
                    }
                    else if (trailingVkCode == 0x0D)
                    {
                        screen.Append('\n');
                    }
                }
                else
                {
                    screen.Append(ch);
                }
            }

            return screen.ToString();
        }
    }
}
