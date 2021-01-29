using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormFieldDir : Form
    {
        //class variables
        private readonly FormGPS mf;

        public FormFieldDir(Form _callingForm)
        {
            //get copy of the calling main form
            Owner = mf = _callingForm as FormGPS;

            InitializeComponent();

            label1.Text = String.Get("gsEnterFieldName");
            chkAddDate.Text = String.Get("gsAddDate");
            label4.Text = String.Get("gsEnterTask");
            label5.Text = String.Get("gsEnterVehicleUsed");
            Text = String.Get("gsCreateNewField");
        }

        private void FormFieldDir_Load(object sender, EventArgs e)
        {
            btnSave.Enabled = false;
            //tboxVehicle.Text = mf.vehicleFileName + " " + mf.toolFileName;
            lblFilename.Text = "";
        }

        private void TboxFieldName_TextChanged(object sender, EventArgs e)
        {
            var textboxSender = (TextBox)sender;
            var cursorPosition = textboxSender.SelectionStart;
            textboxSender.Text = Regex.Replace(textboxSender.Text, Glm.fileRegex, "");
            textboxSender.SelectionStart = cursorPosition;
            
            if (string.IsNullOrEmpty(tboxFieldName.Text.Trim()))
            {
                btnSave.Enabled = false;
            }
            else
            {
                btnSave.Enabled = true;
            }

            lblFilename.Text = tboxFieldName.Text.Trim();
            if (!string.IsNullOrEmpty(tboxTask.Text.Trim())) lblFilename.Text += " " + tboxTask.Text.Trim();
            if (!string.IsNullOrEmpty(tboxVehicle.Text.Trim())) lblFilename.Text += " " + tboxVehicle.Text.Trim();
            if (chkAddDate.Checked) lblFilename.Text += " " + DateTime.Now.ToString("yyyy.MMM.dd HH_mm", CultureInfo.InvariantCulture);
        }

        private void TboxTask_TextChanged(object sender, EventArgs e)
        {
            var textboxSender = (TextBox)sender;
            var cursorPosition = textboxSender.SelectionStart;
            textboxSender.Text = Regex.Replace(textboxSender.Text, Glm.fileRegex, "");
            textboxSender.SelectionStart = cursorPosition;

            lblFilename.Text = tboxFieldName.Text.Trim();
            if (!string.IsNullOrEmpty(tboxTask.Text.Trim())) lblFilename.Text += " " + tboxTask.Text.Trim();
            if (!string.IsNullOrEmpty(tboxVehicle.Text.Trim())) lblFilename.Text += " " + tboxVehicle.Text.Trim();
            if (chkAddDate.Checked) lblFilename.Text += " " + DateTime.Now.ToString("yyyy.MMM.dd HH_mm", CultureInfo.InvariantCulture);
        }

        private void TboxVehicle_TextChanged(object sender, EventArgs e)
        {
            var textboxSender = (TextBox)sender;
            var cursorPosition = textboxSender.SelectionStart;
            textboxSender.Text = Regex.Replace(textboxSender.Text, Glm.fileRegex, "");
            textboxSender.SelectionStart = cursorPosition;

            lblFilename.Text = tboxFieldName.Text.Trim();
            if (!string.IsNullOrEmpty(tboxTask.Text.Trim())) lblFilename.Text += " " + tboxTask.Text.Trim();
            if (!string.IsNullOrEmpty(tboxVehicle.Text.Trim())) lblFilename.Text += " " + tboxVehicle.Text.Trim();
            if (chkAddDate.Checked) lblFilename.Text += " " + DateTime.Now.ToString("yyyy.MMM.dd HH_mm", CultureInfo.InvariantCulture);
        }

        private void BtnSerialCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            //fill something in
            if (string.IsNullOrEmpty(tboxFieldName.Text.Trim()))
            {
                return;
            }

            mf.currentFieldDirectory = tboxFieldName.Text.Trim();

            //task
            if (!string.IsNullOrEmpty(tboxTask.Text.Trim())) mf.currentFieldDirectory += " " + tboxTask.Text.Trim();

            //vehicle
            if (!string.IsNullOrEmpty(tboxVehicle.Text.Trim())) mf.currentFieldDirectory += " " + tboxVehicle.Text.Trim();

            //date
            if (chkAddDate.Checked) mf.currentFieldDirectory += " " + string.Format("{0}", DateTime.Now.ToString("yyyy.MMM.dd HH_mm", CultureInfo.InvariantCulture));

            //get the directory and make sure it exists, create if not
            string dirNewField = mf.fieldsDirectory + mf.currentFieldDirectory + "\\";

            //if no template set just make a new file.
            try
            {
                //start a new job
                mf.JobNew();

                //create it for first save
                string directoryName = Path.GetDirectoryName(dirNewField);

                if ((!string.IsNullOrEmpty(directoryName)) && (Directory.Exists(directoryName)))
                {
                    MessageBox.Show(String.Get("gsChooseADifferentName"), String.Get("gsDirectoryExists"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return;
                }
                else
                {
                    mf.SetPlaneToLocal(mf.Latitude, mf.Longitude);
                    mf.StartTasks(null, 1, TaskName.Save);
                    mf.FileOpenField();
                }
            }
            catch (Exception ex)
            {
                mf.WriteErrorLog("Creating new field " + ex);

                MessageBox.Show(String.Get("gsError"), ex.ToString());
                mf.currentFieldDirectory = "";
            }       

            DialogResult = DialogResult.OK;
            Close();
        }

        private void TboxFieldName_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void TboxTask_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void TboxVehicle_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void ChkAddDate_CheckedChanged(object sender, EventArgs e)
        {
            lblFilename.Text = tboxFieldName.Text.Trim();
            if (!string.IsNullOrEmpty(tboxTask.Text.Trim())) lblFilename.Text += " " + tboxTask.Text.Trim();
            if (!string.IsNullOrEmpty(tboxVehicle.Text.Trim())) lblFilename.Text += " " + tboxVehicle.Text.Trim();
            if (chkAddDate.Checked) lblFilename.Text += " " + DateTime.Now.ToString("yyyy.MMM.dd HH_mm", CultureInfo.InvariantCulture);
        }
    }
}