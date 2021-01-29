namespace AgOpenGPS
{
    partial class FormFieldDir
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.tboxFieldName = new System.Windows.Forms.TextBox();
            this.btnSerialCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.tboxTask = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tboxVehicle = new System.Windows.Forms.TextBox();
            this.lblFilename = new System.Windows.Forms.Label();
            this.chkAddDate = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Tahoma", 18.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(10, 10);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(380, 30);
            this.label1.TabIndex = 4;
            this.label1.Text = "Enter Field Name";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tboxFieldName
            // 
            this.tboxFieldName.BackColor = System.Drawing.Color.AliceBlue;
            this.tboxFieldName.Font = new System.Drawing.Font("Tahoma", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tboxFieldName.Location = new System.Drawing.Point(10, 40);
            this.tboxFieldName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tboxFieldName.Name = "tboxFieldName";
            this.tboxFieldName.Size = new System.Drawing.Size(380, 40);
            this.tboxFieldName.TabIndex = 0;
            this.tboxFieldName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tboxFieldName.Click += new System.EventHandler(this.TboxFieldName_Click);
            this.tboxFieldName.TextChanged += new System.EventHandler(this.TboxFieldName_TextChanged);
            // 
            // btnSerialCancel
            // 
            this.btnSerialCancel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btnSerialCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnSerialCancel.Font = new System.Drawing.Font("Tahoma", 12F);
            this.btnSerialCancel.Image = global::AgOpenGPS.Properties.Resources.Cancel64;
            this.btnSerialCancel.Location = new System.Drawing.Point(10, 410);
            this.btnSerialCancel.Name = "btnSerialCancel";
            this.btnSerialCancel.Size = new System.Drawing.Size(80, 80);
            this.btnSerialCancel.TabIndex = 4;
            this.btnSerialCancel.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnSerialCancel.UseVisualStyleBackColor = true;
            this.btnSerialCancel.Click += new System.EventHandler(this.BtnSerialCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Image = global::AgOpenGPS.Properties.Resources.OK64;
            this.btnSave.Location = new System.Drawing.Point(310, 410);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(80, 80);
            this.btnSave.TabIndex = 3;
            this.btnSave.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // tboxTask
            // 
            this.tboxTask.BackColor = System.Drawing.Color.AliceBlue;
            this.tboxTask.Font = new System.Drawing.Font("Tahoma", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tboxTask.Location = new System.Drawing.Point(10, 120);
            this.tboxTask.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tboxTask.Name = "tboxTask";
            this.tboxTask.Size = new System.Drawing.Size(380, 40);
            this.tboxTask.TabIndex = 1;
            this.tboxTask.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tboxTask.Click += new System.EventHandler(this.TboxTask_Click);
            this.tboxTask.TextChanged += new System.EventHandler(this.TboxTask_TextChanged);
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label4.Location = new System.Drawing.Point(10, 90);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(380, 30);
            this.label4.TabIndex = 144;
            this.label4.Text = "Enter Task";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label5.Location = new System.Drawing.Point(10, 170);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(380, 30);
            this.label5.TabIndex = 146;
            this.label5.Text = "Enter Vehicle Used";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tboxVehicle
            // 
            this.tboxVehicle.BackColor = System.Drawing.Color.AliceBlue;
            this.tboxVehicle.Font = new System.Drawing.Font("Tahoma", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tboxVehicle.Location = new System.Drawing.Point(10, 200);
            this.tboxVehicle.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tboxVehicle.Name = "tboxVehicle";
            this.tboxVehicle.Size = new System.Drawing.Size(380, 40);
            this.tboxVehicle.TabIndex = 2;
            this.tboxVehicle.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tboxVehicle.Click += new System.EventHandler(this.TboxVehicle_Click);
            this.tboxVehicle.TextChanged += new System.EventHandler(this.TboxVehicle_TextChanged);
            // 
            // lblFilename
            // 
            this.lblFilename.Font = new System.Drawing.Font("Tahoma", 18.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFilename.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lblFilename.Location = new System.Drawing.Point(10, 300);
            this.lblFilename.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblFilename.Name = "lblFilename";
            this.lblFilename.Size = new System.Drawing.Size(380, 100);
            this.lblFilename.TabIndex = 147;
            this.lblFilename.Text = "Filename";
            this.lblFilename.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // chkAddDate
            // 
            this.chkAddDate.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkAddDate.FlatAppearance.BorderColor = System.Drawing.Color.Black;
            this.chkAddDate.FlatAppearance.CheckedBackColor = System.Drawing.Color.PaleGreen;
            this.chkAddDate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.chkAddDate.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkAddDate.Location = new System.Drawing.Point(10, 250);
            this.chkAddDate.Name = "chkAddDate";
            this.chkAddDate.Size = new System.Drawing.Size(380, 40);
            this.chkAddDate.TabIndex = 257;
            this.chkAddDate.Text = "Add Date?";
            this.chkAddDate.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkAddDate.UseVisualStyleBackColor = true;
            this.chkAddDate.CheckedChanged += new System.EventHandler(this.ChkAddDate_CheckedChanged);
            // 
            // FormFieldDir
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(400, 500);
            this.ControlBox = false;
            this.Controls.Add(this.chkAddDate);
            this.Controls.Add(this.lblFilename);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.tboxVehicle);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.tboxTask);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnSerialCancel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tboxFieldName);
            this.Font = new System.Drawing.Font("Tahoma", 14.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "FormFieldDir";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create New Field ";
            this.Load += new System.EventHandler(this.FormFieldDir_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tboxFieldName;
        private System.Windows.Forms.Button btnSerialCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox tboxTask;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tboxVehicle;
        private System.Windows.Forms.Label lblFilename;
        private System.Windows.Forms.CheckBox chkAddDate;
    }
}