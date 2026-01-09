using System;

namespace temp_module.Models
{
    public class DetectInfo
    {
        public string? QRCode { get; set; }
        public string? Size { get; set; }
        public string? ProductTotal { get; set; }
        public string? Color { get; set; }
        public string? ProductCode { get; set; }

        public override string ToString()
        {
            return $"QRCode: {QRCode}\n" +
                   $"Size: {Size}\n" +
                   $"Total: {ProductTotal}\n" +
                   $"Color: {Color}\n" +
                   $"Product Code: {ProductCode}";
        }
    }


    public class TimeLineStatictis
    {
        public double GetFrame { get; set; }
        public double YoloProcess { get; set; }
        public double RotationProcess { get; set; }
        public double EnhancersProcess { get; set; }
        public double QRDetectProcess { get; set; }
        public double ImageCroptProcess { get; set; }
        public double OCRDetectProcess { get; set; }
    }
}