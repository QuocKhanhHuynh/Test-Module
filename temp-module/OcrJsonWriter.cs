using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using temp_module.OCR.Utils;
using temp_module.Models;
using demo_ocr_label;

namespace temp_module
{
    public static class OcrJsonWriter
    {
        public static void WriteOcrResult(
            string jsonPath,
            string qrFailPath,
            string txtDir,
            string imageFilePath,
            double totalTime1, double totalTime2, double totalTime3,
            DetectInfo result1, DetectInfo result2, DetectInfo result3)
        {
            string fileName = Path.GetFileNameWithoutExtension(imageFilePath);
            string txtPath = Path.Combine(txtDir, fileName + ".txt");
            if (!File.Exists(txtPath)) return;
            var gtLines = File.ReadAllLines(txtPath).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();
            string gtProductCode = gtLines.Length > 0 ? gtLines[0] : "";
            string gtProductTotal = gtLines.Length > 1 ? gtLines[1] : "";
            string gtProductVariant = gtLines.Length > 2 ? gtLines[2] : "";
            string gtSize = gtLines.Length > 3 ? gtLines[3] : "";
            string gtColor = gtLines.Length > 4 ? gtLines[4] : "";

            double Fuzzy(string a, string b) => utils.LevenshteinSimilarity((a ?? "").ToLowerInvariant(), (b ?? "").ToLowerInvariant()) * 100.0;

            // Helper: gom các thuộc tính có giá trị của DetectInfo vào mảng (chỉ dùng cho variant, size, color)
            string[] GetAllFields(DetectInfo info)
            {
                return new string[] { info?.ProductCode, info?.Size, info?.Color }
                    .Where(s => !string.IsNullOrEmpty(s)).ToArray();
            }

            // Helper: fuzzy so sánh ground truth với mảng, lấy giá trị fuzzy cao nhất
            string BestMatch(string[] arr, string gt, out double maxFuzzy)
            {
                maxFuzzy = 0;
                string best = "";
                foreach (var s in arr)
                {
                    double f = Fuzzy(s, gt);
                    if (f > maxFuzzy)
                    {
                        maxFuzzy = f;
                        best = s;
                    }
                }
                return best;
            }

            // Chuẩn bị mảng cho từng task
            var arr1 = GetAllFields(result1);
            var arr2 = GetAllFields(result2);
            var arr3 = GetAllFields(result3);

            // Fuzzy cho từng trường
            double fuzzyQr1, fuzzyQr2, fuzzyQr3;
            double fuzzyTotal1, fuzzyTotal2, fuzzyTotal3;
            double fuzzyVariant1, fuzzyVariant2, fuzzyVariant3;
            double fuzzySize1, fuzzySize2, fuzzySize3;
            double fuzzyColor1, fuzzyColor2, fuzzyColor3;

            // Lấy trực tiếp QR và ProductTotal
            string qr1Val = result1?.QRCode ?? "";
            string qr2Val = result2?.QRCode ?? "";
            string qr3Val = result3?.QRCode ?? "";
            fuzzyQr1 = Fuzzy(qr1Val, gtProductCode);
            fuzzyQr2 = Fuzzy(qr2Val, gtProductCode);
            fuzzyQr3 = Fuzzy(qr3Val, gtProductCode);

            string total1Val = result1?.ProductTotal ?? "";
            string total2Val = result2?.ProductTotal ?? "";
            string total3Val = result3?.ProductTotal ?? "";
            fuzzyTotal1 = Fuzzy(total1Val, gtProductTotal);
            fuzzyTotal2 = Fuzzy(total2Val, gtProductTotal);
            fuzzyTotal3 = Fuzzy(total3Val, gtProductTotal);

            var record = new
            {
                imageFile = fileName,
                totalTime = new { task1 = totalTime1, task2 = totalTime2, task3 = totalTime3 },
                qrDetected = new {
                    task1 = string.IsNullOrEmpty(result1?.QRCode) == false ? 1 : 0,
                    task2 = string.IsNullOrEmpty(result2?.QRCode) == false ? 1 : 0,
                    task3 = string.IsNullOrEmpty(result3?.QRCode) == false ? 1 : 0
                },
                qrValue = new {
                    groundTruth = gtProductCode,
                    task1 = qr1Val,
                    fuzzy1 = fuzzyQr1,
                    task2 = qr2Val,
                    fuzzy2 = fuzzyQr2,
                    task3 = qr3Val,
                    fuzzy3 = fuzzyQr3
                },
                productTotal = new {
                    groundTruth = gtProductTotal,
                    task1 = total1Val,
                    fuzzy1 = fuzzyTotal1,
                    task2 = total2Val,
                    fuzzy2 = fuzzyTotal2,
                    task3 = total3Val,
                    fuzzy3 = fuzzyTotal3
                },
                productVariant = new {
                    groundTruth = gtProductVariant,
                    task1 = BestMatch(arr1, gtProductVariant, out fuzzyVariant1),
                    fuzzy1 = fuzzyVariant1,
                    task2 = BestMatch(arr2, gtProductVariant, out fuzzyVariant2),
                    fuzzy2 = fuzzyVariant2,
                    task3 = BestMatch(arr3, gtProductVariant, out fuzzyVariant3),
                    fuzzy3 = fuzzyVariant3
                },
                size = new {
                    groundTruth = gtSize,
                    task1 = BestMatch(arr1, gtSize, out fuzzySize1),
                    fuzzy1 = fuzzySize1,
                    task2 = BestMatch(arr2, gtSize, out fuzzySize2),
                    fuzzy2 = fuzzySize2,
                    task3 = BestMatch(arr3, gtSize, out fuzzySize3),
                    fuzzy3 = fuzzySize3
                },
                color = new {
                    groundTruth = gtColor,
                    task1 = BestMatch(arr1, gtColor, out fuzzyColor1),
                    fuzzy1 = fuzzyColor1,
                    task2 = BestMatch(arr2, gtColor, out fuzzyColor2),
                    fuzzy2 = fuzzyColor2,
                    task3 = BestMatch(arr3, gtColor, out fuzzyColor3),
                    fuzzy3 = fuzzyColor3
                }
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

            // Nếu cả 3 QR đều rỗng thì append tên file ảnh vào file txt (txtPath)
            bool qr1 = string.IsNullOrEmpty(result1?.QRCode);
            bool qr2 = string.IsNullOrEmpty(result2?.QRCode);
            bool qr3 = string.IsNullOrEmpty(result3?.QRCode);
            if (qr1 && qr2 && qr3)
            {
                try
                {
                    File.AppendAllText(qrFailPath, fileName + System.Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
