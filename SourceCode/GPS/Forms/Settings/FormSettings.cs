//Please, if you use this, share the improvements

using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormSettings : Form
    {
        //class variables
        private readonly FormGPS mf;

        private double antennaHeight, antennaOffset, antennaPivot, wheelbase, minTurningRadius, hydliftsecs, snapDistance;

        private bool isSteerAxleAhead;
        private int isPivotBehindAntenna, vehicleType, lightbarCmPerPixie, linewidth;

        //constructor
        public FormSettings(Form callingForm, int page)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();

            //Language keys
            tabSettings.Text = String.Get("gsAntenna");
            tabVehicle.Text = String.Get("gsVehicle");
            gboxAttachment.Text = String.Get("gsVehicleType");
            label6.Text = String.Get("gsTurnRadius");
            label26.Text = String.Get("gsWheelbase");
            label9.Text = String.Get("gsLeftIs-");
            label15.Text = String.Get("gsHeight");
            label7.Text = String.Get("gsOffset");
            label18.Text = String.Get("gsDistance");
            groupBox3.Text = String.Get("gsHydraulicLiftLookAhead");
            tabConfig.Text = String.Get("gsType");
            tabGuidance.Text = String.Get("gsGuidance");


            groupBox2.Text = String.Get("gsCmPerLightbarPixel");
            groupBox1.Text = String.Get("gs<<>>SnapDistance");
            label17.Text = String.Get("gsMeasurementsIn");
            groupBox4.Text = String.Get("gsGuidanceLineWidth");
            Text = String.Get("gsVehicleSettings");

            lblInchesCm.Text = mf.isMetric? String.Get("gsCentimeters") : String.Get("gsInches");

            //select the page as per calling menu or button from mainGPS form
            tabControl1.SelectedIndex = page;
        }

        //do any field initializing for form here
        private void FormSettings_Load(object sender, EventArgs e)
        {
            //TypeTab
            isPivotBehindAntenna = Properties.Vehicle.Default.setVehicle_isPivotBehindAntenna;
            isSteerAxleAhead = Properties.Vehicle.Default.setVehicle_isSteerAxleAhead;


            vehicleType = Properties.Vehicle.Default.VehicleType;

            //front page
            if (vehicleType == 0) rbtnTractor.Checked = true;
            else if (vehicleType == 1) rbtnHarvester.Checked = true;
            else rbtn4WD.Checked = true;

            FixRadioButtonsAndImages();

            //SettingsTab
            TboxAntennaPivot.Text = ((antennaPivot = Properties.Vehicle.Default.setVehicle_antennaPivot) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxAntennaPivot.CheckValue(ref antennaPivot, 0, 10);
            TboxAntennaHeight.Text = ((antennaHeight = Properties.Vehicle.Default.setVehicle_antennaHeight) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxAntennaHeight.CheckValue(ref antennaHeight, 0, 10);
            TboxWheelbase.Text = ((wheelbase = Math.Abs(Properties.Vehicle.Default.setVehicle_wheelbase)) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxWheelbase.CheckValue(ref wheelbase, 0, 20);
            TboxAntennaOffset.Text = ((antennaOffset = Properties.Vehicle.Default.setVehicle_antennaOffset) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxAntennaOffset.CheckValue(ref antennaOffset, -10, 10);



            //VehicleTab
            TboxMinTurnRadius.Text = ((minTurningRadius = Properties.Vehicle.Default.setVehicle_minTurningRadius) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxMinTurnRadius.CheckValue(ref minTurningRadius, 0.5, 100);

            TboxHydLiftSecs.Text = (hydliftsecs = Properties.Vehicle.Default.setVehicle_hydraulicLiftLookAhead).ToString("0.0#");
            TboxHydLiftSecs.CheckValue(ref hydliftsecs, 0, 20);

            //GuidanceTab
            TboxSnapDistance.Text = (snapDistance = Properties.Vehicle.Default.SnapOffsetDistance).ToString();
            TboxSnapDistance.CheckValue(ref snapDistance, 1, 50);
            TboxLightbarCmPerPixel.Text = (lightbarCmPerPixie = Properties.Settings.Default.setDisplay_lightbarCmPerPixel).ToString();
            TboxLightbarCmPerPixel.CheckValue(ref lightbarCmPerPixie, 1, 20);
            TboxLineWidth.Text = (linewidth = Properties.Settings.Default.setDisplay_lineWidth).ToString();
            TboxLineWidth.CheckValue(ref linewidth, 1, 8);

        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            //TypeTab
            Properties.Vehicle.Default.setVehicle_isPivotBehindAntenna = mf.vehicle.isPivotBehindAntenna = isPivotBehindAntenna;
            Properties.Vehicle.Default.setVehicle_isSteerAxleAhead = mf.vehicle.isSteerAxleAhead = isSteerAxleAhead;
            Properties.Vehicle.Default.VehicleType = mf.vehicle.VehicleType = vehicleType;

            //SettingsTab
            Properties.Vehicle.Default.setVehicle_antennaPivot = mf.vehicle.antennaPivot = antennaPivot * isPivotBehindAntenna;
            Properties.Vehicle.Default.setVehicle_antennaHeight = mf.vehicle.antennaHeight = antennaHeight;
            Properties.Vehicle.Default.setVehicle_wheelbase = mf.vehicle.wheelbase = wheelbase * (isSteerAxleAhead ? 1 : -1);
            Properties.Vehicle.Default.setVehicle_antennaOffset = mf.vehicle.antennaOffset = antennaOffset;

            //VehicleTab
            Properties.Vehicle.Default.setVehicle_hydraulicLiftLookAhead = mf.vehicle.hydLiftLookAheadTime = hydliftsecs;
            Properties.Vehicle.Default.setVehicle_minTurningRadius = mf.vehicle.minTurningRadius = minTurningRadius;

            if (Properties.Vehicle.Default.UturnTriggerDistance < mf.vehicle.minTurningRadius)
            {
                Properties.Vehicle.Default.UturnTriggerDistance = mf.vehicle.minTurningRadius;

                for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
                {
                    mf.StartTasks(mf.bnd.Boundaries[i], i, TaskName.TurnLine);
                }
            }

            //GuidanceTab
            Properties.Vehicle.Default.SnapOffsetDistance = snapDistance;
            Properties.Settings.Default.setDisplay_lightbarCmPerPixel = mf.lightbarCmPerPixel = lightbarCmPerPixie;
            Properties.Settings.Default.setDisplay_lineWidth = mf.lineWidth = linewidth;

            Properties.Settings.Default.Save();
            Properties.Vehicle.Default.Save();

            //back to FormGPS
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnChangeAttachment_Click(object sender, EventArgs e)
        {
            btnChangeAttachment.Enabled = false;
            btnChangeAttachment.BackColor = System.Drawing.Color.Transparent;

            FixRadioButtonsAndImages();
            tabControl1.SelectedTab = tabSettings;

            btnNext.Focus();

            isPivotBehindAntenna = vehicleType == 2 ? -1 : 1;//4WD
            isSteerAxleAhead = vehicleType == 0;//harvestor
        }


        private void Rbtn4WD_CheckedChanged(object sender, EventArgs e)
        {
            var radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                btnChangeAttachment.Enabled = true;
                btnChangeAttachment.BackColor = System.Drawing.SystemColors.ActiveCaption;
            }
        }

        private void FixRadioButtonsAndImages()
        {
            if (rbtnTractor.Checked)
            {
                vehicleType = 0;
                tabSettings.BackgroundImage = Properties.Resources.VehicleSettingsTractor;

            }
            else if (rbtnHarvester.Checked)
            {
                vehicleType = 1;
                tabSettings.BackgroundImage = Properties.Resources.VehicleSettingsHarvester;

            }
            else if (rbtn4WD.Checked)
            {
                vehicleType = 2;
                tabSettings.BackgroundImage = Properties.Resources.VehicleSettings4WD;
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        #region SettingsTab
        private void TboxAntennaPivot_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 10, antennaPivot, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxAntennaPivot.Text = ((antennaPivot = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxAntennaHeight_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 10, antennaHeight, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxAntennaHeight.Text = ((antennaHeight = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxWheelbase_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 20, wheelbase, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxWheelbase.Text = ((wheelbase = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxAntennaOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-10, 10, antennaOffset, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxAntennaOffset.Text = ((antennaOffset = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }
        #endregion SettingsTab
        #region VehicleTab
        private void TboxHydLiftSecs_Enter(object sender, EventArgs e)
        {

            using (var form = new FormNumeric(0, 20, hydliftsecs, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxHydLiftSecs.Text = (hydliftsecs = form.ReturnValue).ToString("0.0#");
                }
            }
            btnCancel.Focus();
        }

        private void TboxMinTurnRadius_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.5, 100, minTurningRadius, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxMinTurnRadius.Text = ((minTurningRadius = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }
        #endregion VehicleTab
        #region GuidanceTab
        private void TboxSnapDistance_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(1, 50, snapDistance, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxSnapDistance.Text = ((snapDistance = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxLightbarCmPerPixel_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(1, 20, lightbarCmPerPixie, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLightbarCmPerPixel.Text = (lightbarCmPerPixie = (int)form.ReturnValue).ToString();
                }
            }
            btnCancel.Focus();
        }

        private void TboxLineWidth_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(1, 8, linewidth, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLineWidth.Text = (linewidth = (int)form.ReturnValue).ToString();
                }
            }
            btnCancel.Focus();
        }
        #endregion GuidanceTab
    }
}