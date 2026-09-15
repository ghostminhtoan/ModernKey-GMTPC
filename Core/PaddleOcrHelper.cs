using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ trích xuất chữ OCR tiếng Việt chuyên sâu bằng Deep Learning Baidu PaddleOCR (PP-OCRv6 Vietnamese + ChineseV5 Detection).
    /// Hỗ trợ 100% toàn bộ hệ thống nguyên âm có dấu tiếng Việt, chữ hoa, chữ thường và ký tự đặc biệt.
    /// Tự động nạp từ thư mục Portable hoặc AppData, tự động tải mô hình nếu chưa có.
    /// </summary>
    public static class PaddleOcrHelper
    {
        private static PaddleOcrAll _cachedEngine;
        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private static bool _isInitialized = false;

        private const string ViModelSubDir = "vi_rec_v6";

        static PaddleOcrHelper()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var name = new System.Reflection.AssemblyName(args.Name).Name;
                        if (name == "System.Buffers") return typeof(System.Buffers.ArrayPool<>).Assembly;
                        if (name == "System.Memory") return typeof(System.Memory<>).Assembly;
                        if (name == "System.Runtime.CompilerServices.Unsafe") return typeof(System.Runtime.CompilerServices.Unsafe).Assembly;
                    }
                    catch { }
                    return null;
                };

                ConfigureModelDirectory();
            }
            catch { }
        }

        public static string GetModelRootDirectory()
        {
            try
            {
                string portableDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".portable", "paddleocr-models");
                if (Directory.Exists(portableDir)) return portableDir;

                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModernKey", "paddleocr-models");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                return appDataDir;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "paddleocr-models");
            }
        }

        private static void ConfigureModelDirectory()
        {
            try
            {
                Settings.GlobalModelDirectory = GetModelRootDirectory();
            }
            catch { }
        }

        public static string GetVietnameseRecDirectory()
        {
            string[] candidateDirs = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".portable", "paddleocr-models", ViModelSubDir),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModernKey", "paddleocr-models", ViModelSubDir),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paddleocr-models", ViModelSubDir),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paddleocr-models", "test_vi_rec")
            };

            foreach (var dir in candidateDirs)
            {
                if (IsValidVietnameseModelDir(dir))
                {
                    return dir;
                }
            }

            return Path.Combine(GetModelRootDirectory(), ViModelSubDir);
        }

        private static bool IsValidVietnameseModelDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;

            string jsonFile = Path.Combine(dir, "inference.json");
            string paramsFile = Path.Combine(dir, "inference.pdiparams");
            string ymlFile = Path.Combine(dir, "inference.yml");
            string keysFile = Path.Combine(dir, "ppocr_keys.txt");

            if (!File.Exists(jsonFile) || !File.Exists(paramsFile) || !File.Exists(ymlFile) || !File.Exists(keysFile))
                return false;

            try
            {
                var fi = new FileInfo(paramsFile);
                if (fi.Length < 60000000) // File phải tối thiểu ~60MB để không bị cắt nửa chừng
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static bool IsAvailable()
        {
            try
            {
                return _isInitialized && _cachedEngine != null;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool success, string message)> DownloadAndInitModelAsync(Action<string> progressCallback = null)
        {
            await _initLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cachedEngine != null)
                {
                    return (true, "Mô hình PaddleOCR Tiếng Việt đã được nạp và sẵn sàng!");
                }

                ConfigureModelDirectory();
                string viRecDir = GetVietnameseRecDirectory();

                if (!IsValidVietnameseModelDir(viRecDir))
                {
                    Directory.CreateDirectory(viRecDir);
                    progressCallback?.Invoke("⏳ Đang tải mô hình chuyên dụng tiếng Việt PP-OCRv6 (tieubaoca/HuggingFace)...");

                    string baseUrl = "https://huggingface.co/tieubaoca/pp-ocrv6-medium-rec-vietnamese/resolve/main";
                    var filesToDownload = new[]
                    {
                        ("inference.json", "inference.json"),
                        ("inference.yml", "inference.yml"),
                        ("ppocr_keys.txt", "ppocr_keys.txt"),
                        ("inference.pdiparams", "inference.pdiparams")
                    };

                    using (var client = new WebClient())
                    {
                        foreach (var (remoteName, localName) in filesToDownload)
                        {
                            string destPath = Path.Combine(viRecDir, localName);
                            if (File.Exists(destPath))
                            {
                                if (localName == "inference.pdiparams")
                                {
                                    var fi = new FileInfo(destPath);
                                    if (fi.Length >= 60000000) continue;
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            progressCallback?.Invoke($"⏳ Đang tải {localName}...");
                            await Task.Run(() => client.DownloadFile($"{baseUrl}/{remoteName}", destPath)).ConfigureAwait(false);
                        }
                    }

                    if (!IsValidVietnameseModelDir(viRecDir))
                    {
                        return (false, "⚠ Tải tệp mô hình không thành công hoặc tệp bị thiếu dữ liệu.");
                    }
                }

                progressCallback?.Invoke("⏳ Đang nạp mô hình phát hiện chữ (Detection V5)...");
                var det = await OnlineDetectionModel.ChineseV5.DownloadAsync().ConfigureAwait(false);

                progressCallback?.Invoke("⏳ Đang nạp mô hình nhận dạng tiếng Việt (PP-OCRv6)...");
                var rec = RecognizationModel.FromDirectoryV5(viRecDir);

                progressCallback?.Invoke("⏳ Đang khởi tạo bộ máy suy luận Deep Learning MKL-DNN...");
                var fullModel = new FullOcrModel(det, null, rec);

                _cachedEngine = new PaddleOcrAll(fullModel, PaddleDevice.Mkldnn())
                {
                    Enable180Classification = false,
                    AllowRotateDetection = true
                };

                try
                {
                    _cachedEngine.Detector.MaxSize = 2048;
                    _cachedEngine.Detector.UnclipRatio = 1.8f;
                    _cachedEngine.Detector.BoxScoreThreahold = 0.5f;
                }
                catch { }

                _isInitialized = true;
                return (true, "✓ Đã nạp thành công mô hình AI PaddleOCR Tiếng Việt toàn diện!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PaddleOCR Init Error: " + ex.Message);
                return (false, "⚠ Lỗi nạp PaddleOCR: " + ex.Message);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static readonly object _engineRunLock = new object();

        public static async Task<string> RecognizeTextAsync(string imagePath, Action<string> progressCallback)
        {
            return await RecognizeTextAsync(imagePath, false, progressCallback).ConfigureAwait(false);
        }

        public static async Task<string> RecognizeTextAsync(string imagePath, bool preserveLineBreaks = false, Action<string> progressCallback = null)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return string.Empty;

            if (_cachedEngine == null)
            {
                SafeReport(progressCallback, "⏳ Đang khởi tạo bộ máy AI PaddleOCR Tiếng Việt...");
                var initResult = await DownloadAndInitModelAsync(progressCallback).ConfigureAwait(false);
                if (!initResult.success || _cachedEngine == null)
                {
                    Debug.WriteLine("PaddleOCR không khả dụng, không thể nhận diện.");
                    return string.Empty;
                }
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (Mat src = Cv2.ImRead(imagePath))
                    {
                        if (src.Empty()) return string.Empty;

                        SafeReport(progressCallback, "⏳ Đang tiền xử lý ảnh: Tách nền thông minh & tăng nét chữ...");
                        using (Mat prep = ImagePreprocessor.PreprocessForOcr(src))
                        {
                            Mat inputMat = (prep != null && !prep.Empty()) ? prep : src;

                            SafeReport(progressCallback, "⏳ Đang nhận dạng chữ tiếng Việt qua mạng neuron Deep Learning...");
                            PaddleOcrResult result;
                            lock (_engineRunLock)
                            {
                                result = _cachedEngine.Run(inputMat);
                            }

                            if (result != null && result.Regions != null && result.Regions.Length > 0)
                            {
                                // Lọc các box rác: confidence thấp, box quá bé hoặc là ký tự rác icon độc lập
                                var validRegions = new System.Collections.Generic.List<PaddleOcrResultRegion>();
                                for (int i = 0; i < result.Regions.Length; i++)
                                {
                                    var r = result.Regions[i];
                                    if (float.IsNaN(r.Score) || r.Score < 0.45f) continue;
                                    string t = r.Text != null ? r.Text.Trim() : string.Empty;
                                    if (string.IsNullOrEmpty(t)) continue;

                                    // Lọc box quá bé (icon like, avatar, reaction arrow)
                                    if (r.Rect.Size.Width < 25 && r.Rect.Size.Height < 25 && t.Length <= 2) continue;

                                    // Lọc các icon chữ đơn lẻ phổ biến trong giao diện MXH
                                    if (t.Length <= 2 && System.Text.RegularExpressions.Regex.IsMatch(t, @"^(?:dB|đB|ĐB|dĐo|tn|taR|t3|mm|[BĐVRv1k])$")) continue;

                                    validRegions.Add(r);
                                }

                                if (validRegions.Count > 0)
                                {
                                    return FormatOcrText(validRegions, preserveLineBreaks);
                                }
                            }

                            return result?.Text != null ? result.Text.Trim() : string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("PaddleOCR Recognition Error: " + ex);
                    Debug.WriteLine("PaddleOCR Recognition Error: " + ex.Message);
                    return string.Empty;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gom dòng và định dạng văn bản nhận dạng:
        /// - Gom các hộp chữ cùng dòng ngang (2D topological line grouping), khắc phục lỗi tráo đổi vị trí
        /// - Chế độ preserveLineBreaks = true: Giữ nguyên mỗi dòng hình ảnh là 1 hàng mới
        /// - Chế độ preserveLineBreaks = false: Nối liền câu thông minh, ghép các dòng bị ngắt giữa chừng thành câu trọn vẹn
        /// </summary>
        public static string FormatOcrText(System.Collections.Generic.List<PaddleOcrResultRegion> regions, bool preserveLineBreaks)
        {
            if (regions == null || regions.Count == 0) return string.Empty;

            var physicalLines = GroupIntoPhysicalLines(regions);
            if (physicalLines.Count == 0) return string.Empty;

            if (preserveLineBreaks)
            {
                var lineStrings = physicalLines.Select(line => string.Join(" ", line.Select(r => (r.Text ?? "").Trim()).Where(t => !string.IsNullOrEmpty(t))));
                return string.Join(Environment.NewLine, lineStrings.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            if (physicalLines.Count == 1)
            {
                return string.Join(" ", physicalLines[0].Select(r => (r.Text ?? "").Trim()).Where(t => !string.IsNullOrEmpty(t)));
            }

            // Tính khoảng cách trung vị giữa các dòng liên tiếp
            var lineGaps = new System.Collections.Generic.List<float>();
            for (int i = 1; i < physicalLines.Count; i++)
            {
                float prevY = (float)physicalLines[i - 1].Average(r => r.Rect.Center.Y);
                float curY = (float)physicalLines[i].Average(r => r.Rect.Center.Y);
                float gap = curY - prevY;
                if (gap > 0) lineGaps.Add(gap);
            }
            float medianGap = lineGaps.Count > 0 ? lineGaps.OrderBy(g => g).ElementAt(lineGaps.Count / 2) : 30f;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < physicalLines.Count; i++)
            {
                var curLineRegions = physicalLines[i];
                string curText = string.Join(" ", curLineRegions.Select(r => (r.Text ?? "").Trim()).Where(t => !string.IsNullOrEmpty(t))).Trim();
                if (string.IsNullOrEmpty(curText)) continue;

                if (sb.Length == 0)
                {
                    sb.Append(curText);
                    continue;
                }

                var prevLineRegions = physicalLines[i - 1];
                string prevText = string.Join(" ", prevLineRegions.Select(r => (r.Text ?? "").Trim()).Where(t => !string.IsNullOrEmpty(t))).Trim();

                float prevY = (float)prevLineRegions.Average(r => r.Rect.Center.Y);
                float curY = (float)curLineRegions.Average(r => r.Rect.Center.Y);
                float actualGap = curY - prevY;

                // 1. Kiểm tra dấu kết thúc câu (. ! ? : hoặc ...)
                bool prevEndsWithTerminal = System.Text.RegularExpressions.Regex.IsMatch(prevText, @"[\.!\?:…]+[""'\)\]]*$");

                // 2. Dòng hiện tại là bullet list hoặc bắt đầu số mục (1., -, *, +)
                bool isCurrentBullet = System.Text.RegularExpressions.Regex.IsMatch(curText, @"^(?:[-*•–—+]|\d+[\.\)])\s+");

                // 3. Tiêu đề in hoa riêng biệt (ALL CAPS)
                bool isPrevAllUpper = prevText.Length >= 3 && prevText == prevText.ToUpperInvariant() && !char.IsDigit(prevText[0]);
                bool isCurrentAllUpper = curText.Length >= 3 && curText == curText.ToUpperInvariant() && !char.IsDigit(curText[0]);

                // 4. Khoảng cách vật lý giữa 2 dòng vượt quá 1.55 lần khoảng cách dòng chuẩn (Paragraph break / Box riêng biệt)
                bool isLargeVerticalGap = actualGap > medianGap * 1.55f;

                if (prevEndsWithTerminal || (isPrevAllUpper && isCurrentAllUpper) || (isPrevAllUpper && !isCurrentAllUpper) || isCurrentBullet || isLargeVerticalGap)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(curText);
                }
                else
                {
                    // Nối liền câu trong cùng 1 đoạn văn
                    if (prevText.EndsWith("-"))
                    {
                        sb.Length--; // Bỏ dấu gạch nối ngắt dòng
                        sb.Append(curText);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(curText);
                    }
                }
            }

            return sb.ToString().Trim();
        }

        public static System.Collections.Generic.List<System.Collections.Generic.List<PaddleOcrResultRegion>> GroupIntoPhysicalLines(System.Collections.Generic.List<PaddleOcrResultRegion> regions)
        {
            if (regions == null || regions.Count == 0) return new System.Collections.Generic.List<System.Collections.Generic.List<PaddleOcrResultRegion>>();
            if (regions.Count == 1) return new System.Collections.Generic.List<System.Collections.Generic.List<PaddleOcrResultRegion>> { regions };

            float avgHeight = (float)regions.Average(r => Math.Min(r.Rect.Size.Width, r.Rect.Size.Height));
            if (avgHeight <= 0) avgHeight = 20;

            var byY = regions.OrderBy(r => r.Rect.Center.Y).ToList();
            var lines = new System.Collections.Generic.List<System.Collections.Generic.List<PaddleOcrResultRegion>>();
            float lineThreshold = avgHeight * 0.55f;

            foreach (var r in byY)
            {
                bool placed = false;
                foreach (var line in lines)
                {
                    float lineAvgY = (float)line.Average(item => item.Rect.Center.Y);
                    if (Math.Abs(r.Rect.Center.Y - lineAvgY) <= lineThreshold)
                    {
                        line.Add(r);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    lines.Add(new System.Collections.Generic.List<PaddleOcrResultRegion> { r });
                }
            }

            var sortedLines = lines.OrderBy(l => l.Average(item => item.Rect.Center.Y)).ToList();
            for (int i = 0; i < sortedLines.Count; i++)
            {
                sortedLines[i] = sortedLines[i].OrderBy(item => item.Rect.Center.X).ToList();
            }

            return sortedLines;
        }

        private static void SafeReport(Action<string> callback, string message)
        {
            if (callback == null) return;
            try { callback(message); } catch { }
        }
    }
}
