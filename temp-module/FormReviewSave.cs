using System;
using System.IO;
using System.Windows.Forms;

namespace temp_module
{
    public class FormReviewSave : Form
    {
        public string QRCode { get; private set; }
        public string ProductTotal { get; private set; }
        public string Size { get; private set; }
        public string ProductCode { get; private set; }
        public string Color { get; private set; }

        private TextBox txtQR;
        private TextBox txtTotal;
        private TextBox txtSize;
        private TextBox txtCode;
        private TextBox txtColor;
        private Button btnConfirm;
        private Button btnCancel;

        public FormReviewSave(string qr, string total, string size, string code, string color)
        {
            this.Text = "Review & Chỉnh sửa nội dung lưu";
            this.Width = 400;
            this.Height = 350;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lblQR = new Label { Text = "QR Code:", Left = 10, Top = 10, Width = 80 };
            txtQR = new TextBox { Left = 100, Top = 10, Width = 260, Text = qr };

            Label lblTotal = new Label { Text = "Tổng số lượng:", Left = 10, Top = 45, Width = 80 };
            txtTotal = new TextBox { Left = 100, Top = 45, Width = 260, Text = total };

            Label lblSize = new Label { Text = "Size:", Left = 10, Top = 80, Width = 80 };
            txtSize = new TextBox { Left = 100, Top = 80, Width = 260, Text = size };

            Label lblCode = new Label { Text = "Mã sản phẩm:", Left = 10, Top = 115, Width = 80 };
            txtCode = new TextBox { Left = 100, Top = 115, Width = 260, Text = code };

            Label lblColor = new Label { Text = "Màu:", Left = 10, Top = 150, Width = 80 };
            txtColor = new TextBox { Left = 100, Top = 150, Width = 260, Text = color };

            btnConfirm = new Button { Text = "Xác nhận lưu", Left = 10, Top = 200, Width = 120, Height = 35 };
            btnConfirm.Click += (s, e) => {
                QRCode = txtQR.Text;
                ProductTotal = txtTotal.Text;
                Size = txtSize.Text;
                ProductCode = txtCode.Text;
                Color = txtColor.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button { Text = "Hủy", Left = 240, Top = 200, Width = 120, Height = 35 };
            btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(lblQR);
            this.Controls.Add(txtQR);
            this.Controls.Add(lblTotal);
            this.Controls.Add(txtTotal);
            this.Controls.Add(lblSize);
            this.Controls.Add(txtSize);
            this.Controls.Add(lblCode);
            this.Controls.Add(txtCode);
            this.Controls.Add(lblColor);
            this.Controls.Add(txtColor);
            this.Controls.Add(btnConfirm);
            this.Controls.Add(btnCancel);
        }
    }
}
