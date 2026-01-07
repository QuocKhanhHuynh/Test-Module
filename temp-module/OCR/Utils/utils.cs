using System;
using System.Diagnostics;

namespace demo_ocr_label
{
    public static class utils
    {
        // Config mặc định - không còn đọc từ file JSON nữa
        public static Config fileConfig = new Config();
        
        // Static constructor - khởi tạo config mặc định khi class được load lần đầu
        static utils()
        {
            Debug.WriteLine("Khởi tạo OCR config mặc định (không dùng file JSON)");
        }
        
        // Giữ lại method này để tương thích với code cũ, nhưng không làm gì
        [Obsolete("Không còn dùng file JSON nữa, config mặc định được khởi tạo tự động")]
        public static void LoadConfigFile(string configFileName)
        {
            // Không làm gì - config đã được khởi tạo mặc định
            Debug.WriteLine("LoadConfigFile được gọi nhưng không còn dùng file JSON nữa");
        }
        
        // Hàm tính Levenshtein similarity, trả về giá trị từ 0 đến 1.0
        public static double LevenshteinSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int len1 = s1.Length;
            int len2 = s2.Length;
            int[,] dp = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++) dp[i, 0] = i;
            for (int j = 0; j <= len2; j++) dp[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }

            int maxLen = Math.Max(len1, len2);
            return 1.0 - (double)dp[len1, len2] / maxLen;
        }
    }
}

