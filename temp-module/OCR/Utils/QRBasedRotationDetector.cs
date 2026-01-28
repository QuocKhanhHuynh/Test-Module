using OpenCvSharp;
using System;
using System.Linq;

namespace temp_module.OCR.Utils
{
    /// <summary>
    /// Xác định góc xoay của label dựa trên vị trí QR code
    /// Thay thế cho PaddleRotationDetector (tiết kiệm resource, nhanh hơn)
    /// </summary>
    public static class QRBasedRotationDetector
    {
        /// <summary>
        /// Xác định label có bị ngược không dựa trên vị trí QR code
        /// Logic: QR code phải nằm bên PHẢI của label (x > midpoint)
        /// </summary>
        /// <param name="imageWidth">Chiều rộng của ảnh label</param>
        /// <param name="imageHeight">Chiều cao của ảnh label (không dùng nhưng giữ cho đầy đủ)</param>
        /// <param name="qrPoints">Tọa độ 4 góc của QR code (Point2f[])</param>
        /// <returns>True nếu cần xoay 180°, False nếu đúng hướng</returns>
        public static bool IsLabelUpsideDown(int imageWidth, int imageHeight, Point2f[] qrPoints)
        {
            if (qrPoints == null || qrPoints.Length != 4)
                throw new ArgumentException("qrPoints must contain exactly 4 points (QR corners)");

            // Tính tâm QR code (trung bình 4 điểm)
            float qrCenterX = qrPoints.Average(p => p.X);

            // Trung điểm của ảnh theo trục X
            float imageMidX = imageWidth / 2.0f;

            // Logic: QR ở bên TRÁI (< midpoint) = NGƯỢC
            //        QR ở bên PHẢI (> midpoint) = ĐÚNG
            bool isUpsideDown = qrCenterX < imageMidX;

            System.Diagnostics.Debug.WriteLine($"[QR-ROTATION] QR Center X: {qrCenterX:F1}, Image Mid X: {imageMidX:F1}, Upside Down: {isUpsideDown}");

            return isUpsideDown;
        }

        /// <summary>
        /// Xác định label có bị ngược không (overload cho Mat)
        /// </summary>
        /// <param name="image">Ảnh label (Mat)</param>
        /// <param name="qrPoints">Tọa độ 4 góc của QR code</param>
        /// <returns>True nếu cần xoay 180°, False nếu đúng hướng</returns>
        public static bool IsLabelUpsideDown(Mat image, Point2f[] qrPoints)
        {
            return IsLabelUpsideDown(image.Width, image.Height, qrPoints);
        }

        /// <summary>
        /// Xác định label có bị ngược không (overload cho Bitmap)
        /// </summary>
        /// <param name="image">Ảnh label (Bitmap)</param>
        /// <param name="qrPoints">Tọa độ 4 góc của QR code</param>
        /// <returns>True nếu cần xoay 180°, False nếu đúng hướng</returns>
        public static bool IsLabelUpsideDown(System.Drawing.Bitmap image, Point2f[] qrPoints)
        {
            return IsLabelUpsideDown(image.Width, image.Height, qrPoints);
        }

        /// <summary>
        /// Xoay ảnh 180° nếu label bị ngược
        /// </summary>
        /// <param name="src">Ảnh nguồn</param>
        /// <param name="isUpsideDown">True = xoay 180°, False = giữ nguyên</param>
        /// <returns>Ảnh đã xoay (hoặc clone nếu không cần xoay)</returns>
        public static Mat RotateIfNeeded(Mat src, bool isUpsideDown)
        {
            if (!isUpsideDown)
            {
                System.Diagnostics.Debug.WriteLine("[QR-ROTATION] ✓ Label is correct orientation (0°)");
                return src.Clone();
            }

            System.Diagnostics.Debug.WriteLine("[QR-ROTATION] ⟲ Rotating label 180° (upside down detected)");
            Mat dst = new Mat();
            Cv2.Rotate(src, dst, RotateFlags.Rotate180);
            return dst;
        }

        /// <summary>
        /// Xoay Bitmap 180° nếu label bị ngược
        /// </summary>
        public static System.Drawing.Bitmap RotateIfNeeded(System.Drawing.Bitmap src, bool isUpsideDown)
        {
            if (!isUpsideDown)
            {
                System.Diagnostics.Debug.WriteLine("[QR-ROTATION] ✓ Label is correct orientation (0°)");
                return (System.Drawing.Bitmap)src.Clone();
            }

            System.Diagnostics.Debug.WriteLine("[QR-ROTATION] ⟲ Rotating label 180° (upside down detected)");
            src.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipNone);
            return src;
        }

        /// <summary>
        /// All-in-one: Kiểm tra và xoay nếu cần
        /// </summary>
        /// <param name="image">Ảnh label sau khi enhance</param>
        /// <param name="qrPoints">Tọa độ QR code</param>
        /// <returns>Ảnh đã được xoay đúng hướng</returns>
        public static Mat DetectAndRotate(Mat image, Point2f[] qrPoints)
        {
            bool needRotate = IsLabelUpsideDown(image, qrPoints);
            return RotateIfNeeded(image, needRotate);
        }

        /// <summary>
        /// All-in-one cho Bitmap
        /// </summary>
        public static System.Drawing.Bitmap DetectAndRotate(System.Drawing.Bitmap image, Point2f[] qrPoints)
        {
            bool needRotate = IsLabelUpsideDown(image, qrPoints);
            return RotateIfNeeded(image, needRotate);
        }
    }
}
