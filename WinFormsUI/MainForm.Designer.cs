namespace WinFormsUI
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
            components = new System.ComponentModel.Container();
            MainGLControl = new OpenTK.GLControl.GLControl();
            MainTimer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // MainGLControl
            // 
            MainGLControl.API = OpenTK.Windowing.Common.ContextAPI.OpenGL;
            MainGLControl.APIVersion = new Version(3, 3, 0, 0);
            MainGLControl.Dock = DockStyle.Fill;
            MainGLControl.Flags = OpenTK.Windowing.Common.ContextFlags.Default;
            MainGLControl.IsEventDriven = true;
            MainGLControl.Location = new Point(0, 0);
            MainGLControl.Name = "MainGLControl";
            MainGLControl.Profile = OpenTK.Windowing.Common.ContextProfile.Core;
            MainGLControl.SharedContext = null;
            MainGLControl.Size = new Size(1904, 1041);
            MainGLControl.TabIndex = 0;
            MainGLControl.Load += OnGLLoad;
            MainGLControl.Paint += OnGLPaint;
            // 
            // MainTimer
            // 
            MainTimer.Interval = 1;
            MainTimer.Tick += TimerTick;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1904, 1041);
            Controls.Add(MainGLControl);
            KeyPreview = true;
            Name = "MainForm";
            Text = "Form1";
            Load += MainFormLoad;
            ResumeLayout(false);
        }

        #endregion

        private OpenTK.GLControl.GLControl MainGLControl;
        private System.Windows.Forms.Timer MainTimer;
    }
}
