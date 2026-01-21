using OpenCvSharp;

namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// Result from OCR text detection and recognition.
    /// Contains bounding box, recognized text, and confidence score.
    /// </summary>
    public class OCRResult
    {
        /// <summary>
        /// Bounding box as 4 corner points [TL, TR, BR, BL]
        /// </summary>
        public OpenCvSharp.Point[] BoundingBox { get; set; } = new OpenCvSharp.Point[4];

        /// <summary>
        /// Recognized text string
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score (0-1)
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Source of this box: 'detected' or 'created'
        /// </summary>
        public string Source { get; set; } = "detected";

        public override string ToString()
        {
            return $"Text: {Text}, Score: {Score:F3}, Source: {Source}";
        }
    }
}
