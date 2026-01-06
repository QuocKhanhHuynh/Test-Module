using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace demo_ocr_label
{
    public class Config
    {
        // vùng chứa chứa 3 thông tin: mã áo, size áo và màu áo
        public Component bottomLeftComponent { get; set; } = new Component
        {
            width = 0.75f,
            height = 0.35f
        };
        // vùng chứa thông tin số lượng đơn hàng và thứ tự đơn hàng
        public Component aboveQrComponent { get; set; } = new Component
        {
            doiTamSangPhai = 0.2f,
            doiTamLenTren = 0.1f,
            width = 0.6f,
            height = 0.45f
        };
        // các tham số của mô hình PadlleOCR
        public PaddleOCRParams modelParams { get; set; } = new PaddleOCRParams
        {
            // --- 1. Điều khiển tác vụ (Bám sát JSON) ---
            det = true,                             // enabled: true
            rec = true,                             // rec: true
            cls = false,                            // useTextlineOrientation: false
            use_angle_cls = false,                  // Đồng nhất với việc tắt orientation

            cpu_math_library_num_threads = 2,       // cpuThreads: 2
                                                    // mkldnnCacheCapacity: 10 (Thường được xử lý nội bộ trong bộ khởi tạo Core)

            // --- 3. Tham số Detection (Khớp textDetThresh & textDetBoxThresh) ---
            det_db_thresh = 0.15f,                  // textDetThresh: 0.15
            det_db_box_thresh = 0.15f,              // textDetBoxThresh: 0.15
            det_db_unclip_ratio = 2.0f,             // textDetUnclipRatio: 2.0

            // --- 4. Giới hạn hình ảnh (Khớp textDetLimitSideLen: 640) ---
            det_limit_side_len = 640,               // textDetLimitSideLen: 640


            det_db_score_mode = true,                // Chế độ tính điểm chuẩn cho DB
        };
        public SystemArivables systemArivable { get; set; } = new SystemArivables
        {
            debugMode = false,
            showTime = true,
            saveJsonResult = false
        };
        public LabelRectangle labelRectangle { get; set; } = new LabelRectangle
        {
            up = 1.2f,
            down = 2.2f,
            left = 3.8f,
            right = 1.4f
        };
    }
        
    // mô tả một vùng cắt thông tin số lượng đơn hàng - nằm phía trên qr code. Độ lớn tính tương đối % so sánh với độ dài cạnh của qr code
    public class Component
    {
        public float doiTamSangPhai { get; set; }   // dời vị trí cắt sang phải, tính từ góc trên bên phải của qr code
        public float doiTamLenTren { get; set; }   // dời vị trí cắt lên trên, tính từ góc trên bên phải của qr code
        public float width { get; set; } // chiều rộng vùng cắt, tính từ vị trí cắt sang trái
        public float height { get; set; } // chiều cao vùng cắt, tính từ vị trí cắt lên trên
    }

    public class PaddleOCRParams
    {
        // 🔹 Có nhận diện chữ (Detection)
        public bool det { get; set; } = true;

        // 🔹 Có nhận diện hướng chữ (Classification)
        public bool cls { get; set; } = false;

        // 🔹 Sử dụng bộ phân loại hướng chữ (Angle Classifier)
        public bool use_angle_cls { get; set; }

        // 🔹 Có nhận diện nội dung chữ (Recognition)
        public bool rec { get; set; } = true;

        // 🔹 Ngưỡng nhị phân hóa trong DB Detector (0.0–1.0)
        public float det_db_thresh { get; set; } = 0.3f;

        // 🔹 Ngưỡng confidence để giữ lại box (0.0–1.0)
        public float det_db_box_thresh { get; set; } = 0.5f;

        // 🔹 Ngưỡng confidence khi kiểm tra hướng chữ (classification)
        public float cls_thresh { get; set; } = 0.9f;

        // 🔹 Bật tăng tốc tính toán bằng Intel MKL-DNN (oneDNN)
        public bool enable_mkldnn { get; set; } = true;

        // 🔹 Số luồng CPU song song được dùng
        public int cpu_math_library_num_threads { get; set; } = 6;

        // tính score dựa trên đa giác, chính xách hơn nhưng chậm hơn xíu
        public bool det_db_score_mode { get; set; } = false;


        public float det_db_unclip_ratio { get; set; }

        public int det_limit_side_len { get; set; }        // textDetLimitSideLen: 640

    }
    public class SystemArivables
    {
        public bool debugMode { get; set; } = false; // lưu ảnh ở từng model để debug
        public bool showTime { get; set; } = true; // show thời gian ở chế độ debug

        public bool saveJsonResult { get; set; } = true; // lưu kết quả dạng json cho label trích xuất thành công
    }

    public class LabelRectangle
    {
        public float up { get; set; }
        public float down { get; set; }
        public float left { get; set; }
        public float right { get; set; }
    }
}

