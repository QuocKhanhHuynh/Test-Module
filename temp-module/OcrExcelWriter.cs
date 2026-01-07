using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using temp_module.OCR.Utils;
using temp_module.Models;
using demo_ocr_label;

namespace temp_module
{
    public static class OcrExcelWriter
    {
        public static void WriteOcrResult(
            string excelPath,
            string txtDir,
            string imageFilePath,
            double totalTime1, double totalTime2, double totalTime3,
            DetectInfo result1, DetectInfo result2, DetectInfo result3)
        {
            // Lấy tên file không có phần mở rộng
            string fileName = Path.GetFileNameWithoutExtension(imageFilePath);
            string txtPath = Path.Combine(txtDir, fileName + ".txt");
            if (!File.Exists(txtPath)) return;
            var gtLines = File.ReadAllLines(txtPath).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();
            string gtProductCode = gtLines.Length > 0 ? gtLines[0] : "";
            string gtProductTotal = gtLines.Length > 1 ? gtLines[1] : "";
            string gtProductVariant = gtLines.Length > 2 ? gtLines[2] : "";
            string gtSize = gtLines.Length > 3 ? gtLines[3] : "";
            string gtColor = gtLines.Length > 4 ? gtLines[4] : "";

            // Hàm tính % fuzzy
            double Fuzzy(string a, string b) => utils.LevenshteinSimilarity(a ?? "", b ?? "") * 100.0;

            // QR detected
            bool qr1 = !string.IsNullOrEmpty(result1?.QRCode);
            bool qr2 = !string.IsNullOrEmpty(result2?.QRCode);
            bool qr3 = !string.IsNullOrEmpty(result3?.QRCode);

            // Tạo hoặc mở file Excel
            var fileExists = File.Exists(excelPath);
            var wb = fileExists ? new XLWorkbook(excelPath) : new XLWorkbook();
            var ws = fileExists ? wb.Worksheets.First() : wb.Worksheets.Add("OCR Results");

            // Nếu file mới, tạo header
            if (!fileExists && ws.LastRowUsed() == null)
            {
                ws.Cell(1, 1).Value = "Tổng thời gian";
                ws.Range(1, 1, 1, 3).Merge();
                ws.Cell(1, 4).Value = "Nhận diện được QR không";
                ws.Range(1, 4, 1, 6).Merge();
                ws.Cell(1, 7).Value = "Giá trị QR";
                ws.Range(1, 7, 1, 13).Merge();
                ws.Cell(1, 14).Value = "Giá trị tổng số lượng";
                ws.Range(1, 14, 1, 20).Merge();
                ws.Cell(1, 21).Value = "Giá trị kiểu áo";
                ws.Range(1, 21, 1, 27).Merge();
                ws.Cell(1, 28).Value = "Giá trị size áo";
                ws.Range(1, 28, 1, 34).Merge();
                ws.Cell(1, 35).Value = "Giá trị màu áo";
                ws.Range(1, 35, 1, 41).Merge();
                ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(1).Height = 30;
                string[] header2 = {
    "task 1", "task 2", "task 3",
    "task 1", "task 2", "task 3",
    "task 1", "% chính xác task 1", "task 2", "% chính xác task 2", "task 3", "% chính xác task 3", "chuẩn",
    "task 1", "% chính xác task 1", "task 2", "% chính xác task 2", "task 3", "% chính xác task 3", "chuẩn",
    "task 1", "% chính xác task 1", "task 2", "% chính xác task 2", "task 3", "% chính xác task 3", "chuẩn",
    "task 1", "% chính xác task 1", "task 2", "% chính xác task 2", "task 3", "% chính xác task 3", "chuẩn",
    "task 1", "% chính xác task 1", "task 2", "% chính xác task 2", "task 3", "% chính xác task 3", "chuẩn"
};
                for (int i = 0; i < header2.Length; i++)
                {
                    ws.Cell(2, i + 1).Value = header2[i];
                }
            }

            // Tìm dòng tiếp theo
            int nextRow = ws.LastRowUsed()?.RowNumber() + 1 ?? 3;
            ws.Cell(nextRow, 1).Value = totalTime1;
            ws.Cell(nextRow, 2).Value = totalTime2;
            ws.Cell(nextRow, 3).Value = totalTime3;
            ws.Cell(nextRow, 4).Value = qr1;
            ws.Cell(nextRow, 5).Value = qr2;
            ws.Cell(nextRow, 6).Value = qr3;
            ws.Cell(nextRow, 7).Value = result1?.QRCode ?? "";
            ws.Cell(nextRow, 8).Value = Fuzzy(result1?.QRCode, gtProductCode);
            ws.Cell(nextRow, 9).Value = result2?.QRCode ?? "";
            ws.Cell(nextRow, 10).Value = Fuzzy(result2?.QRCode, gtProductCode);
            ws.Cell(nextRow, 11).Value = result3?.QRCode ?? "";
            ws.Cell(nextRow, 12).Value = Fuzzy(result3?.QRCode, gtProductCode);
            ws.Cell(nextRow, 13).Value = gtProductCode;
            ws.Cell(nextRow, 14).Value = result1?.ProductTotal ?? "";
            ws.Cell(nextRow, 15).Value = Fuzzy(result1?.ProductTotal, gtProductTotal);
            ws.Cell(nextRow, 16).Value = result2?.ProductTotal ?? "";
            ws.Cell(nextRow, 17).Value = Fuzzy(result2?.ProductTotal, gtProductTotal);
            ws.Cell(nextRow, 18).Value = result3?.ProductTotal ?? "";
            ws.Cell(nextRow, 19).Value = Fuzzy(result3?.ProductTotal, gtProductTotal);
            ws.Cell(nextRow, 20).Value = gtProductTotal;
            ws.Cell(nextRow, 21).Value = result1?.ProductCode ?? "";
            ws.Cell(nextRow, 22).Value = Fuzzy(result1?.ProductCode, gtProductVariant);
            ws.Cell(nextRow, 23).Value = result2?.ProductCode ?? "";
            ws.Cell(nextRow, 24).Value = Fuzzy(result2?.ProductCode, gtProductVariant);
            ws.Cell(nextRow, 25).Value = result3?.ProductCode ?? "";
            ws.Cell(nextRow, 26).Value = Fuzzy(result3?.ProductCode, gtProductVariant);
            ws.Cell(nextRow, 27).Value = gtProductVariant;
            ws.Cell(nextRow, 28).Value = result1?.Size ?? "";
            ws.Cell(nextRow, 29).Value = Fuzzy(result1?.Size, gtSize);
            ws.Cell(nextRow, 30).Value = result2?.Size ?? "";
            ws.Cell(nextRow, 31).Value = Fuzzy(result2?.Size, gtSize);
            ws.Cell(nextRow, 32).Value = result3?.Size ?? "";
            ws.Cell(nextRow, 33).Value = Fuzzy(result3?.Size, gtSize);
            ws.Cell(nextRow, 34).Value = gtSize;
            ws.Cell(nextRow, 35).Value = result1?.Color ?? "";
            ws.Cell(nextRow, 36).Value = Fuzzy(result1?.Color, gtColor);
            ws.Cell(nextRow, 37).Value = result2?.Color ?? "";
            ws.Cell(nextRow, 38).Value = Fuzzy(result2?.Color, gtColor);
            ws.Cell(nextRow, 39).Value = result3?.Color ?? "";
            ws.Cell(nextRow, 40).Value = Fuzzy(result3?.Color, gtColor);
            ws.Cell(nextRow, 41).Value = gtColor;

            wb.SaveAs(excelPath);
        }
    }
}
