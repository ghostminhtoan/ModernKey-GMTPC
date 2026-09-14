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
        public static Task<string> RecognizeTextAsync(string imagePath)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    return string.Empty;

                try
                {
                    string escapedPath = imagePath.Replace("'", "''");
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

    # 1. Tìm gói ngôn ngữ OCR tiếng Việt (vi-VN hoặc vi)
    $engine = $null
    $avail = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
    foreach ($l in $avail) {
        if ($l.LanguageTag -like 'vi*' -or $l.DisplayName -like '*Vietnamese*') {
            $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($l)
            if ($engine -ne $null) { break }
        }
    }

    # 2. Nếu không có tiếng Việt, thử theo ngôn ngữ người dùng hoặc ngôn ngữ đầu tiên khả dụng
    if ($engine -eq $null) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    }
    if ($engine -eq $null -and $avail.Count -gt 0) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($avail[0])
    }

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
                        return PostProcessVietnamese(raw);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("OCR Error: " + ex.Message);
                    return string.Empty;
                }
            });
        }

        /// <summary>
        /// Chuẩn hóa các ký tự nhận dạng OCR đặc thù khi hệ thống dùng engine Latin nhận dạng tiếng Việt
        /// và tự động sửa các lỗi chính tả tiếng Việt phổ biến sinh ra từ nhận dạng OCR font màn hình.
        /// </summary>
        public static string PostProcessVietnamese(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string text = input;

            // 1. Xử lý các ký tự dính lỗi OCR phổ biến từ font màn hình
            text = text.Replace("ø", "o").Replace("Ø", "O");
            text = text.Replace("å", "a").Replace("Å", "A");
            text = text.Replace("ä", "a").Replace("Ä", "A");
            text = text.Replace("ö", "o").Replace("Ö", "O");
            text = text.Replace("ü", "u").Replace("Ü", "U");
            text = text.Replace("•", " ");

            // Nối dòng bị gãy giữa từ bởi dấu gạch ngang
            text = Regex.Replace(text, @"(\w+)-\r?\n(\w+)", "$1$2");

            // 2. Chuẩn hóa dấu thanh và dấu mũ/móc bị OCR tách rời (vd: o' -> ơ, u' -> ư, a' -> á...)
            text = Regex.Replace(text, @"(?<=[aA])['’]", "á");
            text = Regex.Replace(text, @"(?<=[aA])[`\\]", "à");
            text = Regex.Replace(text, @"(?<=[aA])\?", "ả");
            text = Regex.Replace(text, @"(?<=[aA])~", "ã");
            text = Regex.Replace(text, @"(?<=[aA])\^", "â");
            text = Regex.Replace(text, @"(?<=[aA])\(", "ă");

            text = Regex.Replace(text, @"(?<=[oO])['’]", "ơ");
            text = Regex.Replace(text, @"(?<=[oO])\^", "ô");
            text = Regex.Replace(text, @"(?<=[uU])['’]", "ư");
            text = Regex.Replace(text, @"(?<=[eE])\^", "ê");

            text = Regex.Replace(text, @"(?<=[ơƠ])['’]", "ớ");
            text = Regex.Replace(text, @"(?<=[ơƠ])[`\\]", "ờ");
            text = Regex.Replace(text, @"(?<=[ơƠ])\?", "ở");
            text = Regex.Replace(text, @"(?<=[ơƠ])~", "ỡ");

            text = Regex.Replace(text, @"(?<=[ưƯ])['’]", "ứ");
            text = Regex.Replace(text, @"(?<=[ưƯ])[`\\]", "ừ");
            text = Regex.Replace(text, @"(?<=[ưƯ])\?", "ử");
            text = Regex.Replace(text, @"(?<=[ưƯ])~", "ữ");

            text = Regex.Replace(text, @"(?<=[ôÔ])['’]", "ố");
            text = Regex.Replace(text, @"(?<=[ôÔ])[`\\]", "ồ");
            text = Regex.Replace(text, @"(?<=[ôÔ])\?", "ổ");
            text = Regex.Replace(text, @"(?<=[ôÔ])~", "ỗ");

            text = Regex.Replace(text, @"(?<=[êÊ])['’]", "ế");
            text = Regex.Replace(text, @"(?<=[êÊ])[`\\]", "ề");
            text = Regex.Replace(text, @"(?<=[êÊ])\?", "ể");
            text = Regex.Replace(text, @"(?<=[êÊ])~", "ễ");

            text = Regex.Replace(text, @"(?<=[âÂ])['’]", "ấ");
            text = Regex.Replace(text, @"(?<=[âÂ])[`\\]", "ầ");
            text = Regex.Replace(text, @"(?<=[âÂ])\?", "ẩ");
            text = Regex.Replace(text, @"(?<=[âÂ])~", "ẫ");

            text = Regex.Replace(text, @"(?<=[ăĂ])['’]", "ắ");
            text = Regex.Replace(text, @"(?<=[ăĂ])[`\\]", "ằ");
            text = Regex.Replace(text, @"(?<=[ăĂ])\?", "ẳ");
            text = Regex.Replace(text, @"(?<=[ăĂ])~", "ẵ");

            // 3. Sửa lỗi nhận diện chữ 'đ' / 'Đ' từ 'cl' / 'ct' hoặc nhầm 'd' ở các từ luôn là 'đ'
            string[] clToDWords = new[]
            {
                "ược", "ầu", "i", "ến", "ã", "e", "ang", "ây", "ó", "ạt", "ổi", "úng",
                "ường", "ơn", "ộng", "ội", "ồng", "ịnh", "ặc", "ặt", "ủ", "ất", "ối",
                "ọc", "ức", "ông", "ều", "óng", "áp", "oàn", "ấu", "ao", "ài", "ạo",
                "ời", "ám", "ạn", "ập", "iểm", "iều", "iện", "ược", "ứng"
            };

            foreach (var w in clToDWords)
            {
                text = Regex.Replace(text, $@"\b[cC][lL]{Regex.Escape(w)}\b", "đ" + w);
                text = Regex.Replace(text, $@"\b[cC][lL]{Regex.Escape(w.ToUpper())}\b", "Đ" + w.ToUpper());
                text = Regex.Replace(text, $@"\b[cC][tT]{Regex.Escape(w)}\b", "đ" + w);
            }

            // Sửa các cặp từ tiếng Việt thông dụng thường bị OCR Latin làm mất dấu hoặc sai chính tả
            var phraseCorrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "muc dich", "mục đích" },
                { "mục dích", "mục đích" },
                { "sao ke", "sao kê" },
                { "to chuc", "tổ chức" },
                { "to chức", "tổ chức" },
                { "ca nhan", "cá nhân" },
                { "ca nhân", "cá nhân" },
                { "ro rang", "rõ ràng" },
                { "rõ rang", "rõ ràng" },
                { "ro ràng", "rõ ràng" },
                { "nguoi", "người" },
                { "người ta", "người ta" },
                { "tai khoan", "tài khoản" },
                { "ung ho", "ủng hộ" },
                { "cong dong", "cộng đồng" },
                { "bao chi", "báo chí" },
                { "chinh quyen", "chính quyền" },
                { "chinh xac", "chính xác" },
                { "thong tin", "thông tin" },
                { "du lieu", "dữ liệu" },
                { "phat trien", "phát triển" },
                { "cong khai", "công khai" },
                { "minh bach", "minh bạch" },
                { "chi tiet", "chi tiết" },
                { "thuc hien", "thực hiện" },
                { "hoat dong", "hoạt động" },
                { "thoi gian", "thời gian" },
                { "tuong tu", "tương tự" },
                { "huong dan", "hướng dẫn" },
                { "su dung", "sử dụng" },
                { "ung dung", "ứng dụng" },
                { "tro giup", "trợ giúp" },
                { "khong", "không" },
                { "trieu", "triệu" },
                { "ti le", "tỉ lệ" }
            };

            foreach (var kv in phraseCorrections)
            {
                text = Regex.Replace(text, $@"\b{Regex.Escape(kv.Key)}\b", match =>
                {
                    string m = match.Value;
                    if (string.IsNullOrEmpty(m)) return kv.Value;
                    if (char.IsUpper(m[0]))
                    {
                        if (m.Length > 1 && char.IsUpper(m[1])) return kv.Value.ToUpper();
                        return char.ToUpper(kv.Value[0]) + kv.Value.Substring(1);
                    }
                    return kv.Value;
                }, RegexOptions.IgnoreCase);
            }

            // 4. Áp dụng từ điển sửa lỗi chính tả nếu có cấu hình tùy chỉnh
            try
            {
                var dictManager = SpellingCorrectionManager.Instance;
                if (dictManager != null)
                {
                    // Quét các từ đơn lẻ qua từ điển sửa lỗi
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
    }
}
