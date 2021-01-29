using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormDisplayOptions : Form
    {
        //class variables
        private readonly FormGPS mf = null;

        //constructor
        public FormDisplayOptions(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();

            //Language keys
            Text = String.Get("gsOptions");

            chkExtraGuides.Text = String.Get("gsExtraGuides");
            chkGrid.Text = String.Get("gsGridOn");
            chkLogNMEA.Text = String.Get("gsLogNMEA");
            chkPolygons.Text = String.Get("gsPolygonsOn");
            chkPursuitLines.Text = String.Get("gsPursuitLine");
            chkSky.Text = String.Get("gsSkyOn");
            chkUTurnOn.Text = String.Get("gsUTurnAlwaysOn");
            chkCompass.Text = String.Get("gsCompassOn");
            chkSpeedo.Text = String.Get("gsSpeedoOn");
            chkStartFullScreen.Text = String.Get("gsStartFullScreen");
            chkDayNight.Text = String.Get("gsAutoDayNightMode");
            chkAutoLoadFields.Text = String.Get("gsAutoLoadFields");
            rbtnMetric.Text = String.Get("gsMetric");
            rbtnImperial.Text = String.Get("gsImperial");
            unitsGroupBox.Text = String.Get("gsUnits");

        }

        private void FormDisplaySettings_Load(object sender, EventArgs e)
        {
            chkSky.Checked = mf.isSkyOn;
            chkGrid.Checked = mf.isGridOn;
            chkCompass.Checked = mf.isCompassOn;
            chkSpeedo.Checked = mf.isSpeedoOn;
            chkDayNight.Checked = mf.isAutoDayNight;
            chkStartFullScreen.Checked = Properties.Settings.Default.setDisplay_isStartFullScreen;
            chkExtraGuides.Checked = mf.isSideGuideLines;
            chkLogNMEA.Checked = mf.isLogNMEA;
            chkPolygons.Checked = mf.isDrawPolygons;
            chkPursuitLines.Checked = mf.isPureDisplayOn;
            chkUTurnOn.Checked = mf.isUTurnAlwaysOn;
            chkAutoLoadFields.Checked = mf.isAutoLoadFields;
            chkDrawBackBuffer.Checked = mf.DrawBackBuffer;

            if (mf.isMetric) rbtnMetric.Checked = true;
            else rbtnImperial.Checked = true;
        }
        private void BntOK_Click(object sender, EventArgs e)
        {
            mf.isDrawPolygons = chkPolygons.Checked;
            Properties.Settings.Default.setMenu_isSkyOn = mf.isSkyOn = chkSky.Checked;
            Properties.Settings.Default.setMenu_isGridOn = mf.isGridOn = chkGrid.Checked;
            Properties.Settings.Default.setMenu_isCompassOn = mf.isCompassOn = chkCompass.Checked;
            Properties.Settings.Default.setMenu_isSpeedoOn = mf.isSpeedoOn = chkSpeedo.Checked;
            Properties.Settings.Default.setDisplay_isAutoDayNight = mf.isAutoDayNight = chkDayNight.Checked;
            Properties.Settings.Default.setDisplay_isStartFullScreen = chkStartFullScreen.Checked;
            Properties.Settings.Default.setMenu_isSideGuideLines = mf.isSideGuideLines = chkExtraGuides.Checked;
            Properties.Settings.Default.setMenu_isLogNMEA = mf.isLogNMEA = chkLogNMEA.Checked;
            Properties.Settings.Default.setMenu_isPureOn = mf.isPureDisplayOn = chkPursuitLines.Checked;
            Properties.Settings.Default.setMenu_isUTurnAlwaysOn = mf.isUTurnAlwaysOn = chkUTurnOn.Checked;
            Properties.Settings.Default.AutoLoadFields = mf.isAutoLoadFields = chkAutoLoadFields.Checked;
            Properties.Settings.Default.DrawBackBuffer = mf.DrawBackBuffer = chkDrawBackBuffer.Checked;
            Properties.Settings.Default.setMenu_isMetric = mf.isMetric = rbtnMetric.Checked;
            Properties.Settings.Default.Save();

            if (mf.isAutoLoadFields) mf.LoadFields();
            else mf.Fields.Clear();

            //metric settings
            if (mf.isMetric)
            {
                mf.Mtr2Unit = 1.0;
                mf.Unit2Mtr = 1.0;
                mf.Kmh2Unit = 1.0;
                mf.Unit2Kmh = 1.0;
                mf.Decimals = 2;
            }
            else
            {
                mf.Mtr2Unit = Glm.m2in;
                mf.Unit2Mtr = Glm.in2m;
                mf.Kmh2Unit = 0.62137273665;
                mf.Unit2Kmh = 1.60934;
                mf.Decimals = 3;
            }

            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}