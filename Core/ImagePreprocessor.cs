using System;
using OpenCvSharp;

namespace ModernKey.Core
{
    /// <summary>
    /// Các chế độ OCR chuyên dụng cho ngữ cảnh mạng xã hội và video
    /// </summary>
    public enum OcrPreset
    {
        Auto = 0,       // Tự động phân loại ngữ cảnh thông minh (Mặc định)
        Subtitle = 1,   // Phụ đề Video (YouTube, Phim, TikTok/Reels - tập trung 35% dưới, khử viền đen)
        Thumbnail = 2,  // Thumbnail / Ảnh bìa YouTube (Chữ nghệ thuật lớn, CLAHE tương phản cao, khử bóng)
        SocialPost = 3  // Bài đăng Facebook / Meme / Ảnh chụp màn hình bình luận
    }

    /// <summary>
    /// Bộ tiền xử lý hình ảnh thông minh đa chế độ (Smart Preprocessor) cho OCR Tiếng Việt:
    /// - Tận dụng 100% OpenCV (OpenCvSharp) tối ưu hiệu năng.
    /// - Chuyên trị ảnh Facebook, YouTube, video có phông nền người và cảnh vật phức tạp.
    /// - Hỗ trợ các bộ lọc: CLAHE cục bộ, Bilateral Filter khử hạt nén video, Morphological khử viền đen phụ đề.
    /// </summary>
    public static class ImagePreprocessor
    {
        public static Mat PreprocessForOcr(Mat src, OcrPreset preset = OcrPreset.Auto)
        {
            if (src == null || src.Empty())
                return null;

            try
            {
                switch (preset)
                {
                    case OcrPreset.Subtitle:
                        return PreprocessSubtitle(src);

                    case OcrPreset.Thumbnail:
                        return PreprocessThumbnail(src);

                    case OcrPreset.SocialPost:
                        return PreprocessSocialPost(src);

                    case OcrPreset.Auto:
                    default:
                        return PreprocessAuto(src);
                }
            }
            catch
            {
                // Fallback an toàn: trả về bản sao của ảnh gốc
                return src.Clone();
            }
        }

        /// <summary>
        /// Tiền xử lý chuyên trị Phụ đề Video (YouTube / Phim / TikTok / Reels):
        /// - Khoanh vùng 35% phía dưới (Subtitle ROI) để loại bỏ 65% cảnh vật và mặt người phía trên.
        /// - Lọc mịn hạt nén video (Bilateral Filter).
        /// - Nâng tương phản CLAHE và làm liền khối chữ để triệt tiêu viền đen (Stroke/Shadow) quanh chữ phụ đề.
        /// </summary>
        private static Mat PreprocessSubtitle(Mat src)
        {
            Mat roiMat = null;
            bool isCropped = false;

            // Cắt 35% dưới chân khung hình nếu ảnh đủ cao (> 140px)
            if (src.Height >= 140)
            {
                int cropY = (int)(src.Height * 0.62);
                int cropH = src.Height - cropY;
                Rect roi = new Rect(0, cropY, src.Width, cropH);
                roiMat = new Mat(src, roi);
                isCropped = true;
            }
            else
            {
                roiMat = src;
            }

            try
            {
                // 1. Phóng to 1.5x nếu phụ đề nhỏ để bảo toàn nét dấu tiếng Việt
                Mat scaled = roiMat;
                bool isScaled = false;
                if (roiMat.Width < 1600 || roiMat.Height < 400)
                {
                    scaled = new Mat();
                    Cv2.Resize(roiMat, scaled, new Size((int)(roiMat.Width * 1.5), (int)(roiMat.Height * 1.5)), 0, 0, InterpolationFlags.Cubic);
                    isScaled = true;
                }

                try
                {
                    // 2. Lọc mịn hạt nén MP4/YouTube bằng Bilateral Filter
                    using (Mat filtered = new Mat())
                    {
                        Cv2.BilateralFilter(scaled, filtered, d: 7, sigmaColor: 50, sigmaSpace: 50);

                        // 3. Tăng cường tương phản qua không gian màu LAB (kênh Luminance)
                        using (Mat lab = new Mat())
                        {
                            Cv2.CvtColor(filtered, lab, ColorConversionCodes.BGR2Lab);
                            Mat[] labChannels = Cv2.Split(lab);
                            try
                            {
                                using (var clahe = Cv2.CreateCLAHE(clipLimit: 2.8, tileGridSize: new Size(8, 8)))
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

                                            // 4. Khử viền đen & làm sắc nét chữ
                                            using (Mat blurred = new Mat())
                                            {
                                                Cv2.GaussianBlur(bgrEnhanced, blurred, new Size(0, 0), 1.5);
                                                Mat sharpResult = new Mat();
                                                Cv2.AddWeighted(bgrEnhanced, 1.35, blurred, -0.35, 0, sharpResult);
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
                }
                finally
                {
                    if (isScaled && scaled != null) scaled.Dispose();
                }
            }
            finally
            {
                if (isCropped && roiMat != null) roiMat.Dispose();
            }
        }

        /// <summary>
        /// Tiền xử lý chuyên trị Thumbnail / Ảnh bìa YouTube:
        /// - Chữ nghệ thuật to, nhiều màu sắc, nền phong cảnh hoặc mặt YouTuber phía sau.
        /// - Tăng tương phản mạnh CLAHE, khử hạt nén và làm sắc nét cạnh chữ.
        /// </summary>
        private static Mat PreprocessThumbnail(Mat src)
        {
            Mat workingSrc = src;
            bool isScaled = false;
            if (src.Width < 1400)
            {
                workingSrc = new Mat();
                Cv2.Resize(src, workingSrc, new Size((int)(src.Width * 1.4), (int)(src.Height * 1.4)), 0, 0, InterpolationFlags.Cubic);
                isScaled = true;
            }

            try
            {
                using (Mat filtered = new Mat())
                {
                    Cv2.BilateralFilter(workingSrc, filtered, d: 7, sigmaColor: 45, sigmaSpace: 45);

                    using (Mat lab = new Mat())
                    {
                        Cv2.CvtColor(filtered, lab, ColorConversionCodes.BGR2Lab);
                        Mat[] labChannels = Cv2.Split(lab);
                        try
                        {
                            using (var clahe = Cv2.CreateCLAHE(clipLimit: 3.2, tileGridSize: new Size(8, 8)))
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

                                        using (Mat blurred = new Mat())
                                        {
                                            Cv2.GaussianBlur(bgrEnhanced, blurred, new Size(0, 0), 2.0);
                                            Mat sharpResult = new Mat();
                                            Cv2.AddWeighted(bgrEnhanced, 1.45, blurred, -0.45, 0, sharpResult);
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
            }
            finally
            {
                if (isScaled && workingSrc != null) workingSrc.Dispose();
            }
        }

        /// <summary>
        /// Tiền xử lý chuyên trị bài đăng Facebook, meme, ảnh chụp bình luận:
        /// - Làm phẳng nền thích ứng (Flat-field Closing + Division) triệt tiêu màu nền badge.
        /// - Hỗ trợ cả Light mode và Dark mode.
        /// </summary>
        private static Mat PreprocessSocialPost(Mat src)
        {
            using (Mat gray = new Mat())
            {
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.MeanStdDev(gray, out Scalar grayMean, out Scalar _);

                bool isDarkMode = grayMean.Val0 < 115.0;
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
                            if (isScaled && workingGray != null) workingGray.Dispose();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tự động phân loại ảnh thông minh (Tài liệu / UI vs Ảnh chụp / Banner)
        /// </summary>
        private static Mat PreprocessAuto(Mat src)
        {
            // 1. Phân tích độ bão hòa màu HSV
            double satMeanVal = 0.0;
            using (Mat hsv = new Mat())
            {
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                Mat[] hsvChannels = Cv2.Split(hsv);
                try
                {
                    Cv2.MeanStdDev(hsvChannels[1], out Scalar satMean, out _);
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

            // 2. Phân tích độ phân bố màu nền
            double peakRatio = 0.0;
            using (Mat gray = new Mat())
            {
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                using (Mat hist = new Mat())
                {
                    int[] hdims = { 256 };
                    Rangef[] hranges = { new Rangef(0, 256) };
                    Cv2.CalcHist(new[] { gray }, new[] { 0 }, null, hist, 1, hdims, hranges);
                    Cv2.MinMaxLoc(hist, out _, out double maxVal, out _, out _);
                    peakRatio = maxVal / (double)(src.Width * src.Height);
                }
            }

            // Nếu độ bão hòa màu thấp hoặc có màu nền chủ đạo đồng nhất -> Chế độ tài liệu/UI
            if (satMeanVal < 60.0 || peakRatio > 0.20)
            {
                return PreprocessSocialPost(src);
            }

            // Ngược lại -> Chế độ ảnh chụp phong cảnh / thumbnail đồ họa
            return PreprocessThumbnail(src);
        }
    }
}
