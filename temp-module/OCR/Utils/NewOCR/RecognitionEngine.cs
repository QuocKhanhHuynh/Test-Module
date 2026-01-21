using OpenCvSharp;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace temp_module.OCR.Utils.NewOCR
{
    /// <summary>
    /// Text recognition engine using PaddleOCR recognition model.
    /// Performs batch preprocessing, inference, and CTC decoding.
    /// </summary>
    public class RecognitionEngine : IDisposable
    {
        private readonly CompiledModel _compiledModel;
        private readonly InferRequest _inferRequest;
        private readonly List<string> _characters;
        private readonly int _imageHeight;
        private readonly int _maxWidth;
        private readonly int _batchSize;
        private readonly float _scoreThresh;

        public RecognitionEngine(
            CompiledModel compiledModel,
            List<string> characters,
            int imageHeight,
            int maxWidth,
            int batchSize,
            float scoreThresh)
        {
            _compiledModel = compiledModel;
            _inferRequest = compiledModel.create_infer_request();
            _characters = characters;
            _imageHeight = imageHeight;
            _maxWidth = maxWidth;
            _batchSize = batchSize;
            _scoreThresh = scoreThresh;

            Debug.WriteLine($"[RecognitionEngine] Initialized with {characters.Count} characters");
        }

        /// <summary>
        /// Recognize text from cropped text regions.
        /// </summary>
        /// <param name="crops">List of cropped text region images</param>
        /// <returns>Tuple of (texts, scores)</returns>
        public (List<string>, List<float>) Recognize(List<Mat> crops)
        {
            int n = crops.Count;
            if (n == 0)
                return (new List<string>(), new List<float>());

            // Initialize results
            string[] texts = new string[n];
            float[] scores = new float[n];

            // 1. Calculate width ratios and sort indices
            // This groups images with similar width ratios together to minimize padding in batches
            var indices = Enumerable.Range(0, n)
                .OrderBy(i => (float)crops[i].Width / crops[i].Height)
                .ToList();

            // 2. Process in batches using sorted indices
            for (int i = 0; i < n; i += _batchSize)
            {
                int batchEnd = Math.Min(i + _batchSize, n);
                int currentBatchSize = batchEnd - i;
                
                // Get batch crops based on sorted indices
                List<Mat> batchCrops = new List<Mat>();
                List<int> batchOriginalIndices = new List<int>();

                for (int j = 0; j < currentBatchSize; j++)
                {
                    int originalIndex = indices[i + j];
                    batchCrops.Add(crops[originalIndex]);
                    batchOriginalIndices.Add(originalIndex);
                }

                // Run recognition on batch
                var (batchTexts, batchScores) = RecognizeBatch(batchCrops);

                // Store results at original indices
                for (int j = 0; j < currentBatchSize; j++)
                {
                    int originalIndex = batchOriginalIndices[j];
                    texts[originalIndex] = batchTexts[j];
                    scores[originalIndex] = batchScores[j];
                }
            }

            return (texts.ToList(), scores.ToList());
        }

        /// <summary>
        /// Recognize a batch of text crops.
        /// </summary>
        private (List<string>, List<float>) RecognizeBatch(List<Mat> crops)
        {
            // Preprocess batch
            (float[] inputData, int batchSize, int maxW) = PreprocessBatch(crops);

            // Create input tensor
            Tensor inputTensor = _inferRequest.get_input_tensor();
            inputTensor.set_shape(new Shape(batchSize, 3, _imageHeight, maxW));
            inputTensor.set_data(inputData);

            // Run inference
            _inferRequest.infer();

            // Get output
            Tensor outputTensor = _inferRequest.get_output_tensor();
            int outputSize = (int)outputTensor.get_size();
            float[] output = outputTensor.get_data<float>(outputSize);

            // Decode CTC
            return DecodeCTC(output, batchSize);
        }

        /// <summary>
        /// Preprocess a batch of crops for recognition.
        /// </summary>
        private (float[], int, int) PreprocessBatch(List<Mat> crops)
        {
            int batchSize = crops.Count;

            // Calculate max width in batch (after resize)
            int maxW = 0;
            List<Mat> resizedCrops = new List<Mat>();

            foreach (var crop in crops)
            {
                // Calculate resize ratio to match target height
                float ratio = (float)_imageHeight / crop.Height;
                int targetW = (int)(crop.Width * ratio);

                // Limit width
                targetW = Math.Min(targetW, _maxWidth);

                // Resize
                Mat resized = new Mat();
                Cv2.Resize(crop, resized, new OpenCvSharp.Size(targetW, _imageHeight));
                resizedCrops.Add(resized);

                maxW = Math.Max(maxW, targetW);
            }

            // Create batch tensor with padding
            float[] batchData = new float[batchSize * 3 * _imageHeight * maxW];

            for (int b = 0; b < batchSize; b++)
            {
                Mat crop = resizedCrops[b];
                int cropW = crop.Width;

                // Convert BGR to RGB and normalize
                Mat rgb = new Mat();
                Cv2.CvtColor(crop, rgb, ColorConversionCodes.BGR2RGB);

                unsafe
                {
                    byte* srcPtr = (byte*)rgb.Data;
                    int offset = b * 3 * _imageHeight * maxW;

                    for (int c = 0; c < 3; c++)
                    {
                        for (int h = 0; h < _imageHeight; h++)
                        {
                            for (int w = 0; w < maxW; w++)
                            {
                                if (w < cropW)
                                {
                                    // PaddleOCR normalization: (pixel/255 - 0.5) / 0.5
                                    // This maps [0, 255] → [-1, 1]
                                    float pixel = srcPtr[(h * cropW + w) * 3 + c] / 255.0f;
                                    pixel = (pixel - 0.5f) / 0.5f;
                                    
                                    batchData[offset + c * _imageHeight * maxW + h * maxW + w] = pixel;
                                }
                                else
                                {
                                    // Padding with zero
                                    batchData[offset + c * _imageHeight * maxW + h * maxW + w] = 0f;
                                }
                            }
                        }
                    }
                }

                rgb.Dispose();
                crop.Dispose();
            }

            return (batchData, batchSize, maxW);
        }

        /// <summary>
        /// Decode CTC output to text strings.
        /// </summary>
        private (List<string>, List<float>) DecodeCTC(float[] output, int batchSize)
        {
            List<string> texts = new List<string>();
            List<float> scores = new List<float>();

            // Output shape: [batch, time_steps, num_classes]
            int timeSteps = output.Length / (batchSize * _characters.Count);

            for (int b = 0; b < batchSize; b++)
            {
                List<int> indices = new List<int>();
                List<float> confidences = new List<float>();

                int prevIndex = -1;

                for (int t = 0; t < timeSteps; t++)
                {
                    // Find max probability at this time step
                    float maxProb = float.MinValue;
                    int maxIndex = 0;

                    for (int c = 0; c < _characters.Count; c++)
                    {
                        int idx = b * timeSteps * _characters.Count + t * _characters.Count + c;
                        float prob = output[idx];

                        if (prob > maxProb)
                        {
                            maxProb = prob;
                            maxIndex = c;
                        }
                    }

                    // CTC decoding rules:
                    // 1. Skip blank tokens (index 0)
                    // 2. Skip consecutive duplicates
                    if (maxIndex != 0 && maxIndex != prevIndex)
                    {
                        indices.Add(maxIndex);
                        confidences.Add(maxProb);
                    }

                    prevIndex = maxIndex;
                }

                // Convert indices to text
                string text = string.Join("", indices.Select(i => 
                    i < _characters.Count ? _characters[i] : "?"));

                // Calculate average confidence
                float avgScore = confidences.Count > 0 
                    ? confidences.Average() 
                    : 0f;

                texts.Add(text);
                scores.Add(avgScore);
            }

            return (texts, scores);
        }

        public void Dispose()
        {
            _inferRequest?.Dispose();
        }
    }
}
