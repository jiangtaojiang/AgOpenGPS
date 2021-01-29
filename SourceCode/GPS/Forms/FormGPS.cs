using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgOpenGPS
{
    //the main form object
    public partial class FormGPS : Form
    {
        #region // Class Props and instances

        //list of vec3 points of Dubins shortest path between 2 points - To be converted to RecPt
        public List<Vec3> flagDubinsList = new List<Vec3>();

        //How many youturn functions
        public const int MAXFUNCTIONS = 8;

        //The base directory where AgOpenGPS will be stored and fields and vehicles branch from
        public string baseDirectory;

        //current directory of vehicle
        public string vehiclesDirectory, vehicleFileName = "";

        //current directory of Tools
        public string toolsDirectory, toolFileName = "";

        //current directory of Environments
        public string envDirectory, envFileName = "";

        //current fields and field directory
        public string fieldsDirectory, currentFieldDirectory;
        public int flagNumberPicked = 0;

        //bool for whether or not a job is active
        public bool isJobStarted = false, isAutoSteerBtnOn, isLidarBtnOn = true;

        //if we are saving a file
        public bool isLogNMEA = false, isLogElevation = false;

        //texture holders
        public uint[] texture = new uint[13];

        //the currentversion of software
        public string currentVersionStr, inoVersionStr;
        public int inoVersionInt;

        //create instance of a stopwatch for timing of frames and NMEA hz determination
        public readonly Stopwatch swFrame = new Stopwatch();

        //create instance of a stopwatch for timing of frames and NMEA hz determination
        public readonly Stopwatch swHz = new Stopwatch();

        private readonly Timer Timer = new Timer();

        //whether or not to use Stanley control
        public bool isStanleyUsed = true;

        public int pbarSteer, pbarMachine, pbarUDP;

        public double BoundarySpacing = 2;

        //the autoManual drive button. Assume in Auto
        public bool isInAutoDrive = true;

        //the trackbar angle for TestAutoSteer
        public bool TestAutoSteer, ShutDown = false;
        public int TestAutoSteerAngle = 0;
        public int TotalSections = 0;

        public CGuidance Guidance;

        /// <summary>
        /// create the scene camera
        /// </summary>
        public CCamera camera = new CCamera();

        /// <summary>
        /// create world grid
        /// </summary>
        public CWorldGrid worldGrid;

        /// <summary>
        /// The NMEA class that decodes it
        /// </summary>
        public CNMEA pn;

        /// <summary>
        /// Our vehicle only
        /// </summary>
        public CVehicle vehicle;

        /// <summary>
        /// Just the tool attachment that includes the sections
        /// </summary>
        public List<CTool> Tools = new List<CTool>();

        /// <summary>
        /// All the structs for recv and send of information out ports
        /// </summary>
        public CModuleComm mc;

        /// <summary>
        /// The boundary object
        /// </summary>
        public CBoundary bnd;

        /// <summary>
        /// The entry and exit sequences, functions, actions
        /// </summary>
        public CSequence seq;

        /// <summary>
        /// The internal simulator
        /// </summary>
        public CSim sim;

        /// <summary>
        /// Heading, Roll, Pitch, GPS, Properties
        /// </summary>
        public CAHRS ahrs;

        /// <summary>
        /// Recorded Path
        /// </summary>
        public CRecordedPath recPath;

        /// <summary>
        /// Most of the displayed field data for GUI
        /// </summary>
        public CFieldData fd;

        /// <summary>
        /// The font class
        /// </summary>
        public CFont font;
        public bool LeftMouseDownOnOpenGL { get; set; }
        public double FrameTime { get; set; } = 0;
        public double FrameTime2 { get; set; } = 0;
        public double HzTime { get; set; } = 8;
        public SoundPlayer SndBoundaryAlarm { get; set; }
        public int MinuteCounter { get; set; } = 1;
        public int TenMinuteCounter { get; set; } = 1;
        public int NtripCounter { get; set; } = 0;
        public FormTimedMessage Form { get; set; } = new FormTimedMessage();

        #endregion // Class Props and instances

        private void Draggable()
        {
            ControlExtension.Draggable(oglZoom, true);
            ControlExtension.Draggable(oglBack, true);
        }

        // Constructor, Initializes a new instance of the "FormGPS" class.
        public FormGPS()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(this.GetType().Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));

                if (resource != "" && resource != null)
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                    {
                        byte[] assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData);
                    }
                }
                else return null;
            };

            //winform initialization
            InitializeComponent();
            Timer.Tick += new EventHandler(TimerRepeat_Tick);

            Draggable();

            //build the gesture structures
            SetupStructSizes();

            //create the world grid
            worldGrid = new CWorldGrid(this);

            //our vehicle made with gl object and pointer of mainform
            vehicle = new CVehicle(this);

            //our NMEA parser
            pn = new CNMEA(this);

            Guidance = new CGuidance(this);

            //module communication
            mc = new CModuleComm(this);

            //boundary object
            bnd = new CBoundary(this);

            //headland entry/exit sequences
            seq = new CSequence(this);

            //nmea simulator built in.
            sim = new CSim(this);

            //all the attitude, heading, roll, pitch reference system
            ahrs = new CAHRS(this);

            //A recorded path
            recPath = new CRecordedPath(this);

            //fieldData all in one place
            fd = new CFieldData(this);

            // Add Message Event handler for Form decoupling from client socket thread
            updateRTCM_DataEvent = new UpdateRTCM_Data(OnAddMessage);

            //access to font class
            font = new CFont(this);
        }

        private void UpdateGuiText()
        {
            setWorkingDirectoryToolStripMenuItem.Text = String.Get("gsDirectories");
            enterSimCoordsToolStripMenuItem.Text = String.Get("gsEnterSimCoords");
            topMenuLoadVehicle.Text = String.Get("gsLoadVehicle");
            topMenuSaveVehicle.Text = String.Get("gsSaveVehicle");
            aboutToolStripMenuItem.Text = String.Get("gsAbout");
            shortcutKeysToolStripMenuItem.Text = String.Get("gsShortcutKeys");
            menustripLanguage.Text = String.Get("gsLanguage");
            topMenuLoadTool.Text = String.Get("gsLoadTool");
            topMenuSaveTool.Text = String.Get("gsSaveTool");
            topMenuLoadEnvironment.Text = String.Get("gsLoadEnvironment");
            topMenuSaveEnvironment.Text = String.Get("gsSaveEnvironment");
            gPSInfoToolStripMenuItem.Text = String.Get("gsModuleInfo");
            showStartScreenToolStripMenuItem.Text = String.Get("gsShowStartScreen");
            //Display Menu

            //settingsToolStripMenuItem.Text = gStr.gsDisplay;
            topMenuFileExplorer.Text = String.Get("gsWindowsFileExplorer");
            optionsToolStripMenuItem.Text = String.Get("gsOptions");

            simulatorOnToolStripMenuItem.Text = String.Get("gsSimulatorOn");

            resetALLToolStripMenuItem.Text = String.Get("gsResetAll");
            colorsToolStripMenuItem.Text = String.Get("gsColors");
            lightbarToolStripMenuItem.Text = String.Get("gsLightbarOn");
            topFieldViewToolStripMenuItem.Text = String.Get("gsTopFieldView");
            toolToolStripMenu.Text = String.Get("gsTool");

            resetEverythingToolStripMenuItem.Text = String.Get("gsResetAllForSure");
            fileExplorerToolStripMenuItem.Text = String.Get("gsWindowsFileExplorer");

            //Settings Menu
            toolstripYouTurnConfig.Text = String.Get("gsUTurn");
            toolstripAutoSteerConfig.Text = String.Get("gsAutoSteer");
            steerChartStripMenu.Text = String.Get("gsSteerChart");
            toolstripVehicleConfig.Text = String.Get("gsVehicle");
            toolstripDisplayConfig.Text = String.Get("gsDataSources");
            toolstripUSBPortsConfig.Text = String.Get("gsSerialPorts");
            toolstripUDPConfig.Text = String.Get("gsUDP");
            toolStripNTRIPConfig.Text = String.Get("gsNTRIP");

            //Tools Menu
            treePlantToolStrip.Text = String.Get("gsTreePlanter");
            boundariesToolStripMenuItem.Text = String.Get("gsBoundary");
            headlandToolStripMenuItem.Text = String.Get("gsHeadland");
            deleteAppliedAreaToolStripMenuItem.Text = String.Get("gsDeleteAppliedArea");
            deleteForSureToolStripMenuItem.Text = String.Get("gsAreYouSure");
            webcamToolStrip.Text = String.Get("gsWebCam");
            googleEarthFlagsToolStrip.Text = String.Get("gsGoogleEarth");
            offsetFixToolStrip.Text = String.Get("gsOffsetFix");
            moduleConfigToolStripMenuItem.Text = String.Get("gsModuleConfiguration");

            //Recorded Path
            deletePathMenu.Text = String.Get("gsDeletePath");
            recordPathMenu.Text = String.Get("gsRecordStop");
            goPathMenu.Text = String.Get("gsGoStop");
            pausePathMenu.Text = String.Get("gsPauseResume");


            stripSectionColor.Text = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);


            if (isNTRIP_TurnedOn)
            {
                if (NtripCounter > 20) NTRIPStartStopStrip.Text = (isNTRIP_Connecting ? String.Get("gsAuthourizing") : isNTRIP_Sending ? String.Get("gsSendingGGA") : (NTRIP_Watchdog > 10 ? String.Get("gsWaiting") : String.Get("gsListening"))) + "\n" + string.Format("{0:00}:{1:00}", ((NtripCounter - 21) / 60), (Math.Abs(NtripCounter - 21)) % 60);
                else NTRIPStartStopStrip.Text = String.Get("gsConnectingIn") + "\n" + (Math.Abs(NtripCounter - 21));
            }
            else NTRIPStartStopStrip.Text = String.Get("gsNTRIPOff") + "\n";
        }


        private void ZoomByMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (camera.zoomValue <= 20) camera.zoomValue += camera.zoomValue * 0.02;
                else camera.zoomValue += camera.zoomValue * 0.01;
                if (camera.zoomValue > 440) camera.zoomValue = 440;
                camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
            }
            else
            {
                if (camera.zoomValue <= 20)
                {
                    if ((camera.zoomValue -= camera.zoomValue * 0.02) < 6.0) camera.zoomValue = 6.0; 
                }
                else
                { 
                    if ((camera.zoomValue -= camera.zoomValue * 0.01) < 6.0) camera.zoomValue = 6.0;
                }

                camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
            }
            SetZoom(camera.camSetDistance);
        }

        //Initialize items before the form Loads or is visible
        private void FormGPS_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.setDisplay_isTermsOn)
            {
                SetLanguage((object)Properties.Settings.Default.setF_culture, null);

                Form form = new Form_First(this);
                form.ShowDialog(this);
            }

            MouseWheel += ZoomByMouseWheel;

            //remembered window position

            if (Properties.Settings.Default.setWindow_Maximized)
                WindowState = FormWindowState.Maximized;
            Location = Properties.Settings.Default.setWindow_Location;
            Size = Properties.Settings.Default.setWindow_Size;


            if (Properties.Settings.Default.setDisplay_isStartFullScreen)
            {
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                btnFullScreen.BackgroundImage = Properties.Resources.WindowNormal;
                isFullScreen = true;
            }
            else
                isFullScreen = false;

            vehicleFileName = Properties.Vehicle.Default.setVehicle_vehicleName;
            toolFileName = Properties.Vehicle.Default.setVehicle_toolName;

            currentVersionStr = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);

            string[] fullVers = currentVersionStr.Split('.');
            int inoV = int.Parse(fullVers[0], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[1], CultureInfo.InvariantCulture);
            inoV += int.Parse(fullVers[2], CultureInfo.InvariantCulture);
            inoVersionInt = inoV;
            inoVersionStr = inoV.ToString();

            // load all the gui settings in gui.designer.cs
            LoadSettings();

            //Calculate total width and each section width
            LoadTools();

            NMEAWatchdog.Enabled = true;
        }

        //form is closing so tidy up and save settings
        private async void FormGPS_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Save, return, cancel save
            if (isJobStarted)
            {
                using (var form = new FormSaveOrNot(true, this))
                {
                    var result = form.ShowDialog(this);
                    //OK
                    if (result == DialogResult.Ignore)
                    {
                        e.Cancel = true;
                        return;
                    }
                    else
                    {
                        isUDPSendConnected = false;
                        Properties.Settings.Default.setF_CurrentDir = currentFieldDirectory;
                        Properties.Settings.Default.Save();

                        FileSaveEverythingBeforeClosingField();
                    }
                }
            }

            //save window settings
            if (WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.setWindow_Location = Location;
                Properties.Settings.Default.setWindow_Size = Size;
            }
            else
            {
                Properties.Settings.Default.setWindow_Location = RestoreBounds.Location;
                Properties.Settings.Default.setWindow_Size = RestoreBounds.Size;
            }

            Properties.Settings.Default.setWindow_Maximized = WindowState == FormWindowState.Maximized;
            Properties.Settings.Default.setWindow_Minimized = WindowState == FormWindowState.Minimized;

            Properties.Settings.Default.setDisplay_camPitch = camera.camPitch;
            Properties.Settings.Default.setF_UserTotalArea = fd.workedAreaTotalUser;
            Properties.Settings.Default.setF_UserTripAlarm = fd.userSquareMetersAlarm;

            //Properties.Settings.Default.setDisplay_panelSnapLocation = panelSnap.Location;
            Properties.Settings.Default.setDisplay_panelSimLocation = panelSim.Location;

            Properties.Settings.Default.Save();
            
            List<Task> tasks = new List<Task>();
            for (int j = 0; j < TaskList.Count; j++)
            {
                if (TaskList[j].Task.IsCompleted)
                {
                    TaskList.RemoveAt(j);
                    j--;
                }
                else
                {
                    tasks.Add(TaskList[j].Task);
                }
            }

            if (TaskList.Count > 0)
            {
                e.Cancel = true;
                await Task.WhenAll(tasks);
                Close();
            }
            else if (ShutDown)
            {
                Process.Start("shutdown", "/s /t 0");
            }
        }

        //called everytime window is resized, clean up button positions
        private void FormGPS_Resize(object sender, EventArgs e)
        {
            LineUpManualBtns();
            FixPanelsAndMenus();
        }

        // Procedures and Functions ---------------------------------------

        public void LoadGLTextures()
        {
            GL.Enable(EnableCap.Texture2D);
            Bitmap[] tt = new Bitmap[]
            {
               Properties.Resources.Landscape,Properties.Resources.Floor,Properties.Resources.Font,Properties.Resources.Turn,
               Properties.Resources.TurnCancel,Properties.Resources.TurnManual,Properties.Resources.Compass,Properties.Resources.Speedo,
               Properties.Resources.SpeedoNeedle,Properties.Resources.Lift,Properties.Resources.LandscapeNight,Properties.Resources.Steer
            };

            for (int h = 0; h < tt.Length; h++)
            {
                using (Bitmap bitmap = tt[h])
                {
                    GL.GenTextures(1, out texture[h]);
                    GL.BindTexture(TextureTarget.Texture2D, texture[h]);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 9729);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, 9729);
                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmapData.Width, bitmapData.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bitmapData.Scan0);
                    bitmap.UnlockBits(bitmapData);
                    if (h == 2)
                    {
                        font.textureWidth = bitmap.Width; font.textureHeight = bitmap.Height;
                    }
                    else if (h == 3 || h == 4 || h == 5 || h == 8 || h == 9)
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    else if (h == 6)
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, 9726);
                }
            }
        }// Load Bitmaps And Convert To Textures


        public void SwapDirection(bool Reset = true)
        {
            if (!Guidance.isYouTurnTriggered)
            {
                Guidance.isYouTurnRight = !Guidance.isYouTurnRight;
                if (Reset) Guidance.ResetCreatedYouTurn();
            }
        }

        public void KeyboardToText(TextBox sender, Form callingForm)
        {
            sender.BackColor = Color.Red;
            using (var form = new FormKeyboard(sender.Text, callingForm))
            {
                var result = form.ShowDialog(callingForm);
                if (result == DialogResult.OK)
                {
                    sender.Text = form.ReturnString;
                }
            }
            sender.BackColor = Color.AliceBlue;
        }

        public void CheckToolSettings()
        {
            if (Properties.Vehicle.Default.ToolSettings == null)
            {
                Properties.Vehicle.Default.ToolSettings = new List<ToolSettings>() { new ToolSettings() { Sections = { new double[] { -4.415, 3.0}, new double[] { 1.415, 3.0} } }, new ToolSettings() { HitchLength = 3.0, BehindPivot = false, Sections = { new double[] { -1.5, 3.0 } } } };
                Properties.Vehicle.Default.Save();
            }
        }

        //function to set section positions
        //function to calculate the width of each section and update
        public void LoadTools()
        {
            CheckToolSettings();

            for (int i = Tools.Count - 1; i >= Properties.Vehicle.Default.ToolSettings.Count; i--)
            {
                TotalSections -= Tools[i].numOfSections;
                Tools[i].RemoveSections();
                Tools.RemoveAt(i);
            }

            for (int i = 0; i < Properties.Vehicle.Default.ToolSettings.Count; i++)
            {
                if (Tools.Count <= i)
                {
                    Tools.Add(new CTool(this, Tools.Count));
                }
                else
                {
                    Tools[i].SetToolSettings(i);
                }
            }
            LineUpManualBtns();
        }

        //request a new job
        public void JobNew()
        {
            if (Properties.Settings.Default.setMenu_isOGLZoomOn == 1)
            {
                oglZoom.BringToFront();
                oglZoom.Width = 300;
                oglZoom.Height = 300;
            }
            isJobStarted = true;

            MinuteCounter = 0;

            btnSection_Update();

            //update the menu
            layoutPanelRight.Enabled = true;
            toolStripBtnDropDownBoundaryTools.Enabled = true;
        }

        //close the current job
        public void JobClose()
        {
            //zoom gone
            oglZoom.SendToBack();

            isJobStarted = false;

            layoutPanelRight.Enabled = false;
            toolStripBtnDropDownBoundaryTools.Enabled = false;

            snapLeftBigStrip.Enabled = false;
            snapRightBigStrip.Enabled = false;
            snapToCurrent.Enabled = false;

            //CurveLine
            btnGuidance.Image = Properties.Resources.CurveOff;

            Guidance.BtnGuidanceOn = false;
            Guidance.isOkToAddPoints = false;
            Guidance.isEditing = false;
            Guidance.ExtraGuidanceLines.Clear();
            Guidance.Lines.Clear();
            Guidance.CurrentLine = -1;
            Guidance.ResetABLine = true;
            btnCycleLines.Text = String.Get("gsOff");

            //TramLines
            Guidance.TramDisplayMode = 0;
            Guidance.TramList.Clear();
            Guidance.BoundaryTram.Clear();
            Guidance.ResetBoundaryTram = true;

            //turn off headland
            btnHeadlandOnOff.Image = Properties.Resources.HeadlandOff;
            bnd.BtnHeadLand = false;
            btnHydLift.Image = Properties.Resources.HydraulicLiftOff;
            vehicle.BtnHydLiftOn = false;

            SetPlaneToLocal(Latitude, Longitude);

            bnd.Boundaries.Clear();
            PatchSaveList.Clear();
            PatchDrawList.Clear();

            autoBtnState = btnStates.Off;
            btnSection_Update();

            for (int i = 0; i < Tools.Count; i++)
            {
                for (int j = 0; j < Tools[i].Sections.Count; j++)
                {
                    //clear the section lists
                    Tools[i].Sections[j].triangleList.Clear();
                }
            }

            //clear the flags
            flagPts.Clear();

            //AutoSteer
            isAutoSteerBtnOn = false;
            btnAutoSteer.Image = Properties.Resources.AutoSteerOff;

            //auto YouTurn shutdown
            YouTurnButtons(false);

            //reset acre and distance counters
            fd.workedAreaTotal = 0;

            ////turn off path record
            recPath.recList.Clear();
            if (recPath.isRecordOn)
            {
                recPath.isRecordOn = false;
                recordPathMenu.Image = Properties.Resources.BoundaryRecord;
            }

            //reset all Port Module values
            mc.ResetAllModuleCommValues(true);
        }

        //bring up field dialog for new/open/resume
        private void JobNewOpenResume()
        {
            //bring up dialog if no job active, close job if one is
            if (!isJobStarted)
            {
                using (var form = new FormJob(this))
                {
                    DialogResult result = form.ShowDialog(this);

                    if (result == DialogResult.Retry)
                    {
                        //ask for a directory name
                        using (var form2 = new FormFieldDir(this))
                        {
                            form2.ShowDialog(this);
                        }
                    }
                    else if (result == DialogResult.Yes)
                    {
                        FileOpenField();
                        Text = "AgOpenGPS - " + currentFieldDirectory;
                    }
                }
            }

            //close the current job and ask how to or if to save
            else
            {
                using (FormSaveOrNot form = new FormSaveOrNot(false, this))
                {
                    DialogResult result = form.ShowDialog(this);
                    if (result != DialogResult.Ignore)
                    {
                        Properties.Settings.Default.setF_CurrentDir = currentFieldDirectory;
                        Properties.Settings.Default.Save();
                        FileSaveEverythingBeforeClosingField();
                        if (result == DialogResult.Yes)
                        {
                            //ask for a directory name
                            using (FormSaveAs form2 = new FormSaveAs(this))
                            {
                                form2.ShowDialog(this);
                            }
                        }
                    }
                }
            }
        }

        //Does the logic to process section on off requests
        private void ProcessSectionOnOffRequests()
        {
            int counter = 0;
            for (int i = 0; i < Tools.Count; i++)
            {
                counter += Tools[i].numOfSections;
                for (int j = 0; j < Tools[i].numOfSections; j++)
                {
                    //SECTIONS - 
                    if (Tools[i].Sections[j].SectionOnRequest)
                    {
                        if (!Tools[i].Sections[j].IsSectionOn)
                        {
                            UpdateSendDataText("Sections Status: Tool " + (i + 1).ToString() + ", Section " + (j + 1).ToString() + ", State On");
                            Tools[i].Sections[j].IsSectionOn = true;
                        }
                        Tools[i].Sections[j].SectionOverlapTimer = (int)(HzTime * Tools[i].TurnOffDelay + 1);

                        if (Tools[i].Sections[j].MappingOnTimer == 0) Tools[i].Sections[j].MappingOnTimer = (int)(HzTime * Tools[i].MappingOnDelay + 1);
                    }
                    else
                    {
                        if (Tools[i].Sections[j].SectionOverlapTimer > 0) Tools[i].Sections[j].SectionOverlapTimer--;
                        if (Tools[i].Sections[j].IsSectionOn && Tools[i].Sections[j].SectionOverlapTimer == 0)
                        {
                            Tools[i].Sections[j].IsSectionOn = false;
                            UpdateSendDataText("Sections Status: Tool " + (i + 1).ToString() + ", Section " + (j + 1).ToString() + ", State Off");
                        }
                    }

                    //MAPPING -
                    
                    if (Tools[i].SuperSection)
                    {
                        if (Tools[i].Sections[j].IsMappingOn)
                        {
                            Tools[i].TurnMappingOff(j);
                            Tools[i].Sections[j].MappingOnTimer = 1;
                        }
                    }
                    else
                    {
                        if (!Tools[i].Sections[j].IsMappingOn && Tools[i].Sections[j].MappingOnTimer > 0)
                        {
                            if (Tools[i].Sections[j].MappingOnTimer > 0) Tools[i].Sections[j].MappingOnTimer--;
                            if (Tools[i].Sections[j].MappingOnTimer == 0)
                            {
                                Tools[i].TurnMappingOn(j);
                            }
                        }

                        if (Tools[i].Sections[j].IsSectionOn)
                        {
                            Tools[i].Sections[j].MappingOffTimer = (int)(Tools[i].MappingOffDelay * HzTime + 1);
                        }
                        else
                        {
                            if (Tools[i].Sections[j].MappingOffTimer > 0) Tools[i].Sections[j].MappingOffTimer--;
                            if (Tools[i].Sections[j].MappingOffTimer == 0)
                            {
                                if (Tools[i].Sections[j].IsMappingOn)
                                {
                                    Tools[i].TurnMappingOff(j);
                                    Tools[i].Sections[j].MappingOnTimer = 0;
                                }
                            }
                        }
                    }
                }

                if (Tools[i].SuperSection)
                {
                    if (!Tools[i].Sections[Tools[i].numOfSections].IsMappingOn)
                    {
                        Tools[i].TurnMappingOn(Tools[i].numOfSections);
                    }
                }
                else if (Tools[i].numOfSections > 0)
                {
                    if (Tools[i].Sections[Tools[i].numOfSections].IsMappingOn)
                    {
                        Tools[i].TurnMappingOff(Tools[i].numOfSections);
                    }
                }
            }

            if (counter > 2000) counter = 2000;

            mc.Send_Sections = new byte[(int)(Math.Ceiling(counter / 8.0) + 7)];

            mc.Send_Sections[0] = 0x80;
            mc.Send_Sections[1] = 0x81;
            mc.Send_Sections[2] = 0x7F;
            mc.Send_Sections[3] = 0x71;
            mc.Send_Sections[4] = (byte)(Math.Ceiling(counter / 8.0));
            mc.Send_Sections[5] = (byte)(counter % 8.0);


            int set = 1;
            int machine = 0;
            int idx = 6;

            for (int i = 0; i < Tools.Count; i++)
            {
                for (int j = 0; j < Tools[i].numOfSections; j++)
                {
                    if (Tools[i].SuperSection || Tools[i].Sections[j].IsSectionOn) machine |= set;
                    set <<= 1;
                    if (set == 256)
                    {
                        mc.Send_Sections[idx++] = (byte)machine;
                        set = 1;
                        machine = 0;
                    }
                }
            }
            if (set > 1)
                mc.Send_Sections[idx++] = (byte)machine;

            SendData(mc.Send_Sections, false);
        }

        //called by you turn class to set control byte, click auto man buttons
        public void DoYouTurnSequenceEvent(int function, int action)
        {
            switch (function)
            {
                case 0: //should not be here - it means no function at all
                    TimedMessageBox(2000, "ID 0 ??????", "YouTurn fucked up");
                    break;

                case 1: //Manual button
                    if (action == 0) autoBtnState = btnStates.Off;
                    else autoBtnState = btnStates.On;
                    btnSection_Update();
                    break;

                case 2: //Auto Button
                    if (action == 0) autoBtnState = btnStates.Off;
                    else autoBtnState = btnStates.Auto;
                    btnSection_Update();
                    break;

                case 3: //Machine 1
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos3, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11111110;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos3, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00000001;
                    }
                    break;

                case 4: //Machine 2
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos4, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11111101;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos4, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00000010;
                    }
                    break;

                case 5: //Machine 3
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos5, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11111011;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos5, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00000100;
                    }
                    break;

                case 6: //Machine 4
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos6, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11110111;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos6, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00001000;
                    }
                    break;

                case 7: //Machine 5
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos7, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11101111;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos7, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00010000;
                    }
                    break;

                case 8: //Machine 6
                    if (action == 0)
                    {
                        TimedMessageBox(1000, seq.pos8, String.Get("gsTurnOff"));
                        mc.Send_Uturn[5] &= 0b11011111;
                    }
                    else
                    {
                        TimedMessageBox(1000, seq.pos8, String.Get("gsTurnOn"));
                        mc.Send_Uturn[5] |= 0b00100000;
                    }
                    break;
            }
        }

        //take the distance from object and convert to camera data
        public void SetZoom(double Distance)
        {
            //match grid to cam distance and redo perspective
            if (Distance <= -20000) camera.gridZoom = 4000;
            else if (Distance >= -20000 && Distance < -10000) camera.gridZoom = 4000;
            else if (Distance >= -10000 && Distance < -5000) camera.gridZoom = 2000;
            else if (Distance >= -5000 && Distance < -2000) camera.gridZoom = 1000;
            else if (Distance >= -2000 && Distance < -1000) camera.gridZoom = 400;
            else if (Distance >= -1000 && Distance < -500) camera.gridZoom = 200;
            else if (Distance >= -500 && Distance < -250) camera.gridZoom = 100;
            else if (Distance >= -250 && Distance < -150) camera.gridZoom = 50;
            else if (Distance >= -150 && Distance < -50) camera.gridZoom = 30;
            else if (Distance >= -50 && Distance < -1) camera.gridZoom = 10;
        }

        //All the files that need to be saved when closing field or app
        public void FileSaveEverythingBeforeClosingField()
        {
            if (isJobStarted)
            {
                //turn off all the sections
                for (int i = 0; i < Tools.Count; i++)
                {
                    for (int j = 0; j < Tools[i].Sections.Count; j++)
                    {
                        if (Tools[i].Sections[j].IsMappingOn) Tools[i].TurnMappingOff(j);
                        Tools[i].Sections[j].SectionOnRequest = false;
                    }
                }
                isJobStarted = false;
                StartTasks(null, 0, TaskName.Save);
                StartTasks(null, 0, TaskName.CloseJob);
                Text = "AgOpenGPS";
            }
        }

        public void DrawPatchList(int mipmap)
        {
            //for every new chunk of patch
            for (int i = 0; i < PatchDrawList.Count; i++)
            {
                bool isDraw = true;
                for (int k = 1; k < PatchDrawList[i].Count; k += 3)
                {
                    //determine if point is in frustum or not, if < 0, its outside so abort, z always is 0                            
                    if (frustum[0] * PatchDrawList[i][k].Easting + frustum[1] * PatchDrawList[i][k].Northing + frustum[3] <= 0)
                        continue;//right
                    if (frustum[4] * PatchDrawList[i][k].Easting + frustum[5] * PatchDrawList[i][k].Northing + frustum[7] <= 0)
                        continue;//left
                    if (frustum[16] * PatchDrawList[i][k].Easting + frustum[17] * PatchDrawList[i][k].Northing + frustum[19] <= 0)
                        continue;//bottom
                    if (frustum[20] * PatchDrawList[i][k].Easting + frustum[21] * PatchDrawList[i][k].Northing + frustum[23] <= 0)
                        continue;//top
                    if (frustum[8] * PatchDrawList[i][k].Easting + frustum[9] * PatchDrawList[i][k].Northing + frustum[11] <= 0)
                        continue;//far
                    if (frustum[12] * PatchDrawList[i][k].Easting + frustum[13] * PatchDrawList[i][k].Northing + frustum[15] <= 0)
                        continue;//near

                    //point is in frustum so draw the entire patch. The downside of triangle strips.
                    isDraw = true;
                    break;
                }

                if (isDraw)
                {
                    if (PatchDrawList[i].Count < 4) continue;
                    //draw the triangle in each triangle strip
                    GL.Begin(PrimitiveType.TriangleStrip);

                    if (isDay) GL.Color4((byte)PatchDrawList[i][0].Northing, (byte)PatchDrawList[i][0].Easting, (byte)PatchDrawList[i][0].Heading, (byte)152);
                    else GL.Color4((byte)PatchDrawList[i][0].Northing, (byte)PatchDrawList[i][0].Easting, (byte)PatchDrawList[i][0].Heading, (byte)(152 * 0.5));

                    //if large enough patch and camera zoomed out, fake mipmap the patches, skip triangles
                    if (PatchDrawList[i].Count >= (mipmap + 2))
                    {
                        int step = mipmap;
                        for (int k = 1; k + 1 < PatchDrawList[i].Count; k += step)
                        {
                            GL.Vertex3(PatchDrawList[i][k].Easting, PatchDrawList[i][k].Northing, 0); k++;
                            GL.Vertex3(PatchDrawList[i][k].Easting, PatchDrawList[i][k].Northing, 0); k++;
                            if (PatchDrawList[i].Count - k <= (mipmap + 2)) step = 0;//too small to mipmap it
                        }
                    }
                    else { for (int k = 1; k < PatchDrawList[i].Count; k++) GL.Vertex3(PatchDrawList[i][k].Easting, PatchDrawList[i][k].Northing, 0); }
                    GL.End();
                }
            }
        }

        public void DrawSectionsPatchList(bool color)
        {
            // the follow up to sections patches
            int patchCount;

            for (int i = 0; i < Tools.Count; i++)
            {
                if (Tools[i].Sections.Count > 0)
                {
                    Vec2 diff = (Tools[i].RightPoint - Tools[i].LeftPoint) / Tools[i].ToolWidth;

                    for (int j = 0; j < Tools[i].Sections.Count; j++)
                    {
                        // the follow up to sections patches
                        if (Tools[i].Sections[j].IsMappingOn && (patchCount = Tools[i].Sections[j].triangleList.Count) > 0)
                        {
                            if (color)
                            {
                                if (isDay) GL.Color4((byte)Tools[i].Sections[j].triangleList[0].Northing, (byte)Tools[i].Sections[j].triangleList[0].Easting, (byte)Tools[i].Sections[j].triangleList[0].Heading, (byte)152);
                                else GL.Color4((byte)Tools[i].Sections[j].triangleList[0].Northing, (byte)Tools[i].Sections[j].triangleList[0].Easting, (byte)Tools[i].Sections[j].triangleList[0].Heading, (byte)(152 * 0.5));
                            }
                            //draw the triangle in each triangle strip
                            GL.Begin(PrimitiveType.TriangleStrip);

                            for (int k = 1; k < patchCount; k++)
                            {
                                GL.Vertex3(Tools[i].Sections[j].triangleList[k].Easting, Tools[i].Sections[j].triangleList[k].Northing, 0);
                            }
                            Vec2 Pos = Tools[i].LeftPoint + diff * (Tools[i].ToolWidth / 2 + Tools[i].Sections[j].positionLeft);
                            GL.Vertex3(Pos.Easting, Pos.Northing, 0);
                            Pos = Tools[i].LeftPoint + diff * (Tools[i].ToolWidth / 2 + Tools[i].Sections[j].positionRight);
                            GL.Vertex3(Pos.Easting, Pos.Northing, 0);

                            GL.End();
                        }
                    }
                }
            }
        }

        //an error log called by all try catches
        public void WriteErrorLog(string strErrorText)
        {
            try
            {
                //set up file and folder if it doesn't exist
                const string strFileName = "Error Log.txt";

                //Write out the error appending to existing
                File.AppendAllText(baseDirectory + "\\" + strFileName, strErrorText + " - " +
                    DateTime.Now.ToString() + "\r\n\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error in WriteErrorLog: " + ex.Message, "Error Logging", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        //message box pops up with info then goes away
        public void TimedMessageBox(int timeout, string s1, string s2)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke((MethodInvoker)(() => TimedMessageBox(timeout, s1, s2)));
                return;
            }
            Form.SetTimedMessage(timeout, s1, s2, this);
        }
    }//class FormGPS
}//namespace AgOpenGPS

/*The order is:
 *
 * The watchdog timer times out and runs this function ScanForNMEA_Tick().
 * 50 times per second so statusUpdateCounter counts to 25 and updates strip menu etc at 2 hz
 * it also makes sure there is new sentences showing up otherwise it shows **** No GGA....
 * saveCounter ticks 1 x per second, used at end of draw routine every minute to save a backup of field
 * then ScanForNMEA function checks for a complete sentence if contained in pn.rawbuffer
 * if not it comes right back and waits for next watchdog trigger and starts all over
 * if a new sentence is there, UpdateFix() is called
 * Right away CalculateLookAhead(), no skips, is called to determine lookaheads and trigger distances to save triangles plotted
 * Then UpdateFix() continues.
 * Hitch, pivot, antenna locations etc and directions are figured out if trigDistance is triggered
 * When that is done, DoRender() is called on the visible OpenGL screen and its draw routine _draw is run
 * before triangles are drawn, frustum cull figures out how many of the triangles should be drawn
 * When its all the way thru, it triggers the sectioncontrol Draw, its frustum cull, and determines if sections should be on
 * ProcessSectionOnOffRequests() runs and that does the section on off magic
 * SectionControlToArduino() runs and spits out the port machine control based on sections on or off
 * If field needs saving (1.5 minute since last time) field is saved
 * Now the program is "Done" and waits for the next watchdog trigger, determines if a new sentence is available etc
 * and starts all over from the top.
 */
