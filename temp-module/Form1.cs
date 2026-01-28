using AForge.Video;
using AForge.Video.DirectShow;
using demo_ocr_label;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Text;
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
using temp_module.OCR.Utils.NewOCR;


namespace temp_module
{
    public partial class Form1 : Form
    {
        // Biến lưu đường dẫn ảnh import gần nhất và kết quả detect gần nhất
        private string? _lastImportedImagePath = null;
        private string? _lastDetectResult = null;
    
        private VideoCaptureDevice? videoSource;
        private bool isCameraRunning = false;

        // Trạng thái bật/tắt lấy frame OCR
        private bool _isGetFrameOcrEnabled = false;

        // OCR Engines
        private PaddleOCROpenVINO?[] _ocrEngines = new PaddleOCROpenVINO?[3];
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

        private int numProcessinng = 0;
        private int countProcessed = 0;
        private Stopwatch stopwatch = new Stopwatch();
        private Stopwatch totalStopwatch = new Stopwatch();
         private Stopwatch stopwatchAllowGetFrame = new Stopwatch();
        private string currentOcrSessionDir = string.Empty;
        private DetectInfo[] results = new DetectInfo[3];


        private IEnumerable<OpenCvSharp.Point> yoloPoints = null;
        private string wechatQrTexts = null;
        private Point2f[] wechatQrPoints = null;
        private Bitmap frame1 = null;
        private Bitmap frame2 = null;
        private Bitmap frame3 = null;


        private bool flagProcess = true;


        public Form1()
        {
            InitializeComponent();
            LoadAvailableCameras();
            InitializeOCREngines();
            // Khởi tạo trạng thái nút Get Frame OCR
            btnGetFrameOcr.Text = "Get Frame OCR (OFF)";
            btnGetFrameOcr.BackColor = System.Drawing.Color.LightGray;
            _isGetFrameOcrEnabled = false;
        }
        // Sự kiện click cho nút Get Frame OCR
        private void btnGetFrameOcr_Click(object sender, EventArgs e)
        {
            _isGetFrameOcrEnabled = !_isGetFrameOcrEnabled;
            if (_isGetFrameOcrEnabled)
            {
                numProcessinng = 0;
                countProcessed++;
                stopwatchAllowGetFrame.Restart();
                results = new DetectInfo[3];
                /*for (int i = 0; i < 3; i++)
                {
                    results[i] = new DetectInfo();
                }*/
                textBoxResults.Clear();
                btnGetFrameOcr.Text = "Get Frame OCR (ON)";
                btnGetFrameOcr.BackColor = System.Drawing.Color.LightGreen;

                // Tạo thư mục lưu frame cho lần OCR này
                string imagesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                if (!Directory.Exists(imagesRoot))
                    Directory.CreateDirectory(imagesRoot);
                currentOcrSessionDir = Path.Combine(imagesRoot, countProcessed.ToString());
                if (!Directory.Exists(currentOcrSessionDir))
                    Directory.CreateDirectory(currentOcrSessionDir);
            }
            else
            {
                btnGetFrameOcr.Text = "Get Frame OCR (OFF)";
                btnGetFrameOcr.BackColor = System.Drawing.Color.LightGray;
            }
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

        /*private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
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
                

                // Chỉ lấy frame OCR nếu chế độ được bật
                if (_isGetFrameOcrEnabled)
                {
                    var timeAllowed = stopwatchAllowGetFrame.ElapsedMilliseconds;
                    if (timeAllowed < 2000)
                    {
                        return;
                    }
                    
                    
                    if (numProcessinng < 3)
                    {
                        if (numProcessinng == 0)
                        {
                            totalStopwatch.Restart();
                            numProcessinng++;
                            stopwatch.Restart();
                            Bitmap frameToProcess = null;
                            string imgPath = Path.Combine(currentOcrSessionDir, $"{countProcessed}_attempt-frame_1.jpg");
                            if (bitmap != null)
                            {
                                frameToProcess = (Bitmap)bitmap.Clone();
                                // Lưu frame ảnh đầu tiên
                                try
                                {
                                    
                                   
                                    frameToProcess.Save(imgPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[Save Frame1] {ex.Message}");
                                }
                            }
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                try
                                {
                                    results[0] = ProcessOCR1(frameToProcess);
                                    results[0].ImagePath = imgPath;
                                   
                                    CheckResult();
                                }
                                finally
                                {
                                    frameToProcess?.Dispose();
                                }
                            });
                            return;
                        }
                        if (numProcessinng == 1)
                        {
                            var batchTime = stopwatch.ElapsedMilliseconds;
                            if (batchTime >= 30)
                            {
                                numProcessinng++;
                                stopwatch.Restart();
                                Bitmap frameToProcess = null;
                                string imgPath = Path.Combine(currentOcrSessionDir, $"{countProcessed}_attempt-frame_2.jpg");
                                if (bitmap != null)
                                {
                                    frameToProcess = (Bitmap)bitmap.Clone();
                                    // Lưu frame ảnh thứ 2
                                    try
                                    {
                                       
                                       
                                        frameToProcess.Save(imgPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[Save Frame2] {ex.Message}");
                                    }
                                }
                                System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        results[1] = ProcessOCR2(frameToProcess);
                                        results[1].ImagePath = imgPath;
                                        CheckResult();
                                    }
                                    finally
                                    {
                                        frameToProcess?.Dispose();
                                    }
                                });
                                return;
                            }
                        }
                        if (numProcessinng == 2)
                        {
                            var batchTime = stopwatch.ElapsedMilliseconds;
                            if (batchTime >= 30)
                            {
                                numProcessinng++;
                                stopwatch.Restart();
                                _isGetFrameOcrEnabled = false;
                                // Đảm bảo cập nhật control trên UI thread
                                if (btnGetFrameOcr.InvokeRequired)
                                {
                                    btnGetFrameOcr.BeginInvoke(new Action(() =>
                                    {
                                        btnGetFrameOcr.Text = "Get Frame OCR (OFF)";
                                        btnGetFrameOcr.BackColor = System.Drawing.Color.LightGray;
                                    }));
                                }
                                else
                                {
                                    btnGetFrameOcr.Text = "Get Frame OCR (OFF)";
                                    btnGetFrameOcr.BackColor = System.Drawing.Color.LightGray;
                                }
                                Bitmap frameToProcess = null;
                                string imgPath = Path.Combine(currentOcrSessionDir, $"{countProcessed}_attempt-frame_3.jpg");
                                if (bitmap != null)
                                {
                                    frameToProcess = (Bitmap)bitmap.Clone();
                                    // Lưu frame ảnh thứ 3
                                    try
                                    {
                                        
                                       
                                        frameToProcess.Save(imgPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[Save Frame3] {ex.Message}");
                                    }
                                }
                                System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        results[2] = ProcessOCR3(frameToProcess);
                                        results[2].ImagePath = imgPath;
                                        
                                        CheckResult();
                                    }
                                    finally
                                    {
                                        frameToProcess?.Dispose();
                                    }
                                });
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Camera Error] {ex.Message}");
            }
        }*/

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Clone original frame from camera
                Bitmap originalBitmap = (Bitmap)eventArgs.Frame.Clone();

                // Clone separate bitmap for UI display to avoid race condition
                Bitmap displayBitmap = (Bitmap)originalBitmap.Clone();

                // Hiển thị frame trên UI thread
                picOriginal?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var old = picOriginal.Image;
                        picOriginal.Image = displayBitmap;
                        old?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Control đã bị dispose, cleanup bitmap
                        displayBitmap?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        displayBitmap?.Dispose();
                    }
                }));




                if (numProcessinng < 3)
                {

                    if (numProcessinng == 0)
                    {

                        numProcessinng++;
                        stopwatch.Restart();

                        if (originalBitmap != null)
                        {
                            try
                            {
                                frame1 = (Bitmap)originalBitmap.Clone();
                            }
                            catch (Exception ex)
                            {
                                numProcessinng = 0;
                                originalBitmap?.Dispose();
                                return;
                            }
                        }
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                results[0] = ProcessOCR1(frame1);

                                CheckResult();
                            }
                            finally
                            {
                                frame1?.Dispose();
                            }
                        });
                        originalBitmap?.Dispose();
                        return;
                    }
                    if (numProcessinng == 1)
                    {
                        var batchTime = stopwatch.ElapsedMilliseconds;
                        if (batchTime >= 30)
                        {
                            numProcessinng++;
                            stopwatch.Restart();
                            if (originalBitmap != null)
                            {
                                try
                                {
                                    frame2 = (Bitmap)originalBitmap.Clone();
                                }
                                catch (Exception ex)
                                {
                                    numProcessinng = 1; // Reset to retry
                                }
                            }

                        }
                    }
                    if (numProcessinng == 2)
                    {
                        var a = frame2;
                        var batchTime = stopwatch.ElapsedMilliseconds;
                        if (batchTime >= 30)
                        {
                            numProcessinng++;
                            stopwatch.Restart();

                            if (originalBitmap != null)
                            {
                                try
                                {
                                    frame3 = (Bitmap)originalBitmap.Clone();
                                    ActivateFrame1And2Processing(false);
                                }
                                catch (Exception ex)
                                {
                                    numProcessinng = 2; // Reset to retry
                                }
                            }
                        }
                    }

                }


                // Cleanup original bitmap if not used
                originalBitmap?.Dispose();
            }
            catch (Exception ex)
            {
            }
        }

        private void CheckResult()
        {
            if (results[0] != null && results[1] != null && results[2] != null)
            {
                DisplayOCRResults(results[0], 1);
                DisplayOCRResults(results[1], 2);
                DisplayOCRResults(results[2], 3);

                var totalTime = totalStopwatch.ElapsedMilliseconds;

                // Build JSON result object
                var sessionResult = new
                {
                    countProcessed = this.countProcessed,
                    totalTimeProcess = totalTime,
                    frameT = new {
                        imagePath = results[0]?.ImagePath,
                        processTimeFrame = results[0]?.TimeProcess,
                        qrDetected = results[0]?.QRCode != null,
                        qrCodeValue = results[0]?.QRCode,
                        totalValue = results[0]?.ProductTotal,
                        productCodeValue = results[0]?.ProductCode,
                        sizeValue = results[0]?.Size,
                        colorValue = results[0]?.Color,
                    },
                    frameT1 = new {
                        imagePath = results[1]?.ImagePath,
                        processTimeFrame = results[1]?.TimeProcess,
                        qrDetected = results[1]?.QRCode != null,
                        qrCodeValue = results[1]?.QRCode,
                        totalValue = results[1]?.ProductTotal,
                        productCodeValue = results[1]?.ProductCode,
                        sizeValue = results[1]?.Size,
                        colorValue = results[1]?.Color,
                    },
                    frameT2 = new {
                        imagePath = results[2]?.ImagePath,
                        processTimeFrame = results[2]?.TimeProcess,
                        qrDetected = results[2]?.QRCode != null,
                        qrCodeValue = results[2]?.QRCode,
                        totalValue = results[2]?.ProductTotal,
                        productCodeValue = results[2]?.ProductCode,
                        sizeValue = results[2]?.Size,
                        colorValue = results[2]?.Color,
                    }
                };

                try
                {
                    string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_result_t.json");
                    System.Collections.Generic.List<object> resultList = new System.Collections.Generic.List<object>();
                    if (System.IO.File.Exists(jsonPath))
                    {
                        try
                        {
                            string existingJson = System.IO.File.ReadAllText(jsonPath);
                            if (!string.IsNullOrWhiteSpace(existingJson))
                            {
                                var existingArray = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonArray>(existingJson);
                                if (existingArray != null)
                                {
                                    foreach (var item in existingArray)
                                    {
                                        resultList.Add(item);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Read JSON] {ex.Message}");
                        }
                    }
                    resultList.Add(sessionResult);
                    string json = System.Text.Json.JsonSerializer.Serialize(resultList, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                    });
                    System.IO.File.WriteAllText(jsonPath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Save JSON] {ex.Message}");
                }


                results = new DetectInfo[3];
                yoloPoints = null;
                frame1 = null;
                frame2 = null;
                frame3 = null;
                wechatQrTexts = null;
                wechatQrPoints = null;
                numProcessinng = 0;
                flagProcess = true;
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
        /// Khởi tạo PaddleOCROpenVINO engine
        /// </summary>
        private PaddleOCROpenVINO? InitializeOCREngine()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string detModel = Path.Combine(baseDir, "models", "det", "PP-OCRv5_mobile_det_infer.onnx");
                string recModel = Path.Combine(baseDir, "models", "rec", "rec.xml");
                string charDict = Path.Combine(baseDir, "fonts", "dictv5.txt");

                // Check if files exist
                if (!File.Exists(detModel))
                {
                    Debug.WriteLine($"[OCR] Detection model not found: {detModel}");
                    return null;
                }
                if (!File.Exists(recModel))
                {
                    Debug.WriteLine($"[OCR] Recognition model not found: {recModel}");
                    return null;
                }
                if (!File.Exists(charDict))
                {
                    Debug.WriteLine($"[OCR] Character dictionary not found: {charDict}");
                    return null;
                }

                var config = new OCRConfig
                {
                    DetThresh = 0.15f,
                    DetBoxThresh = 0.15f,
                    DetUnclipRatio = 2.0f,
                    RecBatchSize = 6,
                    NumThreads = 4,
                    NumStreams = 1,
                    Device = "CPU"
                };

                var engine = new PaddleOCROpenVINO(detModel, recModel, charDict, config);
                Debug.WriteLine("[OCR] PaddleOCROpenVINO initialized successfully");
                return engine;
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
            var a = _rotationDetectors;
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
                Mat compressed3 = new Mat();
                var encodeParams3 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 100) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData3, encodeParams3);
                compressed3 = Cv2.ImDecode(jpegData3, ImreadModes.Color);
                sw3.Restart();
                result3 = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed3.Clone(),
                    yoloDetector: _yoloDetectors[0],
                    ocr: _ocrEngines[0],
                    rotationDetector: _rotationDetectors[0],
                    weChatQRCode: _weChatQRCodes[0],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                     points: ref yoloPoints,
                    qrPoints: ref wechatQrPoints,
                    qrText: ref wechatQrTexts,
                    ActivateFrame1And2Processing: ActivateFrame1And2Processing,
                    fileName: null
                );
                totalTime3 = sw3.Elapsed.TotalMilliseconds;

                DisplayOCRResults(result3, 3);


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
            /*try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Result_Template.json");
                string txtDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "txt");
                OcrJsonWriter.WriteOcrResult(
                    jsonPath,
                    txtDir,
                    _lastImportedImagePath ?? "",
                    totalTime1, totalTime2, totalTime3,
                    result1, result2, result3
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Excel Write] Error: {ex.Message}");
            }*/
        }



        private DetectInfo ProcessOCR1(Bitmap image)
        {
            if (image == null)
            {
                Debug.WriteLine("[ProcessOCR] Image is null");
                return null;
            }

            DetectInfo result;
            var mat = BitmapConverter.ToMat(image);

            try
            {
                Mat compressed3 = new Mat();
                var encodeParams3 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 100) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData3, encodeParams3);
                compressed3 = Cv2.ImDecode(jpegData3, ImreadModes.Color);
                
                sw1.Restart();
                result = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed3.Clone(),
                    yoloDetector: _yoloDetectors[0],
                    ocr: _ocrEngines[0],
                    rotationDetector: _rotationDetectors[0],
                    weChatQRCode: _weChatQRCodes[0],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                     points: ref yoloPoints,
                    qrPoints: ref wechatQrPoints,
                    qrText: ref wechatQrTexts,
                    ActivateFrame1And2Processing: ActivateFrame1And2Processing,
                    fileName: null
                );
                result.TimeProcess = sw1.ElapsedMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                return null;
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");
                
            }
        }



        private DetectInfo ProcessOCR2(Bitmap image)
        {
            if (image == null)
            {
                Debug.WriteLine("[ProcessOCR] Image is null");
                return null;
            }

            DetectInfo result;
            var mat = BitmapConverter.ToMat(image);

            try
            {
                Mat compressed3 = new Mat();
                var encodeParams3 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 100) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData3, encodeParams3);
                compressed3 = Cv2.ImDecode(jpegData3, ImreadModes.Color);

                sw2.Restart();
                result = DetectLabelFromImageV2.DetectLabel(
                    workSessionId: 0,
                    frame: compressed3.Clone(),
                    yoloDetector: _yoloDetectors[1],
                    ocr: _ocrEngines[1],
                    rotationDetector: _rotationDetectors[1],
                    weChatQRCode: _weChatQRCodes[1],
                    currentThreshold: 180,
                    cameraBox: picOriginal,
                    processImage: picProcessed,
                    isDebugOcr: false,
                     points: ref yoloPoints,
                    qrPoints: ref wechatQrPoints,
                    qrText: ref wechatQrTexts,
                    ActivateFrame1And2Processing: ActivateFrame1And2Processing,
                    fileName: null
                );
                result.TimeProcess = sw2.ElapsedMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                return null;
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");

            }
        }

        private DetectInfo ProcessOCR3(Bitmap image)
        {
            if (image == null)
            {
                Debug.WriteLine("[ProcessOCR] Image is null");
                return null;
            }

            DetectInfo result;
            var mat = BitmapConverter.ToMat(image);

            try
            {
                Mat compressed3 = new Mat();
                var encodeParams3 = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 100) };
                Cv2.ImEncode(".jpg", mat, out byte[] jpegData3, encodeParams3);
                compressed3 = Cv2.ImDecode(jpegData3, ImreadModes.Color);

                sw3.Restart();
                result = DetectLabelFromImageV2.DetectLabel(
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
                     points: ref yoloPoints,
                    qrPoints: ref wechatQrPoints,
                    qrText: ref wechatQrTexts,
                    ActivateFrame1And2Processing: ActivateFrame1And2Processing,
                    fileName: null
                );
                result.TimeProcess = sw3.ElapsedMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                return null;
                Debug.WriteLine($"[ProcessOCR] Error: {ex.Message}");

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


        private void ActivateFrame1And2Processing(bool isReset)
        {
            if (isReset)
            {
                results = new DetectInfo[3];
                yoloPoints = null;
                frame1 = null;
                frame2 = null;
                frame3 = null;
                wechatQrTexts = null;
                wechatQrPoints = null;
                numProcessinng = 0;

                flagProcess = true;

                return;
            }
            if (frame2 == null || frame3 == null || yoloPoints == null)
            {
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    results[1] = ProcessOCR2(frame2);
                    CheckResult();
                }
                finally
                {
                    frame2?.Dispose();
                }
            });


            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    results[2] = ProcessOCR3(frame3);
                    CheckResult();
                }
                finally
                {
                    frame3?.Dispose();
                }
            });
        }

        private void btnAutoStatistic_Click(object sender, EventArgs e)
        {
            string imagesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            if (!Directory.Exists(imagesRoot))
            {
                MessageBox.Show($"Không tìm thấy thư mục Images: {imagesRoot}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var sessionDirs = Directory.GetDirectories(imagesRoot)
                .OrderBy(d =>
                {
                    var name = Path.GetFileName(d);
                    if (int.TryParse(name, out int n)) return n;
                    return int.MaxValue;
                })
                .ToList();
            if (sessionDirs.Count == 0)
            {
                MessageBox.Show("Không tìm thấy session nào trong Images!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (var sessionDir in sessionDirs)
            {
                try
                {
                    while (!flagProcess)
                    {
                        Console.WriteLine("Waiting for processing...");
                        System.Threading.Thread.Sleep(50);
                    }
                    flagProcess = false;
                    string sessionName = Path.GetFileName(sessionDir);
                    int sessionIndex = 0;
                    int.TryParse(sessionName, out sessionIndex);
                    countProcessed = sessionIndex;

                    string frame1Path = Path.Combine(sessionDir, $"{sessionName}_attempt-frame_1.jpg");
                    string frame2Path = Path.Combine(sessionDir, $"{sessionName}_attempt-frame_2.jpg");
                    string frame3Path = Path.Combine(sessionDir, $"{sessionName}_attempt-frame_3.jpg");
                    if (!File.Exists(frame1Path) || !File.Exists(frame2Path) || !File.Exists(frame3Path))
                        continue;

                    totalStopwatch.Restart();
                    
                    // Load frame1 from byte array (completely avoid file lock)
                    byte[] imageBytes1 = File.ReadAllBytes(frame1Path);
                    using (var ms1 = new MemoryStream(imageBytes1))
                    {
                        frame1 = new Bitmap(ms1);
                    }

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            results[0] = ProcessOCR1(frame1);
                            CheckResult();
                        }
                        finally
                        {
                            frame1?.Dispose();
                        }
                    });

                    // Load frame2 and frame3 from byte arrays
                    byte[] imageBytes2 = File.ReadAllBytes(frame2Path);
                    using (var ms2 = new MemoryStream(imageBytes2))
                    {
                        frame2 = new Bitmap(ms2);
                    }
                    
                    byte[] imageBytes3 = File.ReadAllBytes(frame3Path);
                    using (var ms3 = new MemoryStream(imageBytes3))
                    {
                        frame3 = new Bitmap(ms3);
                    }
                    
                    ActivateFrame1And2Processing(false);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoStatistic] {ex.Message}");
                }
            }
            MessageBox.Show($"Đã thống kê xong session.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            // Thêm nút import batch
            var btnImportBatch = new Button();
            btnImportBatch.Text = "Import TestData";
            btnImportBatch.Width = btnImportImage.Width;
            btnImportBatch.Height = btnImportImage.Height;
            btnImportBatch.Top = btnImportImage.Top;
            btnImportBatch.Left = btnSaveResult.Right + 10; // Cách phải 10px
            btnImportBatch.Click += btnImportBatch_Click;
            panelControls.Controls.Add(btnImportBatch);

            // Thêm nút Auto Statistic
            var btnAutoStatistic = new Button();
            btnAutoStatistic.Text = "Auto Statistic";
            btnAutoStatistic.Width = btnImportImage.Width;
            btnAutoStatistic.Height = btnImportImage.Height;
            btnAutoStatistic.Top = btnImportImage.Top;
            btnAutoStatistic.Left = btnImportBatch.Right + 10; // Cách phải 10px
            btnAutoStatistic.Click += btnAutoStatistic_Click;
            panelControls.Controls.Add(btnAutoStatistic);

        }

        // Xử lý sự kiện import batch
        private void btnImportBatch_Click(object sender, EventArgs e)
        {
            string testDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
            if (!Directory.Exists(testDataDir))
            {
                MessageBox.Show($"Không tìm thấy thư mục TestData: {testDataDir}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Lấy danh sách file ảnh (jpg, png, bmp)
            
            var imageFiles = Directory.GetFiles(testDataDir, "*.jpg").ToList();
            imageFiles.AddRange(Directory.GetFiles(testDataDir, "*.png"));
            imageFiles.AddRange(Directory.GetFiles(testDataDir, "*.bmp"));
            if (imageFiles.Count == 0)
            {
                MessageBox.Show("Không tìm thấy file ảnh nào trong TestData!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Import lần lượt từng file
            foreach (var file in imageFiles)
            {
                try
                {
                    using (var image = new Bitmap(file))
                    {
                        picOriginal.Image = new Bitmap(image);
                        _lastImportedImagePath = file;
                        ProcessOCR((Bitmap)picOriginal.Image);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi đọc file ảnh: {file}\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            MessageBox.Show($"Đã import xong {imageFiles.Count} file ảnh từ TestData.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
    }
}
