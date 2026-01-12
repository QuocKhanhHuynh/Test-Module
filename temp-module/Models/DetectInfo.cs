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
        public double? TimeProcess { get; set; }
        public string? ImagePath { get; set; }

        public override string ToString()
        {
            return $"QRCode: {QRCode}\n" +
                   $"Size: {Size}\n" +
                   $"Total: {ProductTotal}\n" +
                   $"Color: {Color}\n" +
                   $"Product Code: {ProductCode}";
        }
    }
}