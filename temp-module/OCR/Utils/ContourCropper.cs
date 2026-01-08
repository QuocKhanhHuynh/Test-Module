using OpenCvSharp;
using System.Collections.Generic;

namespace temp_module.OCR.Utils
{
    /// <summary>
    /// Crop image by contour points (OpenCvSharp.Point)
    /// </summary>
    public static class ContourCropper
    {
        /// <summary>
        /// Cắt và xoay vùng ảnh theo contour points (giống ProcessRotationImage)
        /// </summary>
        /// <param name="img">Ảnh gốc</param>
        /// <param name="contourPoints">Danh sách điểm contour</param>
        /// <returns>Mat đã được cắt và xoay theo bounding box xoay của contour</returns>
        public static Mat CropByContour(Mat img, IEnumerable<OpenCvSharp.Point> contourPoints)
        {
            var pts = new List<OpenCvSharp.Point>(contourPoints);
            if (pts.Count < 3) return img.Clone();

            // Chuyển sang Point2f
            Point2f[] ptsF = pts.Select(p => new Point2f(p.X, p.Y)).ToArray();
            // Tìm min area rect
            RotatedRect minRect = Cv2.MinAreaRect(ptsF);
            Point2f[] box = minRect.Points();

            // Sắp xếp lại thứ tự điểm: TL, TR, BR, BL
            Point2f[] orderedBox = OrderPoints(box);
            var tl = orderedBox[0];
            var tr = orderedBox[1];
            var br = orderedBox[2];
            var bl = orderedBox[3];

            // Tính kích thước hình chữ nhật mới
            double widthA = Math.Sqrt(Math.Pow(br.X - bl.X, 2) + Math.Pow(br.Y - bl.Y, 2));
            double widthB = Math.Sqrt(Math.Pow(tr.X - tl.X, 2) + Math.Pow(tr.Y - tl.Y, 2));
            double heightA = Math.Sqrt(Math.Pow(tr.X - br.X, 2) + Math.Pow(tr.Y - br.Y, 2));
            double heightB = Math.Sqrt(Math.Pow(tl.X - bl.X, 2) + Math.Pow(tl.Y - bl.Y, 2));

            int maxWidth = (int)Math.Round(Math.Max(widthA, widthB));
            int maxHeight = (int)Math.Round(Math.Max(heightA, heightB));

            // Nếu chiều cao lớn hơn chiều rộng, hoán đổi width/height và xoay lại điểm đích để cạnh dài nằm ngang
            bool needRotate = maxHeight > maxWidth;
            if (needRotate)
            {
                int tmp = maxWidth;
                maxWidth = maxHeight;
                maxHeight = tmp;
                // Xoay điểm đích 90 độ
                Point2f[] dstPts = new Point2f[] {
                    new Point2f(0, maxHeight - 1),
                    new Point2f(0, 0),
                    new Point2f(maxWidth - 1, 0),
                    new Point2f(maxWidth - 1, maxHeight - 1)
                };
                Mat M = Cv2.GetPerspectiveTransform(orderedBox, dstPts);
                Mat warped = new Mat();
                Cv2.WarpPerspective(img, warped, M, new OpenCvSharp.Size(maxWidth, maxHeight), InterpolationFlags.Linear, BorderTypes.Replicate);
                return warped;
            }
            else
            {
                if (maxWidth <= 0 || maxHeight <= 0)
                    return null;
                Point2f[] dstPts = new Point2f[] {
                    new Point2f(0, 0),
                    new Point2f(maxWidth - 1, 0),
                    new Point2f(maxWidth - 1, maxHeight - 1),
                    new Point2f(0, maxHeight - 1)
                };
                Mat M = Cv2.GetPerspectiveTransform(orderedBox, dstPts);
                Mat warped = new Mat();
                Cv2.WarpPerspective(img, warped, M, new OpenCvSharp.Size(maxWidth, maxHeight), InterpolationFlags.Linear, BorderTypes.Replicate);
                return warped;
            }

        }

        /// <summary>
        /// Sắp xếp lại thứ tự 4 điểm: TL, TR, BR, BL
        /// </summary>
        private static Point2f[] OrderPoints(Point2f[] pts)
        {
            // Tìm centroid
            var center = new Point2f(pts.Average(p => p.X), pts.Average(p => p.Y));
            // Tính góc từng điểm so với tâm
            var angles = pts.Select(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToArray();
            // Sắp xếp theo góc tăng dần
            var sorted = pts.Zip(angles, (p, a) => new { p, a }).OrderBy(x => x.a).Select(x => x.p).ToArray();
            // Tìm top-left (tổng x+y nhỏ nhất)
            var sums = sorted.Select(p => p.X + p.Y).ToArray();
            int tlIdx = Array.IndexOf(sums, sums.Min());
            // Xoay mảng để top-left lên đầu
            var ordered = new Point2f[4];
            for (int i = 0; i < 4; i++)
                ordered[i] = sorted[(i + tlIdx) % 4];
            return ordered;
        }
    }
}
