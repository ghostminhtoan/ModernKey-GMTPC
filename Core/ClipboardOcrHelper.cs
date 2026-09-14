using System;
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
        /// </summary>
        private static string PostProcessVietnamese(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Xử lý các ký tự dính lỗi OCR phổ biến từ font màn hình
            string text = input;
            text = text.Replace("ø", "o").Replace("Ø", "O");
            text = text.Replace("å", "a").Replace("Å", "A");
            text = text.Replace("ä", "a").Replace("Ä", "A");
            text = text.Replace("ö", "o").Replace("Ö", "O");
            text = text.Replace("ü", "u").Replace("Ü", "U");
            text = text.Replace("•", " ");

            return text.Trim();
        }
    }
}
