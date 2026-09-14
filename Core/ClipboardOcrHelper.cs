using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ModernKey.Core
{
    /// <summary>
    /// Trích xuất văn bản từ hình ảnh (OCR) sử dụng Native Windows 10/11 OcrEngine (Windows.Media.Ocr)
    /// Hoàn toàn không phụ thuộc thư viện bên thứ ba (Zero external dependencies).
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
                    // Tạo script PowerShell gọi Native Windows.Media.Ocr.OcrEngine
                    string escapedPath = imagePath.Replace("'", "''");
                    string script = @"
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine,Windows.Foundation.UniversalApiContract,ContentType=WindowsRuntime] | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }
Function AwaitTask($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(5000) | Out-Null
    $netTask.Result
}
try {
    $file = AwaitTask ([Windows.Storage.StorageFile]::GetFileFromPathAsync('" + escapedPath + @"')) ([Windows.Storage.StorageFile])
    $stream = AwaitTask ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = AwaitTask ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = AwaitTask ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    if ($engine -ne $null) {
        $res = AwaitTask ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        if ($res -ne $null) { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Output $res.Text }
    }
} catch { }
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
                        proc.WaitForExit(6000);
                        return output?.Trim() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("OCR Error: " + ex.Message);
                    return string.Empty;
                }
            });
        }
    }
}
