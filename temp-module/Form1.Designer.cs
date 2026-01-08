namespace temp_module
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelControls = new Panel();
            btnImportImage = new Button();
            btnStartCamera = new Button();
            btnStopCamera = new Button();
            comboBoxCameras = new ComboBox();
            label1 = new Label();
            btnSaveResult = new Button();
            btnImportBatch = new Button();
            splitContainerMain = new SplitContainer();
            splitContainerLeft = new SplitContainer();
            panelOriginal = new Panel();
            labelOriginal = new Label();
            picOriginal = new PictureBox();
            panelProcessed = new Panel();
            labelProcessed = new Label();
            picProcessed = new PictureBox();
            panelResults = new Panel();
            labelResults = new Label();
            textBoxResults = new TextBox();
            openFileDialog = new OpenFileDialog();
            panelControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerLeft).BeginInit();
            splitContainerLeft.Panel1.SuspendLayout();
            splitContainerLeft.Panel2.SuspendLayout();
            splitContainerLeft.SuspendLayout();
            panelOriginal.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picOriginal).BeginInit();
            panelProcessed.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picProcessed).BeginInit();
            panelResults.SuspendLayout();
            SuspendLayout();
            // 
            // panelControls
            // 
            panelControls.Controls.Add(btnImportImage);
            panelControls.Controls.Add(btnStartCamera);
            panelControls.Controls.Add(btnStopCamera);
            panelControls.Controls.Add(comboBoxCameras);
            panelControls.Controls.Add(label1);
            panelControls.Controls.Add(btnSaveResult);
            panelControls.Dock = DockStyle.Top;
            panelControls.Location = new Point(0, 0);
            panelControls.Margin = new Padding(4, 5, 4, 5);
            panelControls.Name = "panelControls";
            panelControls.Size = new Size(1714, 100);
            panelControls.TabIndex = 0;
            // 
            // btnImportImage
            // 
            btnImportImage.Location = new Point(643, 25);
            btnImportImage.Margin = new Padding(4, 5, 4, 5);
            btnImportImage.Name = "btnImportImage";
            btnImportImage.Size = new Size(171, 50);
            btnImportImage.TabIndex = 4;
            btnImportImage.Text = "Import Ảnh";
            btnImportImage.UseVisualStyleBackColor = true;
            btnImportImage.Click += btnImportImage_Click;
            // 
            // btnStartCamera
            // 
            btnStartCamera.Location = new Point(457, 25);
            btnStartCamera.Margin = new Padding(4, 5, 4, 5);
            btnStartCamera.Name = "btnStartCamera";
            btnStartCamera.Size = new Size(171, 50);
            btnStartCamera.TabIndex = 3;
            btnStartCamera.Text = "Bật Camera";
            btnStartCamera.UseVisualStyleBackColor = true;
            btnStartCamera.Click += btnStartCamera_Click;
            // 
            // btnStopCamera
            // 
            btnStopCamera.Enabled = false;
            btnStopCamera.Location = new Point(457, 25);
            btnStopCamera.Margin = new Padding(4, 5, 4, 5);
            btnStopCamera.Name = "btnStopCamera";
            btnStopCamera.Size = new Size(171, 50);
            btnStopCamera.TabIndex = 2;
            btnStopCamera.Text = "Tắt Camera";
            btnStopCamera.UseVisualStyleBackColor = true;
            btnStopCamera.Click += btnStopCamera_Click;
            // 
            // comboBoxCameras
            // 
            comboBoxCameras.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCameras.FormattingEnabled = true;
            comboBoxCameras.Location = new Point(114, 30);
            comboBoxCameras.Margin = new Padding(4, 5, 4, 5);
            comboBoxCameras.Name = "comboBoxCameras";
            comboBoxCameras.Size = new Size(327, 33);
            comboBoxCameras.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(17, 35);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(76, 25);
            label1.TabIndex = 0;
            label1.Text = "Camera:";
            // 
            // btnSaveResult
            // 
            btnSaveResult.Location = new Point(829, 25);
            btnSaveResult.Margin = new Padding(4, 5, 4, 5);
            btnSaveResult.Name = "btnSaveResult";
            btnSaveResult.Size = new Size(171, 50);
            btnSaveResult.TabIndex = 5;
            btnSaveResult.Text = "Lưu thông tin";
            btnSaveResult.UseVisualStyleBackColor = true;
            btnSaveResult.Click += btnSaveResult_Click;
            // 
            // splitContainerMain
            // 
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.Location = new Point(0, 100);
            splitContainerMain.Margin = new Padding(4, 5, 4, 5);
            splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            splitContainerMain.Panel1.Controls.Add(splitContainerLeft);
            // 
            // splitContainerMain.Panel2
            // 
            splitContainerMain.Panel2.Controls.Add(panelResults);
            splitContainerMain.Size = new Size(1714, 950);
            splitContainerMain.SplitterDistance = 1142;
            splitContainerMain.SplitterWidth = 6;
            splitContainerMain.TabIndex = 1;
            // 
            // splitContainerLeft
            // 
            splitContainerLeft.Dock = DockStyle.Fill;
            splitContainerLeft.Location = new Point(0, 0);
            splitContainerLeft.Margin = new Padding(4, 5, 4, 5);
            splitContainerLeft.Name = "splitContainerLeft";
            splitContainerLeft.Orientation = Orientation.Horizontal;
            // 
            // splitContainerLeft.Panel1
            // 
            splitContainerLeft.Panel1.Controls.Add(panelOriginal);
            // 
            // splitContainerLeft.Panel2
            // 
            splitContainerLeft.Panel2.Controls.Add(panelProcessed);
            splitContainerLeft.Size = new Size(1142, 950);
            splitContainerLeft.SplitterDistance = 475;
            splitContainerLeft.SplitterWidth = 7;
            splitContainerLeft.TabIndex = 0;
            // 
            // panelOriginal
            // 
            panelOriginal.Controls.Add(labelOriginal);
            panelOriginal.Controls.Add(picOriginal);
            panelOriginal.Dock = DockStyle.Fill;
            panelOriginal.Location = new Point(0, 0);
            panelOriginal.Margin = new Padding(4, 5, 4, 5);
            panelOriginal.Name = "panelOriginal";
            panelOriginal.Padding = new Padding(7, 8, 7, 8);
            panelOriginal.Size = new Size(1142, 475);
            panelOriginal.TabIndex = 0;
            // 
            // labelOriginal
            // 
            labelOriginal.AutoSize = true;
            labelOriginal.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelOriginal.Location = new Point(11, 13);
            labelOriginal.Margin = new Padding(4, 0, 4, 0);
            labelOriginal.Name = "labelOriginal";
            labelOriginal.Size = new Size(167, 25);
            labelOriginal.TabIndex = 1;
            labelOriginal.Text = "Ảnh Gốc / Camera";
            // 
            // picOriginal
            // 
            picOriginal.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            picOriginal.BackColor = Color.Black;
            picOriginal.Location = new Point(11, 47);
            picOriginal.Margin = new Padding(4, 5, 4, 5);
            picOriginal.Name = "picOriginal";
            picOriginal.Size = new Size(1119, 415);
            picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
            picOriginal.TabIndex = 0;
            picOriginal.TabStop = false;
            // 
            // panelProcessed
            // 
            panelProcessed.Controls.Add(labelProcessed);
            panelProcessed.Controls.Add(picProcessed);
            panelProcessed.Dock = DockStyle.Fill;
            panelProcessed.Location = new Point(0, 0);
            panelProcessed.Margin = new Padding(4, 5, 4, 5);
            panelProcessed.Name = "panelProcessed";
            panelProcessed.Padding = new Padding(7, 8, 7, 8);
            panelProcessed.Size = new Size(1142, 468);
            panelProcessed.TabIndex = 0;
            // 
            // labelProcessed
            // 
            labelProcessed.AutoSize = true;
            labelProcessed.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelProcessed.Location = new Point(11, 13);
            labelProcessed.Margin = new Padding(4, 0, 4, 0);
            labelProcessed.Name = "labelProcessed";
            labelProcessed.Size = new Size(127, 25);
            labelProcessed.TabIndex = 1;
            labelProcessed.Text = "Ảnh Đã Xử Lý";
            // 
            // picProcessed
            // 
            picProcessed.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            picProcessed.BackColor = Color.Black;
            picProcessed.Location = new Point(11, 47);
            picProcessed.Margin = new Padding(4, 5, 4, 5);
            picProcessed.Name = "picProcessed";
            picProcessed.Size = new Size(1119, 408);
            picProcessed.SizeMode = PictureBoxSizeMode.Zoom;
            picProcessed.TabIndex = 0;
            picProcessed.TabStop = false;
            // 
            // panelResults
            // 
            panelResults.Controls.Add(labelResults);
            panelResults.Controls.Add(textBoxResults);
            panelResults.Dock = DockStyle.Fill;
            panelResults.Location = new Point(0, 0);
            panelResults.Margin = new Padding(4, 5, 4, 5);
            panelResults.Name = "panelResults";
            panelResults.Padding = new Padding(7, 8, 7, 8);
            panelResults.Size = new Size(566, 950);
            panelResults.TabIndex = 0;
            // 
            // labelResults
            // 
            labelResults.AutoSize = true;
            labelResults.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelResults.Location = new Point(11, 13);
            labelResults.Margin = new Padding(4, 0, 4, 0);
            labelResults.Name = "labelResults";
            labelResults.Size = new Size(123, 25);
            labelResults.TabIndex = 1;
            labelResults.Text = "Kết Quả OCR";
            // 
            // textBoxResults
            // 
            textBoxResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxResults.Font = new Font("Consolas", 9F);
            textBoxResults.Location = new Point(11, 47);
            textBoxResults.Margin = new Padding(4, 5, 4, 5);
            textBoxResults.Multiline = true;
            textBoxResults.Name = "textBoxResults";
            textBoxResults.ReadOnly = true;
            textBoxResults.ScrollBars = ScrollBars.Vertical;
            textBoxResults.Size = new Size(541, 887);
            textBoxResults.TabIndex = 0;
            // 
            // openFileDialog
            // 
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*";
            openFileDialog.Title = "Chọn Ảnh để Xử Lý OCR";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1714, 1050);
            Controls.Add(splitContainerMain);
            Controls.Add(panelControls);
            Margin = new Padding(4, 5, 4, 5);
            MinimumSize = new Size(1133, 963);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "OCR Camera Module";
            FormClosing += Form1_FormClosing;
            panelControls.ResumeLayout(false);
            panelControls.PerformLayout();
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            splitContainerLeft.Panel1.ResumeLayout(false);
            splitContainerLeft.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerLeft).EndInit();
            splitContainerLeft.ResumeLayout(false);
            panelOriginal.ResumeLayout(false);
            panelOriginal.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picOriginal).EndInit();
            panelProcessed.ResumeLayout(false);
            panelProcessed.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picProcessed).EndInit();
            panelResults.ResumeLayout(false);
            panelResults.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelControls;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxCameras;
        private System.Windows.Forms.Button btnStartCamera;
        private System.Windows.Forms.Button btnStopCamera;
        private System.Windows.Forms.Button btnImportImage;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.SplitContainer splitContainerLeft;
        private System.Windows.Forms.Panel panelOriginal;
        private System.Windows.Forms.Label labelOriginal;
        private System.Windows.Forms.PictureBox picOriginal;
        private System.Windows.Forms.Panel panelProcessed;
        private System.Windows.Forms.Label labelProcessed;
        private System.Windows.Forms.PictureBox picProcessed;
        private System.Windows.Forms.Panel panelResults;
        private System.Windows.Forms.Label labelResults;
        private System.Windows.Forms.TextBox textBoxResults;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.Button btnSaveResult;
        private System.Windows.Forms.Button btnImportBatch;
    }
}
