namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// Configuration for PaddleOCR OpenVINO engine.
    /// Matches Python TextExtractor constructor parameters.
    /// </summary>
    public class OCRConfig
    {
        // Detection parameters
        public string DetLimitType { get; set; } = "max";
        public int DetLimitSideLen { get; set; } = 640;
        public float DetThresh { get; set; } = 0.15f;
        public float DetBoxThresh { get; set; } = 0.15f;
        public float DetUnclipRatio { get; set; } = 2.0f;

        // Recognition parameters
        public int RecImageHeight { get; set; } = 48; // Updated to match Python default
        public int RecMaxWidth { get; set; } = 320;
        public int RecBatchSize { get; set; } = 6;
        public float RecScoreThresh { get; set; } = 0.3f;

        // OpenVINO parameters
        public string Device { get; set; } = "CPU";
        public int NumThreads { get; set; } = 2;
        public int NumStreams { get; set; } = 1;
        public string PerformanceHint { get; set; } = "LATENCY";
        public bool EnableHyperThreading { get; set; } = false;
        public bool EnableCpuPinning { get; set; } = true;
        public string CacheDir { get; set; } = "";
    }
}
