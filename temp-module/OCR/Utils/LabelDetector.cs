using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace demo_ocr_label
{
    public class LabelDetector
    {

        /// <summary>
        /// Xác định tọa độ label, coi label có nằm trong Guild Box không,
        /// Input: Bitmap (BGR), tọa độ QR code
        /// Output: (rotatedRect, boxPoints, qrText, qrPoints) or (null, null, null, null) nếu label không nằm trong Guild Box
        /// </summary>
        //public static (, OpenCvSharp.Point[]? box, string? qrText, Point2f[]? qrPoints180, Point2f[]? qrPoints1)
        public static (RotatedRect? rect, Point2f[] rectPoints, Bitmap DebugBitMap,bool rectInGuildlBox) DetectLabelRegionWithQrCode(Bitmap inputBmp, Point2f[] qrPoints)
        {
            //if (inputBmp == null)
            //    return (null, null);

            // Độ dài cạnh QR code
            float qrSideLength = (float)Point2f.Distance(qrPoints[1], qrPoints[0]);


            // Convert Bitmap -> Mat (BGR)
            Mat src;
            using (var ms = new MemoryStream())
            {
                inputBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                src = Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
            }

            if (qrPoints != null)
            {
                // Tính hình chữ nhật bao quanh
                Point2f[] rectPoints = RectangleAroundQR.GetRectangleAroundQR(qrPoints, offsetX: 0.0f, offsetY: 0.00f, widthScale: 4f, heightScale: 2f, imageWidth: inputBmp.Width, imageHeight: inputBmp.Height);
                
                // Vẽ hình chữ nhật lên Guild Box để debug
                Bitmap debugBmp = RectangleAroundQR.DrawDebugRectangle(inputBmp, qrPoints, rectPoints);
                
                // Kiểm tra xem 4 đỉnh có nằm trong ROI box không
                // ROI box là inputBmp (ảnh đã crop), nên kiểm tra trong bounds (0, 0, width, height)
                bool rectInGuildlBox = true;
                float roiWidth = inputBmp.Width;
                float roiHeight = inputBmp.Height;
                foreach (var point in rectPoints)
                {
                    if (point.X < 0 || point.X >= roiWidth || point.Y < 0 || point.Y >= roiHeight)
                    {
                        rectInGuildlBox = false;
                        break;
                    }
                }

                // tính react
                RotatedRect rect = Cv2.MinAreaRect(rectPoints);
                

                return (rect, rectPoints, debugBmp, rectInGuildlBox);
            }

            return (null, null, null, false);
        }



        /// <summary>
        /// Xoay và cắt label theo tọa độ rect trong ROI.
        /// Nhận vào: ROI bitmap, rect, box, qrPoints → trả về ảnh label đã xoay thẳng.
        /// </summary>
        public static (Bitmap BitMapCropped, OpenCvSharp.Point[] qrBox) CropAndAlignLabel(Bitmap roi, RotatedRect rect, OpenCvSharp.Point2f[] box,
                                Point2f[] qrPoints180, Point2f[] qrPoints)
        {
            try
            {
                if (roi == null)
                    throw new ArgumentNullException(nameof(roi));

                using var src = BitmapToMat(roi);

                // 🔹 1) Kích thước label
                int labelWidth = (int)rect.Size.Width;
                int labelHeight = (int)rect.Size.Height;
                if (labelWidth <= 0 || labelHeight <= 0)
                    return (null, null);

                // 🔹 2) Chuẩn hóa góc xoay
                float angle = rect.Angle;

                if (rect.Size.Width < rect.Size.Height)
                    angle += 90;

                if (angle >= 135 && angle <= 180)
                {
                    angle -= 180;
                    labelWidth = (int)rect.Size.Height;
                    labelHeight = (int)rect.Size.Width;
                }
                if (angle > 90 && angle <= 135)
                {
                    labelWidth = (int)rect.Size.Height;
                    labelHeight = (int)rect.Size.Width;
                }

                float labelAngle = angle;

                // 🔹 3) Tính góc QR Code từ qrPoints180 (dùng để xác định ngược)
                Point2f vec_QR_Top = qrPoints180[1] - qrPoints180[0];
                float qrAngle = (float)(Math.Atan2(vec_QR_Top.Y, vec_QR_Top.X) * (180.0 / Math.PI));

                // 🔹 4) So sánh góc
                float deltaAngle = labelAngle - qrAngle;
                while (deltaAngle <= -180) deltaAngle += 360;
                while (deltaAngle > 180) deltaAngle -= 360;
                bool needs180Flip = Math.Abs(deltaAngle) > 90;

                // 🔹 5) Ma trận xoay quanh tâm label
                Mat rotationMatrix = Cv2.GetRotationMatrix2D(rect.Center, labelAngle, 1.0);

                // 🔹 6) Xoay ROI
                Mat rotated = new Mat();
                Cv2.WarpAffine(src, rotated, rotationMatrix, src.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

                // 🔹 7) Cắt vùng label
                OpenCvSharp.Point2f center = rect.Center;
                int x = (int)(center.X - labelWidth / 2.0f);
                int y = (int)(center.Y - labelHeight / 2.0f);

                x = Math.Max(0, Math.Min(x, rotated.Width - 1));
                y = Math.Max(0, Math.Min(y, rotated.Height - 1));
                labelWidth = Math.Min(labelWidth, rotated.Width - x);
                labelHeight = Math.Min(labelHeight, rotated.Height - y);

                OpenCvSharp.Rect cropRect = new(x, y, labelWidth, labelHeight);
                Mat cropped = new Mat(rotated, cropRect);

                // 🔹 8) Tính lại tọa độ QR trong ảnh đã xoay & cắt (dựa trên qrPoints)
                Point2f[] rotatedQRPoints = new Point2f[qrPoints.Length];

                // --- affine chuẩn ---
                Mat affine33 = Mat.Eye(3, 3, MatType.CV_64F);
                rotationMatrix.CopyTo(affine33[new OpenCvSharp.Rect(0, 0, 3, 2)]);

                Mat cropTranslate = Mat.Eye(3, 3, MatType.CV_64F);
                cropTranslate.At<double>(0, 2) = -x;
                cropTranslate.At<double>(1, 2) = -y;

                Mat finalTransform = cropTranslate * affine33;

                for (int i = 0; i < qrPoints.Length; i++)
                {
                    double px = qrPoints[i].X;
                    double py = qrPoints[i].Y;

                    double X = finalTransform.At<double>(0, 0) * px +
                               finalTransform.At<double>(0, 1) * py +
                               finalTransform.At<double>(0, 2);
                    double Y = finalTransform.At<double>(1, 0) * px +
                               finalTransform.At<double>(1, 1) * py +
                               finalTransform.At<double>(1, 2);

                    rotatedQRPoints[i] = new Point2f((float)X, (float)Y);
                }

                // 🔹 9) Lật 180° nếu cần
                if (needs180Flip)
                {
                    Cv2.Rotate(cropped, cropped, RotateFlags.Rotate180);
                    for (int i = 0; i < rotatedQRPoints.Length; i++)
                    {
                        rotatedQRPoints[i].X = labelWidth - rotatedQRPoints[i].X;
                        rotatedQRPoints[i].Y = labelHeight - rotatedQRPoints[i].Y;
                    }
                }

                // 🔹 10) Ảnh phải có 3 kênh
                if (cropped.Channels() == 1)
                    Cv2.CvtColor(cropped, cropped, ColorConversionCodes.GRAY2BGR);

                // 🔹 12) Vẽ QR box
                OpenCvSharp.Point[] qrBox = rotatedQRPoints
                    .Select(p => new OpenCvSharp.Point((int)Math.Round(p.X), (int)Math.Round(p.Y)))
                    .ToArray();

                Bitmap BitMapCropped = MatToBitmap(cropped);

                 // 🔹 13) Trả kết quả
                 return (BitMapCropped, qrBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CropAndAlignLabel ERROR] {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// Tìm QR code trong vùng ROI.
        /// </summary>
        /// <param name="roi">Vùng ảnh cần tìm QR code</param>
        /// <returns>Tọa độ 4 điểm của QR code (Point2f[]) nếu tìm thấy, null nếu không tìm thấy</returns>
        public static (Point2f[]? qrPoints, string qrText) DetectQRCode(Bitmap roi)
        {
            if (roi == null)
                return (null, null);

            try
            {
                using var mat = BitmapToMat(roi);
                using var qr = new QRCodeDetector();
                using var straight = new Mat();

                string qrText = qr.DetectAndDecode(mat, out Point2f[] qrPoints, straight);

                if (!string.IsNullOrEmpty(qrText) && qrPoints != null && qrPoints.Length == 4)
                    return (qrPoints, qrText);

                return (null, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DetectQRCode ERROR] {ex.Message}");
                return (null, null);
            }
        }

        // === Helper ===
        public static Mat BitmapToMat(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }

        public static Bitmap MatToBitmap(Mat mat)
        {
            Cv2.ImEncode(".png", mat, out var buf);
            using var ms = new MemoryStream(buf);
            using var tmp = new Bitmap(ms);
            return new Bitmap(tmp);
        }


    }
}

