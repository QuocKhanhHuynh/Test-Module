namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// Information about image shape transformations during preprocessing.
    /// Used to scale detection boxes back to original image coordinates.
    /// </summary>
    public class ShapeInfo
    {
        public int SrcHeight { get; set; }
        public int SrcWidth { get; set; }
        public int ResizeHeight { get; set; }
        public int ResizeWidth { get; set; }
        public float RatioHeight { get; set; }
        public float RatioWidth { get; set; }
    }
}
