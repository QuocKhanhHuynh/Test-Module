using OpenCvSharp;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// PaddleOCR engine using OpenVINO Runtime.
    /// Provides text detection and recognition without PaddleOCRSharp library dependency.
    /// Port of Python TextExtractor from PaddleOCR-OpenVINO project.
    /// </summary>
    public class PaddleOCROpenVINO : IDisposable
    {
        private readonly Core _core;
        private Model _model;
        private readonly CompiledModel _detCompiled;
        private readonly CompiledModel _recCompiled;
        private readonly InferRequest _detInferRequest;
        private readonly RecognitionEngine _recognitionEngine;
        private readonly OCRConfig _config;
        private readonly List<string> _characters;

        public PaddleOCROpenVINO(
            string detModelPath,
            string recModelPath,
            string charDictPath,
            OCRConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Validate paths
            if (!File.Exists(detModelPath))
                throw new FileNotFoundException($"Detection model not found: {detModelPath}");
            if (!File.Exists(recModelPath))
                throw new FileNotFoundException($"Recognition model not found: {recModelPath}");
            if (!File.Exists(charDictPath))
                throw new FileNotFoundException($"Character dictionary not found: {charDictPath}");

            Console.WriteLine("[PaddleOCROpenVINO] Initializing...");

            // Load character dictionary
            _characters = LoadCharacterDict(charDictPath);

            // Initialize OpenVINO Core
            Console.WriteLine("[OpenVINO] Initializing Core...");
            _core = new Core();

            // Configure CPU properties (like YOLO pattern)
            if (config.Device.ToUpper().Contains("CPU"))
            {
                var ovConfigs = new List<KeyValuePair<string, string>>
                {
                    new ("INFERENCE_NUM_THREADS", config.NumThreads.ToString()),
                    new ("NUM_STREAMS", config.NumStreams.ToString()),
                };

                // Apply configurations
                foreach (var cfg in ovConfigs)
                {
                    _core.set_property("CPU", cfg);
                }

                Console.WriteLine($"[OpenVINO] CPU config: {config.NumThreads} threads, {config.NumStreams} streams");
            }

            // Load and compile detection model
            Console.WriteLine($"[OpenVINO] Reading detection model: {detModelPath}");
            try
            {
                _model = _core.read_model(detModelPath);
                Console.WriteLine("[OpenVINO] Detection model loaded successfully");
                
                // Compile model for device (2-parameter signature like YOLO)
                Console.WriteLine($"[OpenVINO] Compiling detection model for {config.Device}...");
                _detCompiled = _core.compile_model(_model, config.Device);
                _detInferRequest = _detCompiled.create_infer_request();
                
                Console.WriteLine("[OpenVINO] Detection model ready");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenVINO] Detection model error: {ex.Message}");
                throw new Exception($"Failed to load detection model: {ex.Message}", ex);
            }

            // Load and compile recognition model
            Console.WriteLine($"[OpenVINO] Reading recognition model: {recModelPath}");
            try
            {
                Model recModel = _core.read_model(recModelPath);
                Console.WriteLine("[OpenVINO] Recognition model loaded successfully");
                
                // Compile model for device
                Console.WriteLine($"[OpenVINO] Compiling recognition model for {config.Device}...");
                _recCompiled = _core.compile_model(recModel, config.Device);
                
                Console.WriteLine("[OpenVINO] Recognition model ready");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenVINO] Recognition model error: {ex.Message}");
                throw new Exception($"Failed to load recognition model: {ex.Message}", ex);
            }

            // Create recognition engine
            _recognitionEngine = new RecognitionEngine(
                _recCompiled,
                _characters,
                config.RecImageHeight,
                config.RecMaxWidth,
                config.RecBatchSize,
                config.RecScoreThresh
            );

            Console.WriteLine("[PaddleOCROpenVINO] Initialization complete. Ready for inference.");
        }

        /// <summary>
        /// Detect and recognize text in an image.
        /// </summary>
        /// <param name="image">Input image (BGR format)</param>
        /// <returns>List of OCR results with bounding boxes and text</returns>
        public List<OCRResult> DetectText(Mat image)
        {
            if (image == null || image.Empty())
            {
                Debug.WriteLine("[PaddleOCROpenVINO] Empty image provided");
                return new List<OCRResult>();
            }

            Stopwatch sw = Stopwatch.StartNew();

            // Step 1: Preprocess for detection
            (float[] inputData, ShapeInfo shapeInfo) = PreprocessDetection(image);

            // Step 2: Run detection inference
            Tensor inputTensor = _detInferRequest.get_input_tensor();
            inputTensor.set_shape(new Shape(1, 3, shapeInfo.ResizeHeight, shapeInfo.ResizeWidth));
            inputTensor.set_data(inputData);

            _detInferRequest.infer();

            // Get detection output
            Tensor outputTensor = _detInferRequest.get_output_tensor();
            int outputSize = (int)outputTensor.get_size();
            float[] output = outputTensor.get_data<float>(outputSize);

            // Step 3: Postprocess detection
            List<OpenCvSharp.Point[]> boxes = DetectionPostprocessor.ProcessOutput(
                output,
                shapeInfo,
                _config.DetThresh,
                _config.DetBoxThresh,
                _config.DetUnclipRatio
            );

            if (boxes.Count == 0)
            {
                Debug.WriteLine("[PaddleOCROpenVINO] No text detected");
                return new List<OCRResult>();
            }

            // Step 4: Sort boxes
            boxes = DetectionPostprocessor.SortBoxes(boxes);

            // Step 5: Crop text regions
            List<Mat> crops = new List<Mat>();
            int separatedHeight = 10;
            int totalHeight = 0;
            int maxWidth = 0;

            foreach (var box in boxes)
            {
                Mat crop = GetRotateCropImage(image, box);
                if (crop != null && !crop.Empty())
                {
                    crops.Add(crop);
                    totalHeight += crop.Height + separatedHeight;
                    if (crop.Width > maxWidth) maxWidth = crop.Width;
                }
            }

            

            if (crops.Count == 0)
            {
                Debug.WriteLine("[PaddleOCROpenVINO] No valid crops after filtering");
                return new List<OCRResult>();
            }

            // Step 6: Run recognition
            (List<string> texts, List<float> scores) = _recognitionEngine.Recognize(crops);

            // Dispose crops
            foreach (var crop in crops)
                crop?.Dispose();

            // Step 7: Combine results
            List<OCRResult> results = new List<OCRResult>();
            for (int i = 0; i < Math.Min(boxes.Count, texts.Count); i++)
            {
                results.Add(new OCRResult
                {
                    BoundingBox = boxes[i],
                    Text = texts[i],
                    Score = scores[i],
                    Source = "detected"
                });
            }

            sw.Stop();
            Debug.WriteLine($"[PaddleOCROpenVINO] Detected {results.Count} text regions in {sw.ElapsedMilliseconds}ms");

            return results;
        }

        /// <summary>
        /// Preprocess image for text detection.
        /// </summary>
        private (float[], ShapeInfo) PreprocessDetection(Mat image)
        {
            int srcH = image.Height;
            int srcW = image.Width;

            // Calculate resize ratio
            float ratio;
            if (_config.DetLimitType == "max")
                ratio = Math.Min((float)_config.DetLimitSideLen / Math.Max(srcH, srcW), 1.0f);
            else // "min"
                ratio = Math.Max((float)_config.DetLimitSideLen / Math.Min(srcH, srcW), 1.0f);

            int newH = (int)(srcH * ratio);
            int newW = (int)(srcW * ratio);

            // Ensure dimensions are divisible by 32 (required by DB model)
            newH = Math.Max(32, (newH / 32) * 32);
            newW = Math.Max(32, (newW / 32) * 32);

            // Resize image
            Mat resized = new Mat();
            Cv2.Resize(image, resized, new OpenCvSharp.Size(newW, newH));

            // Convert BGR to RGB
            Mat rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            // Normalize and convert to CHW format
            float[] inputData = new float[3 * newH * newW];
            float[] mean = { 0.485f, 0.456f, 0.406f };
            float[] std = { 0.229f, 0.224f, 0.225f };

            unsafe
            {
                byte* ptr = (byte*)rgb.Data;
                for (int c = 0; c < 3; c++)
                {
                    for (int h = 0; h < newH; h++)
                    {
                        for (int w = 0; w < newW; w++)
                        {
                            float pixel = ptr[(h * newW + w) * 3 + c] / 255.0f;
                            pixel = (pixel - mean[c]) / std[c];
                            inputData[c * newH * newW + h * newW + w] = pixel;
                        }
                    }
                }
            }

            resized.Dispose();
            rgb.Dispose();

            ShapeInfo shapeInfo = new ShapeInfo
            {
                SrcHeight = srcH,
                SrcWidth = srcW,
                ResizeHeight = newH,
                ResizeWidth = newW,
                RatioHeight = (float)newH / srcH,
                RatioWidth = (float)newW / srcW
            };

            return (inputData, shapeInfo);
        }

        /// <summary>
        /// Crop and straighten a text region to horizontal orientation using geometric transformation.
        /// </summary>
        private Mat GetRotateCropImage(Mat image, OpenCvSharp.Point[] box)
        {
            try
            {
                // Convert to Point2f for geometric operations
                Point2f[] points = new Point2f[box.Length];
                for (int i = 0; i < box.Length; i++)
                {
                    points[i] = new Point2f(box[i].X, box[i].Y);
                }

                // Order points: TL, TR, BR, BL
                Point2f[] ordered = DetectionPostprocessor.OrderPoints(points);

                // Helper to calculate distance
                float Distance(Point2f p1, Point2f p2)
                {
                    float dx = p1.X - p2.X;
                    float dy = p1.Y - p2.Y;
                    return (float)Math.Sqrt(dx * dx + dy * dy);
                }

                // Calculate width (TL-TR and BL-BR)
                float widthA = Distance(ordered[0], ordered[1]);
                float widthB = Distance(ordered[3], ordered[2]);
                int width = (int)Math.Max(widthA, widthB);

                // Calculate height (TL-BL and TR-BR)
                float heightA = Distance(ordered[0], ordered[3]);
                float heightB = Distance(ordered[1], ordered[2]);
                int height = (int)Math.Max(heightA, heightB);

                // Basic size check
                if (width < 3 || height < 3)
                    return null;

                // Define destination points
                Point2f[] dstPts = new Point2f[]
                {
                    new Point2f(0, 0),          // TL
                    new Point2f(width, 0),      // TR
                    new Point2f(width, height), // BR
                    new Point2f(0, height)      // BL
                };

                // Get perspective transform
                // Note: GetPerspectiveTransform accepts IEnumerable<Point2f> or InputArray
                using (Mat M = Cv2.GetPerspectiveTransform(ordered, dstPts))
                {
                    // Apply transform
                    Mat cropped = new Mat();
                    Cv2.WarpPerspective(
                        image, 
                        cropped, 
                        M, 
                        new OpenCvSharp.Size(width, height), 
                        InterpolationFlags.Linear, 
                        BorderTypes.Replicate);

                    // Filter out regions that are too small or narrow
                    if (cropped.Height < 12 || cropped.Width < cropped.Height / 2)
                    {
                        cropped.Dispose();
                        return null;
                    }

                    return cropped;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PaddleOCROpenVINO] Rotate crop error: {ex.Message}");
                // Ensure we don't crash, return null so we can fallback or skip
                return null; 
            }
        }

        /// <summary>
        /// Load character dictionary from file.
        /// </summary>
        private List<string> LoadCharacterDict(string path)
        {
            List<string> characters = new List<string>();
            characters.Add("blank");

            // Read lines without trimming
            string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
            foreach (string line in lines)
            {
                // Just remove carriage return, keep spaces
                string charStr = line.Replace("\r", "");
                characters.Add(charStr);
            }

            Debug.WriteLine($"[PaddleOCROpenVINO] Loaded {characters.Count} characters");
            return characters;
        }

        public void Dispose()
        {
            _recognitionEngine?.Dispose();
            _detInferRequest?.Dispose();
            _detCompiled?.Dispose();
            _recCompiled?.Dispose();
            _core?.Dispose();
        }
    }
}
