using System;
using System.Text.Json.Serialization;

namespace temp_module.Models
{
    /// <summary>
    /// Model for deserializing ocr_result_t.json records
    /// </summary>
    public class OcrResultRecord
    {
        [JsonPropertyName("countProcessed")]
        public int CountProcessed { get; set; }

        [JsonPropertyName("totalTimeProcess")]
        public double TotalTimeProcess { get; set; }

        [JsonPropertyName("frameT")]
        public FrameData FrameT { get; set; }

        [JsonPropertyName("frameT1")]
        public FrameData FrameT1 { get; set; }

        [JsonPropertyName("frameT2")]
        public FrameData FrameT2 { get; set; }
    }

    public class FrameData
    {
        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; }

        [JsonPropertyName("processTimeFrame")]
        public double ProcessTimeFrame { get; set; }

        [JsonPropertyName("qrDetected")]
        public bool QrDetected { get; set; }

        [JsonPropertyName("qrCodeValue")]
        public string QrCodeValue { get; set; }

        [JsonPropertyName("totalValue")]
        public object TotalValue { get; set; } // Can be string or int

        [JsonPropertyName("productCodeValue")]
        public string ProductCodeValue { get; set; }

        [JsonPropertyName("sizeValue")]
        public string SizeValue { get; set; }

        [JsonPropertyName("colorValue")]
        public string ColorValue { get; set; }

        /// <summary>
        /// Get TotalValue as string regardless of actual type
        /// </summary>
        public string GetTotalValueAsString()
        {
            return TotalValue?.ToString();
        }
    }
}
