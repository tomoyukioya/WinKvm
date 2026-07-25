namespace WinKvm
{
    partial class MainForm
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
            menuStrip1 = new MenuStrip();
            menuItemVideo = new ToolStripMenuItem();
            menuItemKeyMouse = new ToolStripMenuItem();
            pictureBox = new PictureBox();
            statusStrip = new StatusStrip();
            statusLabelVideo = new ToolStripStatusLabel();
            statusLabelSerial = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(24, 24);
            menuStrip1.Items.AddRange(new ToolStripItem[] { menuItemVideo, menuItemKeyMouse });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(800, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // menuItemVideo
            // 
            menuItemVideo.Name = "menuItemVideo";
            menuItemVideo.Size = new Size(68, 20);
            menuItemVideo.Text = "ビデオ設定";
            // 
            // menuItemKeyMouse
            // 
            menuItemKeyMouse.Name = "menuItemKeyMouse";
            menuItemKeyMouse.Size = new Size(118, 20);
            menuItemKeyMouse.Text = "マウス/キーボード設定";
            // 
            // pictureBox
            // 
            pictureBox.Location = new Point(0, 27);
            pictureBox.Margin = new Padding(0, 0, 0, 20);
            pictureBox.Name = "pictureBox";
            pictureBox.Size = new Size(800, 398);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 1;
            pictureBox.TabStop = false;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseEnter += PictureBox_MouseEnter;
            pictureBox.MouseLeave += PictureBox_MouseLeave;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(24, 24);
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabelVideo, statusLabelSerial });
            statusStrip.Location = new Point(0, 428);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(800, 22);
            statusStrip.TabIndex = 2;
            statusStrip.Text = "statusStrip";
            // 
            // statusLabelVideo
            // 
            statusLabelVideo.Name = "statusLabelVideo";
            statusLabelVideo.Size = new Size(96, 17);
            statusLabelVideo.Text = "statusLabelVideo";
            // 
            // statusLabelSerial
            // 
            statusLabelSerial.Name = "statusLabelSerial";
            statusLabelSerial.Size = new Size(94, 17);
            statusLabelSerial.Text = "statusLabelSerial";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(800, 450);
            Controls.Add(statusStrip);
            Controls.Add(pictureBox);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "WinKVM";
            FormClosing += Form1_FormClosing;
            Load += MainForm_Load;
            KeyDown += MainForm_KeyDown;
            KeyUp += MainForm_KeyUp;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem menuItemVideo;
        private PictureBox pictureBox;
        private ToolStripMenuItem menuItemKeyMouse;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabelVideo;
        private ToolStripStatusLabel statusLabelSerial;
    }
}