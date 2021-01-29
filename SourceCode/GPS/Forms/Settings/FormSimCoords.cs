using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormSimCoords : Form
    {
        //class variables
        private readonly FormGPS mf;
        private double latitude = 0, longitude = 0;

        public FormSimCoords(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();

            this.label18.Text = String.Get("gsLatitude");
            this.label1.Text = String.Get("gsLongitude");

            this.btnGetFieldFix.Text = String.Get("gsUseField");
            this.label7.Text = String.Get("gsFieldOrigin");
            this.label5.Text = String.Get("gsGPSCurrentFix");
            this.btnLoadGPSFix.Text = String.Get("gsUseGPS");

            this.Text = String.Get("gsEnterCoordinatesForSimulator");
        }

        private void FormSimCoords_Load(object sender, EventArgs e)
        {
            TboxLatitude.Text = (latitude = Math.Round(Properties.Settings.Default.setGPS_SimLatitude, 7)).ToString("N7");
            TboxLongitude.Text = (longitude = Math.Round(Properties.Settings.Default.setGPS_SimLongitude, 7)).ToString("N7");

            lblLatStart.Text = (mf.LatStart + 0).ToString("N6");
            lblLonStart.Text = (mf.LonStart + 0).ToString("N6");
            if (mf.LatStart == 0)
            {
                btnGetFieldFix.Enabled = false;
            }

            lblGPSLat.Text = (mf.Latitude + 0).ToString("N6");
            lblGPSLon.Text = (mf.Longitude + 0).ToString("N6");
        }

        private void BntOK_Click(object sender, EventArgs e)
        {
            mf.FileSaveEverythingBeforeClosingField();
            mf.sim.latitude = latitude;
            mf.sim.longitude = longitude;
            Properties.Settings.Default.setGPS_SimLatitude = mf.sim.latitude;
            Properties.Settings.Default.setGPS_SimLongitude = mf.sim.longitude;
            Properties.Settings.Default.Save();
            Close();
        }

        private void BtnGetFieldFix_Click(object sender, EventArgs e)
        {
            TboxLatitude.Text = (latitude = Math.Round(mf.LatStart, 7)).ToString("N7");
            TboxLongitude.Text = (longitude = Math.Round(mf.LonStart, 7)).ToString("N7");
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            TboxLatitude.Text = (latitude = Math.Round(mf.Latitude, 7)).ToString("N7");
            TboxLongitude.Text = (longitude = Math.Round(mf.Longitude, 7)).ToString("N7");
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            lblGPSLat.Text = (mf.Latitude + 0).ToString("N6");
            lblGPSLon.Text = (mf.Longitude + 0).ToString("N6");
        }

        private void TboxLatitude_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-90, 90, latitude, this, 7, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLatitude.Text = (latitude = form.ReturnValue).ToString("N7");
                }
            }
            btnCancel.Focus();
        }

        private void TboxLongitude_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-180, 180, longitude, this, 7, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLongitude.Text = (longitude = form.ReturnValue).ToString("N7");
                }
            }
            btnCancel.Focus();
        }
    }
}