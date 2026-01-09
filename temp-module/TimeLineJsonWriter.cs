using DocumentFormat.OpenXml.ExtendedProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using temp_module.Models;

namespace temp_module
{
    public static class TimeLineJsonWriter
    {
        public static void WriteTimeLineResult(
            string jsonPath,
            string imageFilePath,
            TimeLineStatictis timeLine,
            bool isQRDetect)
        {
            string fileName = Path.GetFileNameWithoutExtension(imageFilePath);
            var totalTime = timeLine.GetFrame + timeLine.YoloProcess + timeLine.RotationProcess + timeLine.EnhancersProcess + timeLine.QRDetectProcess + timeLine.ImageCroptProcess + timeLine.OCRDetectProcess;
            var record = new
            {
                imageFile = fileName,
                isQRDetect = isQRDetect ? 1 : 0,
                totalTime = totalTime,
                getFrame = timeLine.GetFrame,
                getFramePercent = GetPercentTimeLine(timeLine.GetFrame, totalTime),
                yoloProcess = timeLine.YoloProcess,
                yoloProcessPercent = GetPercentTimeLine(timeLine.YoloProcess, totalTime),
                rotationProcess = timeLine.RotationProcess,
                rotationProcessPercent = GetPercentTimeLine(timeLine.RotationProcess, totalTime),
                EnhancersProcess = timeLine.EnhancersProcess,
                EnhancersProcessPercent = GetPercentTimeLine(timeLine.EnhancersProcess, totalTime),
                QRDetectProcess = timeLine.QRDetectProcess,
                QRDetectProcessPercent = GetPercentTimeLine(timeLine.QRDetectProcess, totalTime),
                ImageCroptProcess = timeLine.ImageCroptProcess,
                ImageCroptProcessPercent = GetPercentTimeLine(timeLine.ImageCroptProcess, totalTime),
                OCRDetectProcess = timeLine.OCRDetectProcess,
                OCRDetectProcessPercent = GetPercentTimeLine(timeLine.OCRDetectProcess, totalTime),
            };

            // Đọc file json cũ nếu có
            object[] records;
            if (File.Exists(jsonPath))
            {
                var oldJson = File.ReadAllText(jsonPath);
                try
                {
                    records = JsonSerializer.Deserialize<object[]>(oldJson) ?? new object[0];
                }
                catch
                {
                    records = new object[0];
                }
            }
            else
            {
                records = new object[0];
            }

            // Thêm record mới
            records = records.Concat(new[] { record }).ToArray();
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(records, options));
        }

        private static double GetPercentTimeLine(double time, double totalTime)
        {
            return (time / totalTime) * 100;
        }
    }
}
