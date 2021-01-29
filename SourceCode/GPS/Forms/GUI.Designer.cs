using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //ABLines directory
        public string ablinesDirectory;

        //colors for sections and field background
        public byte flagColor = 0;

        //how many cm off line per big pixel
        public int lightbarCmPerPixel, Decimals = 2;
        public string GuiFix = "0.0#";
        //polygon mode for section drawing
        public bool isDrawPolygons;

        //Is it in 2D or 3D, metric or imperial, display lightbar, display grid etc
        public bool isMetric = true, isLightbarOn = true, isGridOn, isFullScreen, TwoSecondUpdateBool, OneSecondUpdateBool;
        public bool isUTurnAlwaysOn, isAutoLoadFields, isCompassOn, isSpeedoOn, isAutoDayNight, isSideGuideLines = true;
        public bool isPureDisplayOn = true, DrawBackBuffer = false, isSkyOn = true, isRollMeterOn = false;
        public bool isDay = true, isDayTime = true, isSimNoisy, isKeyboardOn = true;

        public double Mtr2Unit = 1.0, Unit2Mtr = 1.0, Kmh2Unit = 1.0, Unit2Kmh = 1.0;
        public string Units = " Mtr";
        public int lineWidth = 1;
        //master Manual and Auto, 3 states possible
        public enum btnStates { Off, Auto, On }
        public btnStates autoBtnState = btnStates.Off;

        public Color dayColor = Properties.Settings.Default.setDisplay_colorDayMode;
        public Color nightColor = Properties.Settings.Default.setDisplay_colorNightMode;
        public Color sectionColorDay = Properties.Settings.Default.setDisplay_colorSectionsDay;
        public Color sectionColorNight = Properties.Settings.Default.setDisplay_colorSectionsNight;
        public Color fieldColorDay = Properties.Settings.Default.setDisplay_colorFieldDay;
        public Color fieldColorNight = Properties.Settings.Default.setDisplay_colorFieldNight;

        public int[] customColorsList = new int[16];

        //sunrise sunset
        public DateTime dateToday = DateTime.Today, sunrise = DateTime.Now, sunset = DateTime.Now;

        private void IsBetweenSunriseSunset(double lat, double lon)
        {
            CSunTimes.Instance.CalculateSunRiseSetTimes(Latitude, Longitude, dateToday, ref sunrise, ref sunset);
            isDay = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);
        }

        private void LoadSettings()
        {
            //metric settings
            if (isMetric = Properties.Settings.Default.setMenu_isMetric)
            {
                Mtr2Unit = 1.0;
                Unit2Mtr = 1.0;
                Kmh2Unit = 1.0;
                Unit2Kmh = 1.0;
                Decimals = 2;
                GuiFix = "0.0#";
                Units = " Mtr";
            }
            else
            {
                Mtr2Unit = Glm.m2in;
                Unit2Mtr = Glm.in2m;
                Kmh2Unit = 0.62137273665;
                Unit2Kmh = 1.60934;
                Decimals = 2;
                GuiFix = "0.0#";
                Units = " Inch";
            }


            if (Properties.Settings.Default.setF_workingDirectory == "Default")
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\AgOpenGPS\\";
            else baseDirectory = Properties.Settings.Default.setF_workingDirectory + "\\AgOpenGPS\\";

            //get the fields directory, if not exist, create
            fieldsDirectory = baseDirectory + "Fields\\";
            string dir = Path.GetDirectoryName(fieldsDirectory);
            //get the fields directory, if not exist, create
            vehiclesDirectory = baseDirectory + "Vehicles\\";

            //get the Tools directory, if not exist, create
            toolsDirectory = baseDirectory + "Tools\\";

            //get the Tools directory, if not exist, create
            envDirectory = baseDirectory + "Environments\\";

            //set the language to last used
            SetLanguage((object)Properties.Settings.Default.setF_culture, null);

            SndBoundaryAlarm = new SoundPlayer(Properties.Resources.Alarm10);

            //grab the current vehicle filename - make sure it exists
            envFileName = Properties.Vehicle.Default.setVehicle_envName;

            timerSim.Interval = 94;

            fixUpdateTime = 1.0 / (double)HzTime;

            //get the abLines directory, if not exist, create
            ablinesDirectory = baseDirectory + "ABLines\\";
            dir = Path.GetDirectoryName(fieldsDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            //set baud and port from last time run
            baudRateGPS = Properties.Settings.Default.setPort_baudRate;
            portNameGPS = Properties.Settings.Default.setPort_portNameGPS;

            if (Properties.Settings.Default.setMenu_isSimulatorOn)
            {
                simulatorOnToolStripMenuItem.Checked = true;
                panelSim.Visible = true;
                timerSim.Enabled = true;
            }
            else
            {
                //try and open
                SerialPortOpenGPS();
            }

            //same for SectionMachine port
            portNameMachine = Properties.Settings.Default.setPort_portNameMachine;
            wasRateMachineConnectedLastRun = Properties.Settings.Default.setPort_wasMachineConnected;
            if (wasRateMachineConnectedLastRun) SerialPortMachineOpen();

            //same for AutoSteer port
            portNameAutoSteer = Properties.Settings.Default.setPort_portNameAutoSteer;
            wasAutoSteerConnectedLastRun = Properties.Settings.Default.setPort_wasAutoSteerConnected;
            if (wasAutoSteerConnectedLastRun) SerialPortAutoSteerOpen();


            //set the correct zoom and grid
            camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
            SetZoom(camera.camSetDistance);

            //which heading source is being used
            HeadingFromSource = Properties.Vehicle.Default.HeadingFromSource;

            StartLocalUDPServer();

            //start NTRIP if required
            if (isNTRIP_TurnedOn = Properties.Settings.Default.setNTRIP_isOn) StartUDPServer();
            else StopUDPServer();


            //workswitch stuff
            mc.isWorkSwitchEnabled = Properties.Vehicle.Default.setF_IsWorkSwitchEnabled;
            mc.isWorkSwitchActiveLow = Properties.Vehicle.Default.setF_IsWorkSwitchActiveLow;
            mc.isWorkSwitchManual = Properties.Vehicle.Default.setF_IsWorkSwitchManual;
            mc.RemoteAutoSteer = Properties.Vehicle.Default.setAS_isAutoSteerAutoOn;

            FixStepDist = Properties.Vehicle.Default.FixStepDist;
            DualHeadingCorrection = Properties.Vehicle.Default.DualHeadingCorrection;
            DualAntennaDistance = Properties.Vehicle.Default.DualAntennaDistance;

            fd.workedAreaTotalUser = Properties.Settings.Default.setF_UserTotalArea;
            fd.userSquareMetersAlarm = Properties.Settings.Default.setF_UserTripAlarm;

            if (Properties.Settings.Default.setAS_youTurnShape == "Custom")
                Guidance.LoadYouTurnShapeFromData(Properties.Settings.Default.Custom);
            else if (Properties.Settings.Default.setAS_youTurnShape == "KeyHole")
                Guidance.LoadYouTurnShapeFromData(Properties.Settings.Default.KeyHole);
            else if (Properties.Settings.Default.setAS_youTurnShape == "SemiCircle")
                Guidance.LoadYouTurnShapeFromData(Properties.Settings.Default.SemiCircle);
            else if (Properties.Settings.Default.setAS_youTurnShape == "WideReturn")
                Guidance.LoadYouTurnShapeFromData(Properties.Settings.Default.WideReturn);
            else
                Guidance.LoadYouTurnShapeFromData(Properties.Settings.Default.KeyHole);

            //load th elightbar resolution
            lightbarCmPerPixel = Properties.Settings.Default.setDisplay_lightbarCmPerPixel;

            //Stanley guidance
            isStanleyUsed = Properties.Vehicle.Default.setVehicle_isStanleyUsed;

            isRTK = Properties.Settings.Default.setGPS_isRTK;
            isSkyOn = Properties.Settings.Default.setMenu_isSkyOn;
            isGridOn = Properties.Settings.Default.setMenu_isGridOn;
            isCompassOn = Properties.Settings.Default.setMenu_isCompassOn;
            isSpeedoOn = Properties.Settings.Default.setMenu_isSpeedoOn;
            isAutoDayNight = Properties.Settings.Default.setDisplay_isAutoDayNight;
            isSideGuideLines = Properties.Settings.Default.setMenu_isSideGuideLines;
            isLogNMEA = Properties.Settings.Default.setMenu_isLogNMEA;
            isPureDisplayOn = Properties.Settings.Default.setMenu_isPureOn;
            isUTurnAlwaysOn = Properties.Settings.Default.setMenu_isUTurnAlwaysOn;
            isAutoLoadFields = Properties.Settings.Default.AutoLoadFields;
            DrawBackBuffer = Properties.Settings.Default.DrawBackBuffer;

            simulatorOnToolStripMenuItem.Checked = Properties.Settings.Default.setMenu_isSimulatorOn;
            if (simulatorOnToolStripMenuItem.Checked)
            {
                panelSim.Visible = true;
                timerSim.Enabled = true;
            }
            else
            {
                panelSim.Visible = false;
                timerSim.Enabled = false;
            }

            //set the flag mark button to red dot
            btnFlag.Image = Properties.Resources.FlagRed;

            //night mode
            //isDay = Properties.Settings.Default.setDisplay_isDayMode;
            isDay = !isDay;
            SwapDayNightMode();

            //load the string of custom colors
            string[] words = Properties.Settings.Default.setDisplay_customColors.Split(',');
            for (int i = 0; i < 16; i++)
            {
                customColorsList[i] = int.Parse(words[i], CultureInfo.InvariantCulture);
            }

            //load up colors
            fieldColorDay = Properties.Settings.Default.setDisplay_colorFieldDay;
            sectionColorDay = Properties.Settings.Default.setDisplay_colorSectionsDay;
            fieldColorNight = Properties.Settings.Default.setDisplay_colorFieldNight;
            sectionColorNight = Properties.Settings.Default.setDisplay_colorSectionsNight;
            dayColor = Properties.Settings.Default.setDisplay_colorDayMode;
            nightColor = Properties.Settings.Default.setDisplay_colorNightMode;

            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;

            YouTurnButtons(false);

            isLightbarOn = Properties.Settings.Default.setMenu_isLightbarOn;
            lightbarToolStripMenuItem.Checked = isLightbarOn;

            //set up grid and lightbar

            isKeyboardOn = Properties.Settings.Default.setDisplay_isKeyboardOn;
            keyboardToolStripMenuItem1.Checked = isKeyboardOn;


            if (Properties.Settings.Default.setMenu_isOGLZoomOn == 1)
                topFieldViewToolStripMenuItem.Checked = true;
            else topFieldViewToolStripMenuItem.Checked = false;

            oglZoom.Width = 400;
            oglZoom.Height = 400;
            oglZoom.Visible = true;
            oglZoom.Left = 300;
            oglZoom.Top = 80;

            oglZoom.SendToBack();

            Guidance.rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
            cboxpRowWidth.SelectedIndex = Guidance.rowSkipsWidth - 1;

            FixPanelsAndMenus();

            UpdateNtripButton();

            stripSectionColor.BackColor = sectionColorDay;
        }

        public void LoadFields()
        {
            Fields.Clear();
            if (fieldsDirectory != null)
            {
            string[] dirs = Directory.GetDirectories(fieldsDirectory);
                foreach (string dir in dirs)
                {
                    string line;
                    string[] words;
                    //grab the boundary
                    string filename = dir + "\\Boundary.txt";
                    if (!File.Exists(filename))
                        filename = dir + "\\Boundary.Tmp";

                    if (File.Exists(filename))
                    {
                        using (StreamReader reader = new StreamReader(filename))
                        {
                            try
                            {
                                CAutoLoadField Field = new CAutoLoadField();
                                Field.Dir = Path.GetFileName(dir);

                                line = reader.ReadLine();//"$Version,5.0.10"
                                if (line != null)
                                {
                                    words = line.Split(',');
                                    if (words.Length > 1 && words[1] == "5.0.10")
                                    {
                                        line = reader.ReadLine(); words = line.Split(',');

                                        Field.LatStart = double.Parse(words[1], CultureInfo.InvariantCulture);
                                        Field.LonStart = double.Parse(words[2], CultureInfo.InvariantCulture);
                                        line = reader.ReadLine(); words = line.Split(',');
                                        int BndCount = int.Parse(words[1]);//Boundaries

                                        double MPerDegLat = 111132.92 - 559.82 * Math.Cos(2.0 * Field.LatStart * 0.01745329251994329576923690766743) + 1.175 * Math.Cos(4.0 * Field.LatStart * 0.01745329251994329576923690766743) - 0.0023 * Math.Cos(6.0 * Field.LatStart * 0.01745329251994329576923690766743);


                                        for (int i = 0; i < BndCount; i++)
                                        {
                                            line = reader.ReadLine(); words = line.Split(',');
                                            //newbnd.isDriveThru = bool.Parse(words[1]);
                                            //newbnd.isDriveAround = bool.Parse(words[2]);

                                            line = reader.ReadLine(); words = line.Split(',');

                                            line = reader.ReadLine(); words = line.Split(',');
                                            Field.Northingmax = double.Parse(words[1], CultureInfo.InvariantCulture);
                                            Field.Northingmin = double.Parse(words[2], CultureInfo.InvariantCulture);

                                            line = reader.ReadLine(); words = line.Split(',');
                                            Field.Eastingmax = double.Parse(words[1], CultureInfo.InvariantCulture);
                                            Field.Eastingmin = double.Parse(words[2], CultureInfo.InvariantCulture);

                                            ConvertLocalToCurrentLocal(Field.Northingmax, Field.Eastingmax, Field.LatStart, Field.LonStart, MPerDegLat, out Field.Northingmax, out Field.Eastingmax);
                                            ConvertLocalToCurrentLocal(Field.Northingmin, Field.Eastingmin, Field.LatStart, Field.LonStart, MPerDegLat, out Field.Northingmin, out Field.Eastingmin);

                                            line = reader.ReadLine(); words = line.Split(',');
                                            Field.Area = double.Parse(words[1], CultureInfo.InvariantCulture);

                                            line = reader.ReadLine(); words = line.Split(',');
                                            int numPoints = int.Parse(words[1]);
                                            for (int j = 0; j < numPoints; j++)
                                            {
                                                line = reader.ReadLine(); words = line.Split(',');
                                                double easting = double.Parse(words[0], CultureInfo.InvariantCulture);
                                                double northing = double.Parse(words[1], CultureInfo.InvariantCulture);
                                                ConvertLocalToCurrentLocal(northing, easting, Field.LatStart, Field.LonStart, MPerDegLat, out double Northing, out double Easting);
                                                Field.Polygon.Points.Add(new Vec2(Easting, Northing));
                                            }

                                            line = reader.ReadLine(); words = line.Split(',');
                                            int IdxPoints = int.Parse(words[1]);
                                            for (int j = 0; j < IdxPoints; j++)
                                            {
                                                line = reader.ReadLine();
                                                int Idx = int.Parse(line, CultureInfo.InvariantCulture);
                                                Field.Polygon.Indexer.Add(Idx);
                                            }
                                            Field.Polygon.ResetIndexer = true;

                                            line = reader.ReadLine(); words = line.Split(',');

                                            if (numPoints > 0)
                                                Fields.Add(Field);

                                            //limit to only first polygon boundary
                                            if (i == 0) break;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }
        }

        public void SwapDayNightMode()
        {
            isDay = !isDay;
            if (isDay)
            {
                btnDayNightMode.Image = Properties.Resources.WindowNightMode;

                this.BackColor = dayColor;
                foreach (Control c in this.Controls)
                {
                    c.ForeColor = Color.Black;
                }
            }
            else //nightmode
            {
                btnDayNightMode.Image = Properties.Resources.WindowDayMode;

                this.BackColor = nightColor;

                foreach (Control c in this.Controls)
                {
                    c.ForeColor = Color.White;
                }
            }
            LineUpManualBtns();
            Properties.Settings.Default.setDisplay_isDayMode = isDay;
            Properties.Settings.Default.Save();
        }

        private void FixPanelsAndMenus()
        {
            oglMain.Left = statusStripLeft.Width;
            if (Properties.Settings.Default.setDisplay_isSimple)
            {
                oglMain.Width = Width - 17 - statusStripLeft.Width - layoutPanelRight.Width / 2;
            }
            else
            {
                oglMain.Width = Width - 16 - statusStripLeft.Width - layoutPanelRight.Width;
            }

            if (isFullScreen) oglMain.Width += 16;

            if (Properties.Settings.Default.setDisplay_isSimple)
            {
                toolToolbottomStripBtn.Visible = false;
                vehicleToolStripBtn.Visible = false;
                AutoSteerToolBtn.Visible = false;

                lblDateTime.Visible = true;
                snapLeftBigStrip.Visible = true;
                snapRightBigStrip.Visible = true;
            }
            else
            {
                toolToolbottomStripBtn.Visible = true;
                vehicleToolStripBtn.Visible = true;
                AutoSteerToolBtn.Visible = true;

                if (Width > 1100)
                {
                    snapLeftBigStrip.Visible = true;
                    snapRightBigStrip.Visible = true;
                }
                else
                {
                    snapLeftBigStrip.Visible = false;
                    snapRightBigStrip.Visible = false;
                }
                if (Width > 1300)
                {
                    lblDateTime.Visible = true;
                }
                else
                {
                    lblDateTime.Visible = false;
                }
            }
        }

        public string FindDirection(double heading)
        {
            if (heading < 0) heading += Glm.twoPI;

            heading = Glm.ToDegrees(heading);

            if (heading > 337.5 || heading < 22.5)
            {
                return (" " +  String.Get("gsNorth") + " ");
            }
            if (heading > 22.5 && heading < 67.5)
            {
                return (" " +  String.Get("gsN_East") + " ");
            }
            if (heading > 67.5 && heading < 111.5)
            {
                return (" " +  String.Get("gsEast") + " ");
            }
            if (heading > 111.5 && heading < 157.5)
            {
                return (" " +  String.Get("gsS_East") + " ");
            }
            if (heading > 157.5 && heading < 202.5)
            {
                return (" " +  String.Get("gsSouth") + " ");
            }
            if (heading > 202.5 && heading < 247.5)
            {
                return (" " +  String.Get("gsS_West") + " ");
            }
            if (heading > 247.5 && heading < 292.5)
            {
                return (" " +  String.Get("gsWest") + " ");
            }
            if (heading > 292.5 && heading < 337.5)
            {
                return (" " +  String.Get("gsN_West") + " ");
            }
            return (" " +  String.Get("gsLost") + " ");
        }

        //line up section On Off Auto buttons based on how many there are
        public void LineUpManualBtns()
        {
            int oglCenter = statusStripLeft.Width + oglMain.Width / 2;

            int top = 180;
            if (panelSim.Visible == true) top = 230;

            panelSim.Width = Math.Min(panelSim.MaximumSize.Width, oglMain.Width - 10);
            panelSim.Left = oglCenter - panelSim.Width / 2;


            for (int i = 0; i < Tools.Count; i++)
            {
                if (Tools[i].numOfSections > 0)
                {
                    Size Size = new System.Drawing.Size(Math.Min((oglMain.Width * 3 / 4) / Tools[i].numOfSections, 120), 30);

                    for (int j = 0; j < Tools[i].Sections.Count; j++)
                    {
                        if (j < Tools[i].numOfSections)
                        {
                            Tools[i].Sections[j].SectionButton.Top = Height - top - 30 * i;
                            Tools[i].Sections[j].SectionButton.Size = Size;
                            Tools[i].Sections[j].SectionButton.Left = (oglCenter) - (Tools[i].numOfSections * Size.Width) / 2 + Size.Width * j;
                            Tools[i].Sections[j].SectionButton.Visible = true;
                        }
                        else Tools[i].Sections[j].SectionButton.Visible = false;
                    }
                }
            }
        }

        //Mouse Clicks 
        private void oglMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //0 at bottom for opengl, 0 at top for windows, so invert Y value
                Point point = oglMain.PointToClient(Cursor.Position);

                //label3.Text = point.X.ToString();

                if (point.Y < 140 && point.Y > 40)
                {
                    int middle = oglMain.Width / 2 + oglMain.Width / 5;
                    if (point.X > middle - 80 && point.X < middle + 80)
                    {
                        SwapDirection();
                        return;
                    }

                    middle = oglMain.Width / 2 - oglMain.Width / 4;
                    if (point.X > middle - 140 && point.X < middle)
                    {
                        if (Guidance.isYouTurnTriggered)
                        {
                            Guidance.ResetYouTurn();
                            Guidance.ResetABLine = true;
                        }
                        else
                        {
                            Guidance.isYouTurnTriggered = true;
                            Guidance.BuildManualYouTurn(false, true);
                            return;
                        }
                    }

                    if (point.X > middle && point.X < middle + 140)
                    {
                        if (Guidance.isYouTurnTriggered)
                        {
                            Guidance.ResetYouTurn();
                            Guidance.ResetABLine = true;
                        }
                        else
                        {
                            Guidance.isYouTurnTriggered = true;
                            Guidance.BuildManualYouTurn(true, true);
                            return;
                        }
                    }
                }

                //prevent flag selection if flag form is up
                Form fc = Application.OpenForms["FormFlags"];
                if (fc != null)
                {
                    fc.Focus();
                    return;
                }

                mouseX = point.X;
                mouseY = oglMain.Height - point.Y;
                LeftMouseDownOnOpenGL = true;
            }
        }

        private void oglZoom_MouseClick(object sender, MouseEventArgs e)
        {
            if ((sender as Control).IsDragging()) return;

            if (oglZoom.Width == 180)
            {
                oglZoom.Width = 300;
                oglZoom.Height = 300;
            }

            else if (oglZoom.Width == 300)
            {
                oglZoom.Width = 180;
                oglZoom.Height = 180;
            }
        }               

        //Function to delete flag
        public void DeleteSelectedFlag()
        {
            //delete selected flag and set selected to none
            flagPts.RemoveAt(flagNumberPicked - 1);
            flagNumberPicked = 0;

            // re-sort the id's based on how many flags left
            int flagCnt = flagPts.Count;
            if (flagCnt > 0)
            {
                for (int i = 0; i < flagCnt; i++) flagPts[i].ID = i + 1;
            }
        }

        public void YouTurnButtons(bool Enable)
        {
            Guidance.ResetYouTurn();
            Guidance.isYouTurnBtnOn = false;
            btnAutoYouTurn.Enabled = Enable;
            btnAutoYouTurn.Image = Properties.Resources.YouTurnNo;
        }

        private void HalfSecond_Update(object sender, EventArgs e)
        {
            lblInt.Text = vehicle.inty.ToString("N2");
            lblDelta.Text = Glm.ToDegrees(abFixHeadingDelta).ToString("N2");
            lblDist.Text = Glm.ToDegrees(xTrackCorrection).ToString("N2");
            lblSetSteerAngle.Text = SetSteerAngle;

            HalfSecondUpdate.Enabled = false;

            AutoSteerToolBtn.Text = SetSteerAngle + "\r\n" + ActualSteerAngle;

            lblSpeed.Text = Math.Round(avgSpeed * Kmh2Unit, Decimals).ToString(GuiFix);

            if (isMetric)  //metric or imperial
            {
                btnEditHeading.Text = (crossTrackError / 10 + String.Get("gsCM")); //cross track error
            }
            else  //Imperial Measurements
            {
                btnEditHeading.Text = ((int)(crossTrackError / 25.54) + " in"); //cross track errorss
            }

            lblHz.Text = HzTime.ToString("##.0") + " Hz " + Math.Round(FrameTime, 0) + " ms "+ Math.Round(FrameTime2, 0) + " ms\r\n" + FixQuality;

            if (OneSecondUpdateBool = !OneSecondUpdateBool)
            {
                //counter used for saving field in background
                MinuteCounter++;

                //Make sure it is off when it should
                if ((!Guidance.BtnGuidanceOn && isAutoSteerBtnOn) || (recPath.isDrivingRecordedPath && isAutoSteerBtnOn))
                {
                    isAutoSteerBtnOn = false;
                    btnAutoSteer.Image = Properties.Resources.AutoSteerOff;
                }

                //do all the NTRIP routines
                if (isNTRIP_TurnedOn)
                {
                    //increment once every second
                    NtripCounter++;

                    //Thinks is connected but not receiving anything // 30sec maybe a bit much?
                    if (NTRIP_Watchdog++ > 10 && isNTRIP_Connected) ReconnectRequest();

                    //Once all connected set the timer GGA to NTRIP Settings
                    if (sendGGAInterval > 0 && NtripCounter == 40) tmr.Interval = sendGGAInterval * 1000;

                    //Have we connection
                    if (!isNTRIP_Connected && !isNTRIP_Connecting)
                    {
                        if (NtripCounter > 20) StartNTRIP();
                    }
                    else if (isNTRIP_Connecting)
                    {
                        if (NtripCounter > 25)//give it 5 seconds
                        {
                            TimedMessageBox(2000, String.Get("gsSocketConnectionProblem"), String.Get("gsNotConnectingToCaster"));
                            ReconnectRequest();
                        }
                        if (clientSocket != null && clientSocket.Connected)
                        {
                            SendAuthorization();
                        }
                    }

                    //update byte counter and up counter
                    if (NtripCounter > 20) NTRIPStartStopStrip.Text = (isNTRIP_Connecting ? String.Get("gsAuthourizing") : isNTRIP_Sending ? String.Get("gsSendingGGA") : (NTRIP_Watchdog > 10 ? String.Get("gsWaiting") : String.Get("gsListening"))) + "\n" + string.Format("{0:00}:{1:00}", ((NtripCounter - 21) / 60), (Math.Abs(NtripCounter - 21)) % 60);
                    else NTRIPStartStopStrip.Text = String.Get("gsConnectingIn") + "\n" + (Math.Abs(NtripCounter - 21));

                    pbarNtripMenu.Value = unchecked((byte)(tripBytes * 0.02));
                    NTRIPBytesMenu.Text = ((tripBytes) * 0.001).ToString("###,###,###") + " kb";

                    if (sendGGAInterval > 0 && isNTRIP_Sending)
                    {
                        isNTRIP_Sending = false;
                    }
                }

                //the main formgps window
                //status strip values
                if (isMetric)
                {
                    distanceToolBtn.Text = fd.DistanceUserMeters + "\r\n" + fd.WorkedUserHectares;
                    fieldStatusStripText.Text = fd.WorkedAreaRemainHectares + "\r\n" +
                                                   fd.WorkedAreaRemainPercentage + "\r\n" +
                                                   fd.TimeTillFinished + "\r\n" +
                                                   fd.WorkRateHectares;
                }
                else
                {
                    distanceToolBtn.Text = fd.DistanceUserFeet + "\r\n" + fd.WorkedUserAcres;
                    fieldStatusStripText.Text = fd.WorkedAreaRemainAcres + "\r\n" +
                           fd.WorkedAreaRemainPercentage + "\r\n" +
                           fd.TimeTillFinished + "\r\n" +
                           fd.WorkRateAcres;
                }

                //statusbar flash red undefined headland
                if (mc.isOutOfBounds && statusStripBottom.BackColor == Color.Transparent) statusStripBottom.BackColor = Color.Tomato;
                else if (!mc.isOutOfBounds && statusStripBottom.BackColor == Color.Tomato) statusStripBottom.BackColor = Color.Transparent;

                //check to make sure the grid is on the right place
                worldGrid.CheckWorldGrid(pn.fix.Northing, pn.fix.Easting);

                if (Guidance.BtnGuidanceOn) btnGuidance.Text = "# " + Guidance.HowManyPathsAway.ToString();
                else btnGuidance.Text = "";

                lblDateTime.Text = DateTime.Now.ToString("HH:mm:ss") + "\n\r" + DateTime.Now.ToString("ddd MMM yyyy");

                zoomUpdateCounter = TwoSecondUpdateBool = !TwoSecondUpdateBool;
            }

            HalfSecondUpdate.Enabled = true;
        }

        #region Properties // ---------------------------------------------------------------------


        public string FixQuality
        {
            get
            {
                if (pn.FixQuality == 0) return "Invalid ";
                else if (pn.FixQuality == 1) return "GPS Single ";
                else if (pn.FixQuality == 2) return "DGPS ";
                else if (pn.FixQuality == 3) return "PPS ";
                else if (pn.FixQuality == 4) return "RTK Fix ";
                else if (pn.FixQuality == 5) return "Float ";
                else if (pn.FixQuality == 6) return "Estimate ";
                else if (pn.FixQuality == 7) return "Man IP ";
                else if (pn.FixQuality == 8) return "Sim ";
                else return "Unknown: ";
            }
        }
        public string GyroInDegrees
        {
            get
            {
                if (ahrs.correctionHeadingX16 != 9999)
                    return Math.Round(ahrs.correctionHeadingX16 * 0.0625, 1) + "\u00B0";
                else return "-";
            }
        }
        public string RollInDegrees
        {
            get
            {
                if (ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR)
                    return Math.Round((ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625, 1) + "\u00B0";
                else return "-";
            }
        }
        public string SetSteerAngle
        {
            get {

                if (TestAutoSteer)
                    return ((double)(TestAutoSteerAngle)).ToString("N2") + "\u00B0";
                else
                    return ((double)(guidanceLineSteerAngle) * 0.01).ToString("N2") + "\u00B0";
            }
        }
        public string ActualSteerAngle { get { return ((double)(actualSteerAngleDisp) * 0.01).ToString("N2") + "\u00B0"; } }
        public string Altitude { get { return Convert.ToString(Math.Round(pn.altitude,1)); } }
        public string AltitudeFeet { get { return Convert.ToString((Math.Round((pn.altitude * 3.28084),1))); } }
        public string DistPivotM
        {
            get
            {
                if (distancePivotToTurnLine > 0 )
                    return ((int)(distancePivotToTurnLine)) + " m";
                else return "--";
            }
        }
        public string DistPivotFt
        {
            get
            {
                if (distancePivotToTurnLine > 0 ) return (((int)(Glm.m2ft * (distancePivotToTurnLine))) + " ft");
                else return "--";
            }
        }
        #endregion properties 
    }//end class
}//end namespace