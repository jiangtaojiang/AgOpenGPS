using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //very first fix to setup grid etc
        public bool isGPSPositionInitialized = false, StableHeading = false;

        double xTrackCorrection, abFixHeadingDelta;
        //string to record fixes for elevation maps
        public StringBuilder sbFix = new StringBuilder();

        // autosteer variables for sending serial
        public Int16 guidanceLineDistanceOff, guidanceLineSteerAngle, distanceDisplay;
        public Vec2 GoalPoint = new Vec2(0, 0), radiusPoint = new Vec2(0, 0);
        public double distanceFromCurrentLine, steerAngle, rEast, rNorth, ppRadius;
        public int currentLocationIndexA,currentLocationIndexB, A, B;

        //how many fix updates per sec
        public int fixUpdateHz = 8;
        public double fixUpdateTime = 0.125;

        //for heading or Atan2 as camera
        public string HeadingFromSource;

        public Vec3 pivotAxlePos = new Vec3(0, 0, 0), steerAxlePos = new Vec3(0, 0, 0);

        //headings
        public double fixHeading = 0.0, camHeading = 0.0, treeSpacingCounter = 0.0;
        public byte treeTrigger = 0x00;

        //how far travelled since last section was added, section points
        double sectionTriggerStepDistance = 0;

        public Vec2 prevSectionPos = new Vec2(0, 0), prevBoundaryPos = new Vec2(0, 0);

        public bool NotLoadedField = true;

        //are we still getting valid data from GPS, resets to 0 in NMEA OGI block, watchdog 
        public int recvCounter = 0;

        //Everything is so wonky at the start
        int startCounter = 50;

        //individual points for the flags in a list
        public List<CFlag> flagPts = new List<CFlag>();

        //tally counters for display
        //public double totalSquareMetersWorked = 0, totalUserSquareMeters = 0, userSquareMetersAlarm = 0;

        public double avgSpeed;//for average speed
        public int crossTrackError;

        //youturn
        public double distancePivotToTurnLine = -2222;
        public int Geofence;
        public double distanceToolToTurnLine = -2222;

        //the value to fill in you turn progress bar
        public int youTurnProgressBar = 0;

        //IMU 
        public double rollCorrectionDistance = 0, DualAntennaDistance = 1.40;
        double gyroCorrection, gyroCorrected;

        //step position - slow speed spinner killer
        private static int totalFixSteps = 30;
        public Vec3[] stepFixPts = new Vec3[totalFixSteps];
        
        public double distanceCurrentStepFix = 0, FixStepDist = 0, DualHeadingCorrection = 0;

        public double nowHz = 0, rollUsed;

        public bool isRTK;
        // Instantiate random number generator.  
        private readonly Random _random = new Random();
        public double RandomNumber(int min, int max)
        {
            return min + _random.NextDouble() * (max - min);
        }

        private void ScanForNMEA_Tick(object sender, EventArgs e)
        {
            NMEAWatchdog.Enabled = false;

            NMEAWatchdog.Interval = 15;
            oglMain.Refresh();

            NMEAWatchdog.Enabled = true;
        }

        public void UpdateFixPosition()
        {
            NMEAWatchdog.Interval = 2000;
            NMEAWatchdog.Stop();
            NMEAWatchdog.Start();

            //Measure the frequency of the GPS updates
            swHz.Stop();
            nowHz = ((double)System.Diagnostics.Stopwatch.Frequency) / (double)swHz.ElapsedTicks;

            //simple comp filter
            if (nowHz < 20) HzTime = 0.95 * HzTime + 0.05 * nowHz;
            fixUpdateTime = 1.0 / (double)HzTime;

            swHz.Reset();
            swHz.Start();

            //start the watch and time till it finishes
            swFrame.Reset();
            swFrame.Start();

            recvCounter = 0;

            if (startCounter > 0) startCounter--;

            if (!isGPSPositionInitialized)
            {
                InitializeFirstFewGPSPositions();
                LoadFields();
            }
            else
            {
                ConvertWGS84ToLocal(Latitude, Longitude, out pn.fix.Northing, out pn.fix.Easting);

                #region Heading
                if ((pn.HeadingForced != 9999 && HeadingFromSource != "Fix") || timerSim.Enabled)
                {
                    //off for testing with sim
                    camHeading = pn.HeadingForced;
                    fixHeading = Glm.ToRadians(pn.HeadingForced);
                    StableHeading = true;
                }
                #endregion Heading

                if (StableHeading)
                {
                    #region Antenna Offset
                    if (vehicle.antennaOffset != 0)
                    {
                        pn.fix.Northing += (Math.Sin(fixHeading) * vehicle.antennaOffset);
                        pn.fix.Easting -= (Math.Cos(fixHeading) * vehicle.antennaOffset);
                    }
                    #endregion

                    #region Roll
                    rollUsed = 0;
                    //used only for draft compensation in OGI Sentence
                    if (ahrs.rollX16 != 9999 && ahrs.isRollFromOGI) rollUsed = ((double)(ahrs.rollX16 - ahrs.rollZeroX16)) * 0.0625;
                    else if (ahrs.rollX16 != 9999 && (ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR))
                    {
                        rollUsed = (ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625;

                        //change for roll to the right is positive times -1
                        rollCorrectionDistance = Math.Sin(Glm.ToRadians(rollUsed)) * -vehicle.antennaHeight;

                        // roll to left is positive  **** important!!
                        // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                        pn.fix.Northing += (Math.Sin(-fixHeading) * rollCorrectionDistance);
                        pn.fix.Easting += (Math.Cos(-fixHeading) * rollCorrectionDistance);
                    }
                    #endregion Roll
                }

                //grab the most current fix and save the distance from the last fix
                distanceCurrentStepFix = Glm.Distance(pn.fix, stepFixPts[0]);

                if (distanceCurrentStepFix > FixStepDist / totalFixSteps && Math.Abs(pn.speed) > 0.1)
                {
                    for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];

                    //**** Time of the vec4 structure is used for distance in Step fix!!!!!
                    stepFixPts[0].Northing = pn.fix.Northing;
                    stepFixPts[0].Easting = pn.fix.Easting;
                    stepFixPts[0].Heading = distanceCurrentStepFix;
                    if ((fd.distanceUser += distanceCurrentStepFix) > 3000) fd.distanceUser %= 3000;

                    double fixStepDist = 0;
                    for (int currentStepFix = 0; currentStepFix < totalFixSteps - 1; currentStepFix++)
                    {
                        //fixStepDist += stepFixPts[currentStepFix].Heading;
                        fixStepDist = ((pn.fix.Easting - stepFixPts[currentStepFix].Easting) * (pn.fix.Easting - stepFixPts[currentStepFix].Easting))
                                        + ((pn.fix.Northing - stepFixPts[currentStepFix].Northing) * (pn.fix.Northing - stepFixPts[currentStepFix].Northing));

                        if (fixStepDist >= FixStepDist / 2)
                        {
                            double Heading = Math.Atan2(pn.fix.Easting - stepFixPts[currentStepFix + 1].Easting, pn.fix.Northing - stepFixPts[currentStepFix + 1].Northing);
                            if (Heading > Glm.twoPI) Heading -= Glm.twoPI;
                            else if (Heading < 0) Heading += Glm.twoPI;

                            if (StableHeading)
                            {
                                if (Math.PI - Math.Abs(Math.Abs(fixHeading - Heading) - Math.PI) < Glm.PIBy2)
                                {
                                    vehicle.isReverse = false;
                                    button1.BackgroundImage = Properties.Resources.UpArrow64;
                                }
                                else
                                {
                                    vehicle.isReverse = true;
                                    button1.BackgroundImage = Properties.Resources.DnArrow64;
                                }
                            }

                            if ((pn.HeadingForced == 9999 || HeadingFromSource == "Fix") && !timerSim.Enabled)
                            {
                                fixHeading = Heading - (vehicle.isReverse ? Math.PI : 0);
                                if (fixHeading > Glm.twoPI) fixHeading -= Glm.twoPI;
                                if (fixHeading < 0) fixHeading += Glm.twoPI;
                                camHeading = Glm.ToDegrees(fixHeading);
                            }

                            if (!StableHeading)
                            {
                                if (vehicle.antennaOffset != 0)
                                {
                                    for (int i = 0; i < totalFixSteps; i++)
                                    {
                                        stepFixPts[i].Northing += (Math.Sin(fixHeading) * vehicle.antennaOffset);
                                        stepFixPts[i].Easting -= (Math.Cos(fixHeading) * vehicle.antennaOffset);
                                    }
                                }
                                if (ahrs.rollX16 != 9999 && (ahrs.isRollFromAutoSteer || ahrs.isRollFromAVR))
                                {
                                    rollUsed = (ahrs.rollX16 - ahrs.rollZeroX16) * 0.0625;

                                    //change for roll to the right is positive times -1
                                    rollCorrectionDistance = Math.Sin(Glm.ToRadians(rollUsed)) * -vehicle.antennaHeight;

                                    for (int i = 0; i < totalFixSteps; i++)
                                    {
                                        stepFixPts[i].Easting += (Math.Cos(-fixHeading) * rollCorrectionDistance);
                                        stepFixPts[i].Northing += (Math.Sin(-fixHeading) * rollCorrectionDistance);
                                    }
                                }

                                StableHeading = true;
                            }
                            break;
                        }
                    }
                }

                if (!StableHeading) return;

                #region Heading Correction
                //an IMU with heading correction, add the correction

                if (ahrs.correctionHeadingX16 != 9999 && (ahrs.isHeadingCorrectionFromBrick || ahrs.isHeadingCorrectionFromAutoSteer))
                {
                    //current gyro angle in radians
                    double correctionHeading = Glm.ToRadians((double)ahrs.correctionHeadingX16 * 0.0625);

                    if (isSimNoisy)
                    {
                        double noisecorrectionHeading = RandomNumber(-1, 1) * 0.05;
                        correctionHeading = pn.HeadingForced + noisecorrectionHeading;
                    }

                    //Difference between the IMU heading and the GPS heading
                    double gyroDelta = (correctionHeading + gyroCorrection) - fixHeading;
                    if (gyroDelta < 0) gyroDelta += Glm.twoPI;

                    //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                    if (gyroDelta >= -Glm.PIBy2 && gyroDelta <= Glm.PIBy2)
                        gyroDelta *= -1.0;
                    else
                    {
                        if (gyroDelta > Glm.PIBy2) gyroDelta = Glm.twoPI - gyroDelta;
                        else gyroDelta = (Glm.twoPI + gyroDelta) * -1.0;
                    }
                    gyroDelta %= Glm.twoPI;

                    //if the gyro and last corrected fix is < 10 degrees, super low pass for gps
                    if (Math.Abs(gyroDelta) < 0.18)
                    {
                        //a bit of delta and add to correction to current gyro
                        gyroCorrection += (gyroDelta * (0.25 / HzTime)) % Glm.twoPI;
                    }
                    else if (Math.Abs(gyroDelta) > Glm.PIBy2)
                    {
                        gyroCorrection = -gyroDelta;
                    }
                    else
                    {
                        //delta add to correction to current gyro
                        gyroCorrection += (gyroDelta * (2.0 / HzTime)) % Glm.twoPI;
                    }

                    //determine the Corrected heading based on gyro and GPS
                    gyroCorrected = (correctionHeading + gyroCorrection) % Glm.twoPI;
                    if (gyroCorrected < 0) gyroCorrected += Glm.twoPI;

                    fixHeading = gyroCorrected;
                    camHeading = Glm.ToDegrees(fixHeading);
                }

                #endregion Heading Correction

                #region Step Fix

                CalculatePositionHeading();

                //test if travelled far enough for new boundary point
                if (Glm.Distance(pn.fix, prevBoundaryPos) > 1) AddBoundaryPoint();

                //tree spacing
                if (vehicle.treeSpacing != 0)
                {
                    if ((Tools[0].Sections[0].IsSectionOn || Tools[0].SuperSection) && (treeSpacingCounter += (distanceCurrentStepFix * 200)) > vehicle.treeSpacing)
                    {
                        treeSpacingCounter %= vehicle.treeSpacing;//keep the distance below spacing
                        mc.Send_Treeplant[5] = (treeTrigger ^= 0x01);
                        UpdateSendDataText("Tree Plant: State " + ((treeTrigger == 0x01) ? "On" : "Off"));
                    }
                    SendData(mc.Send_Treeplant, false);
                }
                else if (vehicle.treeSpacing == 0 && mc.Send_Treeplant[5] == 0x01)
                {
                    mc.Send_Treeplant[3] = 0;
                    UpdateSendDataText("Tree Plant: State Off");
                    SendData(mc.Send_Treeplant, false);
                }

                //test if travelled far enough for new Section point, To prevent drawing high numbers of triangles
                if (isJobStarted && Glm.Distance(pn.fix, prevSectionPos) > sectionTriggerStepDistance)
                {
                    //save the north & east as previous
                    prevSectionPos.Northing = pn.fix.Northing;
                    prevSectionPos.Easting = pn.fix.Easting;

                    if (recPath.isRecordOn)
                    {
                        //keep minimum speed of 1.0
                        double speed = pn.speed;
                        if (pn.speed < 1.0) speed = 1.0;
                        bool autoBtn = (autoBtnState == btnStates.Auto);

                        CRecPathPt pt = new CRecPathPt(steerAxlePos.Easting, steerAxlePos.Northing, steerAxlePos.Heading, pn.speed, autoBtn);
                        recPath.recList.Add(pt);
                    }

                    if (Guidance.isOkToAddPoints && Guidance.CurrentEditLine < Guidance.Lines.Count && Guidance.CurrentEditLine > -1)
                    {
                        Vec2 Point = new Vec2(pivotAxlePos.Easting, pivotAxlePos.Northing);

                        Point.Northing += Math.Sin(pivotAxlePos.Heading) * -Guidance.GuidanceOffset;
                        Point.Easting += Math.Cos(pivotAxlePos.Heading) * Guidance.GuidanceOffset;

                        Guidance.Lines[Guidance.CurrentEditLine].Segments.Add(Point);
                    }

                    // if non zero, at least one section is on.
                    int sectionCounter = 0;

                    for (int i = 0; i < Tools.Count; i++)
                    {
                        //send the current and previous GPS fore/aft corrected fix to each section
                        for (int j = 0; j < Tools[i].Sections.Count; j++)
                        {
                            if (Tools[i].Sections[j].IsMappingOn)
                            {
                                Tools[i].AddMappingPoint(j);
                                sectionCounter++;
                            }
                        }
                    }

                    //grab fix and elevation
                    if (isLogElevation) sbFix.Append(pn.fix.Easting.ToString("N2") + "," + pn.fix.Northing.ToString("N2") + ","
                                                        + pn.altitude.ToString("N2") + ","
                                                        + Latitude + "," + Longitude + "\r\n");
                }

                #endregion fix

                #region AutoSteer

                //preset the values
                guidanceLineDistanceOff = 32000;

                if (Guidance.BtnGuidanceOn)
                {
                    Guidance.GetCurrentLine(pivotAxlePos, steerAxlePos);

                    if (Guidance.isRecordingCustomYouTurn)
                    {
                        //save reference of first point
                        if (Guidance.youFileList.Count == 0)
                        {
                            Vec2 start = new Vec2(steerAxlePos.Easting, steerAxlePos.Northing);
                            Guidance.youFileList.Add(start);
                        }
                        else
                        {
                            //keep adding points
                            Vec2 point = new Vec2(steerAxlePos.Easting - Guidance.youFileList[0].Easting, steerAxlePos.Northing - Guidance.youFileList[0].Northing);
                            Guidance.youFileList.Add(point);
                        }
                    }
                }

                // autosteer at full speed of updates
                if (!isAutoSteerBtnOn) //32020 means auto steer is off
                {
                    guidanceLineDistanceOff = 32020;
                }

                //if the whole path driving driving process is green
                if (recPath.isDrivingRecordedPath) recPath.UpdatePosition();


                //sidehill draft compensation
                if (rollUsed != 0)
                {
                    guidanceLineSteerAngle = (Int16)(guidanceLineSteerAngle +
                        ((-rollUsed) * ((double)mc.Config_AutoSteer[mc.ssKd] / 50)) * 500);
                }

                pn.speed.LimitToRange(-163, 163);

                mc.Send_AutoSteer[5] = unchecked((byte)((int)(pn.speed * 200.0) >> 8));
                mc.Send_AutoSteer[6] = unchecked((byte)(pn.speed * 200.0));
                mc.Send_AutoSteer[7] = unchecked((byte)(guidanceLineDistanceOff >> 8));
                mc.Send_AutoSteer[8] = unchecked((byte)(guidanceLineDistanceOff));
                mc.Send_AutoSteer[9] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                mc.Send_AutoSteer[10] = unchecked((byte)(guidanceLineSteerAngle));

                if (TestAutoSteer)//32030
                {
                    mc.Send_AutoSteer[7] = 0x7D;
                    mc.Send_AutoSteer[8] = 0x1E;
                }

                //UpdateSendDataText("Auto Steer: Speed " + ((int)(pn.speed * 200.0)/200.0).ToString("N2") + ", Distance " + guidanceLineDistanceOff.ToString() + ", Angle " + guidanceLineSteerAngle.ToString());
                SendData(mc.Send_AutoSteer, false);

                //for average cross track error
                if (guidanceLineDistanceOff < 29000)
                {
                    crossTrackError = (int)((double)crossTrackError * 0.90 + Math.Abs((double)guidanceLineDistanceOff) * 0.1);
                }
                else crossTrackError = 0;

                #endregion

                #region Youturn

                //reset the fault distance to an appropriate weird number
                //-2222 means it fell out of the loop completely
                //-3333 means unable to find a nearest point at all even though inside the work area of field
                // -4444 means cross trac error too high
                distancePivotToTurnLine = -4444;

                //always force out of bounds and change only if in bounds after proven so
                //mc.isOutOfBounds = true;

                //if an outer boundary is set, then apply critical stop logic
                if (bnd.Boundaries.Count > 0)
                {
                    //Are we inside outer and outside inner all turn boundaries, no turn creation problems
                    if (IsInsideGeoFence() && !Guidance.isTurnCreationTooClose && !Guidance.isTurnCreationNotCrossingError)
                    {
                        NotLoadedField = true;
                        //reset critical stop for bounds violation
                        if (mc.isOutOfBounds)
                        {
                            mc.Send_Uturn[5] |= 0x80;
                            mc.isOutOfBounds = false;
                            UpdateSendDataText("Uturn: " + Convert.ToString(mc.Send_Uturn[5], 2).PadLeft(8, '0'));
                        }

                        //do the auto youturn logic if everything is on.
                        if (Guidance.isYouTurnBtnOn && isAutoSteerBtnOn)
                        {
                            //if we are too much off track > 1.3m, kill the diagnostic creation, start again
                            if (crossTrackError > 1000 && !Guidance.isYouTurnTriggered)
                            {
                                Guidance.ResetCreatedYouTurn();
                            }
                            else
                            {
                                //now check to make sure we are not in an inner turn boundary - drive thru is ok
                                if (Guidance.youTurnPhase == -1) Guidance.youTurnPhase++;
                                else if (Guidance.youTurnPhase != 4)
                                {
                                    if (Guidance.CurrentLine < Guidance.Lines.Count && Guidance.CurrentLine > -1 && crossTrackError < 1000)
                                    {
                                        if (Guidance.YouTurnType == 0)
                                        {
                                            if (Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.AB || Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.Heading) Guidance.BuildABLinePatternYouTurn(Guidance.isYouTurnRight);
                                            else Guidance.BuildCurvePatternYouTurn(Guidance.isYouTurnRight, pivotAxlePos);
                                        }
                                        else if (Guidance.YouTurnType == 2 && (Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.AB || Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.Heading))
                                        {
                                            Guidance.BuildABLineCurveYouTurn();
                                        }
                                        else
                                        {
                                            //yt.BuildGuidanceYouTurn();
                                            if (Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.AB || Guidance.Lines[Guidance.CurrentLine].Mode == Gmode.Heading) Guidance.BuildABLineDubinsYouTurn(Guidance.isYouTurnRight);
                                            else Guidance.BuildCurveDubinsYouTurn(Guidance.isYouTurnRight, pivotAxlePos);
                                        }
                                    }
                                    else Guidance.ResetCreatedYouTurn();
                                }
                                else if (Guidance.ytList.Count > 1)//wait to trigger the actual turn since its made and waiting
                                {
                                    //distance from current pivot to first point of youturn pattern
                                    distancePivotToTurnLine = Glm.Distance(Guidance.ytList[0], steerAxlePos);

                                    if ((distancePivotToTurnLine <= 20.0) && (distancePivotToTurnLine >= 18.0) && !Guidance.isYouTurnTriggered)

                                        if (!isBoundAlarming)
                                        {
                                            SndBoundaryAlarm.Play();
                                            isBoundAlarming = true;
                                        }

                                    //if we are close enough to pattern, trigger.
                                    if ((distancePivotToTurnLine <= 10.0) && (distancePivotToTurnLine >= 0) && !Guidance.isYouTurnTriggered)
                                    {
                                        double dx = Guidance.ytList[1].Northing - Guidance.ytList[0].Northing;
                                        double dy = Guidance.ytList[1].Easting - Guidance.ytList[0].Easting;
                                        double Time = ((steerAxlePos.Northing - Guidance.ytList[0].Northing) * dx + (steerAxlePos.Easting - Guidance.ytList[0].Easting) * dy) / (dx * dx + dy * dy);

                                        if (Time > 0)
                                        {
                                            Guidance.YouTurnTrigger();
                                            isBoundAlarming = false;
                                        }
                                    }
                                }
                            }
                        }
                        else if (distanceFromCurrentLine > 1300 && !Guidance.isYouTurnTriggered)
                            Guidance.ResetCreatedYouTurn();
                    }
                    // here is stop logic for out of bounds - in an inner or out the outer turn border.
                    else
                    {
                        if (!mc.isOutOfBounds)
                        {
                            mc.Send_Uturn[5] &= 0x7F;
                            mc.isOutOfBounds = true;
                            UpdateSendDataText("Uturn: " + Convert.ToString(mc.Send_Uturn[5], 2).PadLeft(8, '0'));
                        }

                        if (Guidance.isYouTurnBtnOn)
                        {
                            Guidance.ResetCreatedYouTurn();
                            sim.stepDistance = 0;
                            sim.reverse = false;
                            btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;
                        }
                    }
                }
                else
                {
                    if (mc.isOutOfBounds)
                    {
                        mc.Send_Uturn[5] |= 0x80;
                        mc.isOutOfBounds = false;
                        UpdateSendDataText("Uturn: " + Convert.ToString(mc.Send_Uturn[5], 2).PadLeft(8, '0'));
                    }
                    if (isJobStarted) Geofence = 0;
                    else Geofence = -1;
                }
                SendData(mc.Send_Uturn, false);

                #endregion
            }
            //update Back
            oglBack.Refresh();

            swFrame.Stop();
            //stop the timer and calc how long it took to do calcs and draw
            FrameTime = (double)swFrame.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000;

            //start the watch and time till it finishes
            swFrame.Reset();
            swFrame.Start();
            //Update Main window
            oglMain.Refresh();

            swFrame.Stop();
            //stop the timer and calc how long it took to do calcs and draw
            FrameTime2 = (double)swFrame.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000;

        }

        public void CalculateSteerAngle(ref List<Vec2> Points2, bool isSameWay, bool Loop = false)
        {
            List<Vec2> Points = (Guidance.isYouTurnTriggered) ? Guidance.ytList : Points2;
            if (Points.Count > 1)
            {
                bool useSteer = isStanleyUsed;

                if (!vehicle.isSteerAxleAhead) useSteer = !useSteer;
                if (vehicle.isReverse) useSteer = !useSteer;

                Vec3 point = (useSteer) ? steerAxlePos : pivotAxlePos;

                int OldA = A;
                double minDist = double.PositiveInfinity;

                int i = Points.Count - 1;
                for (int j = 0; j < Points.Count; i = j++)
                {
                    if (j == 0 && !Loop) continue;

                    double dist = point.FindDistanceToSegment(Points[i], Points[j]);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        A = i;
                        B = j;
                    }
                }

                if (Guidance.isYouTurnTriggered)
                {
                    isSameWay = true;
                    //used for sequencing to find entry, exit positioning
                    double calc = 100 / Guidance.ytLength;
                    double ytLength = 0;

                    for (int k = 0; k+1 < Guidance.ytList.Count && k+1 < (A > B ? A : B); k++)
                    {
                        ytLength += Glm.Distance(Guidance.ytList[k], Guidance.ytList[k + 1]);
                    }
                    ytLength += Glm.Distance(Guidance.ytList[A > B ? B : A], point);
                    
                    Guidance.onA = ytLength;

                    //return and reset if too far away or end of the line
                    if (B >= Points.Count - 1)
                    {
                        seq.DoSequenceEvent(true);
                        Guidance.CompleteYouTurn();
                        return;
                    }
                    else seq.DoSequenceEvent(false);
                }

                //just need to make sure the points continue ascending or heading switches all over the place
                if (A > B) { int C = A; A = B; B = C; }

                if (Loop && A == 0 && B == Points.Count - 1) { int C = A; A = B; B = C; }

                currentLocationIndexA = A;
                currentLocationIndexB = B;

                //get the distance from currently active AB line
                double Dx = Points[B].Northing - Points[A].Northing;
                double Dy = Points[B].Easting - Points[A].Easting;

                if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) return;

                double Heading = Math.Atan2(Dy, Dx);

                //how far from current AB Line is fix
                distanceFromCurrentLine = ((Dx * point.Easting) - (Dy * point.Northing) + (Points[B].Easting
                            * Points[A].Northing) - (Points[B].Northing * Points[A].Easting))
                                / Math.Sqrt((Dx * Dx) + (Dy * Dy));

                // ** Pure pursuit ** - calc point on ABLine closest to current position
                double U = (((point.Easting - Points[A].Easting) * Dy)
                            + ((point.Northing - Points[A].Northing) * Dx))
                            / ((Dy * Dy) + (Dx * Dx));

                rEast = Points[A].Easting + (U * Dy);
                rNorth = Points[A].Northing + (U * Dx);

                if (isStanleyUsed)
                {
                    abFixHeadingDelta = fixHeading - Heading;

                    if (!isSameWay)
                    {
                        distanceFromCurrentLine *= -1.0;
                        abFixHeadingDelta += Math.PI;
                    }

                    //Fix the circular error
                    while (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Glm.twoPI;
                    while (abFixHeadingDelta < -Math.PI) abFixHeadingDelta += Glm.twoPI;


                    vehicle.avgDist = (1 - vehicle.avgXTE) * distanceFromCurrentLine + vehicle.avgXTE * vehicle.avgDist;
                    distanceFromCurrentLine = vehicle.avgDist;
                    double calc = ((Math.Abs(pn.speed * 0.277777)) + 2);

                    xTrackCorrection = Math.Cos(abFixHeadingDelta) * Math.Atan((distanceFromCurrentLine * vehicle.stanleyGain) / calc);

                    abFixHeadingDelta *= vehicle.stanleyHeadingErrorGain;
                    if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
                    if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

                    steerAngle = Glm.ToDegrees((xTrackCorrection + (pn.speed > 0 ? abFixHeadingDelta : -abFixHeadingDelta)) * -1.0);

                    if (steerAngle < -vehicle.maxSteerAngle) steerAngle = -vehicle.maxSteerAngle;
                    if (steerAngle > vehicle.maxSteerAngle) steerAngle = vehicle.maxSteerAngle;
                    
                    //Integral
                    double deltaDeg = Math.Abs(Glm.ToDegrees(abFixHeadingDelta));
                    double integralSpeed = (pn.speed) / 10;

                    double distErr = Math.Abs(distanceFromCurrentLine);

                    if (deltaDeg < vehicle.integralHeadingLimit && distErr < vehicle.integralDistanceAway && pn.speed > 0.5 && isAutoSteerBtnOn)
                    {
                        if ((vehicle.inty < 0 && distanceFromCurrentLine < 0) || (vehicle.inty > 0 && distanceFromCurrentLine > 0))
                            vehicle.inty += distanceFromCurrentLine * -vehicle.stanleyIntegralGain * 3 * integralSpeed;
                        else vehicle.inty += distanceFromCurrentLine * -vehicle.stanleyIntegralGain
                                * integralSpeed * ((vehicle.integralHeadingLimit - deltaDeg) / vehicle.integralHeadingLimit);

                        if (vehicle.stanleyIntegralGain > 0) steerAngle += vehicle.inty;
                        else vehicle.inty = 0;
                    }
                    else
                    {
                        vehicle.inty = 0;
                    }
                }
                else
                {
                    //used for accumulating distance to find goal point
                    double distSoFar;

                    //update base on autosteer settings and distance from line
                    double goalPointDistance = vehicle.UpdateGoalPointDistance(distanceFromCurrentLine);

                    // used for calculating the length squared of next segment.
                    double tempDist = 0;

                    if (!isSameWay)
                    {
                        //counting down
                        distSoFar = Glm.Distance(Points[A], rEast, rNorth);
                        //Is this segment long enough to contain the full lookahead distance?
                        if (distSoFar > goalPointDistance)
                        {
                            //treat current segment like an AB Line
                            GoalPoint.Easting = rEast - (Math.Sin(Heading) * goalPointDistance);
                            GoalPoint.Northing = rNorth - (Math.Cos(Heading) * goalPointDistance);
                        }

                        //multiple segments required
                        else
                        {
                            //cycle thru segments and keep adding lengths. check if start and break if so.
                            while (A > 0 || !Guidance.isYouTurnTriggered)
                            {
                                B = (B - 1).Clamp(Points.Count);
                                A = (A - 1).Clamp(Points.Count);
                                tempDist = Glm.Distance(Points[B], Points[A]);

                                //will we go too far?
                                if ((tempDist + distSoFar) > goalPointDistance) break; //tempDist contains the full length of next segment
                                distSoFar += tempDist;
                            }

                            double t = (goalPointDistance - distSoFar); // the remainder to yet travel
                            t /= tempDist;

                            GoalPoint.Easting = (((1 - t) * Points[B].Easting) + (t * Points[A].Easting));
                            GoalPoint.Northing = (((1 - t) * Points[B].Northing) + (t * Points[A].Northing));
                        }
                    }
                    else
                    {
                        //counting up
                        distSoFar = Glm.Distance(Points[B], rEast, rNorth);

                        //Is this segment long enough to contain the full lookahead distance?
                        if (distSoFar > goalPointDistance)
                        {
                            //treat current segment like an AB Line
                            GoalPoint.Easting = rEast + (Math.Sin(Heading) * goalPointDistance);
                            GoalPoint.Northing = rNorth + (Math.Cos(Heading) * goalPointDistance);
                        }

                        //multiple segments required
                        else
                        {
                            //cycle thru segments and keep adding lengths. check if end and break if so.
                            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
                            while (B < Points.Count - 1 || !Guidance.isYouTurnTriggered)
                            {
                                B = (B + 1).Clamp(Points.Count);
                                A = (A + 1).Clamp(Points.Count);

                                tempDist = Glm.Distance(Points[B], Points[A]);

                                //will we go too far?
                                if ((tempDist + distSoFar) > goalPointDistance)
                                {
                                    break; //tempDist contains the full length of next segment
                                }
                                distSoFar += tempDist;
                            }

                            double t = (goalPointDistance - distSoFar); // the remainder to yet travel
                            t /= tempDist;

                            GoalPoint.Easting = (((1 - t) * Points[A].Easting) + (t * Points[B].Easting));
                            GoalPoint.Northing = (((1 - t) * Points[A].Northing) + (t * Points[B].Northing));
                        }
                    }

                    //calc "D" the distance from pivot axle to lookahead point
                    double goalPointDistanceSquared = Glm.DistanceSquared(GoalPoint.Northing, GoalPoint.Easting, pivotAxlePos.Northing, pivotAxlePos.Easting);

                    //calculate the the delta x in local coordinates and steering angle degrees based on wheelbase
                    double localHeading = Glm.twoPI - fixHeading;
                    ppRadius = goalPointDistanceSquared / (2 * (((GoalPoint.Easting - pivotAxlePos.Easting) * Math.Cos(localHeading)) + ((GoalPoint.Northing - pivotAxlePos.Northing) * Math.Sin(localHeading))));

                    steerAngle = Glm.ToDegrees(Math.Atan(2 * (((GoalPoint.Easting - pivotAxlePos.Easting) * Math.Cos(localHeading))
                        + ((GoalPoint.Northing - pivotAxlePos.Northing) * Math.Sin(localHeading))) * vehicle.wheelbase / goalPointDistanceSquared));

                    if (steerAngle < -vehicle.maxSteerAngle) steerAngle = -vehicle.maxSteerAngle;
                    if (steerAngle > vehicle.maxSteerAngle) steerAngle = vehicle.maxSteerAngle;

                    if (ppRadius < -500) ppRadius = -500;
                    if (ppRadius > 500) ppRadius = 500;

                    radiusPoint.Easting = pivotAxlePos.Easting + (ppRadius * Math.Cos(localHeading));
                    radiusPoint.Northing = pivotAxlePos.Northing + (ppRadius * Math.Sin(localHeading));

                    double angVel = Glm.twoPI * 0.277777 * pn.speed * (Math.Tan(Glm.ToRadians(steerAngle))) / vehicle.wheelbase;

                    //clamp the steering angle to not exceed safe angular velocity
                    if (Math.Abs(angVel) > vehicle.maxAngularVelocity)
                    {
                        steerAngle = Glm.ToDegrees(steerAngle > 0 ?
                            (Math.Atan((vehicle.wheelbase * vehicle.maxAngularVelocity) / (Glm.twoPI * pn.speed * 0.277777)))
                            : (Math.Atan((vehicle.wheelbase * -vehicle.maxAngularVelocity) / (Glm.twoPI * pn.speed * 0.277777))));
                    }

                    if (!isSameWay) distanceFromCurrentLine *= -1.0;
                }

                //Convert to centimeters
                distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);

                guidanceLineDistanceOff = distanceDisplay = (Int16)distanceFromCurrentLine;
                guidanceLineSteerAngle = (Int16)(steerAngle * 100);
            }
            else
            {
                if (Guidance.isYouTurnTriggered) Guidance.CompleteYouTurn();
                //invalid distance so tell AS module
                distanceFromCurrentLine = 32000;
                guidanceLineDistanceOff = 32000;
            }
        }

        public bool isBoundAlarming;

        //all the hitch, pivot, section, trailing hitch, headings and fixes
        private void CalculatePositionHeading()
        {
            #region pivot hitch trail

            //translate from Gps position --> PivotAxle position --> SteerAxle position
            pivotAxlePos.Easting = pn.fix.Easting - (Math.Sin(fixHeading) * vehicle.antennaPivot * vehicle.isPivotBehindAntenna);
            pivotAxlePos.Northing = pn.fix.Northing - (Math.Cos(fixHeading) * vehicle.antennaPivot * vehicle.isPivotBehindAntenna);
            pivotAxlePos.Heading = fixHeading;
            steerAxlePos.Easting = pivotAxlePos.Easting + (Math.Sin(fixHeading) * vehicle.wheelbase);
            steerAxlePos.Northing = pivotAxlePos.Northing + (Math.Cos(fixHeading) * vehicle.wheelbase);
            steerAxlePos.Heading = fixHeading;

            sectionTriggerStepDistance = 10000;

            for (int i = 0; i < Tools.Count; i++)
            {
                //determine where the rigid vehicle hitch ends
                if (Tools[i].isToolBehindPivot)
                {
                    Tools[i].HitchPos.Northing = pn.fix.Northing + (Math.Cos(fixHeading) * (-Tools[i].HitchLength - vehicle.antennaPivot * vehicle.isPivotBehindAntenna));
                    Tools[i].HitchPos.Easting = pn.fix.Easting + (Math.Sin(fixHeading) * (-Tools[i].HitchLength - vehicle.antennaPivot * vehicle.isPivotBehindAntenna));
                }
                else
                {
                    Tools[i].HitchPos.Northing = pn.fix.Northing + (Math.Cos(fixHeading) * (Tools[i].HitchLength - vehicle.antennaPivot * vehicle.isPivotBehindAntenna));
                    Tools[i].HitchPos.Easting = pn.fix.Easting + (Math.Sin(fixHeading) * (Tools[i].HitchLength - vehicle.antennaPivot * vehicle.isPivotBehindAntenna));
                }

                //tool attached via a trailing hitch
                if (Tools[i].isToolTrailing)
                {
                    double over;
                    if (Tools[i].isToolTBT)
                    {
                        //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                        if (distanceCurrentStepFix != 0)
                        {
                            double t = (-Tools[i].TankWheelLength) / distanceCurrentStepFix;
                            Vec2 tt = Tools[i].TankWheelPos - Tools[i].HitchPos;
                            Tools[i].TankWheelPos.Heading = Math.Atan2(t * tt.Easting, t * tt.Northing);
                        }
                        over = Math.Abs(Math.PI - Math.Abs(Math.Abs(Tools[i].TankWheelPos.Heading - fixHeading) - Math.PI));

                        if (over > 2.36 || startCounter > 0)//criteria for a forced reset to put tool directly behind vehicle
                        {
                            Tools[i].TankWheelPos.Heading = fixHeading;
                        }

                        double TankCos = Math.Cos(Tools[i].TankWheelPos.Heading);
                        double TankSin = Math.Sin(Tools[i].TankWheelPos.Heading);

                        Tools[i].TankWheelPos.Northing = Tools[i].HitchPos.Northing + TankCos * -Tools[i].TankWheelLength;
                        Tools[i].TankWheelPos.Easting = Tools[i].HitchPos.Easting + TankSin * -Tools[i].TankWheelLength;

                        Tools[i].TankHitchPos.Northing = Tools[i].TankWheelPos.Northing + TankCos * -Tools[i].TankHitchLength;
                        Tools[i].TankHitchPos.Easting = Tools[i].TankWheelPos.Easting + TankSin * -Tools[i].TankHitchLength;
                    }
                    else
                    {
                        Tools[i].TankWheelPos.Heading = fixHeading;
                        Tools[i].TankWheelPos.Northing = Tools[i].HitchPos.Northing;
                        Tools[i].TankWheelPos.Easting = Tools[i].HitchPos.Easting;
                        Tools[i].TankHitchPos.Northing = Tools[i].TankWheelPos.Northing;
                        Tools[i].TankHitchPos.Easting = Tools[i].TankWheelPos.Easting;
                    }

                    //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                    if (distanceCurrentStepFix != 0)
                    {
                        double t = (-Tools[i].ToolWheelLength) / distanceCurrentStepFix;
                        Vec2 tt = Tools[i].ToolWheelPos - Tools[i].TankHitchPos;
                        Tools[i].ToolWheelPos.Heading = Math.Atan2(t * tt.Easting, t * tt.Northing);
                    }
                    ////the tool is seriously jacknifed or just starting out so just spring it back.
                    over = Math.Abs(Math.PI - Math.Abs(Math.Abs(Tools[i].ToolWheelPos.Heading - Tools[i].TankWheelPos.Heading) - Math.PI));

                    if (over > 2.36 || startCounter > 0)//criteria for a forced reset to put tool directly behind vehicle
                    {
                        Tools[i].ToolWheelPos.Heading = Tools[i].TankWheelPos.Heading;
                    }

                    double ToolCos = Math.Cos(Tools[i].ToolWheelPos.Heading);
                    double ToolSin = Math.Sin(Tools[i].ToolWheelPos.Heading);

                    Tools[i].ToolWheelPos.Northing = Tools[i].TankHitchPos.Northing + ToolCos * -Tools[i].ToolWheelLength;
                    Tools[i].ToolWheelPos.Easting = Tools[i].TankHitchPos.Easting + ToolSin * -Tools[i].ToolWheelLength;
                    
                    Tools[i].ToolHitchPos.Northing = Tools[i].ToolWheelPos.Northing + ToolCos * -Tools[i].ToolHitchLength;
                    Tools[i].ToolHitchPos.Easting = Tools[i].ToolWheelPos.Easting + ToolSin * -Tools[i].ToolHitchLength;
                }
                else
                {
                    Tools[i].ToolWheelPos.Heading = fixHeading;
                    Tools[i].ToolHitchPos.Northing = Tools[i].HitchPos.Northing + Math.Cos(fixHeading) * -Tools[i].ToolHitchLength;
                    Tools[i].ToolHitchPos.Easting = Tools[i].HitchPos.Easting + Math.Sin(fixHeading) * -Tools[i].ToolHitchLength;
                }

                #endregion


                if (Guidance.isOkToAddPoints)
                    sectionTriggerStepDistance = 1.0;
                else if (Tools[i].numOfSections > 0)
                {
                    //used to increase triangle count when going around corners, less on straight
                    //pick the slow moving side edge of tool
                    double distance = Guidance.GuidanceWidth * 0.5;
                    if (distance > 3) distance = 3;

                    double twist;
                    //whichever is less
                    if (Tools[i].ToolFarLeftSpeed < Tools[i].ToolFarRightSpeed) twist = Tools[i].ToolFarLeftSpeed / Tools[i].ToolFarRightSpeed;
                    else twist = Tools[i].ToolFarRightSpeed / Tools[i].ToolFarLeftSpeed;

                    if (twist < 0.2) twist = 0.2;
                    sectionTriggerStepDistance = Math.Min(distance * twist * twist + 0.2, sectionTriggerStepDistance);
                }
                //precalc the sin and cos of heading * -1
                Tools[i].sinSectionHeading = Math.Sin(-Tools[i].ToolWheelPos.Heading);
                Tools[i].cosSectionHeading = Math.Cos(-Tools[i].ToolWheelPos.Heading);
            }
        }

        //perimeter and boundary point generation
        public void AddBoundaryPoint()
        {
            //save the north & east as previous
            prevBoundaryPos.Easting = pn.fix.Easting;
            prevBoundaryPos.Northing = pn.fix.Northing;

            //build the boundary line

            if (bnd.isOkToAddPoints)
            {
                Vec2 point = new Vec2(
                    pivotAxlePos.Easting + (Math.Cos(pivotAxlePos.Heading) * (bnd.isDrawRightSide ? bnd.createBndOffset : -bnd.createBndOffset)),
                    pivotAxlePos.Northing + (Math.Sin(pivotAxlePos.Heading) * (bnd.isDrawRightSide ? -bnd.createBndOffset : bnd.createBndOffset)));
                bnd.bndBeingMadePts.Add(point);
            }
        }

        //calculate the extreme tool left, right velocities, lookahead, and whether or not its going backwards
        public void CalculateSectionLookAhead()
        {
            for (int i = 0; i < Tools.Count; i++)
            {
                double leftSpeed = 0, rightSpeed = 0;
                //now loop all the section rights and the one extreme left

                if (Tools[i].numOfSections > 0)
                {
                    Vec2 lastLeftPoint = Tools[i].LeftPoint;
                    Tools[i].LeftPoint = new Vec2(Tools[i].ToolHitchPos.Easting + Tools[i].cosSectionHeading * (-Tools[i].ToolWidth / 2 + Tools[i].ToolOffset), Tools[i].ToolHitchPos.Northing + Tools[i].sinSectionHeading * (-Tools[i].ToolWidth / 2 + Tools[i].ToolOffset));
                    Vec2 left = Tools[i].LeftPoint - lastLeftPoint;
                    leftSpeed = left.GetLength() / fixUpdateTime;

                    if (leftSpeed < 100)
                    {
                        double head = Math.Atan2(left.Easting, left.Northing);
                        if (Math.PI - Math.Abs(Math.Abs(head - Tools[i].ToolWheelPos.Heading) - Math.PI) > Glm.PIBy2)
                            if (leftSpeed > 0) leftSpeed *= -1;
                        Tools[i].ToolFarLeftSpeed = Tools[i].ToolFarLeftSpeed * 0.7 + leftSpeed * 0.3;
                    }


                    Vec2 lastRightPoint = Tools[i].RightPoint;
                    Tools[i].RightPoint = new Vec2(Tools[i].ToolHitchPos.Easting + Tools[i].cosSectionHeading * (Tools[i].ToolWidth / 2 + Tools[i].ToolOffset), Tools[i].ToolHitchPos.Northing + Tools[i].sinSectionHeading * (Tools[i].ToolWidth / 2 + Tools[i].ToolOffset));
                    Vec2 right = Tools[i].RightPoint - lastRightPoint;
                    rightSpeed = right.GetLength() / fixUpdateTime;


                    if (rightSpeed < 100)
                    {
                        double head = Math.Atan2(right.Easting, right.Northing);
                        if (Math.PI - Math.Abs(Math.Abs(head - Tools[i].ToolWheelPos.Heading) - Math.PI) > Glm.PIBy2)
                            if (rightSpeed > 0) rightSpeed *= -1;

                        Tools[i].ToolFarRightSpeed = Tools[i].ToolFarRightSpeed * 0.7 + rightSpeed * 0.3;
                    }

                    //double Radius = (Tools[i].ToolWidth * (Tools[i].ToolFarLeftSpeed + Tools[i].ToolFarRightSpeed)) / (2 * (Tools[i].ToolFarRightSpeed - Tools[i].ToolFarLeftSpeed));

                    //set the look ahead for hyd Lift in pixels per second
                    vehicle.hydLiftLookAheadDistanceLeft = Math.Max(Math.Min(Tools[i].ToolFarLeftSpeed * vehicle.hydLiftLookAheadTime * 10, 250), -250);
                    vehicle.hydLiftLookAheadDistanceRight = Math.Max(Math.Min(Tools[i].ToolFarRightSpeed * vehicle.hydLiftLookAheadTime * 10, 250), -250);
                    Tools[i].lookAheadDistanceOnPixelsLeft = Math.Max(Math.Min(Tools[i].ToolFarLeftSpeed * Tools[i].LookAheadOnSetting * 10, 250), -250);
                    Tools[i].lookAheadDistanceOnPixelsRight = Math.Max(Math.Min(Tools[i].ToolFarRightSpeed * Tools[i].LookAheadOnSetting * 10, 250), -250);
                    Tools[i].lookAheadDistanceOffPixelsLeft = Math.Max(Math.Min(Tools[i].ToolFarLeftSpeed * Tools[i].LookAheadOffSetting * 10, 200), -200);
                    Tools[i].lookAheadDistanceOffPixelsRight = Math.Max(Math.Min(Tools[i].ToolFarRightSpeed * Tools[i].LookAheadOffSetting * 10, 200), -200);
                }
            }
        }

        //the start of first few frames to initialize entire program
        private void InitializeFirstFewGPSPositions()
        {
            SetPlaneToLocal(Latitude, Longitude);

            for (int i = 0; i < totalFixSteps; i++)
            {
                stepFixPts[i] = new Vec3(pn.fix.Easting, pn.fix.Northing, 0);
            }

            //in radians
            fixHeading = 0;

            for (int i = 0; i < Tools.Count; i++) Tools[i].ToolWheelPos.Heading = fixHeading;

            //send out initial zero settings
            //set up the modules
            mc.ResetAllModuleCommValues(true);
            IsBetweenSunriseSunset(Latitude, Longitude);

            //set display accordingly
            isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

            if (isAutoDayNight)
            {
                isDay = !isDayTime;
                SwapDayNightMode();
            }
            isGPSPositionInitialized = true;
            return;
        }

        public bool IsInsideGeoFence()
        {
            //first where are we, must be inside outer and outside of inner geofence non drive thru turn borders
            if (bnd.Boundaries.Count > 0)
            {
                Geofence = -1;
                if (bnd.Boundaries[0].IsPointInGeoFenceArea(pivotAxlePos))
                {
                    for (int j = 1; j < bnd.Boundaries.Count; j++)
                    {
                        //make sure not inside a non drivethru boundary
                        if (bnd.Boundaries[j].isDriveThru) continue;

                        if (bnd.Boundaries[j].IsPointInGeoFenceArea(pivotAxlePos))
                        {
                            Geofence = j;
                            return false;
                        }
                    }
                    Geofence = 0;
                    return true;
                }
                else return false;
            }
            else
            {
                Geofence = 0;
                return true;
            }
        }

        //WGS84 Lat Long
        public double Latitude, LatStart = 0, Longitude, LonStart = 0;

        public double MPerDegLat = 111268.63590780651;
        public double MPerDegLon = 68571.4104155387;
        public bool UpdateWorking = false;

        public void ConvertWGS84ToLocal(double Lat, double Lon, out double Northing, out double Easting)
        {
            Northing = (Lat - LatStart) * MPerDegLat;
            MPerDegLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            Easting = (Lon - LonStart) * MPerDegLon;
        }

        public void SetPlaneToLocal(double Lat, double Lon)
        {
            double OldLatStart = LatStart;
            double OldLonStart = LonStart;
            double OldMPerDegLat = MPerDegLat;

            LatStart = Lat;
            LonStart = Lon;

            MPerDegLat = 111132.92 - 559.82 * Math.Cos(2.0 * Lat * 0.01745329251994329576923690766743) + 1.175 * Math.Cos(4.0 * Lat * 0.01745329251994329576923690766743) - 0.0023 * Math.Cos(6.0 * Lat * 0.01745329251994329576923690766743);
            MPerDegLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);

            ConvertWGS84ToLocal(Latitude, Longitude, out pn.fix.Northing, out pn.fix.Easting);
            worldGrid.CheckWorldGrid(pn.fix.Northing, pn.fix.Easting);


            /*
            //FileCreateSections();

            //for every new chunk of patch
            foreach (var triList in PatchDrawList)
            {
                int count2 = triList.Count;
                for (int k = 1; k < count2; k += 3)
                {
                    double x = triList[k].Easting;
                    double y = triList[k].Northing;

                    //also tally the max/min of field x and z
                    if (minFieldX > x) minFieldX = x;
                    if (maxFieldX < x) maxFieldX = x;
                    if (minFieldY > y) minFieldY = y;
                    if (maxFieldY < y) maxFieldY = y;
                }
            }
            for (int i = 0; i < Tools.Count; i++)
            {
                // the follow up to sections patches
                for (int j = 0; j < Tools[i].Sections.Count; j++)
                {
                    int patchCount = Tools[i].Sections[j].triangleList.Count;
                    for (int k = 1; k < patchCount; k++)
                    {
                        double x = Tools[i].Sections[j].triangleList[k].Easting;
                        double y = Tools[i].Sections[j].triangleList[k].Northing;

                        //also tally the max/min of field x and z
                        if (minFieldX > x) minFieldX = x;
                        if (maxFieldX < x) maxFieldX = x;
                        if (minFieldY > y) minFieldY = y;
                        if (maxFieldY < y) maxFieldY = y;
                    }
                }
            }
            FileSaveSections();
            */




            for (int i = 0; i < Fields.Count; i++)
            {
                for (int j = 0; j < Fields[i].Polygon.Points.Count; j++)
                {
                    ConvertLocalToCurrentLocal(Fields[i].Polygon.Points[j].Northing, Fields[i].Polygon.Points[j].Easting, OldLatStart, OldLonStart, OldMPerDegLat, out double Northing, out double Easting);

                    if (j == 0)
                    {
                        Fields[i].Northingmin = Fields[i].Northingmax = Northing;
                        Fields[i].Eastingmin = Fields[i].Eastingmax = Easting;
                    }

                    if (Fields[i].Northingmin > Northing) Fields[i].Northingmin = Northing;
                    if (Fields[i].Northingmax < Northing) Fields[i].Northingmax = Northing;
                    if (Fields[i].Eastingmin > Easting) Fields[i].Eastingmin = Easting;
                    if (Fields[i].Eastingmax < Easting) Fields[i].Eastingmax = Easting;

                    Fields[i].Polygon.Points[j] = new Vec2(Easting, Northing);
                }
                Fields[i].Polygon.ResetPoints = true;
            }
        }

        public void ConvertLocalToWGS84(double Northing, double Easting, out double Lat, out double Lon)
        {
            Lat = (Northing / MPerDegLat) + LatStart;
            MPerDegLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            Lon = (Easting / MPerDegLon) + LonStart;
        }

        public void ConvertLocalToCurrentLocal(double northing, double easting, double OldLatStart, double OldLonStart, double OldMPerDegLat, out double Northing, out double Easting)
        {
            double Lat = (northing / OldMPerDegLat) + OldLatStart;
            double OldMPerDegLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            double Lon = (easting / OldMPerDegLon) + OldLonStart;


            Northing = (Lat - LatStart) * MPerDegLat;
            MPerDegLon = 111412.84 * Math.Cos(Lat * 0.01745329251994329576923690766743) - 93.5 * Math.Cos(3.0 * Lat * 0.01745329251994329576923690766743) + 0.118 * Math.Cos(5.0 * Lat * 0.01745329251994329576923690766743);
            Easting = (Lon - LonStart) * MPerDegLon;
        }

        public string ConvertLocalToWGS84(double northing, double easting)
        {
            ConvertLocalToWGS84(northing, easting, out double Lat, out double Lon);
            return (Lon.ToString("N7", CultureInfo.InvariantCulture) + ',' + Lat.ToString("N7", CultureInfo.InvariantCulture) + ",0 ");
        }
    }
}

