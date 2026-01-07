using AForge.Video;
using AForge.Video.DirectShow;
using demo_ocr_label;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Text;
using PaddleOCRSharp;
using Sdcb.RotationDetector;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using temp_module.Models;
using temp_module.OCR.Utils;


namespace temp_module
{
    public partial class Form1 : Form
    {
        // Biến lưu đường dẫn ảnh import gần nhất và kết quả detect gần nhất
        private string? _lastImportedImagePath = null;
        private string? _lastDetectResult = null;
    
        private VideoCaptureDevice? videoSource;
        private bool isCameraRunning = false;

        // OCR Engines
        private PaddleOCREngine?[] _ocrEngines = new PaddleOCREngine?[3];
        private Yolo11SegOpenVINO?[] _yoloDetectors = new Yolo11SegOpenVINO?[3];
        private PaddleRotationDetector?[] _rotationDetectors = new PaddleRotationDetector?[3];
        private WeChatQRCode?[] _weChatQRCodes = new WeChatQRCode?[3];

        // Frame processing control để tránh giật lag
        private bool _isProcessingFrame = false;
        private DateTime _lastOcrProcessTime = DateTime.MinValue;
        private const int MIN_OCR_INTERVAL_MS = 500; // Chỉ xử lý OCR tối đa 2 lần/giây

        private Stopwatch sw1 = new Stopwatch();
        private Stopwatch sw2 = new Stopwatch();
        private Stopwatch sw3 = new Stopwatch();

        public Form1()
        {
            InitializeComponent();
            LoadAvailableCameras();
            InitializeOCREngines();
        }

        private void LoadAvailableCameras()
        {
            comboBoxCameras.Items.Clear();
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            
            foreach (FilterInfo device in videoDevices)
            {
                comboBoxCameras.Items.Add(device.Name);
                comboBoxCameras.Tag = device; // Lưu device info
            }

            if (comboBoxCameras.Items.Count > 0)
            {
                comboBoxCameras.SelectedIndex = 0;
                btnStartCamera.Enabled = true;
            }
            else
            {
                btnStartCamera.Enabled = false;
                MessageBox.Show("Không tìm thấy camera nào!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnStartCamera_Click(object sender, EventArgs e)
        {
            if (comboBoxCameras.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn camera!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                var selectedDevice = videoDevices.Cast<FilterInfo>()
                    .FirstOrDefault(d => d.Name == comboBoxCameras.SelectedItem.ToString());

                if (selectedDevice == null)
                {
                    MessageBox.Show("Camera đã chọn không khả dụng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                videoSource = new VideoCaptureDevice(selectedDevice.MonikerString);
                videoSource.NewFrame += VideoSource_NewFrame;
                videoSource.Start();

                isCameraRunning = true;
                btnStartCamera.Enabled = false;
                btnStopCamera.Enabled = true;
                comboBoxCameras.Enabled = false;
                btnImportImage.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi động camera: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStopCamera_Click(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
                videoSource.NewFrame -= VideoSource_NewFrame;
                videoSource = null;
            }

            isCameraRunning = false;
            btnStartCamera.Enabled = true;
            btnStopCamera.Enabled = false;
            comboBoxCameras.Enabled = true;
            btnImportImage.Enabled = true;

            picOriginal.Image = null;
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();

                // Hiển thị frame trên UI thread
                picOriginal.BeginInvoke(new Action(() =>
                {
                    var old = picOriginal.Image;
                    picOriginal.Image = bitmap;
                    old?.Dispose();
                }));

                // Gọi OCR processing (với throttling để tránh lag)
                if (!_isProcessingFrame && (DateTime.Now - _lastOcrProcessTime).TotalMilliseconds >= MIN_OCR_INTERVAL_MS)
                {
                    _isProcessingFrame = true;
                    _lastOcrProcessTime = DateTime.Now;

                    // Clone bitmap và xử lý trong background thread
                    Bitmap frameToProcess = (Bitmap)bitmap.Clone();
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            ProcessOCR(frameToProcess);
                        }
                        finally
                        {
                            frameToProcess?.Dispose();
                            _isProcessingFrame = false;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Camera Error] {ex.Message}");
            }
        }

        private void btnImportImage_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBoxResults.Clear();
                try
                {
                    using (var image = new Bitmap(openFileDialog.FileName))
                    {
                        picOriginal.Image = new Bitmap(image);
                        _lastImportedImagePath = openFileDialog.FileName; // Lưu lại đường dẫn ảnh
                        // Gọi OCR processing
                        ProcessOCR((Bitmap)picOriginal.Image);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi đọc file ảnh: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Khởi tạo các OCR engines
        /// </summary>
        private void InitializeOCREngines()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    _ocrEngines[i] = InitializeOCREngine();
                    _yoloDetectors[i] = InitializeYoloDetector();
                    _rotationDetectors[i] = InitializeRotationDetector();
                    _weChatQRCodes[i] = InitializeWeChatQRCode();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi tạo OCR engines: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Khởi tạo PaddleOCREngine
        /// </summary>
        private PaddleOCREngine? InitializeOCREngine()
        {
            try
            {
                if (utils.fileConfig?.modelParams == null)
                {
                    Debug.WriteLine("[OCR] Config not found, using default parameters");
                    return new PaddleOCREngine();
                }

                var modelParams = utils.fileConfig.modelParams;

                var ocrParams = new OCRParameter
                {
                    det = modelParams.det,
                    rec = modelParams.rec,
                    cls = modelParams.cls,
                    use_angle_cls = modelParams.use_angle_cls,
                    det_db_thresh = modelParams.det_db_thresh,
                    det_db_box_thresh = modelParams.det_db_box_thresh,
                    det_db_unclip_ratio = modelParams.det_db_unclip_ratio,
                    max_side_len = modelParams.det_limit_side_len,
                    det_db_score_mode = modelParams.det_db_score_mode,
                    cpu_math_library_num_threads = modelParams.cpu_math_library_num_threads,
                    enable_mkldnn = modelParams.enable_mkldnn
                };

                try
                {
                    var engine = new PaddleOCREngine(null, ocrParams);
                    Debug.WriteLine("[OCR] PaddleOCREngine initialized successfully");
                    return engine;
                }
                catch (DllNotFoundException)
                {
                    Debug.WriteLine("[OCR] PaddleOCR DLL not found, using default constructor");
                    return new PaddleOCREngine();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] Failed to initialize: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Khởi tạo YOLO Detector
        /// </summary>
        private Yolo11SegOpenVINO? InitializeYoloDetector()
        {
            try
            {
                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "yolo11n.xml");
                string[] classNames = new[] { "label" };

                if (!File.Exists(modelPath))
                {
                    Debug.WriteLine($"[YOLO] Model not found: {modelPath}");
                    return null;
                }

                var setting = new OpenVinoSetting
                {
                    NumThreads =4,
                    NumStreams = 2,
                    DeviceName = "CPU",
                };

                var detector = new Yolo11SegOpenVINO(
                    modelPath,
                    setting,
                    classNames,
                    confThreshold: 0.5f,
                    iouThreshold: 0.45f
                );

                Debug.WriteLine("[YOLO] Yolo11SegOpenVINO initialized successfully");
                return detector;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YOLO] Failed to initialize: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Khởi tạo Rotation Detector
        /// </summary>
        private PaddleRotationDetector? InitializeRotationDetector()
        {
            try
            {
                var detector = new PaddleRotationDetector(RotationDetectionModel.EmbeddedDefault);
                Debug.WriteLine("[Rotation] PaddleRotationDetector initialized successfully");
                return detector;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Rotation] Failed to initialize: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Khởi tạo WeChatQRCode
        /// </summary>
        private WeChatQRCode? InitializeWeChatQRCode()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string detectPrototxt = Path.Combine(baseDir, "models", "detect.prototxt");
                string detectCaffemodel = Path.Combine(baseDir, "models", "detect.caffemodel");
                string srPrototxt = Path.Combine(baseDir, "models", "sr.prototxt");
                string srCaffemodel = Path.Combine(baseDir, "models", "sr.caffemodel");

                if (!File.Exists(detectPrototxt) || !File.Exists(detectCaffemodel) ||
                    !File.Exists(srPrototxt) || !File.Exists(srCaffemodel))
                {
                    Debug.WriteLine("[WeChatQR] Model files not found, QR detection may not work");
                    return null;
                }

                var reader = WeChatQRCode.Create(detectPrototxt, detectCaffemodel, srPrototxt, srCaffemodel);
                Debug.WriteLine("[WeChatQR] WeChatQRCode initialized successfully");
                return reader;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WeChatQR] Failed to initialize: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Xử lý OCR trên ảnh
        /// </summary>
        private void ProcessOCR(Bitmap image)
        {
            if (image == null)
            {
                Debug.WriteLine("[ProcessOCR] Image is null");
                return;
            }


            // ...existing code...
            DetectInfo result1 = null, result2 = null, result3 = null;
            double totalTime1 = 0, totalTime2 = 0, totalTime3 = 0;
            var mat = BitmapConverter.ToMat(image);
            try
            {
                Mat compressed1 = new Mat();
                var encodeParams1 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 10) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData1, encodeParams1);
                compressed1 = Cv2.ImDecode(jpegData1, ImreadModes.Color);
                sw1.Restart();
                result1 = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed1.Clone(),
                    yoloDetector: _yoloDetectors[0],
                    ocr: _ocrEngines[0],
                    rotationDetector: _rotationDetectors[0],
                    weChatQRCode: _weChatQRCodes[0],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                    fileName: null
                );
                totalTime1 = sw1.Elapsed.TotalMilliseconds;
                this.BeginInvoke(new Action(() =>
                {
                    if (result1 != null)
                        DisplayOCRResults(result1, 1);
                    else
                        AppendTextBoxResults($"[Task 1] Không phát hiện được label hoặc QR code.\r\n");
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");
                this.BeginInvoke(new Action(() =>
                {
                    AppendTextBoxResults($"Lỗi xử lý OCR: {ex.Message}\r\n");
                }));
            }

            try
            {
                Mat compressed2 = new Mat();
                var encodeParams2 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 50) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData2, encodeParams2);
                compressed2 = Cv2.ImDecode(jpegData2, ImreadModes.Color);
                sw2.Restart();
                result2 = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed2.Clone(),
                    yoloDetector: _yoloDetectors[1],
                    ocr: _ocrEngines[1],
                    rotationDetector: _rotationDetectors[1],
                    weChatQRCode: _weChatQRCodes[1],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                    fileName: null
                );
                totalTime2 = sw2.Elapsed.TotalMilliseconds;
                this.BeginInvoke(new Action(() =>
                {
                    if (result2 != null)
                        DisplayOCRResults(result2, 2);
                    else
                        AppendTextBoxResults($"[Task 2] Không phát hiện được label hoặc QR code.\r\n");
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");
                this.BeginInvoke(new Action(() =>
                {
                    AppendTextBoxResults($"Lỗi xử lý OCR: {ex.Message}\r\n");
                }));
            }

            try
            {
                Mat compressed3 = new Mat();
                var encodeParams3 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 100) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData3, encodeParams3);
                compressed3 = Cv2.ImDecode(jpegData3, ImreadModes.Color);
                sw3.Restart();
                result3 = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed3.Clone(),
                    yoloDetector: _yoloDetectors[2],
                    ocr: _ocrEngines[2],
                    rotationDetector: _rotationDetectors[2],
                    weChatQRCode: _weChatQRCodes[2],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                    fileName: null
                );
                totalTime3 = sw3.Elapsed.TotalMilliseconds;
                this.BeginInvoke(new Action(() =>
                {
                    if (result3 != null)
                        DisplayOCRResults(result3, 3);
                    else
                        AppendTextBoxResults($"[Task 3] Không phát hiện được label hoặc QR code.\r\n");
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");
                this.BeginInvoke(new Action(() =>
                {
                    AppendTextBoxResults($"Lỗi xử lý OCR: {ex.Message}\r\n");
                }));
            }

            // Ghi kết quả vào Excel
            try
            {
                string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Result_Template.xlsx");
                string txtDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "txt");
                OcrExcelWriter.WriteOcrResult(
                    excelPath,
                    txtDir,
                    _lastImportedImagePath ?? "",
                    totalTime1, totalTime2, totalTime3,
                    result1, result2, result3
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Excel Write] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Hiển thị kết quả OCR
        /// </summary>
        private void DisplayOCRResults(DetectInfo result, int taskIndex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine($"=== KẾT QUẢ OCR [Task {taskIndex}] ===");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.QRCode))
            {
                sb.AppendLine($"QR Code: {result.QRCode}");
            }
            else
            {
                sb.AppendLine("QR Code: (không phát hiện)");
            }

            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.ProductTotal))
            {
                sb.AppendLine($"Tổng số lượng: {result.ProductTotal}");
            }
            else
            {
                sb.AppendLine("Tổng số lượng: (không phát hiện)");
            }

            if (!string.IsNullOrEmpty(result.ProductCode))
            {
                sb.AppendLine($"Mã sản phẩm: {result.ProductCode}");
            }
            else
            {
                sb.AppendLine("Mã sản phẩm: (không phát hiện)");
            }

            if (!string.IsNullOrEmpty(result.Size))
            {
                sb.AppendLine($"Size: {result.Size}");
            }
            else
            {
                sb.AppendLine("Size: (không phát hiện)");
            }

            if (!string.IsNullOrEmpty(result.Color))
            {
                sb.AppendLine($"Màu: {result.Color}");
            }
            else
            {
                sb.AppendLine("Màu: (không phát hiện)");
            }
            sb.AppendLine("\n\n\n\n\n\n");

            AppendTextBoxResults(sb.ToString());
            // Lưu lại kết quả detect gần nhất (cho nút lưu)
            _lastDetectResult = sb.ToString();
        }

        private void AppendTextBoxResults(string text)
        {
            if (textBoxResults.InvokeRequired)
            {
                textBoxResults.BeginInvoke(new Action(() =>
                {
                    textBoxResults.AppendText(text + "\r\n");
                }));
            }
            else
            {
                textBoxResults.AppendText(text + "\r\n");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopCamera();
            // Cleanup all engines
            for (int i = 0; i < 3; i++)
            {
                _yoloDetectors[i]?.Dispose();
                _yoloDetectors[i] = null;
                _ocrEngines[i]?.Dispose();
                _ocrEngines[i] = null;
                _weChatQRCodes[i]?.Dispose();
                _weChatQRCodes[i] = null;
                _rotationDetectors[i]?.Dispose();
                _rotationDetectors[i] = null;
            }
        }

        private void btnSaveResult_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastImportedImagePath) || _lastDetectResult == null)
            {
                MessageBox.Show("Chưa có kết quả để lưu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Trích xuất các giá trị từ DetectInfo (giá trị cuối cùng đã hiển thị)
            string qr = "";
            string total = "";
            string size = "";
            string code = "";
            string color = "";
            var lines = _lastDetectResult.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("QR Code:")) qr = line.Replace("QR Code:", "").Trim();
                if (line.StartsWith("Tổng số lượng:")) total = line.Replace("Tổng số lượng:", "").Trim();
                if (line.StartsWith("Size:")) size = line.Replace("Size:", "").Trim();
                if (line.StartsWith("Mã sản phẩm:")) code = line.Replace("Mã sản phẩm:", "").Trim();
                if (line.StartsWith("Màu:")) color = line.Replace("Màu:", "").Trim();
            }
            using (var reviewForm = new FormReviewSave(qr, total, size, code, color))
            {
                if (reviewForm.ShowDialog(this) == DialogResult.OK)
                {
                    string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDataTxt");
                    if (!Directory.Exists(saveDir))
                        Directory.CreateDirectory(saveDir);
                    string imageName = Path.GetFileNameWithoutExtension(_lastImportedImagePath);
                    string savePath = Path.Combine(saveDir, imageName + ".txt");
                    // Lưu từng trường, mỗi dòng một giá trị
                    File.WriteAllText(savePath, string.Join("\n", reviewForm.QRCode, reviewForm.ProductTotal, reviewForm.Size, reviewForm.ProductCode, reviewForm.Color));
                    MessageBox.Show($"Đã lưu kết quả vào: {savePath}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Đặt nút lưu cạnh nút import trên panelControls
            var btnSaveResult = new Button();
            btnSaveResult.Text = "Lưu thông tin";
            btnSaveResult.Width = btnImportImage.Width;
            btnSaveResult.Height = btnImportImage.Height;
            btnSaveResult.Top = btnImportImage.Top;
            btnSaveResult.Left = btnImportImage.Right + 10; // Cách phải 10px
            btnSaveResult.Click += btnSaveResult_Click;
            panelControls.Controls.Add(btnSaveResult);
        }
    }
}
