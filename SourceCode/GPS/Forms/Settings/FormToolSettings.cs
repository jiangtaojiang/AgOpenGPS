using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormToolSettings : Form
    {
        //class variables
        private readonly FormGPS mf;

        private ToolSettings setting = new ToolSettings();

        private double toolOverlap, toolOffset, SectionWidth;
        public int Here = 0, numberOfSections;

        public List<TextBox> Section = new List<TextBox>();
        public List<TextBox> Section2 = new List<TextBox>();
        public List<Label> SectionText = new List<Label>();
        public Label LastText = new Label();

        private bool scroll = false;
        private int Position = 0, oldX = 0;
        private double viewableRatio = 0, thumbWidth = 0;
        private readonly int SliderMaxWidth = 0, startscrollY = 0, startscrollX = 0, PanelWidth, PanelHeight;
        private int contentWidth = 0;
        private double ScrollCalc = 0;

        //constructor
        public FormToolSettings(Form callingForm, int page)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();

            //Language keys
            gboxAttachment.Text = String.Get("gsAttachmentStyle");
            tabHitch.Text = String.Get("gsHitch");
            tabConfig.Text = String.Get("gsConfiguration");
            tabSections.Text = String.Get("gsSection");
            tabSettings.Text = String.Get("gsSettings");

            label23.Text = String.Get("gsTurnOffDelaySecs");
            label8.Text = String.Get("gsTurnOffAheadSecs");
            label3.Text = String.Get("gsTurnOnAheadSecs");
            label41.Text = String.Get("gsMinUnapplied");
            label2.Text = String.Get("gs#OfSections");
            label4.Text = String.Get("gsSectionWidth");

            label30.Text = String.Get("gsSectionsTurnOffBelow");

            label17.Text = String.Get("gsMeasurementsIn");
            label16.Text = String.Get("gsToolWidth");

            Text = String.Get("gsToolSettings");

            if (mf.isMetric)
            {
                lblInchesCm.Text = String.Get("gsCentimeters");
                lblSecTotalWidthFeet.Visible = false;
                lblSecTotalWidthInches.Visible = false;
                lblSecTotalWidthMeters.Visible = true;
                lblDoNotExceed.Text = "* < 7500 cm*";
                lblTurnOffBelowUnits.Text = String.Get("gsKMH");
            }
            else
            {
                lblInchesCm.Text = String.Get("gsInches");
                lblSecTotalWidthFeet.Visible = true;
                lblSecTotalWidthInches.Visible = true;
                lblSecTotalWidthMeters.Visible = false;
                lblDoNotExceed.Text = "* < 2952,7559 inches *";
                lblTurnOffBelowUnits.Text = String.Get("gsMPH");
            }

            SliderMaxWidth = Slider_Scroll.Size.Width;
            startscrollY = Slider_Scroll.Location.Y;
            startscrollX = Slider_Scroll.Location.X;
            PanelWidth = SectionPanel.Size.Width;
            PanelHeight = SectionPanel.Size.Height;
            //select the page as per calling menu or button from mainGPS form
            tabControl1.SelectedIndex = page;
        }

        //do any field initializing for form here
        private void FormToolSettings_Load(object sender, EventArgs e)
        {
            mf.CheckToolSettings();

            setting = new ToolSettings(Properties.Vehicle.Default.ToolSettings[Here]);

            //HitchTab
            TboxHitchLength.Text = (setting.HitchLength * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxHitchLength.CheckValue(ref setting.HitchLength, 0, 20);

            TboxTankWheelLength.Text = (setting.TankWheelLength * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxTankWheelLength.CheckValue(ref setting.TankWheelLength, 0, 20);
            TboxTankHitchLength.Text = (setting.TankHitchLength * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxTankHitchLength.CheckValue(ref setting.TankHitchLength, 0, 20);

            TboxToolWheelLength.Text = (setting.ToolWheelLength * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxToolWheelLength.CheckValue(ref setting.ToolWheelLength, 0, 20);
            TboxToolHitchLength.Text = (setting.ToolHitchLength * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxToolHitchLength.CheckValue(ref setting.ToolHitchLength, -20, 20);

            //SettingsTab
            TboxOverlap.Text = ((toolOverlap = Properties.Vehicle.Default.GuidanceOverlap) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxOverlap.CheckValue(ref toolOverlap, -25, 25);
            TboxOffset.Text = ((toolOffset = Properties.Vehicle.Default.GuidanceOffset) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxOffset.CheckValue(ref toolOffset, -25, 25);


            TboxCutoffSpeed.Text = (setting.SlowSpeedCutoff * mf.Kmh2Unit).ToString(mf.GuiFix);
            TboxCutoffSpeed.CheckValue(ref setting.SlowSpeedCutoff, 0, 30);




            TboxLookAheadOn.Text = setting.LookAheadOn.ToString("0.0#");
            TboxMappingOnDelay.Text = setting.MappingOnDelay.ToString("0.0#");
            TboxLookAheadOff.Text = setting.LookAheadOff.ToString("0.0#");
            TboxMappingOffDelay.Text = setting.MappingOffDelay.ToString("0.0#");
            TboxTurnOffDelay.Text = setting.TurnOffDelay.ToString("0.0#");

            //SectionsTab
            TboxSectionWidth.Text = ((SectionWidth = Properties.Vehicle.Default.setTool_defaultSectionWidth) * mf.Mtr2Unit).ToString(mf.GuiFix);
            TboxMinApplied.Text = setting.MinApplied.ToString();
            TboxNumSections.Text = (numberOfSections = Properties.Vehicle.Default.ToolSettings[Here].Sections.Count).ToString();

            rbtnTBT.Checked = false;
            rbtnTrailing.Checked = false;
            rbtnFixedRear.Checked = false;
            rbtnFront.Checked = false;

            if (!setting.BehindPivot) rbtnFront.Checked = true;
            else if (setting.TBT) rbtnTBT.Checked = true;
            else if (!setting.Trailing) rbtnFixedRear.Checked = true;
            else rbtnTrailing.Checked = true;

            btnChangeAttachment.Enabled = false;

            FixRadioButtonsAndImages();


            //based on number of sections and values update the page before displaying
            UpdateNumberOfSections();

            btnChangeAttachment.BackColor = Color.Transparent;
            btnChangeAttachment.Enabled = false;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            mf.CheckToolSettings();
            double ToolWidth = 0;
            for (int i = 0; i < Section.Count; i++)
            {
                ToolWidth += Math.Round(Convert.ToDouble(Section[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
                if (i < Section2.Count)
                    ToolWidth += Math.Round(Convert.ToDouble(Section2[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
            }

            if (ToolWidth > 75) return;

            //Tool  ---------------------------------------------------------
            if (Here > Properties.Vehicle.Default.ToolSettings.Count) Here = 0;

            Properties.Vehicle.Default.ToolSettings[Here] = setting;

            Properties.Vehicle.Default.GuidanceOverlap = mf.Guidance.GuidanceOverlap = toolOverlap;
            Properties.Vehicle.Default.GuidanceOffset = mf.Guidance.GuidanceOffset = toolOffset;
            Properties.Vehicle.Default.GuidanceWidth = mf.Guidance.GuidanceWidth = Math.Round(ToolWidth, 2);
            mf.Guidance.WidthMinusOverlap = mf.Guidance.GuidanceWidth - mf.Guidance.GuidanceOverlap;


            List<double[]> aa = new List<double[]>();

            double LeftPos = ToolWidth / -2;
            LeftPos = Math.Round(LeftPos + toolOffset, 2);

            //save the values in each spinner for section position widths in settings
            for (int i = 0; i < Section.Count; i++)
            {
                double size = Math.Round(Convert.ToDouble(Section[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
                aa.Add(new double[] { LeftPos, size, 0 });
                LeftPos += size;
                if (i < Section2.Count)
                {
                    LeftPos += Math.Round(Convert.ToDouble(Section2[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
                }
            }
            Properties.Vehicle.Default.ToolSettings[Here].Sections = aa;
            Properties.Vehicle.Default.setTool_defaultSectionWidth = SectionWidth;

            Properties.Vehicle.Default.Save();

            mf.LoadTools();

            //back to FormGPS
            DialogResult = DialogResult.OK;
            Close();
        }
               
        private void RbtnFront_CheckedChanged(object sender, EventArgs e)
        {
            var radioButton = sender as RadioButton;

            // Only do something when the event was raised by the radiobutton 
            // being checked, so we don't do this twice.
            if (radioButton.Checked)
            {
                btnChangeAttachment.Enabled = true;
                btnChangeAttachment.BackColor = SystemColors.ActiveCaption;
            }
        }

        private void FixRadioButtonsAndImages()
        {
            if (rbtnTrailing.Checked)
            {
                setting.BehindPivot = true;
                setting.Trailing = true;
                setting.TBT = false;

                TboxTankWheelLength.Visible = false;
                TboxTankHitchLength.Visible = false;
                TboxToolWheelLength.Visible = true;
                TboxToolHitchLength.Visible = true;

                TboxHitchLength.Left = 384;
                TboxToolWheelLength.Left = 596;
                TboxToolHitchLength.Left = 696;

                tabHitch.BackgroundImage = Properties.Resources.ToolHitchPageTrailing;
            }
            else if (rbtnFixedRear.Checked)
            {
                setting.BehindPivot = true;
                setting.Trailing = false;
                setting.TBT = false;

                TboxTankWheelLength.Visible = false;
                TboxTankHitchLength.Visible = false;
                TboxToolWheelLength.Visible = false;
                TboxToolHitchLength.Visible = false;

                TboxHitchLength.Left = 443;

                tabHitch.BackgroundImage = Properties.Resources.ToolHitchPageRear;
            }
            else if (rbtnFront.Checked)
            {
                setting.BehindPivot = false;
                setting.Trailing = false;
                setting.TBT = false;

                TboxTankWheelLength.Visible = false;
                TboxTankHitchLength.Visible = false;
                TboxToolWheelLength.Visible = false;
                TboxToolHitchLength.Visible = false;

                TboxHitchLength.Left = 384;

                tabHitch.BackgroundImage = Properties.Resources.ToolHitchPageFront;
            }
            else //TBT
            {
                setting.BehindPivot = true;
                setting.Trailing = true;
                setting.TBT = true;

                TboxToolWheelLength.Visible = true;
                TboxToolHitchLength.Visible = true;
                TboxTankWheelLength.Visible = true;
                TboxTankHitchLength.Visible = true;

                TboxHitchLength.Left = 283;
                TboxTankWheelLength.Left = 486;
                TboxTankHitchLength.Left = 586;
                TboxToolWheelLength.Left = 700;
                TboxToolHitchLength.Left = 800;

                tabHitch.BackgroundImage = Properties.Resources.ToolHitchPageTBT;
            }
        }

        private void BtnChangeAttachment_Click(object sender, EventArgs e)
        {
            btnChangeAttachment.Enabled = false;
            btnChangeAttachment.BackColor = Color.Transparent;
            FixRadioButtonsAndImages();
            tabControl1.SelectedTab = tabHitch;
            btnNext.Focus();
        }

        #region Sections //---------------------------------------------------------------

        public void UpdateSpinners()
        {
            double Width = 0;

            for (int i = 0; i < Section.Count; i++)
            {
                Width += Math.Round(Convert.ToDouble(Section[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
                if (i < Section2.Count)
                    Width += Math.Round(Convert.ToDouble(Section2[i].Text) * mf.Unit2Mtr, 2, MidpointRounding.AwayFromZero);
            }
            if (Width > 75) SectionPanel.BackColor = Color.Red;
            else SectionPanel.BackColor = SystemColors.Window;

            //update in settings dialog ONLY total tool width
            if (mf.isMetric)
            {
                lblSecTotalWidthMeters.Text = Width.ToString(mf.GuiFix) + " mtr";
            }
            else
            {
                double toFeet = (double)Width * 0.08333333334;
                lblSecTotalWidthFeet.Text = Convert.ToString((int)toFeet) + "'";
                lblSecTotalWidthInches.Text = Math.Round((toFeet - Math.Truncate(toFeet)) * 12, 0).ToString() + '"';
            }
        }

        private void TboxToolWheelLength_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 20, setting.ToolWheelLength, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxToolWheelLength.Text = ((setting.ToolWheelLength = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxTankWheelLength_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 20, setting.TankWheelLength, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxTankWheelLength.Text = ((setting.TankWheelLength = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            numberOfSections = 0;
            UpdateNumberOfSections();

            Here = (Here - 1).Clamp(mf.Tools.Count);
            FormToolSettings_Load(null, null);
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            numberOfSections = 0;
            UpdateNumberOfSections();

            Here = (Here + 1).Clamp(mf.Tools.Count);
            FormToolSettings_Load(null, null);
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            numberOfSections = 0;
            UpdateNumberOfSections();

            Properties.Vehicle.Default.ToolSettings.Add(new ToolSettings());
            Properties.Vehicle.Default.Save();

            mf.Tools.Add(new CTool(mf, mf.Tools.Count));
            Here = (Here + 1).Clamp(mf.Tools.Count);
            FormToolSettings_Load(null, null);
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            if (mf.Tools.Count > 1)
            {
                numberOfSections = 0;
                UpdateNumberOfSections();
                Properties.Vehicle.Default.ToolSettings.RemoveAt(Here);
                Properties.Vehicle.Default.Save();

                mf.Tools.RemoveAt(Here);
                Here = (Here).Clamp(mf.Tools.Count);
                FormToolSettings_Load(null, null);
            }
        }

        private void TboxTankHitchLength_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 20, setting.TankHitchLength, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxTankHitchLength.Text = ((setting.TankHitchLength = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxToolHitchLength_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-20, 20, setting.ToolHitchLength, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxToolHitchLength.Text = ((setting.ToolHitchLength = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void Section_Enter(object sender, EventArgs e)
        {
            if (sender is TextBox b)
            {
                using (var form = new FormNumeric(0.05, 75, Convert.ToDouble(b.Text) * mf.Unit2Mtr, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
                {
                    var result = form.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        b.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix);
                        UpdateSpinners();
                    }
                }
                btnCancel.Focus();
            }
        }

        private void Section_Enter2(object sender, EventArgs e)
        {
            if (sender is TextBox b)
            {
                using (var form = new FormNumeric(0, 74.9, Convert.ToDouble(b.Text) * mf.Unit2Mtr, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
                {
                    var result = form.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        b.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix);

                        UpdateSpinners();
                    }
                }
                btnCancel.Focus();
            }
        }

        private void TboxLookAheadOn_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.2, 5, setting.LookAheadOn, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLookAheadOn.Text = (setting.LookAheadOn = form.ReturnValue).ToString("0.0#");

                    if (setting.LookAheadOff > (setting.LookAheadOn * 0.8))
                    {
                        TboxLookAheadOff.Text = (setting.LookAheadOff = Math.Round(setting.LookAheadOn * 0.8, 2)).ToString("0.0#");
                    }
                }
            }
            btnCancel.Focus();
        }

        private void TboxLookAheadOff_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 4, setting.LookAheadOff, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLookAheadOff.Text = (setting.LookAheadOff = form.ReturnValue).ToString("0.0#");

                    if (setting.LookAheadOff > (setting.LookAheadOn * 0.8))
                    {
                        TboxLookAheadOff.Text = (setting.LookAheadOff = Math.Round(setting.LookAheadOn * 0.8, 2)).ToString("0.0#");
                    }
                    if (setting.LookAheadOff > 0)
                    {
                        TboxTurnOffDelay.Text = (setting.TurnOffDelay = 0).ToString("0.0#");
                    }
                }
            }
            btnCancel.Focus();
        }

        private void TboxHitchLength_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 20, setting.HitchLength, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxHitchLength.Text = ((setting.HitchLength = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxMappingOnDelay_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 5, setting.MappingOnDelay, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxMappingOnDelay.Text = (setting.MappingOnDelay = form.ReturnValue).ToString("0.0#");
                }
            }
            btnCancel.Focus();
        }

        private void TboxMappingOffDelay_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 5, setting.MappingOffDelay, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxMappingOffDelay.Text = (setting.MappingOffDelay = form.ReturnValue).ToString("0.0#");
                }
            }
            btnCancel.Focus();
        }

        private void TboxTurnOffDelay_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 5, setting.TurnOffDelay, this, 2, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxMappingOffDelay.Text = (setting.TurnOffDelay = form.ReturnValue).ToString("0.0#");

                    if (setting.TurnOffDelay > 0)
                    {
                       TboxLookAheadOff.Text = (setting.LookAheadOff = 0).ToString("0.0#");
                    }
                }
            }
            btnCancel.Focus();
        }

        private void TboxOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-25, 25, toolOffset, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxOffset.Text = ((toolOffset = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxCutoffSpeed_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 30, setting.SlowSpeedCutoff, this, 2, true, mf.Unit2Kmh, mf.Kmh2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxCutoffSpeed.Text = ((setting.SlowSpeedCutoff = form.ReturnValue) * mf.Kmh2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        private void TboxOverlap_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-25, 25, toolOverlap, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxOverlap.Text = ((toolOverlap = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnCancel.Focus();
        }

        #endregion Sections //---------------------------------------------------------------

        #region TextBox

        private void TboxSectionWidth_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.05, 75, SectionWidth, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    SectionWidth = form.ReturnValue;
                    double a = SectionWidth;

                    while (numberOfSections * SectionWidth > 75)
                    {
                        SectionWidth = Math.Round(a / numberOfSections, mf.Decimals);
                        a -= 0.01;
                    }

                    TboxSectionWidth.Text = (SectionWidth * mf.Mtr2Unit).ToString(mf.GuiFix);

                    for (int i = 0; i < numberOfSections; i++)
                    {
                        Section[i].Text = (SectionWidth * mf.Mtr2Unit).ToString(mf.GuiFix);
                        if (i < Section2.Count)
                            Section2[i].Text = "0";
                    }
                    UpdateSpinners();
                }
            }
            btnCancel.Focus();
        }

        private void TboxMinApplied_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 100, setting.MinApplied, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxMinApplied.Text = (setting.MinApplied = (int)form.ReturnValue).ToString();
                }
            }
            btnCancel.Focus();
        }

        private void Slider_Scroll_MouseDown(object sender, MouseEventArgs e)
        {
            oldX = MousePosition.X;
            scroll = true;
        }

        private void Slider_Scroll_Mouse(object sender, EventArgs e)
        {
            scroll = false;
        }

        private void Slider_Scroll_MouseMove(object sender, MouseEventArgs e)
        {
            if (scroll == true && viewableRatio < 1)
            {
                if (oldX != MousePosition.X)
                {
                    int diff = MousePosition.X - oldX;

                    if (Slider_Scroll.Location.X + diff > startscrollX)
                    {
                        if ((Slider_Scroll.Location.X + diff) < (startscrollX + SliderMaxWidth - thumbWidth))
                        {
                            oldX += diff;
                            UpdateScroll(Slider_Scroll.Location.X + diff - startscrollX);
                        }
                        else
                        {
                            int tt = (int)((Slider_Scroll.Location.X + diff) - (startscrollX + SliderMaxWidth - thumbWidth));

                            oldX += diff - tt;


                            UpdateScroll(SliderMaxWidth - thumbWidth);
                        }
                    }
                    else
                    {
                        int tt = Slider_Scroll.Location.X + diff - startscrollX;

                        oldX += diff - tt;

                        UpdateScroll(0);
                    }
                }
            }
        }

        public void UpdateScroll(double pos)
        {
            if (viewableRatio >= 1)
            {
                Slider_Scroll.Size = new Size(SliderMaxWidth, 50);
                Slider_Scroll.Location = new Point(startscrollX, startscrollY);
                Position = 0;
            }
            else if (pos < 0)
                Slider_Scroll.Location = new Point((int)(startscrollX + Position / ScrollCalc + 0.5), startscrollY);
            else
            {
                Slider_Scroll.Location = new Point((int)(startscrollX + pos), startscrollY);
                Position = (int)(pos * ScrollCalc + 0.5);
            }

            SectionPanel.AutoScrollPosition = new Point(Position,0);
            SectionPanel.Invalidate();
            SectionPanel.PerformLayout();
        }

        void MouseWheel_Scroll(object sender, MouseEventArgs e)
        {
            Position -= e.Delta;
            if (Position < 0) Position = 0;
            else if (Position > contentWidth - SectionPanel.Size.Width) Position = contentWidth - SectionPanel.Size.Width;
            UpdateScroll(-1);
        }

        private void Right_Scroll_Click(object sender, EventArgs e)
        {
            Position += 150;
            if (Position > contentWidth - SectionPanel.Size.Width) Position = contentWidth - SectionPanel.Size.Width;
            UpdateScroll(-1);
        }

        private void Left_Scroll_Click(object sender, EventArgs e)
        {
            Position -= 150;
            if (Position < 0) Position = 0;
            UpdateScroll(-1);
        }

        private void TboxNumSections_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 100, numberOfSections, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxNumSections.Text = (numberOfSections = (int)form.ReturnValue).ToString();
                    UpdateNumberOfSections();
                }
            }
            btnCancel.Focus();
        }

        private void UpdateNumberOfSections()
        {
            while (Section.Count > numberOfSections)
            {
                Section[Section.Count - 1].Dispose();
                SectionText[SectionText.Count - 1].Dispose();
                Section.RemoveAt(Section.Count - 1);
                SectionText.RemoveAt(SectionText.Count - 1);
            }
            while (Section2.Count > numberOfSections-1 && Section2.Count > 0)
            {
                Section2[Section2.Count - 1].Dispose();
                Section2.RemoveAt(Section2.Count - 1);
            }

            int StartWidth = SectionPanel.AutoScrollPosition.X;

            while (Section.Count < numberOfSections)
            {
                Label newlabel = new Label();
                SectionPanel.Controls.Add(newlabel);
                newlabel.TextAlign = ContentAlignment.MiddleCenter;
                newlabel.Size = new Size(120, 50);
                newlabel.Font = new Font("Tahoma", 20F, FontStyle.Bold, GraphicsUnit.Point, 0);
                newlabel.Text = (Section.Count + 1).ToString();
                newlabel.Location = new Point(StartWidth + 30 + 150 * Section.Count, 0);

                SectionText.Add(newlabel);

                TextBox newTextBox = new TextBox();
                SectionPanel.Controls.Add(newTextBox);
                newTextBox.TextAlign = HorizontalAlignment.Center;
                newTextBox.Size = new Size(120, 50);
                newTextBox.Font = new Font("Tahoma", 27.75F, FontStyle.Regular, GraphicsUnit.Point, 0);

                if (Section.Count < setting.Sections.Count)
                {
                    newTextBox.Text = Math.Round(setting.Sections[Section.Count][1] * mf.Mtr2Unit, mf.Decimals).ToString(mf.GuiFix);
                }
                else
                    newTextBox.Text = Math.Round(Properties.Vehicle.Default.setTool_defaultSectionWidth * mf.Mtr2Unit, mf.Decimals).ToString(mf.GuiFix);

                newTextBox.Location = new Point(StartWidth + 30 + 150 * Section.Count, 60);

                newTextBox.Enter += Section_Enter;
                Section.Add(newTextBox);

                if (Section2.Count < numberOfSections - 1)
                {
                    newTextBox = new TextBox();
                    SectionPanel.Controls.Add(newTextBox);
                    newTextBox.TextAlign = HorizontalAlignment.Center;
                    newTextBox.Size = new Size(120, 50);
                    newTextBox.Font = new Font("Tahoma", 27.75F, FontStyle.Regular, GraphicsUnit.Point, 0);

                    if (Section2.Count + 1 < setting.Sections.Count)
                    {
                        double tt2 = setting.Sections[Section2.Count + 1][0] - setting.Sections[Section2.Count][0];

                        newTextBox.Text = Math.Round((tt2 - setting.Sections[Section2.Count][1]) * mf.Mtr2Unit, mf.Decimals).ToString(mf.GuiFix);
                    }
                    else newTextBox.Text = "0";

                    newTextBox.Location = new Point(StartWidth + 105 + 150 * Section2.Count, 120);

                    newTextBox.Enter += Section_Enter2;
                    Section2.Add(newTextBox);
                }
            }
            UpdateSpinners();

            contentWidth = 150 * numberOfSections + 30;
            viewableRatio = PanelWidth / (double)contentWidth;

            thumbWidth = (SliderMaxWidth * viewableRatio < 100) ? 100 : (SliderMaxWidth * viewableRatio);
            Slider_Scroll.Size = new Size((int)(thumbWidth + 0.5), 50);

            ScrollCalc = (contentWidth - PanelWidth) / (SliderMaxWidth - thumbWidth);

            if (viewableRatio >= 1)
            {
                Slider_Scroll.Visible = false;
                Right_Scroll.Visible = false;
                Left_Scroll.Visible = false;
                Position = 0;
                int X = (PanelWidth - contentWidth) / 2;
                SectionPanel.Location = new Point(X, 0);
                SectionPanel.Size = new Size(PanelWidth - X, PanelHeight);
            }
            else
            {
                Slider_Scroll.Visible = true;
                Right_Scroll.Visible = true;
                Left_Scroll.Visible = true;
                SectionPanel.Location = new Point(0, 0);
                SectionPanel.Size = new Size(PanelWidth, PanelHeight);
            }

            SectionPanel.Controls.Add(LastText);
            LastText.Size = new Size(150, 50);
            LastText.Location = new Point(StartWidth + 30 + 150 * Section.Count, 150);
            LastText.Visible = true;

            SectionPanel.HorizontalScroll.Value = 1;
            SectionPanel.AutoScroll = false;
            SectionPanel.HorizontalScroll.Visible = false;
            SectionPanel.HorizontalScroll.Enabled = true;
            SectionPanel.HorizontalScroll.Maximum = Math.Max(contentWidth - SectionPanel.Size.Width, SectionPanel.Size.Width);
            SectionPanel.HorizontalScroll.Value = 0;

            if (Position < 0) Position = 0;
            else if (Position > contentWidth - SectionPanel.Size.Width) Position = contentWidth - SectionPanel.Size.Width;

            UpdateScroll(-1);
        }

        # endregion TextBox
    }
}
