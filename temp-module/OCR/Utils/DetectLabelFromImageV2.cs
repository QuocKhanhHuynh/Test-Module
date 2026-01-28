using demo_ocr_label;
using temp_module.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Text;
using Sdcb.RotationDetector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using temp_module.OCR.Utils.NewOCR;

using CvSharp = OpenCvSharp;
using Drawing = System.Drawing;

namespace temp_module.OCR.Utils
{
    /// <summary>
    /// Version 2: Sử dụng YOLO11 Detector để phát hiện QR và Label
    /// </summary>
    public static class DetectLabelFromImageV2
    {
        /// <summary>
        /// Detect label sử dụng YOLO11 detector
        /// </summary>
        /// <param name="frame">Mat frame từ camera</param>
        /// <param name="yoloDetector">YOLO11 detector instance (được tạo từ Form1)</param>
        /// <param name="ocr">PaddleOCR engine</param>
        /// <param name="currentThreshold">Threshold hiện tại (không dùng trong version này)</param>
        /// <param name="cameraBox">PictureBox để hiển thị camera</param>
        /// <param name="picPreprocessed">PictureBox để hiển thị preprocessed image</param>
        /// <returns>DetectInfo chứa thông tin QR và label</returns>
        /// 

        private static int counter = 0;
        private static bool isSaving = false;
        private static Stopwatch swEstinate = Stopwatch.StartNew();
        private static Stopwatch sw = Stopwatch.StartNew();
        public static ConcurrentBag<double> SuccessTimes = new ConcurrentBag<double>();
        public static ConcurrentBag<double> CannotExtractOCRTimes = new ConcurrentBag<double>();
        public static ConcurrentBag<string> CannotExtractOCRDetails = new ConcurrentBag<string>();
        public static ConcurrentBag<double> CannotExtractQRTimes = new ConcurrentBag<double>();
        public static ConcurrentBag<string> CannotExtractQRDetails = new ConcurrentBag<string>();

        /// <summary>
        /// Clear all statistics tracking data before starting a new batch test
        /// </summary>
        public static void ClearStatistics()
        {
            SuccessTimes = new ConcurrentBag<double>();
            CannotExtractOCRTimes = new ConcurrentBag<double>();
            CannotExtractOCRDetails = new ConcurrentBag<string>();
            CannotExtractQRTimes = new ConcurrentBag<double>();
            CannotExtractQRDetails = new ConcurrentBag<string>();
        }

        public static DetectInfo DetectLabel(
            int workSessionId,
            Mat frame,
            Yolo11SegOpenVINO yoloDetector,
            PaddleOCROpenVINO ocr,
             PaddleRotationDetector rotationDetector,
             WeChatQRCode weChatQRCode,
            int currentThreshold,
            PictureBox cameraBox,
            PictureBox processImage,
            bool isDebugOcr,
            ref IEnumerable<OpenCvSharp.Point> points,
           ref Point2f[] qrPoints,
           ref string qrText,
           Action<bool> ActivateFrame1And2Processing,
            string? fileName = null
            )
        {
            var result = new DetectInfo();
            try
            {
                sw.Restart();
                Mat originMat = null;
                Mat croptYoloMat = null;
                Mat rotationMat = null;
                Mat preProcessImageMat = null;
                Mat croptMergeMat = null;

               
                if (frame == null || frame.Empty())
                {
                    Debug.WriteLine("[⚠] Frame is null or empty");
                    return null;
                }

                /*Mat compressed = new Mat();

                var encodeParams = new[]
                {
                    new ImageEncodingParam(ImwriteFlags.JpegQuality, 100)
                };

                // Encode (nén)
                Cv2.ImEncode(".jpg", frame, out byte[] jpegData, encodeParams);

                // Decode lại thành Mat
                compressed = Cv2.ImDecode(jpegData, ImreadModes.Color);
                frame.Dispose();
                frame = compressed;*/ ///*****************************************************************************************************************************************************************
                originMat = frame.Clone();

                

                // ============================================
                // 1. YOLO DETECTION - Detect trực tiếp trên frame (Mat)
                // ============================================
                var detections = yoloDetector.Detect(frame);

              
                
                // ============================================
                // 2. VẼ BOUNDING BOXES LÊN FRAME
                // ============================================
                using var displayFrame = frame.Clone();
                

                //IEnumerable<OpenCvSharp.Point> points = new List<OpenCvSharp.Point>();
                foreach (var detection in detections)
                {
                    var bbox = detection.BoundingBox;

                    // Vẽ bounding box
                    Cv2.Rectangle(displayFrame, bbox, Scalar.Yellow, 2);

                    // Vẽ label text
                    string label = $"{detection.ClassName}: {detection.Confidence:P0}";
                    Cv2.PutText(displayFrame, label,
                        new OpenCvSharp.Point(bbox.X, bbox.Y - 5),
                        HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);

                    // Tô màu vùng contour (nếu có)
                    
                    if (detection.Contours != null && detection.Contours.Count > 0)
                    {
                        foreach (var contour in detection.Contours)
                        {
                            points = contour.Select(p => new OpenCvSharp.Point(p.X, p.Y)).ToArray();
                            Cv2.FillPoly(displayFrame, new[] { points }, new Scalar(0, 255, 255, 100)); // Màu vàng nhạt, alpha 100
                        }
                    }

                }


                if (points != null)
                {
                    ActivateFrame1And2Processing.Invoke(false);
                }
                else
                {
                    ActivateFrame1And2Processing.Invoke(true);
                }


                var displayBmp = MatToBitmap(displayFrame);
                cameraBox.BeginInvoke(new Action(() =>
                {
                    var old = cameraBox.Image;
                    cameraBox.Image = displayBmp;
                    old?.Dispose();
                }));

                if (points != null)
                {
                    if (isSaving == false)
                    {
                        swEstinate.Restart();
                        isSaving = true;
                    }
                    
                    counter++;
                    

                    //var croptImage = RotationImage.ProcessRotationImage(frame, maskContours); 
                    var croptImage = ContourCropper.CropByContour(frame, points);
                    croptYoloMat = croptImage.Clone();

                    /*var test = MatToBitmap(croptImage);
                    processImage.BeginInvoke(new Action(() =>
                    {
                        var old = processImage.Image;
                        processImage.Image = test;
                        old?.Dispose();
                    }));

                    // OLD: Rotation detection moved to AFTER QR detection (more accurate)
                    // var rotation = RotationImage.CheckLabelRotation(croptImage, rotationDetector);
                    // croptImage = RotationImage.Rotate(croptImage, rotation);
                    
                    // NOTE: Rotation will be done AFTER QR detection using QR position*/

                    var croppedBmp = MatToBitmap(croptImage);
                    var grayStandard = ImageEnhancer.ConvertToGrayscale(croppedBmp);


                    


                    try
                    {
                        var enhanced = grayStandard;  // Start with original
                        //var enhanced = croppedBmp;

                       
                        // 1️⃣ Tăng sáng (nhà xưởng thường tối)
                        var brightened = ImageEnhancer.EnhanceDark(enhanced, clipLimit: 2.5);
                        if (enhanced != croppedBmp) enhanced.Dispose();
                        enhanced = brightened;
                        Debug.WriteLine($"[ENHANCEMENT] ✓ EnhanceDark completed");


                       


                        // 2️⃣ Làm sắc nét (cải thiện QR detection)
                        var sharpened = ImageEnhancer.SharpenBlurry(enhanced);
                        if (enhanced != croppedBmp) enhanced.Dispose();
                        enhanced = sharpened;
                        Debug.WriteLine($"[ENHANCEMENT] ✓ SharpenBlurry completed");

                        

                        // 3️⃣ Upscale nếu ảnh quá nhỏ
                        int minDim = Math.Min(enhanced.Width, enhanced.Height);
                        if (minDim < 400)
                        {
                            var upscaled = ImageEnhancer.UpscaleSmall(enhanced, 2.0);
                            if (enhanced != croppedBmp) enhanced.Dispose();
                            enhanced = upscaled;
                            Debug.WriteLine($"[ENHANCEMENT] ✓ UpscaleSmall completed: {enhanced.Width}x{enhanced.Height}");
                        }
                        else
                        {
                            Debug.WriteLine($"[ENHANCEMENT] ⊘ UpscaleSmall skipped (size ok)");
                        }


                        // Dispose original cropped bitmap nếu đã enhance
                        if (enhanced != grayStandard)
                        {
                          grayStandard.Dispose();
                          croppedBmp.Dispose();
                          croppedBmp = enhanced;  // Use enhanced version //********************************************************************************************************************
                          preProcessImageMat = enhanced.ToMat();
                        }




                        // if (enhanced != croppedBmp)
                        // {
                        //     croppedBmp.Dispose();
                        //     croppedBmp = enhanced;  // Use enhanced version //********************************************************************************************************************
                        //     preProcessImageMat = enhanced.ToMat();
                        // }


                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[⚠ IMAGE ENHANCEMENT ERROR] {ex.Message}");
                        Debug.WriteLine($"[ENHANCEMENT] ⚠️ Using original image (fallback)");
                        // Continue with original cropped bitmap
                    }


                    //processImage.BeginInvoke(new Action(() =>
                    //{
                    //    try
                    //    {
                    //        var old = processImage.Image;
                    //        processImage.Image = croppedBmp;
                    //        old?.Dispose();
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        Debug.WriteLine($"[⚠ DISPLAY PROCESS IMAGE ERROR] {ex.Message}");
                    //        // If display failed, dispose the clone to prevent memory leak
                    //        croppedBmp?.Dispose();
                    //    }
                    //}));


                    //processImage.BeginInvoke(new Action(() =>
                    //{
                    //    var old = processImage.Image;
                    //    processImage.Image = croppedBmp;
                    //    old?.Dispose();
                    //}));

                    //var (qrPoints, qrText) = LabelDetectorZXing.DetectQRCodeZXing(croppedBmp, zxingReader);
                    //var (qrPoints, qrText) = LabelDetectorWeChat.DetectQRCodeWeChat(croppedBmp, weChatQRCode);
                    if (qrPoints == null)
                    {
                        var (qrPointsLocal, qrTextLocal) = LabelDetectorWeChat.DetectQRCodeWeChat(croppedBmp, weChatQRCode);
                        if (qrPointsLocal != null)
                        {
                            qrPoints = qrPointsLocal;
                            qrText = qrTextLocal;

                            // ============================================
                            // NEW: QR-BASED ROTATION DETECTION
                            // ============================================
                            // Check if label is upside down using QR position
                            // Logic: QR should be on the RIGHT side (X > midpoint)
                            bool isUpsideDown = QRBasedRotationDetector.IsLabelUpsideDown(croppedBmp, qrPoints);
                            
                            if (isUpsideDown)
                            {
                                Debug.WriteLine("[QR-ROTATION] ⟲ Label is upside down - rotating 180°");
                                
                                // Rotate the cropped Mat
                                var rotatedMat = new Mat();
                                Cv2.Rotate(croptImage, rotatedMat, RotateFlags.Rotate180);
                                croptImage.Dispose();
                                croptImage = rotatedMat;
                                
                                // Rotate the enhanced Bitmap
                                croppedBmp.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipNone);
                                
                                // Rotate QR points to match rotated image
                                int imgWidth = croppedBmp.Width;
                                int imgHeight = croppedBmp.Height;
                                for (int i = 0; i < qrPoints.Length; i++)
                                {
                                    qrPoints[i] = new Point2f(
                                        imgWidth - qrPoints[i].X,
                                        imgHeight - qrPoints[i].Y
                                    );
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[QR-ROTATION] ✓ Label orientation is correct (0°)");
                            }
                            
                            rotationMat = croptImage.Clone(); // Save rotated version for debug

                            var test = MatToBitmap(rotationMat);
                            processImage.BeginInvoke(new Action(() =>
                            {
                                var old = processImage.Image;
                                processImage.Image = test;
                                old?.Dispose();
                            }));

                        }
                    }

                    if (qrPoints == null)
                    {
                        var timeProcess = sw.ElapsedMilliseconds;
                        CannotExtractQRTimes.Add(timeProcess);
                        CannotExtractQRDetails.Add(fileName);
                        //SaveImageWithStep(2, workSessionId.ToString(), 4, null, originMat, croptYoloMat, rotationMat, preProcessImageMat, null);
                        return result;
                    }

                    result.QRCode = qrText;

                    OpenCvSharp.Point[] qrBox = qrPoints
                        .Select(p => new OpenCvSharp.Point((int)Math.Round(p.X), (int)Math.Round(p.Y)))
                        .ToArray();

                    // Gọi hàm với kiểu dữ liệu đã đúng
                    var mergedCrop = CropComponent.CropAndMergeBottomLeftAndAboveQr(croppedBmp, qrBox);

                    var gray = ImageEnhancer.ConvertToGrayBgr(mergedCrop);
                    mergedCrop.Dispose();
                    mergedCrop = gray;//********************************************************************************************************************
                    croptMergeMat = gray.ToMat();

                    if (mergedCrop != null)
                    {
                        bool imageDisplayed = false;
                        
                        // Safety check: Ensure processImage is valid and handle is created
                        if (processImage != null && processImage.IsHandleCreated)
                        {
                            // CRITICAL: Clone bitmap before passing to UI thread
                            // to prevent "Object is currently in use" error (race condition)
                            var mergedCropClone = (Bitmap)mergedCrop.Clone();
                            
                            /*processImage.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    var old = processImage.Image;
                                    processImage.Image = mergedCropClone;
                                    old?.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[⚠ DISPLAY PROCESS IMAGE ERROR] {ex.Message}");
                                    // If display failed, dispose the clone to prevent memory leak
                                    mergedCropClone?.Dispose();
                                }
                            }));*/
                            imageDisplayed = true;
                        }
                        else
                        {
                            Debug.WriteLine("[⚠] processImage is null or handle not created - skipping display");
                        }
                        
                        var (ocrTexts, minScore, debugText) = ExtractTextsFromMergedCrop(ocr, mergedCrop, processImage);
                        if (ocrTexts == null || ocrTexts.Count == 0)
                        {
                            var timeOCRProcess = sw.ElapsedMilliseconds;
                            CannotExtractOCRTimes.Add(timeOCRProcess);
                            CannotExtractOCRDetails.Add(fileName);
                            //SaveImageWithStep(2, workSessionId.ToString(), 5, null, originMat, croptYoloMat, rotationMat, preProcessImageMat, croptMergeMat);
                            return result;
                        }
                        var timeProcess = sw.ElapsedMilliseconds;
                        SuccessTimes.Add(timeProcess);
                        // Always dispose mergedCrop after OCR processing
                        // (UI thread has its own clone if display succeeded)
                        mergedCrop.Dispose();

                        if (ocrTexts.Count > 0) result.ProductTotal = ocrTexts[0];
                        if (ocrTexts.Count > 1) result.ProductCode = ocrTexts[1];
                        if (ocrTexts.Count > 2) result.Size = ocrTexts[2];
                        if (ocrTexts.Count > 3) result.Color = ocrTexts[3];

                        if (result.ProductTotal == null || result.ProductCode == null || result.Size == null || result.Color == null)
                        {
                            //SaveImageWithStep(1, workSessionId.ToString(), 5, null, originMat, croptYoloMat, rotationMat, preProcessImageMat, croptMergeMat);
                            //return result;
                        }

                        //SaveImageWithStep(0, workSessionId.ToString(), null, result, originMat, croptYoloMat, rotationMat, preProcessImageMat, croptMergeMat);
                    }
                    else
                    {
                        //SaveImageWithStep(2, workSessionId.ToString(), 4, null, originMat, croptYoloMat, rotationMat, preProcessImageMat, null);
                        //return result;
                    }

                }

                


                originMat?.Dispose();
                croptYoloMat?.Dispose();
                rotationMat?.Dispose();
                preProcessImageMat?.Dispose();
                croptMergeMat?.Dispose();


                if (isSaving && isDebugOcr)
                {
                    var totalMs = sw.ElapsedMilliseconds;
                    var estinateMs = swEstinate.ElapsedMilliseconds;
                    if (totalMs >= 1000 && estinateMs <= 300000)
                    {
                        if (result.ProductTotal != null && result.ProductCode != null)
                        {
                            SaveImageTemp(0, workSessionId.ToString(), frame);
                        }
                        else
                        {
                            SaveImageTemp(1, workSessionId.ToString(), frame);
                        }
                        sw.Restart();
                    }
                }


                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[⚠ DETECT LABEL V2 ERROR] {ex.Message}");
                Debug.WriteLine($"[⚠ Stack Trace] {ex.StackTrace}");
                return result;
            }
        }


        public static (List<string> texts, float minScore, string DebugText) ExtractTextsFromMergedCrop(PaddleOCROpenVINO ocr, Bitmap mergedCrop, PictureBox pictureBox)
        {
            var texts = new List<string>();
            string DebugText = "";
            float minScore = 999;

            try
            {
                if (ocr == null || mergedCrop == null)
                    return (texts, -999, "[?] Input null");

                List<OCRResult> results;
                lock (ocr)
                {
                    // Convert Bitmap to Mat for PaddleOCROpenVINO
                    using var mat = mergedCrop.ToMat();

                    results = ocr.DetectText(mat);
                }

                if (results?.Count > 0)
                {
                    texts = results
                        .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                        .Select(r => r.Text.Trim())
                        .ToList();

                    foreach (var r in results)
                    {
                        if (r.Score < minScore)
                            minScore = r.Score;
                        DebugText += $"{r.Text?.Trim()} | Score: {r.Score * 100:F2}%\r\n";
                    }
                }

                return (texts, minScore, DebugText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[? OCR ONLY ERROR] {ex.Message}");
                return (texts, -999, DebugText);
            }
        }

        /// <summary>
        /// Convert Mat to Bitmap
        /// </summary>
        private static Bitmap MatToBitmap(Mat mat)
        {
            int w = mat.Width;
            int h = mat.Height;
            int channels = mat.Channels();

            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            var rect = new Rectangle(0, 0, w, h);
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);

            int stride = bmpData.Stride;
            int rowLength = w * channels;

            byte[] buffer = new byte[rowLength];

            for (int y = 0; y < h; y++)
            {
                IntPtr src = mat.Data + y * (int)mat.Step();
                System.Runtime.InteropServices.Marshal.Copy(src, buffer, 0, rowLength);

                IntPtr dst = bmpData.Scan0 + y * stride;
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dst, rowLength);
            }

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        /// <summary>
        /// Áp dụng Unsharp Mask để làm sắc nét ảnh
        /// </summary>
        /// <param name="src">Mat nguồn</param>
        /// <param name="amount">Cường độ sharpening (1.0 = 100%, 1.5 = 150%, khuyến nghị: 1.0-2.0)</param>
        /// <param name="radius">Bán kính Gaussian blur (pixels, khuyến nghị: 1-3)</param>
        /// <param name="threshold">Ngưỡng (0-255, 0 = không ngưỡng, khuyến nghị: 0-10)</param>
        /// <returns>Mat đã được sharpened (cần dispose sau khi dùng)</returns>
        private static Mat ApplyUnsharpMask(Mat src, double amount = 1.5, int radius = 2, int threshold = 0)
        {
            // 1. Tạo bản mờ của ảnh gốc (Gaussian Blur)
            var blurred = new Mat();
            int ksize = radius * 2 + 1; // Kernel size phải là số lẻ
            Cv2.GaussianBlur(src, blurred, new OpenCvSharp.Size(ksize, ksize), 0);

            // 2. Tính "mask" = original - blurred
            var mask = new Mat();
            Cv2.Subtract(src, blurred, mask);

            // 3. Nếu có threshold, chỉ sharpen vùng có độ tương phản cao
            if (threshold > 0)
            {
                var maskAbs = new Mat();
                Cv2.ConvertScaleAbs(mask, maskAbs);

                var thresholdMask = new Mat();
                Cv2.Threshold(maskAbs, thresholdMask, threshold, 255, ThresholdTypes.Binary);

                Cv2.BitwiseAnd(mask, mask, mask, thresholdMask);

                maskAbs.Dispose();
                thresholdMask.Dispose();
            }

            // 4. Nhân mask với amount
            var weightedMask = new Mat();
            Cv2.ConvertScaleAbs(mask, weightedMask, amount, 0);

            // 5. Cộng vào ảnh gốc: sharpened = original + (amount * mask)
            var sharpened = new Mat();
            Cv2.Add(src, weightedMask, sharpened);

            // Cleanup
            blurred.Dispose();
            mask.Dispose();
            weightedMask.Dispose();

            return sharpened;
        }

        private static void SaveImageWithStep(int type, string workSessionId, int? stepFail, DetectInfo? info, Mat? originMat, Mat? croptYoloMat, Mat? rotationMat, Mat? preProcessImageMat, Mat? croptMergeMat)
        {
            if ((type == 0 || type == 1) && originMat != null && croptYoloMat != null && rotationMat != null && preProcessImageMat != null && croptMergeMat != null)
            {
                SaveImageWithName(type, workSessionId, 1, originMat);
                SaveImageWithName(type, workSessionId, 2, croptYoloMat);
                SaveImageWithName(type, workSessionId, 3, rotationMat);
                SaveImageWithName(type, workSessionId, 4, preProcessImageMat);
                SaveImageWithName(type, workSessionId, 5, croptMergeMat);
                SaveTextWithName(workSessionId, info);
            }
            else
            {
                if (stepFail.HasValue)
                {
                    if (stepFail.Value == 1 && originMat != null)
                    {
                        SaveImageWithName(type, workSessionId, 1, originMat);
                    }
                    else if (stepFail.Value == 2 && originMat != null && croptYoloMat != null)
                    {
                        SaveImageWithName(type, workSessionId, 1, originMat);
                        SaveImageWithName(type, workSessionId, 2, croptYoloMat);
                    }
                    else if (stepFail.Value == 3 && originMat != null && croptYoloMat != null && rotationMat != null)
                    {
                        SaveImageWithName(type, workSessionId, 1, originMat);
                        SaveImageWithName(type, workSessionId, 2, croptYoloMat);
                        SaveImageWithName(type, workSessionId, 3, rotationMat);
                    }
                    else if (stepFail.Value == 4 && originMat != null && croptYoloMat != null && rotationMat != null && preProcessImageMat != null)
                    {
                        SaveImageWithName(type, workSessionId, 1, originMat);
                        SaveImageWithName(type, workSessionId, 2, croptYoloMat);
                        SaveImageWithName(type, workSessionId, 3, rotationMat);
                        SaveImageWithName(type, workSessionId, 4, preProcessImageMat);
                    }
                    else if (stepFail.Value == 5 && originMat != null && croptYoloMat != null && rotationMat != null && preProcessImageMat != null && croptMergeMat != null)
                    {
                        SaveImageWithName(type, workSessionId, 1, originMat);
                        SaveImageWithName(type, workSessionId, 2, croptYoloMat);
                        SaveImageWithName(type, workSessionId, 3, rotationMat);
                        SaveImageWithName(type, workSessionId, 4, preProcessImageMat);
                        SaveImageWithName(type, workSessionId, 5, croptMergeMat);
                    }
                }
            }
        }

        private static void SaveImageTemp(int type, string workSessionId, Mat mat)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Tạo đường dẫn thư mục (không bao gồm tên file)
            var folderPath = Path.Combine(baseDir, "OCR", type == 0 ? "Success" : "Failed", workSessionId);

            // Tạo tên file cụ thể
            var fileName = $"{DateTime.Now:HHmmss}.jpg";
            var fullPath = Path.Combine(folderPath, fileName);

            // Gọi hàm lưu
            SaveOcrImage(fullPath, mat);
        }







        private static void SaveTextWithName(string workSessionId, DetectInfo info)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Tạo đường dẫn thư mục (không bao gồm tên file)
            var folderPath = Path.Combine(baseDir, "OCR", "Success", workSessionId, counter.ToString());

            // Tạo tên file cụ thể
            var fileName = $"info_{DateTime.Now:HHmmss}.txt";
            var fullPath = Path.Combine(folderPath, fileName);

            // Gọi hàm lưu
            SaveOcrText(fullPath, info.ToString());
        }

        private static void SaveOcrText(string fullFilePath, string content)
        {
            try
            {
                var directory = Path.GetDirectoryName(fullFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Lưu nội dung văn bản với mã hóa UTF-8 để hỗ trợ tiếng Việt/ký tự đặc biệt
                File.WriteAllText(fullFilePath, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveOcrText] Failed: {ex.Message}");
            }
        }


        private static void SaveImageWithName(int type, string workSessionId, int step, Mat mat)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Tạo đường dẫn thư mục (không bao gồm tên file)
            var folderPath = Path.Combine(baseDir, "OCR", type == 0 ? "Success" : type == 1 ? "PostProcessImageFailure" : "ProcessImageFailure", workSessionId, counter.ToString());

            // Tạo tên file cụ thể
            var fileName = $"{ConvertStepToString(step)}_{DateTime.Now:HHmmss}.jpg";
            var fullPath = Path.Combine(folderPath, fileName);

            // Gọi hàm lưu
            SaveOcrImage(fullPath, mat);
        }

        private static string ConvertStepToString(int step)
        {
            return step switch
            {
                1 => "Original",
                2 => "Cropped_YOLO",
                3 => "Rotated",
                4 => "Preprocessed",
                5 => "Cropped_Merged",
                _ => "Unknown_Step"
            };
        }

        private static void SaveOcrImage(string fullFilePath, Mat mat)
        {
            try
            {
                // Lấy đường dẫn thư mục từ đường dẫn file toàn vẹn
                var directory = Path.GetDirectoryName(fullFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Thiết lập chất lượng JPG (50% để tiết kiệm bộ nhớ)
                var jpegParams = new ImageEncodingParam[] {
                    new ImageEncodingParam(ImwriteFlags.JpegQuality, 50)
                };

                Cv2.ImWrite(fullFilePath, mat, jpegParams);
            }
            catch (Exception ex)
            {
                // Debug.WriteLine giúp bạn thấy lỗi trong cửa sổ Output của Visual Studio
                Debug.WriteLine($"[SaveOcrImage] Failed: {ex.Message}");
            }
        }

    }
}
