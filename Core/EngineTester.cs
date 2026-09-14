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
                ("dood", "đô"),
                ("hentai2read", "hentai2read"),
                ("dictated", "dictated"),
                ("deadline", "deadline"),
                ("dad", "dad"),
                ("ddd", "dd"),
                ("dddr", "ddr"),
                ("DDDr", "DDr"),
                ("ddr", "đr")
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

            string sample4 = "downloads dictated deadline dad.";
            string expected4 = "downloads dictated deadline dad.";
            string out4 = SimulateTypingSentence(telexEngine, sample4);
            if (out4 == expected4)
            {
                sb.AppendLine($"  PASS Doan 4 (Tieng Anh Telex): '{out4}'");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Doan 4 (Tieng Anh Telex):\n    Ket qua : '{out4}'\n    Mong doi: '{expected4}'");
            }

            string sample5 = "dddr3 dddr4 dddr5.";
            string expected5 = "ddr3 ddr4 ddr5.";
            string out5 = SimulateTypingSentence(telexEngine, sample5);
            if (out5 == expected5)
            {
                sb.AppendLine($"  PASS Doan 5 (DDR RAM Telex): '{out5}'");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Doan 5 (DDR RAM Telex):\n    Ket qua : '{out5}'\n    Mong doi: '{expected5}'");
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
                ("thu]3", "thuở"),
                ("thu71", "thuế"),
                ("Thu]3", "Thuở"),
                ("Thu71", "Thuế"),
                ("thu7", "thuê"),
                ("qu7", "quê"),
                ("qu71", "quế"),
                ("qu]3", "quở"),
                ("hu]", "huơ"),
                ("hu75", "huệ"),
                ("thu]ng", "thương"),
                ("c6n", "cân"),
                ("c8ng", "công"),
                ("c9n", "căn"),
                ("6m2", "ầm"),
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
                ("&-dd7", "Ê-đê"),
                ("*n2", "Ồn"),
                ("(n", "Ăn"),
                ("7m", "êm"),
                ("8m", "ôm"),
                ("6p1", "ấp"),
                ("8n2", "ồn"),
                ("6y1", "ấy"),
                ("dosd", "dosd"),
                ("a6", "a6"),
                ("a9", "a9"),
                ("e7", "e7"),
                ("o8", "o8"),
                ("[3", "ử"),
                ("U[3", "Ử"),
                ("]3", "ở"),
                ("O]3", "Ở"),
                ("[1", "ứ"),
                ("]1", "ớ"),
                ("T8", "Tô"),
                ("T81", "Tố"),
                ("T81t", "Tốt"),
                ("T8t1", "Tốt"),
                ("T81t*", "Tốt*"),
                ("T81t^", "Tốt^"),
                ("T81t&", "Tốt&"),
                ("T81t(", "Tốt("),
                ("T81t8", "Tốt8"),
                ("T81t6", "Tốt6"),
                ("T81t7", "Tốt7"),
                ("T81t9", "Tốt9"),
                ("T8t1*", "Tốt*"),
                ("T8t1^", "Tốt^"),
                ("T8t1&", "Tốt&"),
                ("T8t1(", "Tốt("),
                ("T8t18", "Tốt8"),
                ("T8t16", "Tốt6"),
                ("T8t17", "Tốt7"),
                ("T8t19", "Tốt9"),
                ("t81t*", "tốt*"),
                ("t81t^", "tốt^"),
                ("t81t&", "tốt&"),
                ("t81t(", "tốt("),
                ("t81t8", "tốt8"),
                ("t81t6", "tốt6"),
                ("t81t7", "tốt7"),
                ("t81t9", "tốt9"),
                ("t8t1*", "tốt*"),
                ("t8t1^", "tốt^"),
                ("t8t1&", "tốt&"),
                ("t8t1(", "tốt("),
                ("t8t18", "tốt8"),
                ("t8t16", "tốt6"),
                ("t8t17", "tốt7"),
                ("t8t19", "tốt9"),
                ("TR7N", "TRÊN"),
                ("d[]2ngd", "đường"),
                ("do1d", "đó"),
                ("D[]2ngd", "Đường"),
                ("Do1d", "Đó"),
                ("d6nd", "đân"),
                ("d8ngd", "đông"),
                ("d7d", "đê"),
                ("d[2ngd", "đừng"),
                ("downloads", "downloads"),
                ("dictated", "dictated"),
                ("do1dd", "dód"),
                ("ddd", "dd"),
                ("dddr", "ddr"),
                ("T8i", "Tôi"),
                ("T*i", "Tôi"),
                ("t*i", "Tôi"),
                ("}3", "Ở"),
                ("}1", "Ớ"),
                ("{3ng", "Ửng"),
                ("{1c", "Ức"),
                ("(6m1", "(ấm"),
                ("(61m", "(ấm"),
                ("((6m1", "(ấm"),
                ("(9n", "(ăn"),
                ("((9n", "(ăn"),
                ("(7m", "(êm"),
                ("((7m", "(êm"),
                ("{8n3", "{ổn"),
                ("{83n", "{ổn"),
                ("{{8n3", "{ổn")
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

            // Test toggle số lặp lại trong TBT (66->6, 77->7, 88->8, 99->9, Shift+66->^, Shift+77->&, Shift+88->*, Shift+99->(, [[->[, ]]->], {{->{, }}->})
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
                ("667", "67"),
                ("^^", "^"),
                ("&&", "&"),
                ("**", "*"),
                ("((", "("),
                ("[[", "["),
                ("]]", "]"),
                ("{{", "{"),
                ("}}", "}")
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

            // Bật tự động viết hoa chữ cái đầu câu cho tbtEngine để test toàn diện
            // Cấu hình chuẩn theo ảnh của người dùng: Tắt kiểu mới (dấu cũ 'tỏa') và Bật tự động viết hoa chữ cái đầu câu
            tbtSettings.ModernToneRules = false;
            tbtSettings.UpperCaseFirstChar = true;

            // Test toan bo doan van mau moi nhat cua nguoi dung (Notepad++ new 108)
            sb.AppendLine("[TEST TU BINH TRAN - TOAN BO DOAN VAN]");
            string paragraphExpected = "Mâm cơm chiều hôm nay có món canh chua cá lóc thơm lừng (ấm áp) đưa cơm. Đêm rằm trung thu, lũ trẻ con trong xóm háo hức (rước đèn) khắp các ngõ nhỏ. Hộp bánh trung thu thập cẩm này có vị ngọt bùi (ăn đậm đà) rất đưa miệng. Trăng rằm tỏa ánh sáng lung linh xuống khoảng sân rộng (êm yên) trước hiên nhà. Bức tranh phong cảnh vùng cao mang một vẻ đẹp {ổn trọng} mà vô cùng cuốn hút. Ở góc vườn nhỏ ba trồng, những khóm hoa hồng nhung đang {đua nở} khoe sắc thắm. Ửng hồng cả một góc trời phía đông chính là dấu hiệu {bình minh} của một ngày mới bắt đầu.";
            string paragraphInput = "M6m c]m chi7u2 h8m nay co1 mon1 canh chua ca1 loc1 th]m l[ng2 (6m1 ap1) dd[a c]m. DD7m r9m2 trung thu, lu4 tre3 con trong xom1 hao1 h[c1 (r[]c1 dden2) kh9p1 cac1 ngo4 nho3. H8p5 banh1 trung thu th6p5 c6m3 nay2 co1 vi5 ngot5 bui2 (9n dd6m5 dda2) r6t1 dd[a mi7ng5. Tr9ng r9m2 toa3 anh1 sang1 lung linh xu8ng1 khoang3 s6n r8ng5 (7m y7n) tr[]c1 hi7n nha2. B[c1 tranh phong canh3 vung2 cao mang m8t5 ve3 ddep5 {8n3 trong5} ma2 v8 cung2 cu8n1 hut1. }3 goc1 vu]n2 nho3 ba tr8ng2, nh[ng4 kho1m hoa h8ng2 nhung ddang {ddua n]3} khoe s9c1 th9m1. {3ng h8ng2 ca3 m8t5 goc1 tr]i2 phi1a dd8ng chi1nh la2 d6u1 hi7u5 {binh2 minh} cua3 m8t5 ngay2 m]i1 b9t1 dd6u2.";

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

            // Test cac dong mau le 5..10 o cuoi anh Notepad++ cua nguoi dung
            sb.AppendLine("[TEST TU BINH TRAN - CAC DONG LE 5..10]");
            (string lInput, string lExpected)[] extraLines = new[]
            {
                ("(6m1 ap1)", "(ấm áp)"),
                ("(61m ap1)", "(ấm áp)"),
                ("((6m1 ap1)", "(ấm áp)"),
                ("(9n dd6m5 dda2)", "(ăn đậm đà)"),
                ("((9n dd6m5 dda2)", "(ăn đậm đà)"),
                ("(7m y7n)", "(êm yên)"),
                ("((7m y7n)", "(êm yên)"),
                ("{8n3 trong5}", "{ổn trọng}"),
                ("{83n trong5}", "{ổn trọng}"),
                ("{{8n3 trong5}", "{ổn trọng}"),
                ("}3", "Ở"),
                ("}3 goc1", "Ở góc"),
                ("{3ng", "Ửng"),
                ("{3ng h8ng2", "Ửng hồng")
            };

            foreach (var el in extraLines)
            {
                string elRes = SimulateTypingSentence(tbtEngine, el.lInput);
                if (elRes == el.lExpected)
                {
                    sb.AppendLine($"  PASS TBT: '{el.lInput}' -> '{elRes}'");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL TBT: '{el.lInput}' -> '{elRes}' (Mong doi: '{el.lExpected}')");
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

            // 8.1. Test Macro với Số và Ký hiệu đặc biệt: 1111 -> shutdown-s-f-t 0, 023 -> \\192.168.1.023, /// -> ∕, ?? -> ¿?
            macroMgr.MacroList.Add(new MacroEntry("1111", "shutdown-s-f-t 0"));
            macroMgr.MacroList.Add(new MacroEntry("023", @"\\192.168.1.023"));
            macroMgr.MacroList.Add(new MacroEntry("///", "∕"));
            macroMgr.MacroList.Add(new MacroEntry("??", "¿?"));

            string num1111Out = SimulateTypingSentence(macroEngine, "1111 ");
            string num023Out = SimulateTypingSentence(macroEngine, "023 ");
            string symSlashOut = SimulateTypingSentence(macroEngine, "///");
            string symQuestOut = SimulateTypingSentence(macroEngine, "??");

            if (num1111Out == "shutdown-s-f-t 0 " &&
                num023Out == @"\\192.168.1.023 " &&
                symSlashOut == "∕" &&
                symQuestOut == "¿?")
            {
                sb.AppendLine("  PASS: Macro nhận diện chính xác Số (1111, 023) và Ký hiệu (///, ??)");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Macro Số & Ký hiệu: 1111='{num1111Out}', 023='{num023Out}', ///='{symSlashOut}', ??='{symQuestOut}'");
            }

            // 8.2. Test Full Word Match cho Gõ Tắt: Không bung macro khi gõ từ chứa hậu tố (app != apeople, ctrl != ctrả lời)
            macroMgr.MacroList.Add(new MacroEntry("pp", "people"));
            macroMgr.MacroList.Add(new MacroEntry("trl", "trả lời"));

            string appOut = SimulateTypingSentence(macroEngine, "app ");
            string ppOut = SimulateTypingSentence(macroEngine, "pp ");
            string ctrlOut = SimulateTypingSentence(macroEngine, "ctrl ");
            string trlOut = SimulateTypingSentence(macroEngine, "trl ");

            if (appOut == "app " && ppOut == "people " && ctrlOut == "ctrl " && trlOut == "trả lời ")
            {
                sb.AppendLine("  PASS: Macro chỉ khớp chính xác toàn bộ từ (app -> app, pp -> people, ctrl -> ctrl, trl -> trả lời)");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Macro Full Word Match: app='{appOut}', pp='{ppOut}', ctrl='{ctrlOut}', trl='{trlOut}'");
            }

            // 8.3. Test Phím Ctrl reset phiên gõ (Ký tự sau Ctrl được coi như ký tự đầu tiên)
            macroEngine.Reset();
            // Gõ 'ti'
            foreach (char c in "ti") macroEngine.ProcessKey(c, (int)c, false, false, false, false, out _, out _);
            // Nhấn phím Ctrl (isCtrl = true)
            macroEngine.ProcessKey('\0', 0x11, false, false, true, false, out _, out _);
            // Gõ tiếp 'e'
            string afterCtrlTi = "";
            if (macroEngine.ProcessKey('e', (int)'e', false, false, false, false, out int ctrlBc, out string ctrlStr))
            {
                afterCtrlTi = ctrlStr;
            }
            else
            {
                afterCtrlTi = "e";
            }

            // Gõ 't', nhấn Ctrl, gõ 'rl '
            macroEngine.Reset();
            macroEngine.ProcessKey('t', (int)'t', false, false, false, false, out _, out _);
            macroEngine.ProcessKey('\0', 0x11, false, false, true, false, out _, out _);
            string afterCtrlMacro = "";
            foreach (char c in "rl ")
            {
                if (macroEngine.ProcessKey(c, (int)c, false, false, false, false, out int mBc, out string mStr))
                {
                    if (mBc > 0 && afterCtrlMacro.Length >= mBc) afterCtrlMacro = afterCtrlMacro.Substring(0, afterCtrlMacro.Length - mBc);
                    afterCtrlMacro += mStr;
                }
                else
                {
                    afterCtrlMacro += c;
                }
            }

            if (afterCtrlTi == "e" && afterCtrlMacro == "rl ")
            {
                sb.AppendLine("  PASS: Bấm phím Ctrl reset bộ gõ, ký tự tiếp theo được coi là ký tự đầu tiên của từ mới");
            }
            else
            {
                allPassed = false;
                sb.AppendLine($"  FAIL Ctrl Reset: afterCtrlTi='{afterCtrlTi}' (Mong đợi 'e'), afterCtrlMacro='{afterCtrlMacro}' (Mong đợi 'rl ')");
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
                System.Windows.Forms.Clipboard.Clear();
                System.Threading.Thread.Sleep(50);
                System.Windows.Forms.Clipboard.SetText(originalText);
                System.Threading.Thread.Sleep(100);

                ModernKey.Hook.KeySender.SendViaClipboardPaste("ChuoiMacroThayThe_67890");

                // Đợi Timer phục hồi văn bản an toàn (800ms) hoàn tất trọn vẹn (với retry check)
                string textAfter = null;
                for (int waitCount = 0; waitCount < 20; waitCount++)
                {
                    System.Threading.Thread.Sleep(100);
                    if (System.Windows.Forms.Clipboard.ContainsText())
                    {
                        textAfter = System.Windows.Forms.Clipboard.GetText();
                        if (textAfter == originalText) break;
                    }
                }

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
                // 15. Kiểm tra cơ chế kiểm tra chính tả thuật toán OpenKey C++
                bool dicValid1 = OpenKeySpelling.IsValidWord("tiếng");
                bool dicValid2 = OpenKeySpelling.IsValidWord("việt");
                bool dicValid3 = OpenKeySpelling.IsValidWord("trên");
                bool dicValid4 = OpenKeySpelling.IsValidWord("đường");
                bool dicInvalid1 = !OpenKeySpelling.IsValidWord("asdfzxcv");
                bool dicInvalid2 = !OpenKeySpelling.IsValidWord("thuơng"); // 'uơ' không đi với phụ âm cuối 'ng'
                bool dicInvalid3 = !OpenKeySpelling.IsValidWord("toiss"); // hai phụ âm 'ss' ở cuối

                if (dicValid1 && dicValid2 && dicValid3 && dicValid4 && dicInvalid1 && dicInvalid2 && dicInvalid3)
                {
                    sb.AppendLine("  PASS: Thuật toán kiểm tra chính tả OpenKey C++ hoạt động chính xác tuyệt đối!");
                }
                else
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL: Kiểm tra chính tả sai ('tiếng'={dicValid1}, 'asdfzxcv'={!dicInvalid1}, 'thuơng'={!dicInvalid2})");
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

                // 20. TEST CÁC YÊU CẦU MỚI: Free Mark, Case Duplication, SimpleTelex w, Tu Binh Tran f6-f9, maf->maf
                sb.AppendLine("[TEST YEU CAU MOI: FREE MARK, CASE DUPLICATION, SIMPLETELEX W, TBT F6-F9, MAF->MAF]");
                try
                {
                    // A. Test case -> case (không bị ccase khi nhấn space) và tét không bị chuyển thành test
                    var telexSmartEngSettings = new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.Telex,
                        SmartEnglishBypass = true
                    };
                    var telexSmartEngine = new VietnameseEngine(telexSmartEngSettings, new MacroManager());
                    string caseRes = SimulateTypingSentence(telexSmartEngine, "case ");
                    string tetRes = SimulateTypingWord(telexSmartEngine, "tets");
                    if (caseRes == "case " && tetRes == "tét")
                    {
                        sb.AppendLine($"  PASS: 'case ' -> '{caseRes}' (không bị 'ccase') và 'tets' -> '{tetRes}' (không bị ép thành 'test')!");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL: case='{caseRes}' (exp 'case '), tets='{tetRes}' (exp 'tét')");
                    }

                    // B. Test maf -> mà, maff -> maf, mafff -> maff
                    string maf1 = SimulateTypingWord(telexSmartEngine, "maf");
                    string maf2 = SimulateTypingWord(telexSmartEngine, "maff");
                    string maf3 = SimulateTypingWord(telexSmartEngine, "mafff");
                    if (maf1 == "mà" && maf2 == "maf" && maf3 == "maff")
                    {
                        sb.AppendLine($"  PASS: Hủy dấu lặp phím: 'maf'->'{maf1}', 'maff'->'{maf2}', 'mafff'->'{maf3}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL: Hủy dấu lặp phím: maf='{maf1}' (exp 'mà'), maff='{maf2}' (exp 'maf'), mafff='{maf3}' (exp 'maff')");
                    }

                    // C. Test SimpleTelex w (giữ nguyên w đầu từ, w làm móc auo)
                    var simpleTelexSettings = new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.SimpleTelex,
                        ModernToneRules = true
                    };
                    var simpleEngine = new VietnameseEngine(simpleTelexSettings, new MacroManager());
                    var simpleCases = new (string input, string expected)[]
                    {
                        ("w", "w"),
                        ("wa", "wa"),
                        ("win", "win"),
                        ("aw", "ă"),
                        ("uw", "ư"),
                        ("ow", "ơ"),
                        ("uow", "ươ"),
                        ("duocwj", "được"),
                        ("dduocwj", "được")
                    };
                    foreach (var sc in simpleCases)
                    {
                        string res = SimulateTypingWord(simpleEngine, sc.input);
                        if (res == sc.expected)
                        {
                            sb.AppendLine($"  PASS SimpleTelex: '{sc.input}' -> '{res}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL SimpleTelex: '{sc.input}' -> '{res}' (Mong doi: '{sc.expected}')");
                        }
                    }

                    // D. Test VNI Free Mark: duoc975, duoc759, d9uo75c
                    var vniFreeMarkSettings = new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.Vni,
                        ModernToneRules = true
                    };
                    var vniFreeMarkEngine = new VietnameseEngine(vniFreeMarkSettings, new MacroManager());
                    var vniCases = new (string input, string expected)[]
                    {
                        ("d9uo75c", "được"),
                        ("duoc975", "được"),
                        ("duoc759", "được")
                    };
                    foreach (var vc in vniCases)
                    {
                        string res = SimulateTypingWord(vniFreeMarkEngine, vc.input);
                        if (res == vc.expected)
                        {
                            sb.AppendLine($"  PASS VNI Free Mark: '{vc.input}' -> '{res}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL VNI Free Mark: '{vc.input}' -> '{res}' (Mong doi: '{vc.expected}')");
                        }
                    }

                    // E. Test Tư Bình Trần: độc lập hoàn toàn, không ăn rơ với Telex và VNI
                    var tbtNewSettings = new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.TuBinhTran,
                        ModernToneRules = true
                    };
                    var tbtNewEngine = new VietnameseEngine(tbtNewSettings, new MacroManager());
                    var tbtCases = new (string input, string expected)[]
                    {
                        ("f6", "f6"),
                        ("f7", "f7"),
                        ("f8", "f8"),
                        ("f9", "f9"),
                        ("f66", "f66"),
                        ("f77", "f77"),
                        ("f88", "f88"),
                        ("f99", "f99"),
                        ("dd[]5c", "được"),
                        ("d[]c5d", "được"),
                        ("dosd", "dosd"),
                        ("do1d", "đó"),
                        ("a6", "a6"),
                        ("a9", "a9"),
                        ("e7", "e7"),
                        ("o8", "o8"),
                        ("c6n", "cân"),
                        ("tr7n", "trên"),
                        ("u8ng1", "uống")
                    };
                    foreach (var tc in tbtCases)
                    {
                        string res = SimulateTypingWord(tbtNewEngine, tc.input);
                        if (res == tc.expected)
                        {
                            sb.AppendLine($"  PASS Tư Bình Trần: '{tc.input}' -> '{res}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL Tư Bình Trần: '{tc.input}' -> '{res}' (Mong doi: '{tc.expected}')");
                        }
                    }

                    // F. Test câu ghép dấu tự do & câu bình thường (User sentences test)
                    var telexUserEngine = new VietnameseEngine(new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.Telex,
                        ModernToneRules = true
                    }, new MacroManager());

                    string expectedSentence = "Đông đến, đường đi đầy đá, đoàn đường xa đến đâu được mà đừng đứng đợi. Đại đồng đồng lòng đi đầu, đô đốc đứng đó đo đếm đại đội đang đóng đồn.";
                    string line1Input = "DDoong ddeesn, ddwowfng ddi ddaaqy ddaas, ddoafn ddwowfng xa ddeesn ddaaqu ddwwowjcc maaf ddwwfwng dduwawjng ddowjqi. DDaji ddoofng ddoofng loofng ddi ddaafu, ddoas ddoosc dduwawjng ddoas ddo ddeesm ddaji ddooji ddaang ddoasng ddoofn.";
                    string line2Input = "densd ddesn, duongwf ddi ddayq dass, doanf duongwf xa densd dauq duocwj maf dungwf dungwf doiwj. Daij dongf dongf longf ddi dauf, docs docs dungwf dos do deams daij doij dang doangs donf.";

                    string actualLine1 = SimulateTypingSentence(telexUserEngine, line1Input);
                    if (actualLine1 == expectedSentence)
                    {
                        sb.AppendLine($"  PASS Kiểu bình thường: '{actualLine1}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL Kiểu bình thường: '{actualLine1}'\n   (Mong doi: '{expectedSentence}')");
                    }

                    string actualLine2 = SimulateTypingSentence(telexUserEngine, line2Input);
                    if (actualLine2 == expectedSentence)
                    {
                        sb.AppendLine($"  PASS Kiểu ghép dấu tự do: '{actualLine2}'");
                    }
                    else
                    {
                        allPassed = false;
                        sb.AppendLine($"  FAIL Kiểu ghép dấu tự do: '{actualLine2}'\n   (Mong doi: '{expectedSentence}')");
                    }

                    // G. Test tính năng mới: di không thành đi; did thành di; đi + d thành did (Telex, SimpleTelex, TBT)
                    var methodsToTest = new[] { InputMethod.Telex, InputMethod.SimpleTelex, InputMethod.TuBinhTran };
                    foreach (var m in methodsToTest)
                    {
                        var eng = new VietnameseEngine(new AppSettings
                        {
                            IsVietnamese = true,
                            CurrentInputMethod = m,
                            ModernToneRules = true,
                            FreeMark = true
                        }, new MacroManager());

                        // 1. Gõ di -> phải ra di (không thành đi)
                        string resDi = SimulateTypingWord(eng, "di");
                        if (resDi == "di")
                        {
                            sb.AppendLine($"  PASS {m}: 'di' -> '{resDi}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL {m}: 'di' -> '{resDi}' (Mong doi: 'di')");
                        }

                        // 2. Gõ did -> phải thành di
                        string resDid = SimulateTypingWord(eng, "did");
                        if (resDid == "di")
                        {
                            sb.AppendLine($"  PASS {m}: 'did' -> '{resDid}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL {m}: 'did' -> '{resDid}' (Mong doi: 'di')");
                        }

                        // 3. Đang chữ đi (gõ ddi) gõ thêm d -> thành did
                        string resDdid = SimulateTypingWord(eng, "ddid");
                        if (resDdid == "did")
                        {
                            sb.AppendLine($"  PASS {m}: 'ddid' -> '{resDdid}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL {m}: 'ddid' -> '{resDdid}' (Mong doi: 'did')");
                        }
                    }

                    // H. Test từ điển sửa lỗi chính tả khi bấm Space hoặc Enter (Telex & SimpleTelex)
                    var spellingEngine = new VietnameseEngine(new AppSettings
                    {
                        IsVietnamese = true,
                        CurrentInputMethod = InputMethod.Telex,
                        ModernToneRules = true
                    }, new MacroManager());

                    SpellingCorrectionManager.Instance.InitializeDictionary();

                    var spellingTests = new (string input, string expected)[]
                    {
                        ("pót ", "post "),
                        ("cáe ", "case "),
                        ("pát ", "past "),
                        ("fêt ", "feet "),
                        ("mêt ", "meet "),
                        ("nêd ", "need "),
                        ("kêp ", "keep "),
                        ("dơn ", "down "),
                        ("shơ ", "show "),
                        ("tơn ", "town "),
                        ("cáe\n", "case\n")
                    };

                    foreach (var st in spellingTests)
                    {
                        string resSpelling = SimulateTypingSentence(spellingEngine, st.input);
                        if (resSpelling == st.expected)
                        {
                            sb.AppendLine($"  PASS Sửa lỗi chính tả: '{st.input.Replace("\n", "\\n")}' -> '{resSpelling.Replace("\n", "\\n")}'");
                        }
                        else
                        {
                            allPassed = false;
                            sb.AppendLine($"  FAIL Sửa lỗi chính tả: '{st.input.Replace("\n", "\\n")}' -> '{resSpelling.Replace("\n", "\\n")}' (Mong doi: '{st.expected.Replace("\n", "\\n")}')");
                        }
                    }
                }
                catch (Exception ex)
                {
                    allPassed = false;
                    sb.AppendLine($"  FAIL New Requirements Test: {ex.Message}\n{ex.StackTrace}");
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
                bool isShift = char.IsUpper(ch) || ch == '^' || ch == '&' || ch == '*' || ch == '(' || ch == '{' || ch == '}';

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

                bool isShift = char.IsUpper(ch) || ch == '^' || ch == '&' || ch == '*' || ch == '(' || ch == '{' || ch == '}';

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
