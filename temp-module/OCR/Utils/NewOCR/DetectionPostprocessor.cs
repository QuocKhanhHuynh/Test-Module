using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// Postprocessing for DBNet text detection output.
    /// Converts detection probability maps to bounding boxes.
    /// </summary>
    public static class DetectionPostprocessor
    {
        /// <summary>
        /// Process detection model output to get text bounding boxes.
        /// </summary>
        /// <param name="output">Detection output tensor [1, 1, H, W]</param>
        /// <param name="shapeInfo">Image shape information</param>
        /// <param name="thresh">Pixel threshold for binary mask</param>
        /// <param name="boxThresh">Minimum box score threshold</param>
        /// <param name="unclipRatio">Box expansion ratio</param>
        /// <returns>List of bounding boxes as Point arrays</returns>
        public static List<OpenCvSharp.Point[]> ProcessOutput(
            float[] output,
            ShapeInfo shapeInfo,
            float thresh,
            float boxThresh,
            float unclipRatio)
        {
            // Extract prediction map (remove batch and channel dimensions)
            int h = shapeInfo.ResizeHeight;
            int w = shapeInfo.ResizeWidth;
            float[,] pred = new float[h, w];
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pred[y, x] = output[y * w + x];
                }
            }

            // Apply threshold to get binary mask
            Mat segmentation = new Mat(h, w, MatType.CV_8UC1);
            unsafe
            {
                byte* ptr = (byte*)segmentation.Data;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        ptr[y * w + x] = pred[y, x] > thresh ? (byte)255 : (byte)0;
                    }
                }
            }

            // Find contours
            Cv2.FindContours(
                segmentation,
                out OpenCvSharp.Point[][] contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple
            );

            List<OpenCvSharp.Point[]> boxes = new List<OpenCvSharp.Point[]>();

            foreach (var contour in contours)
            {
                // Skip small contours
                if (contour.Length < 4)
                    continue;

                // Get minimum area rectangle
                RotatedRect rect = Cv2.MinAreaRect(contour);
                Point2f[] boxF = rect.Points();
                OpenCvSharp.Point[] box = Array.ConvertAll(boxF, p => new OpenCvSharp.Point((int)p.X, (int)p.Y));

                // Calculate box score
                float boxScore = CalculateBoxScore(pred, contour);
                if (boxScore < boxThresh)
                    continue;

                // Expand box using unclip ratio
                OpenCvSharp.Point[] expandedBox = UnclipBox(box, unclipRatio);
                if (expandedBox == null)
                    continue;

                // Get new minimum area rectangle after unclipping
                rect = Cv2.MinAreaRect(expandedBox);
                boxF = rect.Points();
                box = Array.ConvertAll(boxF, p => new OpenCvSharp.Point((int)p.X, (int)p.Y));

                // Skip boxes that are too small
                if (Math.Min(rect.Size.Width, rect.Size.Height) < 3)
                    continue;

                boxes.Add(box);
            }

            segmentation.Dispose();

            if (boxes.Count == 0)
                return new List<OpenCvSharp.Point[]>();

            // Scale boxes back to original image size
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = 0; j < boxes[i].Length; j++)
                {
                    boxes[i][j].X = (int)(boxes[i][j].X / shapeInfo.RatioWidth);
                    boxes[i][j].Y = (int)(boxes[i][j].Y / shapeInfo.RatioHeight);

                    // Clip to image boundaries
                    boxes[i][j].X = Math.Max(0, Math.Min(boxes[i][j].X, shapeInfo.SrcWidth));
                    boxes[i][j].Y = Math.Max(0, Math.Min(boxes[i][j].Y, shapeInfo.SrcHeight));
                }
            }

            return boxes;
        }

        /// <summary>
        /// Calculate average prediction score inside a contour.
        /// </summary>
        private static float CalculateBoxScore(float[,] pred, OpenCvSharp.Point[] contour)
        {
            int h = pred.GetLength(0);
            int w = pred.GetLength(1);

            // Get bounding rectangle
            Rect boundingRect = Cv2.BoundingRect(contour);

            // Clip to prediction map bounds
            int xMin = Math.Max(0, boundingRect.X);
            int yMin = Math.Max(0, boundingRect.Y);
            int xMax = Math.Min(w, boundingRect.X + boundingRect.Width);
            int yMax = Math.Min(h, boundingRect.Y + boundingRect.Height);

            if (xMax <= xMin || yMax <= yMin)
                return 0f;

            // Create mask for the contour
            Mat mask = new Mat(yMax - yMin, xMax - xMin, MatType.CV_8UC1, Scalar.All(0));
            OpenCvSharp.Point[] shiftedContour = contour.Select(p => new OpenCvSharp.Point(p.X - xMin, p.Y - yMin)).ToArray();
            Cv2.FillPoly(mask, new[] { shiftedContour }, Scalar.All(1));

            // Calculate mean score
            float sum = 0f;
            int count = 0;

            unsafe
            {
                byte* maskPtr = (byte*)mask.Data;
                for (int y = 0; y < mask.Height; y++)
                {
                    for (int x = 0; x < mask.Width; x++)
                    {
                        if (maskPtr[y * mask.Width + x] > 0)
                        {
                            sum += pred[yMin + y, xMin + x];
                            count++;
                        }
                    }
                }
            }

            mask.Dispose();
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>
        /// Expand a box using Vatti clipping algorithm.
        /// </summary>
        private static OpenCvSharp.Point[] UnclipBox(OpenCvSharp.Point[] box, float unclipRatio)
        {
            try
            {
                // Calculate polygon area and perimeter
                double area = Cv2.ContourArea(box);
                if (area < 1)
                    return null;

                double perimeter = Cv2.ArcLength(box, true);
                if (perimeter <= 0)
                    return null;

                // Calculate expansion distance
                double distance = area * unclipRatio / perimeter;

                // Convert to Clipper2 path
                Path64 path = new Path64(box.Length);
                foreach (var pt in box)
                {
                    path.Add(new Point64(pt.X, pt.Y));
                }

                // Perform offset (expansion)
                Paths64 solution = Clipper.InflatePaths(
                    new Paths64 { path },
                    distance,
                    JoinType.Round,
                    EndType.Polygon
                );

                if (solution.Count == 0)
                    return null;

                // Convert back to Point array
                OpenCvSharp.Point[] result = new OpenCvSharp.Point[solution[0].Count];
                for (int i = 0; i < solution[0].Count; i++)
                {
                    result[i] = new OpenCvSharp.Point((int)solution[0][i].X, (int)solution[0][i].Y);
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sort boxes from top-to-bottom, then left-to-right.
        /// </summary>
        public static List<OpenCvSharp.Point[]> SortBoxes(List<OpenCvSharp.Point[]> boxes)
        {
            if (boxes.Count == 0)
                return boxes;

            // Sort by y-coordinate of top-left corner, then by x-coordinate
            return boxes
                .OrderBy(box => box.Min(p => p.Y))  // Top-to-bottom
                .ThenBy(box => box.Min(p => p.X))   // Left-to-right
                .ToList();
        }

        /// <summary>
        /// Orders 4 points in consistent order: Top-Left, Top-Right, Bottom-Right, Bottom-Left.
        /// </summary>
        public static Point2f[] OrderPoints(Point2f[] pts)
        {
            // Consistent with Python _order_points
            
            // Calculate center
            float cx = 0, cy = 0;
            foreach (var p in pts)
            {
                cx += p.X;
                cy += p.Y;
            }
            cx /= pts.Length;
            cy /= pts.Length;
            
            Point2f center = new Point2f(cx, cy);

            // Sort by angle relative to center
            // OrderBy requires System.Linq
            var sortedPts = pts.OrderBy(p => Math.Atan2(p.Y - center.Y, p.X - center.X)).ToArray();

            // Find Top-Left (smallest sum x+y)
            float minSum = float.MaxValue;
            int tlIdx = 0;
            
            for (int i = 0; i < sortedPts.Length; i++)
            {
                float sum = sortedPts[i].X + sortedPts[i].Y;
                if (sum < minSum)
                {
                    minSum = sum;
                    tlIdx = i;
                }
            }

            // Roll to put TL at index 0
            Point2f[] ordered = new Point2f[4];
            for (int i = 0; i < 4; i++)
            {
                ordered[i] = sortedPts[(tlIdx + i) % 4];
            }

            return ordered;
        }
    }
}
