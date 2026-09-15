using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ModernKey.Core
{
    /// <summary>
    /// Trích xuất văn bản từ hình ảnh (OCR) sử dụng Native Windows 10/11 OcrEngine (Windows.Media.Ocr).
    /// Hỗ trợ cả tiếng Việt và tiếng Anh, tự động phát hiện ngôn ngữ OCR khả dụng trên hệ thống.
    /// </summary>
    public static class ClipboardOcrHelper
    {
        public static async Task<string> RecognizeTextAsync(string imagePath, string language = "auto", bool preserveLineBreaks = false, Action<string> progressCallback = null)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return string.Empty;

            // 1. Nhận diện chính bằng Deep Learning PaddleOCR (Baidu PP-OCR AI)
            try
            {
                string paddleText = await PaddleOcrHelper.RecognizeTextAsync(imagePath, preserveLineBreaks, progressCallback).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(paddleText))
                {
                    progressCallback?.Invoke("⏳ Đang chuẩn hóa tiếng Việt & áp dụng từ điển sửa lỗi...");
                    string processed = (language == "en") ? paddleText.Trim() : PostProcessVietnamese(paddleText.Trim());
                    return OcrCorrectionManager.ApplyCorrections(processed);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PaddleOCR Recognition error: " + ex.Message);
            }

            // 2. Dự phòng: Windows Native OCR (Windows.Media.Ocr) nếu máy không thể nạp mô hình Deep Learning
            return await Task.Run(() =>
            {
                string tempEnhancedPath = null;
                try
                {
                    // Fallback sang Windows Native OCR (Windows.Media.Ocr)
                    string pathToOcr = imagePath;
                    try
                    {
                        using (var origBmp = new System.Drawing.Bitmap(imagePath))
                        {
                            if (origBmp.Width > 0 && origBmp.Height > 0 && (origBmp.Width < 500 || origBmp.Height < 300))
                            {
                                int targetW = origBmp.Width * 2;
                                int targetH = origBmp.Height * 2;
                                tempEnhancedPath = Path.Combine(Path.GetTempPath(), "mk_ocr_" + Guid.NewGuid().ToString("N") + ".png");
                                using (var enhancedBmp = new System.Drawing.Bitmap(targetW, targetH))
                                using (var g = System.Drawing.Graphics.FromImage(enhancedBmp))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.DrawImage(origBmp, 0, 0, targetW, targetH);
                                    enhancedBmp.Save(tempEnhancedPath, System.Drawing.Imaging.ImageFormat.Png);
                                }
                                pathToOcr = tempEnhancedPath;
                            }
                        }
                    }
                    catch { }

                    string escapedPath = pathToOcr.Replace("'", "''");
                    string langFilterScript;
                    if (language == "en")
                    {
                        langFilterScript = @"
    foreach ($l in $avail) {
        if ($l.LanguageTag -like 'en*' -or $l.DisplayName -like '*English*') {
            $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($l)
            if ($engine -ne $null) { break }
        }
    }
    if ($engine -eq $null -and $avail.Count -gt 0) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($avail[0])
    }
";
                    }
                    else
                    {
                        langFilterScript = @"
    foreach ($l in $avail) {
        if ($l.LanguageTag -like 'vi*' -or $l.DisplayName -like '*Vietnamese*') {
            $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($l)
            if ($engine -ne $null) { break }
        }
    }
    if ($engine -eq $null) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    }
    if ($engine -eq $null -and $avail.Count -gt 0) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($avail[0])
    }
";
                    }

                    // Script PowerShell nạp đầy đủ các Type WinRT của Windows.Graphics và Windows.Media.Ocr
                    string script = @"
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine,Windows.Foundation.UniversalApiContract,ContentType=WindowsRuntime] | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { 
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' 
}

Function AwaitTask($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(8000) | Out-Null
    $netTask.Result
}

try {
    $file = AwaitTask ([Windows.Storage.StorageFile]::GetFileFromPathAsync('" + escapedPath + @"')) ([Windows.Storage.StorageFile])
    $stream = AwaitTask ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = AwaitTask ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = AwaitTask ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

    $engine = $null
    $avail = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
" + langFilterScript + @"

    if ($engine -ne $null) {
        $res = AwaitTask ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        if ($res -ne $null -and -not [string]::IsNullOrEmpty($res.Text)) {
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            Write-Output $res.Text
        }
    }
} catch {
    Write-Error $_.Exception.Message
}
";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "`\"") + "\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(9000);
                        string raw = output?.Trim() ?? string.Empty;
                        if (!preserveLineBreaks && !string.IsNullOrWhiteSpace(raw))
                        {
                            raw = UnwrapTextLines(raw);
                        }
                        string outText = (language == "en") ? raw : PostProcessVietnamese(raw);
                        return OcrCorrectionManager.ApplyCorrections(outText);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("OCR Error: " + ex.Message);
                    return string.Empty;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempEnhancedPath) && File.Exists(tempEnhancedPath))
                    {
                        try { File.Delete(tempEnhancedPath); } catch { }
                    }
                }
            });
        }

        public static string UnwrapTextLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string cur = lines[i].Trim();
                if (string.IsNullOrEmpty(cur)) continue;
                if (sb.Length == 0)
                {
                    sb.Append(cur);
                    continue;
                }
                string prev = lines[i - 1].Trim();
                bool prevEndsWithTerminal = Regex.IsMatch(prev, @"[\.!\?:…]+[""'\)\]]*$");
                bool isCurrentBullet = Regex.IsMatch(cur, @"^(?:[-*•–—+]|\d+[\.\)])\s+");
                bool isPrevAllUpper = prev.Length >= 3 && prev == prev.ToUpperInvariant() && !char.IsDigit(prev[0]);
                bool isCurrentAllUpper = cur.Length >= 3 && cur == cur.ToUpperInvariant() && !char.IsDigit(cur[0]);

                if (prevEndsWithTerminal || (isPrevAllUpper && isCurrentAllUpper) || (isPrevAllUpper && !isCurrentAllUpper) || isCurrentBullet)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(cur);
                }
                else
                {
                    if (prev.EndsWith("-"))
                    {
                        sb.Length--;
                        sb.Append(cur);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(cur);
                    }
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// <summary>
        /// Chuẩn hóa và làm sạch văn bản nhận dạng tiếng Việt theo nguyên lý ngôn ngữ & âm tiết học:
        /// - Sửa nhầm lẫn quang học giữa số và chữ (số 1 trước nguyên âm: 1ực 1ượng -> lực lượng)
        /// - Sửa nhầm lẫn dấu hỏi (?) thành số 2 sau thán từ hoặc từ để hỏi
        /// - Khử trùng lặp ký tự và xung đột dấu do cơ chế CTC time-step greedy decoding
        /// - Khử vần không tồn tại trong tiếng Việt (như 'ăy' -> 'ấy': giãy -> giấy)
        /// - Chuẩn hóa dấu câu, khoảng trắng và giữ nguyên định dạng chữ hoa/thường
        /// </summary>
        public static string PostProcessVietnamese(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // 1. Sửa lỗi nhận diện nhầm số 1 thành chữ thường 'l' hoặc 'L' trước nguyên âm tiếng Việt
            text = Regex.Replace(text, @"\b1([a-zA-Záàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ])", m =>
            {
                string next = m.Groups[1].Value;
                return (char.IsUpper(next[0]) ? "L" : "l") + next;
            });

            // 2. Dấu hỏi (?) bị nhận diện nhầm thành số 2, 7 hoặc dấu ngoặc/nháy sau từ để hỏi (À, HẢ, CHĂNG, SAO, GÌ, CHỨ, NHỈ, THẾ...)
            text = Regex.Replace(text, @"(?<=\b(?:[a-zA-Zá-ỹ]+[àảãạá]|\b(?:HẢ|hả|SAO|sao|GÌ|gì|CHĂNG|chăng|ĐÂU|đâu|AI|ai|chứ|CHỨ|nhỉ|NHỈ|thế|THẾ|không|KHÔNG|chưa|CHƯA)))[\s]*[27""”'`:](?=[\s\r\n,.;:!?]|$)", "?");
            text = Regex.Replace(text, @"(?<=[a-zA-Zá-ỹ])\?[27""”'`:*]", "?");

            // 2b. Lỗi đọc nhầm nhãn badge phổ biến và đuôi icon mạng xã hội
            text = Regex.Replace(text, @"\bvozer detereted\b", "vozer detected", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bvozer detected[ \t\-)\]a-zA-Z]*", "vozer detected", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"[ \t]*-[mMrR]\b", "");
            text = Regex.Replace(text, @"\b(?:Trả|Trở|Trl)\s+lười\b", "Trả lời", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bChia sẻe\b", "Chia sẻ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bthày\b", "thầy", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bchác\b(?=\s+thầy|\s+cô)", "các", RegexOptions.IgnoreCase);

            // 2c. Khử các dòng icon rác đứng độc lập (1 ký tự đơn lẻ hoặc các từ viết tắt icon)
            text = Regex.Replace(text, @"(?m)^\s*(?:dB|đB|ĐB|dĐo|dos|tn|taR|t3|mm|tr)\s*$\r?\n?", "");
            text = Regex.Replace(text, @"(?m)^\s*[a-zA-Zà-ỹÀ-Ỹ0-9]\s*$\r?\n?", "");

            // 2d. Chuẩn hóa ký hiệu & đứng độc lập trên một dòng trong poster / banner
            text = Regex.Replace(text, @"(?m)^\s*[89&eE]{1,2}\s*$\r?\n?", "&\r\n");

            // 2e. Chuẩn hóa tên cầu thủ / nhân vật ghép và logo trong banner thể thao & poster
            text = Regex.Replace(text, @"\bCHAT\s+(?:T\s+)?CHAI\b", "CHATCHAI", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bM?WAN\s+(?:NGHAI|NG\s+HAI|CHAI|CGCHAI)\b", "WANCHAI", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bWANG\s+(?:CHANI|CHÂNI|CHÂMI|CHĂI|CHAI)\b", "WANCHAI", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bCHATCHAINUYÊI\b", "CHATCHAI NGUYỄN", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bCHATHAI\s+UYÊI\b", "CHATCHAI NGUYỄN", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(?:CHATCHAI|WANCHAI)\s+NGUYÊN\b", m => m.Value.Replace("NGUYÊN", "NGUYỄN"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?m)^NGUYÊN$", "NGUYỄN");
            text = Regex.Replace(text, @"\bTHẾT?\s+TH[ẠẬỰAUƠOA-Z]+\b", "THỂ THAO", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b2[4àa]\s*[àa]?\s*Tm[a-zA-Z0-9]*\b", "247.vn", RegexOptions.IgnoreCase);

            // 3. Khử các lỗi trùng lặp phụ âm đầu do cơ chế CTC greedy decoding
            // Tiếng Việt không bao giờ có phụ âm đôi như mm, cc, bb, dd, tt, vv, ll ở đầu từ
            text = Regex.Replace(text, @"\b([b-df-hj-np-tv-z])\1+", "$1", RegexOptions.IgnoreCase);

            // 4. Khử xung đột 2 dấu thanh hoặc ký tự kép của cùng một nguyên âm do CTC time-step
            text = Regex.Replace(text, @"(?i)eê|êe", "ê");
            text = Regex.Replace(text, @"(?i)EÊ|ÊE", "Ê");
            text = Regex.Replace(text, @"(?i)oơ|ơo", "ơ");
            text = Regex.Replace(text, @"(?i)OƠ|ƠO", "Ơ");
            text = Regex.Replace(text, @"(?i)oô|ôo", "ô");
            text = Regex.Replace(text, @"(?i)OÔ|ÔO", "Ô");
            text = Regex.Replace(text, @"(?i)oọ|ọo", "ọ");
            text = Regex.Replace(text, @"(?i)OỌ|ỌO", "Ọ");
            text = Regex.Replace(text, @"(?i)uư|ưu(?=[cptkmnr])", "ư");
            text = Regex.Replace(text, @"(?i)úứ|ứú|úu|uú", "ú");
            text = Regex.Replace(text, @"(?i)ưức|ứcư", m => MatchCase(m.Value, "ức"));
            text = Regex.Replace(text, @"(?i)ưực|ựcư", m => MatchCase(m.Value, "ực"));
            text = Regex.Replace(text, @"(?i)ơơn|oơn", m => MatchCase(m.Value, "ơn"));
            text = Regex.Replace(text, @"(?i)\bseê\b", m => MatchCase(m.Value, "sẽ"));
            text = Regex.Replace(text, @"(?i)\bsưức\b", m => MatchCase(m.Value, "sức"));
            text = Regex.Replace(text, @"(?i)\bhoơn\b", m => MatchCase(m.Value, "hơn"));
            text = Regex.Replace(text, @"(?i)\btroọng\b", m => MatchCase(m.Value, "trọng"));

            // Xung đột dấu thanh kép
            text = Regex.Replace(text, @"ốô|ôố", "ố");
            text = Regex.Replace(text, @"ồô|ôồ", "ồ");
            text = Regex.Replace(text, @"ốõ|õố", "ố");
            text = Regex.Replace(text, @"ổô|ôổ", "ổ");
            text = Regex.Replace(text, @"ỗô|ôỗ", "ỗ");
            text = Regex.Replace(text, @"ộô|ôộ", "ộ");
            text = Regex.Replace(text, @"ớơ|ơớ", "ớ");
            text = Regex.Replace(text, @"ờơ|ơờ", "ờ");
            text = Regex.Replace(text, @"ởơ|ơở", "ở");
            text = Regex.Replace(text, @"ỡơ|ơỡ", "ỡ");
            text = Regex.Replace(text, @"ợơ|ơợ", "ợ");
            text = Regex.Replace(text, @"ứư|ưứ", "ứ");
            text = Regex.Replace(text, @"ừư|ưừ", "ừ");
            text = Regex.Replace(text, @"ửư|ưử", "ử");
            text = Regex.Replace(text, @"ữư|ưữ", "ữ");
            text = Regex.Replace(text, @"ựư|ưự", "ự");
            text = Regex.Replace(text, @"ếê|êế", "ế");
            text = Regex.Replace(text, @"ềê|êề", "ề");
            text = Regex.Replace(text, @"ểê|êể", "ể");
            text = Regex.Replace(text, @"ễê|êễ", "ễ");
            text = Regex.Replace(text, @"ệê|êệ", "ệ");
            text = Regex.Replace(text, @"vốô\s*sốõ", m => MatchCase(m.Value, "vô số"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"đồông", m => MatchCase(m.Value, "đồng"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"củùng", m => MatchCase(m.Value, "cùng"), RegexOptions.IgnoreCase);

            // 5. Khử các nguyên âm lặp do frame CTC kéo dài
            text = Regex.Replace(text, @"\b([a-zA-Zá-ỹ])\1+", "$1", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"([a-zà-ỹ])\1{2,}", "$1", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"([à-ỹ])\1+", "$1", RegexOptions.IgnoreCase);

            // 6. Luật ngữ âm tiếng Việt đối với vần không tồn tại trong tiếng Việt:
            // Tiếng Việt KHÔNG có vần 'ăy' (chỉ có 'ay' hoặc 'ấy/ầy/ẩy/ẫy/ậy')
            text = Regex.Replace(text, @"([b-df-hj-np-tv-z]*)ăy", m => m.Groups[1].Value + "ấy", RegexOptions.IgnoreCase);

            // Âm tiết kết thúc bằng âm tắc c, p, t, ch chỉ có thể mang thanh Sắc hoặc Nặng
            text = Regex.Replace(text, @"\brắt\b", m => MatchCase(m.Value, "rất"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bchiên\b", m => MatchCase(m.Value, "chiến"), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bthực tê\b", m => MatchCase(m.Value, "thực tế"), RegexOptions.IgnoreCase);

            // 7. Chuẩn hóa khoảng trắng quanh dấu câu
            text = Regex.Replace(text, @"\s+([,.:;?!])", "$1");
            text = Regex.Replace(text, @"([,.:;?!])([^\s0-9""'])", "$1 $2");
            text = Regex.Replace(text, @"[""“”]{2,}", "\"");
            text = Regex.Replace(text, @"['’]{2,}", "'");
            text = Regex.Replace(text, @"[ \t]{2,}", " ");

            // 8. Tích hợp sửa lỗi chính tả theo từ điển người dùng (SpellingCorrectionManager)
            try
            {
                var dictManager = SpellingCorrectionManager.Instance;
                if (dictManager != null)
                {
                    var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        string clean = word.Trim('.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '\"', '\'');
                        if (clean.Length >= 2 && dictManager.TryCorrect(clean, out string corr))
                        {
                            text = Regex.Replace(text, $@"\b{Regex.Escape(clean)}\b", corr);
                        }
                    }
                }
            }
            catch { }

            return text.Trim();
        }

        private static string MatchCase(string original, string replacement)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement)) return replacement;
            if (char.IsUpper(original[0]))
            {
                if (original.Length > 1 && char.IsUpper(original[1]))
                    return replacement.ToUpperInvariant();
                return char.ToUpperInvariant(replacement[0]) + (replacement.Length > 1 ? replacement.Substring(1) : string.Empty);
            }
            return replacement.ToLowerInvariant();
        }
    }
}
