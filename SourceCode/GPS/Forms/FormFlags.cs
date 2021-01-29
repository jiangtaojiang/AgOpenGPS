using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormFlags : Form
    {
        //class variables
        private readonly FormGPS mf;
        private readonly Timer Timer = new Timer();
        private byte TimerMode = 0;

        public FormFlags(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();
            Timer.Tick += new EventHandler(TimerRepeat_Tick);
        }

        private void UpdateLabels()
        {
            lblLatStart.Text = mf.flagPts[mf.flagNumberPicked - 1].Latitude.ToString();
            lblLonStart.Text = mf.flagPts[mf.flagNumberPicked - 1].Longitude.ToString();
            lblEasting.Text = mf.flagPts[mf.flagNumberPicked - 1].Easting.ToString("N2");
            lblNorthing.Text = mf.flagPts[mf.flagNumberPicked - 1].Northing.ToString("N2");
            lblHeading.Text = Glm.ToDegrees(mf.flagPts[mf.flagNumberPicked - 1].Heading).ToString("N2");
            lblFlagSelected.Text = mf.flagPts[mf.flagNumberPicked - 1].ID.ToString();
            tboxFlagNotes.Text = mf.flagPts[mf.flagNumberPicked - 1].notes;
        }

        private void FormFlags_Load(object sender, EventArgs e)
        {
            UpdateLabels();
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            mf.flagNumberPicked = 0;
            mf.FileSaveFlags();
            Close();
        }

        private void BtnDeleteFlag_Click(object sender, EventArgs e)
        {
            int flag = mf.flagNumberPicked;
            if (mf.flagPts.Count > 0) mf.DeleteSelectedFlag();
            if (mf.flagPts.Count == 0)
            {
                mf.FileSaveFlags();
                Close();
                return;
            }
            if (flag > mf.flagPts.Count) mf.flagNumberPicked = mf.flagPts.Count;
            else mf.flagNumberPicked = flag;
            UpdateLabels();
        }

        private void TboxFlagNotes_Leave(object sender, EventArgs e)
        {
            mf.flagPts[mf.flagNumberPicked - 1].notes = tboxFlagNotes.Text;
        }

        private void TboxFlagNotes_TextChanged(object sender, EventArgs e)
        {

            //mf.flagPts[mf.flagNumberPicked - 1].notes = tboxFlagNotes.Text;
        }

        private void TboxFlagNotes_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r') e.Handled = true;
        }

        private void BtnDriveToFlag_Click(object sender, EventArgs e)
        {
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            //MakeDubinsLineFromPivotToFlag();
            Vec3 steerAxlePosRP = mf.pivotAxlePos;
            if (mf.isMetric)
                lblDistanceToFlag.Text = Glm.Distance(steerAxlePosRP,
                    mf.flagPts[mf.flagNumberPicked - 1].Easting, mf.flagPts[mf.flagNumberPicked - 1].Northing).ToString("N2") + " m";
            else lblDistanceToFlag.Text = (Glm.Distance(steerAxlePosRP,
                mf.flagPts[mf.flagNumberPicked - 1].Easting, mf.flagPts[mf.flagNumberPicked - 1].Northing) * Glm.m2ft).ToString("N2") + " m";
        }

        private void TboxFlagNotes_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnExit.Focus();
            }
        }

        private void BtnNorth_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 0;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnSouth_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 1;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void Btn_MouseUp(object sender, MouseEventArgs e)
        {
            Timer.Enabled = false;
        }

        private void TimerRepeat_Tick(object sender, EventArgs e)
        {
            if (Timer.Enabled)
            {
                if (Timer.Interval > 50) Timer.Interval -= 50;
            }
            else
                Timer.Interval = 500;

            Timer.Enabled = true;

            if (TimerMode == 0)
            {
                mf.flagNumberPicked++;
                if (mf.flagNumberPicked > mf.flagPts.Count) mf.flagNumberPicked = 1;
            }
            else if (TimerMode == 1)
            {
                mf.flagNumberPicked--;
                if (mf.flagNumberPicked < 1) mf.flagNumberPicked = mf.flagPts.Count;
            }
            UpdateLabels();
        }
    }
}