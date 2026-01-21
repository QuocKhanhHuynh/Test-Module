using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using demo_ocr_label; // Để dùng utils.LevenshteinSimilarity

class OcrResultFuzzyEnhancer
{
    static double Fuzzy(string a, string b)
    {
        return utils.LevenshteinSimilarity((a ?? "").ToLowerInvariant(), (b ?? "").ToLowerInvariant()) * 100.0;
    }

    static void Main(string[] args)
    {
        string inputPath = "ocr_result_t.json";
        string outputPath = "ocr_result_t_fuzzy.json";
        if (args.Length > 0) inputPath = args[0];
        if (args.Length > 1) outputPath = args[1];

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"File not found: {inputPath}");
            return;
        }

        var json = File.ReadAllText(inputPath);
        var arr = JsonNode.Parse(json)?.AsArray();
        if (arr == null)
        {
            Console.WriteLine("Invalid JSON array");
            return;
        }

        var enhancedList = new List<JsonObject>();
        foreach (var item in arr)
        {
            if (item is not JsonObject obj) continue;
            // Lấy ground truth nếu có
            string gtQr = null, gtProduct = null, gtSize = null, gtColor = null;
            if (obj["groundTruth"] is JsonObject gt)
            {
                gtQr = gt["qrCodeValue"]?.ToString();
                gtProduct = gt["productCodeValue"]?.ToString();
                gtSize = gt["sizeValue"]?.ToString();
                gtColor = gt["colorValue"]?.ToString();
            }
            // Nếu không có groundTruth, thử lấy từ frameT
            if (gtQr == null && obj["frameT"] is JsonObject f0)
            {
                gtQr = f0["qrCodeValue"]?.ToString();
                gtProduct = f0["productCodeValue"]?.ToString();
                gtSize = f0["sizeValue"]?.ToString();
                gtColor = f0["colorValue"]?.ToString();
            }

            var newObj = new JsonObject(obj); // copy các trường gốc
            foreach (var frameKey in new[] { "frameT", "frameT1", "frameT2" })
            {
                if (obj[frameKey] is not JsonObject frame) continue;
                var fuzzyObj = new JsonObject();
                fuzzyObj["qrCodeValueFuzzy"] = Fuzzy(frame["qrCodeValue"]?.ToString(), gtQr);
                fuzzyObj["productCodeValueFuzzy"] = Fuzzy(frame["productCodeValue"]?.ToString(), gtProduct);
                fuzzyObj["sizeValueFuzzy"] = Fuzzy(frame["sizeValue"]?.ToString(), gtSize);
                fuzzyObj["colorValueFuzzy"] = Fuzzy(frame["colorValue"]?.ToString(), gtColor);
                // Gắn vào frame
                var newFrame = new JsonObject(frame);
                foreach (var kv in fuzzyObj) newFrame[kv.Key] = kv.Value;
                newObj[frameKey] = newFrame;
            }
            enhancedList.Add(newObj);
        }
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(enhancedList, options));
        Console.WriteLine($"Done. Output: {outputPath}");
    }
}
