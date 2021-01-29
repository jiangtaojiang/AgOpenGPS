//Please, if you use this, share the improvements
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        // Buttons //-----------------------------------------------------------------------
        private byte TimerMode = 0;
        private void btnStartStopNtrip_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.setNTRIP_isOn = isNTRIP_TurnedOn = !isNTRIP_TurnedOn;
            Properties.Settings.Default.Save();

            UpdateNtripButton();
        }

        private void goPathMenu_Click(object sender, EventArgs e)
        {
            if (bnd.Boundaries.Count == 0)
            {
                TimedMessageBox(2000, String.Get("gsNoBoundary"), String.Get("gsCreateABoundaryFirst"));
                return;
            }

            isAutoSteerBtnOn = false;
            btnAutoSteer.Image = Properties.Resources.AutoSteerOff;
            if (Guidance.isYouTurnBtnOn) btnAutoYouTurn.PerformClick();

            snapLeftBigStrip.Enabled = false;
            snapRightBigStrip.Enabled = false;
            snapToCurrent.Enabled = false;

            YouTurnButtons(false);

            Guidance.BtnGuidanceOn = false;
            btnGuidance.Image = Properties.Resources.CurveOff;

            Guidance.isOkToAddPoints = false;
            Guidance.isEditing = false;

            if (!recPath.isPausedDrivingRecordedPath)
            {
                //already running?
                if (recPath.isDrivingRecordedPath)
                {
                    recPath.StopDrivingRecordedPath();
                    return;
                }

                //start the recorded path driving process



                if (!recPath.StartDrivingRecordedPath())
                {
                    //Cancel the recPath - something went seriously wrong
                    recPath.StopDrivingRecordedPath();
                    TimedMessageBox(1500, String.Get("gsProblemMakingPath"), String.Get("gsCouldntGenerateValidPath"));
                }
                else
                {
                    goPathMenu.Image = Properties.Resources.AutoStop;
                }
            }
            else
            {
                recPath.isPausedDrivingRecordedPath = false;
                pausePathMenu.BackColor = Color.Lime;
            }
        }

        private void PausePathMenu_Click(object sender, EventArgs e)
        {
            if (recPath.isPausedDrivingRecordedPath)
            {
                //btnPauseDrivingPath.BackColor = Color.Lime;
                pausePathMenu.BackColor = Color.Lime;
            }
            else
            {
                //btnPauseDrivingPath.BackColor = Color.OrangeRed;
                pausePathMenu.BackColor = Color.OrangeRed;
            }

            recPath.isPausedDrivingRecordedPath = !recPath.isPausedDrivingRecordedPath;

        }

        private void RecordPathMenu_Click(object sender, EventArgs e)
        {
            if (recPath.isRecordOn)
            {
                FileSaveRecPath();
                recPath.isRecordOn = false;
                recordPathMenu.Image = Properties.Resources.BoundaryRecord;
            }
            else if (isJobStarted)
            {
                recPath.recList.Clear();
                recPath.isRecordOn = true;
                recordPathMenu.Image = Properties.Resources.boundaryStop;
            }
        }

        private void DeletePathMenu_Click(object sender, EventArgs e)
        {
            recPath.recList.Clear();
            recPath.StopDrivingRecordedPath();
            FileSaveRecPath();
        }

        //LIDAR control
        private void btnAutoSteer_Click(object sender, EventArgs e)
        {
            System.Media.SystemSounds.Question.Play();

            //new direction so reset where to put turn diagnostic
            Guidance.ResetCreatedYouTurn();

            if (isAutoSteerBtnOn)
            {
                isAutoSteerBtnOn = false;
                btnAutoSteer.Image = Properties.Resources.AutoSteerOff;
                mc.Send_AutoSteerButton[5] = 0x00;
                UpdateSendDataText("Auto Steer Button: State Off");
                SendData(mc.Send_AutoSteerButton, false);
            }
            else
            {
                if (Guidance.BtnGuidanceOn)
                {
                    isAutoSteerBtnOn = true;
                    btnAutoSteer.Image = Properties.Resources.AutoSteerOn;
                    mc.Send_AutoSteerButton[5] = 0x01;
                    UpdateSendDataText("Auto Steer Button: State On");
                    SendData(mc.Send_AutoSteerButton, false);
                }
                else
                {
                    TimedMessageBox(2000, String.Get("gsNoGuidanceLines"), String.Get("gsTurnOnContourOrMakeABLine"));
                }
            }
        }

        private void BtnMakeLinesFromBoundary_Click(object sender, EventArgs e)
        {
            if (bnd.Boundaries.Count == 0)
            {
                TimedMessageBox(2000, String.Get("gsNoBoundary"), String.Get("gsCreateABoundaryFirst"));
                return;
            }

            Guidance.isOkToAddPoints = false;
            Guidance.isEditing = false;

            Form f = Application.OpenForms["FormABDraw"];
            if (f != null)
            {
                f.Focus();
                return;
            }
            Form form = new FormGuidanceDraw(this, false);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void btnCycleLines_Click(object sender, EventArgs e)
        {
            if (Guidance.Lines.Count > 0)
            {
                Guidance.ResetABLine = true;
                Guidance.CurrentLine = (Guidance.CurrentLine + 1 >= Guidance.Lines.Count) ? -1 : Guidance.CurrentLine + 1;
                
                Guidance.ExtraGuidanceLines.Clear();
                Properties.Settings.Default.LastGuidanceLine = Guidance.CurrentLine;
                Properties.Settings.Default.Save();
                Guidance.ResetYouTurn();

                if (Guidance.CurrentLine == -1)
                {
                    btnGuidance.Image = Properties.Resources.CurveOff;
                    btnCycleLines.Text = String.Get("gsOff");
                    Guidance.BtnGuidanceOn = false;
                }
                else
                {
                    btnGuidance.Image = Properties.Resources.GuidanceCurve;
                    btnCycleLines.Text = (Guidance.CurrentLine + 1).ToString() + " of " + Guidance.Lines.Count.ToString();
                    Guidance.BtnGuidanceOn = true;
                }

                YouTurnButtons(Guidance.BtnGuidanceOn);
                snapLeftBigStrip.Enabled = snapRightBigStrip.Enabled = snapToCurrent.Enabled = Guidance.BtnGuidanceOn;
            }
        }

        private void btnGuidance_Click(object sender, EventArgs e)
        {
            //check if window already exists
            Form f = Application.OpenForms["FormABCurve"];

            if (f != null)
            {
                f.Focus();
                return;
            }
            else
            {
                if (!Guidance.BtnGuidanceOn && Properties.Settings.Default.LastGuidanceLine > -1)
                {
                    Guidance.ResetABLine = true;
                    Guidance.CurrentLine = Properties.Settings.Default.LastGuidanceLine;
                    Guidance.ExtraGuidanceLines.Clear();
                    btnGuidance.Image = Properties.Resources.GuidanceCurve;
                    Guidance.BtnGuidanceOn = true;

                    if (Guidance.CurrentLine == -1) btnCycleLines.Text = String.Get("gsOff");
                    else btnCycleLines.Text = (Guidance.CurrentLine + 1).ToString() +
                            " of " + Guidance.Lines.Count.ToString();
                }
                else
                {
                    Form form = new FormABCurve(this);
                    form.Show(this);
                    form.Left = Left + Width / 2 - form.Width / 2;
                    form.Top = Top + Height / 2 - form.Height / 2;
                }
                snapLeftBigStrip.Enabled = snapRightBigStrip.Enabled = snapToCurrent.Enabled = Guidance.BtnGuidanceOn;
            }
            YouTurnButtons(true);
        }

        private void btnSnapCurrent_Click(object sender, EventArgs e)
        {
            if (Guidance.BtnGuidanceOn)
            {
                Guidance.MoveLine(Guidance.CurrentLine, Guidance.isSameWay ? -Guidance.distanceFromRefLine : Guidance.distanceFromRefLine);
                Guidance.distanceFromRefLine = 0;
            }
            else
            {
                TimedMessageBox(2000, String.Get("gsNoGuidanceLines"), String.Get("gsTurnOnContourOrMakeABLine"));
            }
        }

        private void btnSnapRight_Click(object sender, EventArgs e)
        {
            Guidance.ResetCreatedYouTurn();
            double dist = 0.01 * Properties.Vehicle.Default.SnapOffsetDistance;
            Guidance.MoveLine(Guidance.CurrentLine, Guidance.isSameWay ? dist : -dist);
        }

        private void btnSnapLeft_Click(object sender, EventArgs e)
        {
            Guidance.ResetCreatedYouTurn();
            double dist = 0.01 * Properties.Vehicle.Default.SnapOffsetDistance;
            Guidance.MoveLine(Guidance.CurrentLine, Guidance.isSameWay ? -dist : dist);

        }

        //Section Manual and Auto
        private void btnSection_Click(object sender, EventArgs e)
        {
            Button sa = sender as Button;
            if (sa.Name == "btnAutoSection")
            {
                System.Media.SystemSounds.Exclamation.Play();
                if (autoBtnState == btnStates.Auto) autoBtnState = btnStates.Off;
                else autoBtnState = btnStates.Auto;
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
                if (autoBtnState == btnStates.On) autoBtnState = btnStates.Off;
                else autoBtnState = btnStates.On;
            }
            btnSection_Update();
        }

        public void btnSection_Update()
        {
            btnManualSection.Image = autoBtnState == btnStates.On ? Properties.Resources.ManualOn : Properties.Resources.ManualOff;
            btnAutoSection.Image = autoBtnState == btnStates.Auto ? Properties.Resources.SectionMasterOn : Properties.Resources.SectionMasterOff;

            for (int i = 0; i < Tools.Count; i++)
            {
                //turn section buttons all On/OFF/Auto
                for (int j = 0; j < Tools[i].numOfSections; j++)
                {
                    Tools[i].Sections[j].BtnSectionState = autoBtnState;
                    Tools[i].Sections[j].SectionButton.Enabled = isJobStarted;
                    Tools[i].SectionButtonColor(j);
                }
            }
        }

        //individual buttons for sections
        public void btnSectionMan_Click(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (autoBtnState != btnStates.Off)
                {
                    string[] words = btn.Name.Split(',');

                    int toolNumber = Convert.ToInt32(words[0]);
                    int sectNumber = Convert.ToInt32(words[1]);

                    //if auto is off just have on-off for choices of section buttons
                    if (Tools[toolNumber].Sections[sectNumber].BtnSectionState == btnStates.Off)
                    {
                        if (autoBtnState != btnStates.Auto) Tools[toolNumber].Sections[sectNumber].BtnSectionState = btnStates.On;
                        else Tools[toolNumber].Sections[sectNumber].BtnSectionState = btnStates.Auto;
                    }
                    else if (Tools[toolNumber].Sections[sectNumber].BtnSectionState == btnStates.Auto) Tools[toolNumber].Sections[sectNumber].BtnSectionState = btnStates.On;
                    else if (Tools[toolNumber].Sections[sectNumber].BtnSectionState == btnStates.On) Tools[toolNumber].Sections[sectNumber].BtnSectionState = btnStates.Off;

                    Tools[toolNumber].SectionButtonColor(sectNumber);
                }
            }
        }

        //The zoom tilt buttons
        private void btnZoomIn_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 0;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnZoomOut_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 1;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnpTiltUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 2;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnpTiltDown_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 3;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnMoveDown_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 4;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnMoveUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 5;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void btnMoveLeft_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 6;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnMoveRight_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 7;
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
                if (camera.zoomValue <= 20) camera.zoomValue += camera.zoomValue * 0.1;
                else camera.zoomValue += camera.zoomValue * 0.05;
                if (camera.zoomValue > 220) camera.zoomValue = 220;
                camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
                SetZoom(camera.camSetDistance);
            }
            else if (TimerMode == 1)
            {
                if (camera.zoomValue <= 20)
                { if ((camera.zoomValue -= camera.zoomValue * 0.1) < 6.0) camera.zoomValue = 6.0; }
                else { if ((camera.zoomValue -= camera.zoomValue * 0.05) < 6.0) camera.zoomValue = 6.0; }
                camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
                SetZoom(camera.camSetDistance);
            }
            else if (TimerMode == 2)
            {
                if (++camera.camPitch > 0) camera.camPitch = 0;
            }
            else if (TimerMode == 3)
            {
                if (--camera.camPitch < -80) camera.camPitch = -80;
            }
            else if (TimerMode == 4)
            {
                offX -= (Math.Sin(fixHeading) * 10);
                offY -= (Math.Cos(fixHeading) * 10);
            }
            else if (TimerMode == 5)
            {
                offX += (Math.Sin(fixHeading) * 10);
                offY += (Math.Cos(fixHeading) * 10);
            }
            else if (TimerMode == 6)
            {
                offX -= (Math.Cos(fixHeading) * 10);
                offY -= (Math.Sin(fixHeading) * 10);
            }
            else if (TimerMode == 7)
            {
                offX += (Math.Cos(fixHeading) * 10);
                offY += (Math.Sin(fixHeading) * 10);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            fixHeading += Math.PI;
            if (fixHeading > Glm.twoPI) fixHeading -= Glm.twoPI;
            gyroCorrection -= 180;
            if (gyroCorrection < 0) gyroCorrection += 360;

            if (vehicle.antennaOffset != 0)
            {
                for (int i = 0; i < totalFixSteps; i++)
                {
                    stepFixPts[i].Northing += (Math.Sin(fixHeading) * vehicle.antennaOffset *2);
                    stepFixPts[i].Easting -= (Math.Cos(fixHeading) * vehicle.antennaOffset * 2);
                }
            }

            if (ahrs.rollX16 != 9999 && (ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR))
            {
                rollUsed = (ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625;

                //change for roll to the right is positive times -1
                rollCorrectionDistance = Math.Sin(Glm.ToRadians(rollUsed)) * -vehicle.antennaHeight * 2;

                for (int i = 0; i < totalFixSteps; i++)
                {
                    stepFixPts[i].Easting += (Math.Cos(-fixHeading) * rollCorrectionDistance);
                    stepFixPts[i].Northing += (Math.Sin(-fixHeading) * rollCorrectionDistance);
                }
            }

            for (int i = 0; i < Tools.Count; i++)
            {
                Tools[i].ToolFarRightSpeed = Tools[i].ToolFarRightSpeed * -1.0;
                Tools[i].ToolFarLeftSpeed = Tools[i].ToolFarLeftSpeed * -1.0;
            }
        }

        private void btnMoveHome_Click(object sender, EventArgs e)
        {
            offX = 0;
            offY = 0;
        }

        private void btnEditAB_Click(object sender, EventArgs e)
        {
            if (Guidance.CurrentLine > -1 && Guidance.BtnGuidanceOn)
            {
                Form fc = Application.OpenForms["FormEditAB"];

                if (fc != null)
                {
                    fc.Focus();
                    return;
                }
                Form form = new FormEditAB(this);
                form.Show(this);
                form.Left = Left + Width / 2 - form.Width / 2;
                form.Top = Top + Height / 2 - form.Height / 2;
            }
            else
            {
                TimedMessageBox(1500, String.Get("gsNoABLineActive"), String.Get("gsPleaseEnterABLine"));
                return;
            }
        }

        private void btnTramMenu_Click(object sender, EventArgs e)
        {
            Guidance.isOkToAddPoints = false;
            Guidance.isEditing = false;

            Form f = Application.OpenForms["FormTram"];
            if (f != null)
            {
                f.Focus();
                return;
            }
            Form form = new FormTram(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void btnFlag_Click(object sender, EventArgs e)
        {
            int nextflag = flagPts.Count + 1;
            CFlag flagPt = new CFlag(Latitude, Longitude, pn.fix.Easting, pn.fix.Northing, fixHeading, flagColor, nextflag, (nextflag).ToString());
            flagPts.Add(flagPt);
            FileSaveFlags();

            Form fc = Application.OpenForms["FormFlags"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            if (flagPts.Count > 0)
            {
                flagNumberPicked = nextflag;
                Form form = new FormFlags(this);
                form.Show(this);
                form.Left = Left + Width / 2 - form.Width / 2;
                form.Top = Top + Height / 2 - form.Height / 2;
            }
        }

        private void btnAutoYouTurn_Click(object sender, EventArgs e)
        {
            Guidance.rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
            Guidance.ResetYouTurn();

            if (!Guidance.isYouTurnBtnOn)
            {
                if (bnd.Boundaries.Count == 0)
                {
                    TimedMessageBox(2000, String.Get("gsNoBoundary"), String.Get("gsCreateABoundaryFirst"));
                    return;
                }
                if (Guidance.BtnGuidanceOn && !isAutoSteerBtnOn)
                    btnAutoSteer.PerformClick();

                Guidance.isYouTurnBtnOn = true;
                btnAutoYouTurn.Image = Properties.Resources.Youturn80;
            }
            else
            {
                Guidance.isYouTurnBtnOn = false;
                btnAutoYouTurn.Image = Properties.Resources.YouTurnNo;
            }
        }

        // the context menu from autosteer button
        private void autoUTurnHeadLiftStrip_Click(object sender, EventArgs e)
        {
            
            if (bnd.Boundaries.Count > 0)
            {
                bnd.BtnHeadLand = !bnd.BtnHeadLand;
                if (bnd.BtnHeadLand) btnHeadlandOnOff.Image = Properties.Resources.HeadlandOn;
                else btnHeadlandOnOff.Image = Properties.Resources.HeadlandOff;
            }

            if (!Guidance.isYouTurnBtnOn)  btnAutoYouTurn.PerformClick();

            if (!vehicle.BtnHydLiftOn) btnHydLift.PerformClick();
        }

        private void btnHeadlandOnOff_Click(object sender, EventArgs e)
        {
            if (bnd.Boundaries[0].HeadLand.Count > 0)
            {
                bnd.BtnHeadLand = !bnd.BtnHeadLand;
            }
            else bnd.BtnHeadLand = false;

            if (bnd.BtnHeadLand) btnHeadlandOnOff.Image = Properties.Resources.HeadlandOn;
            else btnHeadlandOnOff.Image = Properties.Resources.HeadlandOff;

            if (!bnd.BtnHeadLand)
            {
                vehicle.BtnHydLiftOn = false;
                btnHydLift.Image = Properties.Resources.HydraulicLiftOff;
            }
        }

        private void btnHydLift_Click(object sender, EventArgs e)
        {
            if (bnd.BtnHeadLand)
            {
                vehicle.BtnHydLiftOn = !vehicle.BtnHydLiftOn;
                if (vehicle.BtnHydLiftOn)
                {
                    btnHydLift.Image = Properties.Resources.HydraulicLiftOn;
                }
                else
                {
                    btnHydLift.Image = Properties.Resources.HydraulicLiftOff;
                }
            }
            else
            {
                vehicle.BtnHydLiftOn = false;
                btnHydLift.Image = Properties.Resources.HydraulicLiftOff;
            }
        }

        private void gPSInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormModules"];
            if (f != null)
            {
                f.Focus();
                f.Close();
                return;
            }
            Form form = new FormModules(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;

        }

        private void lblSpeed_Click(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormGPSData"];
            if (f != null)
            {
                f.Close();
                return;
            }
            Form form = new FormGPSData(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void showStartScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.setDisplay_isTermsOn = true;
            Properties.Settings.Default.Save();
        }

        private void btnFullScreen_Click(object sender, EventArgs e)
        {
            isFullScreen = !isFullScreen;
            if (isFullScreen)
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                btnFullScreen.BackgroundImage = Properties.Resources.WindowNormal;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                btnFullScreen.BackgroundImage = Properties.Resources.WindowFullScreen;
            }
        }

        private void BtnShutdown_Click(object sender, EventArgs e)
        {
            using (var form = new FormShutDown(this))
            {
                var result = form.ShowDialog(this);

                if (result == DialogResult.Yes)
                {
                    ShutDown = true;
                    Close();
                }
                else if (result == DialogResult.No)
                {
                    Close();
                }
            }
        }

        private void BtnNoise_Click(object sender, EventArgs e)
        {
            btnNoise.BackColor = (isSimNoisy = !isSimNoisy) ? Color.LightGreen : Color.LightSalmon;
        }

        private void btnReverseDirection_Click(object sender, EventArgs e)
        {
            btnReverseDirection.BackgroundImage = (sim.reverse = !sim.reverse) ? Properties.Resources.DnArrow64 : Properties.Resources.UpArrow64;
            sim.stepDistance = -sim.stepDistance;

            Guidance.ResetYouTurn();
        }

        private void btnSimSetSpeedToZero_Click(object sender, EventArgs e)
        {
            sim.stepDistance = 0;
            hsbarStepDistance.Value = 0;
            sim.reverse = false;
            btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;
        }

        private void btnDayNightMode_Click(object sender, EventArgs e)
        {
            SwapDayNightMode();
        }        

        //Options
        private void cboxpRowWidth_SelectedIndexChanged(object sender, EventArgs e)
        {
            Guidance.rowSkipsWidth = cboxpRowWidth.SelectedIndex + 1;
            Guidance.ResetCreatedYouTurn();
            Properties.Vehicle.Default.set_youSkipWidth = Guidance.rowSkipsWidth;
            Properties.Vehicle.Default.Save();
        }

        // Menu Items ------------------------------------------------------------------
        private void boundariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                Form f = Application.OpenForms["FormBoundary"];
                if (f != null)
                {
                    f.Focus();
                    return;
                }

                Form form = new FormBoundary(this);
                form.Show(this);
                form.Left = Left + Width / 2 - form.Width / 2;
                form.Top = Top + Height / 2 - form.Height / 2;

            }
            else { TimedMessageBox(3000, String.Get("gsFieldNotOpen"), String.Get("gsStartNewField")); }
        }

        private void fileExplorerToolStripItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                FileSaveFieldKML();
            }
            Process.Start(fieldsDirectory + currentFieldDirectory);
        }        

        private void topMenuFileExplorer_Click(object sender, EventArgs e)
        {
            Process.Start(baseDirectory);
        }

        private void toolStripAreYouSure_Click_1(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                DialogResult result3 = MessageBox.Show(String.Get("gsDeleteAllContoursAndSections"),
                    String.Get("gsDeleteForSure"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (result3 == DialogResult.Yes)
                {

                    for (int i = 0; i < Tools.Count; i++)
                    {
                        //clear the section lists
                        for (int j = 0; j< Tools[i].Sections.Count; j++)
                        {
                            if (Tools[i].Sections[j].IsMappingOn)
                            {
                                Tools[i].TurnMappingOff(j);
                                //clean out the lists
                                Tools[i].Sections[j].triangleList.Clear();
                                Tools[i].TurnMappingOn(j);
                            }
                        }
                    }

                    fd.workedAreaTotal = 0;
                    PatchSaveList.Clear();
                    PatchDrawList.Clear();
                    FileCreateSections();
                }
                else
                {
                    TimedMessageBox(1500, String.Get("gsNothingDeleted"), String.Get("gsActionHasBeenCancelled"));
                }
            }
        }

        private void toolStripBtnField_Click(object sender, EventArgs e)
        {
            JobNewOpenResume();
        }

        private void toolstripDisplayConfig_Click_1(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormIMU"];
            if (f != null)
            {
                f.Focus();
                return;
            }
            Form form = new FormIMU(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolstripUSBPortsConfig_Click_1(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormCommSet"];
            if (f != null)
            {
                f.Focus();
                return;
            }

            Form form = new FormCommSet(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolstripUDPConfig_Click_1(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormUDP"];

            if (f != null)
            {
                f.Focus();
                return;
            }

            Form form = new FormUDP(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolStripNTRIPConfig_Click_1(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormNtrip"];
            if (f != null)
            {
                f.Focus();
                return;
            }

            Form form = new FormNtrip(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolstripYouTurnConfig_Click_1(object sender, EventArgs e)
        {
            var form = new FormYouTurn(this);
            form.ShowDialog(this);
            cboxpRowWidth.SelectedIndex = Guidance.rowSkipsWidth - 1;
        }

        private void toolstripAutoSteerConfig_Click_1(object sender, EventArgs e)
        {
            //check if window already exists
            Form fc = Application.OpenForms["FormSteer"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            //
            Form form = new FormSteer(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolStripAutoSteerChart_Click_1(object sender, EventArgs e)
        {
            //check if window already exists
            Form fcg = Application.OpenForms["FormSteerGraph"];

            if (fcg != null)
            {
                fcg.Focus();
                return;
            }

            //
            Form form = new FormSteerGraph(this);
            form.Show();
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void twoDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera.camFollowing = true;
            camera.camPitch = 0;
        }

        private void threeDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera.camFollowing = true;
            camera.camPitch = -73;
        }

        private void northToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera.camFollowing = false;
            camera.camPitch = 0;
        }

        private void dNorthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera.camPitch = -73;
            camera.camFollowing = false;
        }

        private void toolStripDropDownButtonDistance_Click(object sender, EventArgs e)
        {
            fd.distanceUser = 0;
            fd.workedAreaTotalUser = 0;
        }
        
        private void googleEarthFlagsToolStrip_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                //save new copy of flags
                FileSaveFieldKML();

               Process.Start(fieldsDirectory + currentFieldDirectory + "\\Field.KML");
            }
            else
            {
                TimedMessageBox(1500, String.Get("gsFieldIsOpen"), String.Get("gsStartNewField"));
            }
        }

        private void treePlantToolStrip_Click(object sender, EventArgs e)
        {
            //check if window already exists
            Form fc = Application.OpenForms["FormTreePlant"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            //
            Form form = new FormTreePlant(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void webcamToolStrip_Click(object sender, EventArgs e)
        {
            Form form = new FormWebCam(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void offsetFixToolStrip_Click(object sender, EventArgs e)
        {
            using (var form = new FormShiftPos(this))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK) { }
            }
        }

        private void AutoSteerToolBtn_Click(object sender, EventArgs e)
        {
            //check if window already exists
            Form fc = Application.OpenForms["FormSteer"];

            if (fc != null)
            {
                fc.Focus();
                fc.Close();
                return;
            }

            //
            Form form = new FormSteer(this);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void vehicleToolStripBtn_Click(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormSettings"];
            if (f != null)
            {
                f.Focus();
                return;
            }

            Form form = new FormSettings(this, 0);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolStripBtnDrag_ButtonClick(object sender, EventArgs e)
        {
            if (panelDrag.Visible)
            {
                offX = 0;
                offY = 0;
                panelDrag.Visible = false;
            }
            else
            {
                panelDrag.Top = 80;
                panelDrag.Left = 76;
                panelDrag.Visible = true;
            }
        }

        private void ZoomExtentsStripBtn_Click(object sender, EventArgs e)
        {
            if (camera.camSetDistance < -400) camera.camSetDistance = -75;
            else camera.camSetDistance = -3 * maxFieldDistance;
            if (camera.camSetDistance == 0) camera.camSetDistance = -2000;
            SetZoom(camera.camSetDistance);
        }

        private void topFieldViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.setMenu_isOGLZoomOn == 1)
            {
                Properties.Settings.Default.setMenu_isOGLZoomOn = 0;
                Properties.Settings.Default.Save();
                topFieldViewToolStripMenuItem.Checked = false;
                oglZoom.Width = 400;
                oglZoom.Height = 400;
                oglZoom.SendToBack();
                oglZoom.Visible = false;
            }
            else
            {
                Properties.Settings.Default.setMenu_isOGLZoomOn = 1;
                Properties.Settings.Default.Save();
                topFieldViewToolStripMenuItem.Checked = true;
                oglZoom.Visible = true;
                oglZoom.Width = 300;
                oglZoom.Height = 300;
                oglZoom.Left = 80;
                oglZoom.Top = 80;
                if (isJobStarted) oglZoom.BringToFront();
            }
        }

        private void headlandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bnd.Boundaries.Count == 0)
            {
                TimedMessageBox(2000, String.Get("gsNoBoundary"), String.Get("gsCreateABoundaryFirst"));
                return;
            }

            Form f = Application.OpenForms["FormHeadland"];
            if (f != null)
            {
                f.Focus();
                return;
            }
            Form form = new FormGuidanceDraw(this, true);
            form.Show(this);
            form.Left = Left + Width / 2 - form.Width / 2;
            form.Top = Top + Height / 2 - form.Height / 2;
        }

        private void toolToolStripMenu_Click(object sender, EventArgs e)
        {
            using (var form = new FormToolSettings(this, 0))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                }
            }
        }
        private void moduleConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormArduinoSettings(this)) form.ShowDialog(this);
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form f = Application.OpenForms["FormDisplayOptions"];

            if (f != null) f.Focus();
            else
            {
                Form form = new FormDisplayOptions(this);
                form.Show(this);
                form.Left = Left + Width / 2 - form.Width / 2;
                form.Top = Top + Height / 2 - form.Height / 2;
            }
        }

        private void ColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormColor(this))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                }
            }
        }

        private void StripSectionColor_Click(object sender, EventArgs e)
        {
            using (var form = new FormColorPicker(this, this, sectionColorDay))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    sectionColorDay = form.UseThisColor;
                }
            }

            Properties.Settings.Default.setDisplay_colorSectionsDay = sectionColorDay;
            Properties.Settings.Default.Save();

            stripSectionColor.BackColor = sectionColorDay;
        }

        private void KeyboardToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            isKeyboardOn = !isKeyboardOn;
            keyboardToolStripMenuItem1.Checked = isKeyboardOn;
            Properties.Settings.Default.setDisplay_isKeyboardOn = isKeyboardOn;
            Properties.Settings.Default.Save();
        }

        private void simplifyToolStrip_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.setDisplay_isSimple = !Properties.Settings.Default.setDisplay_isSimple;
            Properties.Settings.Default.Save();

            FixPanelsAndMenus();
        }

        //File drop down items
        private void setWorkingDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                TimedMessageBox(2000, String.Get("gsFieldIsOpen"), String.Get("gsCloseFieldFirst"));
                return;
            }

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.Description = "Currently: " + Properties.Settings.Default.setF_workingDirectory;

            if (Properties.Settings.Default.setF_workingDirectory == "Default") fbd.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            else fbd.SelectedPath = Properties.Settings.Default.setF_workingDirectory;

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                if (fbd.SelectedPath != Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                {
                    Properties.Settings.Default.setF_workingDirectory = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.setF_workingDirectory = "Default";
                    Properties.Settings.Default.Save();
                }

                LoadSettings();
            }
        }

        private void enterSimCoordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var result = DialogResult.Cancel;
            using (var form = new FormSimCoords(this))
            {
                result = form.ShowDialog(this);
            }

            if (result == DialogResult.OK)
            {
                if (isJobStarted)
                {
                    FileOpenField();
                }
                else
                {
                    SetPlaneToLocal(Latitude = sim.latitude, Longitude = sim.longitude);
                    for (int i = 0; i < totalFixSteps; i++)
                    {
                        stepFixPts[i] = new Vec3(pn.fix.Easting, pn.fix.Northing, 0);
                    }
                }

                //Reset the sim
                sim.steerAngleScrollBar = 0;
                hsbarSteerAngle.Value = 400;
                btnResetSteerAngle.Text = 0.ToString("N1");
                sim.headingTrue = 0;
                sim.stepDistance = 1.3889;
                sim.reverse = false;
                btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;
                sim.speed = 5.0;
                hsbarStepDistance.Value = 50;
            }
        }

        private void topMenuSaveTool_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this,0))
            {
                form.ShowDialog(this);
            }
        }
        private void topMenuLoadTool_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this, 1))
            {
                form.ShowDialog(this);
            }
        }

        private void topMenuSaveEnvironment_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this, 2))
            {
                form.ShowDialog(this);
            }
        }

        private void topMenuLoadEnvironment_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this, 3))
            {
                form.ShowDialog(this);
            }
        }

        private void topMenuSaveVehicle_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this, 4))
            {
                form.ShowDialog(this);
            }
        }

        private void topMenuLoadVehicle_Click(object sender, EventArgs e)
        {
            using (var form = new FormPicker(this, 5))
            {
                var result = form.ShowDialog(this);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new Form_About(this))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK) { }
            }
        }

        //Shortcut keys
        private void shortcutKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new FormShortcutKeys(this))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK) { }
            }
        }

        //Options Drop down menu items
        private void resetALLToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                MessageBox.Show(String.Get("gsCloseFieldFirst"));
            }
            else
            {
                DialogResult result2 = MessageBox.Show(String.Get("gsReallyResetEverything"), String.Get("gsResetAll"),
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result2 == DialogResult.Yes)
                {
                    Properties.Settings.Default.Reset();
                    Properties.Settings.Default.Save();

                    Properties.Vehicle.Default.Reset();
                    Properties.Vehicle.Default.Save();

                    Properties.Settings.Default.setF_culture = "en";
                    Properties.Settings.Default.setF_workingDirectory = "Default";
                    Properties.Settings.Default.Save();

                    LoadSettings();
                    LoadTools();
                }
            }
        }

        private void lightbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            isLightbarOn = !isLightbarOn;
            lightbarToolStripMenuItem.Checked = isLightbarOn;
            Properties.Settings.Default.setMenu_isLightbarOn = isLightbarOn;
            Properties.Settings.Default.Save();
        }
        private void simulatorOnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (spGPS.IsOpen)
            {
                simulatorOnToolStripMenuItem.Checked = false;
                panelSim.Visible = false;
                timerSim.Enabled = false;

                TimedMessageBox(2000, String.Get("gsGPSConnected"), String.Get("gsSimulatorForcedOff"));
            }
            else
            {
                if (isJobStarted)
                {
                    TimedMessageBox(2000, String.Get("gsFieldIsOpen"), String.Get("gsCloseFieldFirst"));
                    simulatorOnToolStripMenuItem.Checked = !simulatorOnToolStripMenuItem.Checked;
                    return;
                }
                if (simulatorOnToolStripMenuItem.Checked)
                {
                    panelSim.Visible = true;
                    timerSim.Enabled = true;
                    TimedMessageBox(2000, String.Get("gsTurningOnSimulator"), String.Get("gsTurningOnSimulator"));
                }
                else
                {
                    panelSim.Visible = false;
                    timerSim.Enabled = false;
                    TimedMessageBox(2000, String.Get("gsTurningOffSimulator"), String.Get("gsTurningOffSimulator"));
                }
            }

            Properties.Settings.Default.setMenu_isSimulatorOn = simulatorOnToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }


        //The flag context menus
        private void toolStripMenuItemFlagRed_Click(object sender, EventArgs e)
        {
            flagColor = 0;
            btnFlag.Image = Properties.Resources.FlagRed;
        }
        private void toolStripMenuGrn_Click(object sender, EventArgs e)
        {
            flagColor = 1;
            btnFlag.Image = Properties.Resources.FlagGrn;
        }
        private void toolStripMenuYel_Click(object sender, EventArgs e)
        {
            flagColor = 2;
            btnFlag.Image = Properties.Resources.FlagYel;
        }
        private void toolStripMenuFlagForm_Click(object sender, EventArgs e)
        {
            Form fc = Application.OpenForms["FormFlags"];

            if (fc != null)
            {
                fc.Focus();
                return;
            }

            if (flagPts.Count > 0)
            {
                flagNumberPicked = 1;
                Form form = new FormFlags(this);
                form.Show(this);
                form.Left = Left + Width / 2 - form.Width / 2;
                form.Top = Top + Height / 2 - form.Height / 2;
            }            
        }

        //OpenGL Window context Menu and functions
        private void contextMenuStripOpenGL_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //dont bring up menu if no flag selected
            if (flagNumberPicked == 0) e.Cancel = true;
        }
        private void googleEarthOpenGLContextMenu_Click(object sender, EventArgs e)
        {
            if (isJobStarted)
            {
                //save new copy of kml with selected flag and view in GoogleEarth
                FileSaveSingleFlagKML(flagNumberPicked);

                Process.Start(fieldsDirectory + currentFieldDirectory + "\\Flag.KML");
            }
        }

        //Sim controls
        private void timerSim_Tick(object sender, EventArgs e)
        {
            //if a GPS is connected disable sim
            if (!spGPS.IsOpen)
            {
                if (isAutoSteerBtnOn && (guidanceLineDistanceOff != 32000)) sim.DoSimTick(guidanceLineSteerAngle * 0.01);
                else if (recPath.isDrivingRecordedPath) sim.DoSimTick(guidanceLineSteerAngle * 0.01);
                //else if (self.isSelfDriving) sim.DoSimTick(guidanceLineSteerAngle * 0.01);
                else sim.DoSimTick(sim.steerAngleScrollBar);
            }
        }

        private void hsbarSteerAngle_Scroll(object sender, ScrollEventArgs e)
        {
            sim.steerAngleScrollBar = (hsbarSteerAngle.Value - 400) * 0.1;
            btnResetSteerAngle.Text = sim.steerAngleScrollBar.ToString("N1");
        }

        private void hsbarStepDistance_ValueChanged(object sender, EventArgs e)
        {
            sim.stepDistance = ((double)(hsbarStepDistance.Value / 36.0));
            if (sim.reverse) sim.stepDistance *= -1;
        }

        private void hsbarStepDistance_Scroll(object sender, ScrollEventArgs e)
        {
            sim.stepDistance = ((double)(hsbarStepDistance.Value / 36.0));
            if (sim.reverse) sim.stepDistance *= -1;
        }

        private void btnResetSteerAngle_Click(object sender, EventArgs e)
        {
            sim.steerAngleScrollBar = 0;
            hsbarSteerAngle.Value = 400;
            btnResetSteerAngle.Text = 0.ToString("N1");
        }

        private void btnResetSim_Click(object sender, EventArgs e)
        {
            sim.steerAngleScrollBar = 0;
            hsbarSteerAngle.Value = 400;
            btnResetSteerAngle.Text = 0.ToString("N1");
            sim.headingTrue = 0;
            sim.stepDistance = 1.3889;

            sim.reverse = false;
            btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;

            sim.speed = 5.0;
            hsbarStepDistance.Value = 50;

            sim.latitude = Properties.Settings.Default.setGPS_SimLatitude;
            sim.longitude = Properties.Settings.Default.setGPS_SimLongitude;
        }

        //Languages

        private void SetLanguage(object sender, EventArgs e)
        {
            //reset them all to false
            en.Checked = false;
            de.Checked = false;
            ru.Checked = false;
            nl.Checked = false;
            es.Checked = false;
            fr.Checked = false;
            it.Checked = false;
            uk.Checked = false;
            sk.Checked = false;
            pl.Checked = false;


            try
            {
                string Language = "en";
                if (sender is ToolStripMenuItem StripItem)
                {
                    Language = StripItem.Name;
                    StripItem.Checked = true;
                }
                else
                {
                    Language = sender.ToString();
                    ToolStripMenuItem test = (ToolStripMenuItem)menustripLanguage.DropDownItems[Language];
                    test.Checked = true;
                }
                String.Culture = new System.Globalization.CultureInfo(Properties.Settings.Default.setF_culture = Language);
                Properties.Settings.Default.Save();
            }
            catch (Exception)
            {

                Properties.Settings.Default.setF_culture = "en";
                Properties.Settings.Default.Save();
            }
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(Properties.Settings.Default.setF_culture);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Properties.Settings.Default.setF_culture);


            UpdateGuiText();
        }
    }//end class
}//end namespace