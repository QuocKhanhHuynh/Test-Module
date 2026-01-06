# Hướng Dẫn Setup OCR Module

Module này cần các file OCR utils từ `garment-grid-app/GarmentGridApp.Presentation/OCR/Utils/`.

## Các File Cần Copy:

1. **Config.cs** → `temp-module/temp-module/OCR/Utils/Config.cs`
2. **utils.cs** → `temp-module/temp-module/OCR/Utils/utils.cs`
3. **DetectLabelFromImageV2.cs** → `temp-module/temp-module/OCR/Utils/DetectLabelFromImageV2.cs`
4. **Yolo11SegOpenVINO.cs** → `temp-module/temp-module/OCR/Utils/Yolo11SegOpenVINO.cs`
5. **ImageEnhancer.cs** → `temp-module/temp-module/OCR/Utils/ImageEnhancer.cs`
6. **RotationImage.cs** → `temp-module/temp-module/OCR/Utils/RotationImage.cs`
7. **LabelDetectorWeChat.cs** → `temp-module/temp-module/OCR/Utils/LabelDetectorWeChat.cs`
8. **CropComponent.cs** → `temp-module/temp-module/OCR/Utils/CropComponent.cs`
9. **LabelDetector.cs** → `temp-module/temp-module/OCR/Utils/LabelDetector.cs`
10. **GetRectangleAroundQR.cs** → `temp-module/temp-module/OCR/Utils/GetRectangleAroundQR.cs`
11. **OpenVinoSetting.cs** → `temp-module/temp-module/OCR/Utils/OpenVinoSetting.cs`

## Điều Chỉnh Namespace:

Sau khi copy, cần điều chỉnh namespace trong các file:
- `DetectQRCode.Models.Camera` → `temp_module.Models`
- Giữ nguyên `demo_ocr_label` và `GarmentGridApp.Presentation.OCR.Utils` nếu cần

## Models Folder:

Copy các model files (YOLO, WeChat QR Code, PaddleOCR) vào thư mục `models/` trong output directory.

