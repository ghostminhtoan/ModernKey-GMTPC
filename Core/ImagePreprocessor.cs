using System;
using OpenCvSharp;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ tiền xử lý hình ảnh thông minh đa chế độ (Dual-Mode Smart Preprocessor) cho OCR Tiếng Việt:
    /// 1. Tự động nhận diện ngữ cảnh hình ảnh (Context & Scene Classification):
    ///    - Dựa trên độ bão hòa màu HSV (Saturation) và phân phối độ sáng (Grayscale Histogram Peak Ratio).
    ///    - Chế độ 1: DOCUMENT / SCREEN UI (Ảnh chụp màn hình, luồng bình luận Facebook, dark mode, tài liệu, đoạn chat).
    ///      -> Làm phẳng nền thích ứng (Flat-field normalization Closing + Division) triệt tiêu hoàn toàn mảng nền badge màu ([6 triệu], [600k], [vozer detected]).
    ///    - Chế độ 2: PHOTO / SPORTS BANNER / POSTER (Ảnh chụp thực tế, banner đồ họa, cầu thủ, poster bóng đá).
    ///      -> Nâng tương phản độ sáng kênh Luminance trong không gian màu LAB (CLAHE) kết hợp Unsharp Mask. Tuyệt đối không đảo màu bitwise để tránh biến bóng đá, ngôi sao, nếp gấp áo thành chữ rác (SPCC, S1onyua).
    /// 2. Phóng đại siêu mẫu Bicubic 1.5x để bảo toàn trọn vẹn dấu thanh tiếng Việt.
    /// 3. Xuất chuẩn ma trận 3 kênh BGR cho mạng nơ-ron sâu PaddleOCR.
    /// </summary>
    public static class ImagePreprocessor
    {
        public static Mat PreprocessForOcr(Mat src)
        {
            if (src == null || src.Empty())
                return null;

            try
            {
                // 1. Phân tích độ bão hòa màu HSV để nhận biết ảnh tài liệu/UI vs ảnh chụp/banner
                double satMeanVal = 0.0;
                using (Mat hsv = new Mat())
                {
                    Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                    Mat[] hsvChannels = Cv2.Split(hsv);
                    try
                    {
                        Cv2.MeanStdDev(hsvChannels[1], out Scalar satMean, out Scalar satStd);
                        satMeanVal = satMean.Val0;
                    }
                    finally
                    {
                        for (int i = 0; i < hsvChannels.Length; i++)
                        {
                            if (hsvChannels[i] != null) hsvChannels[i].Dispose();
                        }
                    }
                }

                // 2. Phân tích độ sáng Grayscale và tỷ lệ đỉnh màu nền
                double grayMeanVal = 0.0;
                double peakRatio = 0.0;
                using (Mat gray = new Mat())
                {
                    Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.MeanStdDev(gray, out Scalar grayMean, out Scalar grayStd);
                    grayMeanVal = grayMean.Val0;

                    using (Mat hist = new Mat())
                    {
                        int[] hdims = { 256 };
                        Rangef[] hranges = { new Rangef(0, 256) };
                        Cv2.CalcHist(new[] { gray }, new[] { 0 }, null, hist, 1, hdims, hranges);
                        double minVal, maxVal;
                        Point minLoc, maxLoc;
                        Cv2.MinMaxLoc(hist, out minVal, out maxVal, out minLoc, out maxLoc);
                        peakRatio = maxVal / (double)(src.Width * src.Height);
                    }

                    // Phân loại: Nếu độ bão hòa màu thấp (< 60) hoặc nền có màu chủ đạo đồng nhất cao (> 20%) -> Chế độ tài liệu/UI
                    bool isScreenDocument = satMeanVal < 60.0 || peakRatio > 0.20;

                    if (isScreenDocument)
                    {
                        // CHẾ ĐỘ 1: TÀI LIỆU / SCREEN UI / DARK MODE
                        bool isDarkMode = grayMeanVal < 115.0;
                        if (isDarkMode)
                        {
                            Cv2.BitwiseNot(gray, gray);
                        }

                        // Làm phẳng nền thích ứng (Closing 21x21 + Division)
                        using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(21, 21)))
                        using (Mat bg = new Mat())
                        {
                            Cv2.MorphologyEx(gray, bg, MorphTypes.Close, kernel);

                            using (Mat normalized = new Mat())
                            {
                                Cv2.Divide(gray, bg, normalized, scale: 255.0);

                                Mat workingGray = normalized;
                                bool isScaled = false;
                                if (workingGray.Width < 1600 || workingGray.Height < 1000)
                                {
                                    workingGray = new Mat();
                                    Cv2.Resize(normalized, workingGray, new Size((int)(normalized.Width * 1.5), (int)(normalized.Height * 1.5)), 0, 0, InterpolationFlags.Cubic);
                                    isScaled = true;
                                }

                                try
                                {
                                    Mat bgrResult = new Mat();
                                    Cv2.CvtColor(workingGray, bgrResult, ColorConversionCodes.GRAY2BGR);
                                    return bgrResult;
                                }
                                finally
                                {
                                    if (isScaled && workingGray != null)
                                    {
                                        workingGray.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }

                // CHẾ ĐỘ 2: PHOTO / SPORTS BANNER / POSTER ĐỒ HỌA
                // Phóng to nếu ảnh nhỏ để tăng nét chữ
                Mat workingSrc = src;
                bool isSrcScaled = false;
                if (src.Width < 1500)
                {
                    workingSrc = new Mat();
                    Cv2.Resize(src, workingSrc, new Size((int)(src.Width * 1.5), (int)(src.Height * 1.5)), 0, 0, InterpolationFlags.Cubic);
                    isSrcScaled = true;
                }

                try
                {
                    // Chuyển sang không gian màu LAB để tăng cường độ tương phản trên kênh L (Luminance)
                    using (Mat lab = new Mat())
                    {
                        Cv2.CvtColor(workingSrc, lab, ColorConversionCodes.BGR2Lab);
                        Mat[] labChannels = Cv2.Split(lab);
                        try
                        {
                            using (var clahe = Cv2.CreateCLAHE(2.5, new Size(8, 8)))
                            using (Mat lEnhanced = new Mat())
                            {
                                clahe.Apply(labChannels[0], lEnhanced);
                                labChannels[0].Dispose();
                                labChannels[0] = lEnhanced;

                                using (Mat labMerged = new Mat())
                                {
                                    Cv2.Merge(labChannels, labMerged);
                                    using (Mat bgrEnhanced = new Mat())
                                    {
                                        Cv2.CvtColor(labMerged, bgrEnhanced, ColorConversionCodes.Lab2BGR);

                                        // Làm nét chữ bằng Unsharp Mask nhẹ
                                        using (Mat blurred = new Mat())
                                        {
                                            Cv2.GaussianBlur(bgrEnhanced, blurred, new Size(0, 0), 2.0);
                                            Mat sharpResult = new Mat();
                                            Cv2.AddWeighted(bgrEnhanced, 1.4, blurred, -0.4, 0, sharpResult);
                                            return sharpResult;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            for (int i = 0; i < labChannels.Length; i++)
                            {
                                if (labChannels[i] != null) labChannels[i].Dispose();
                            }
                        }
                    }
                }
                finally
                {
                    if (isSrcScaled && workingSrc != null)
                    {
                        workingSrc.Dispose();
                    }
                }
            }
            catch
            {
                // Fallback an toàn: trả về bản sao của ảnh gốc
                return src.Clone();
            }
        }
    }
}
