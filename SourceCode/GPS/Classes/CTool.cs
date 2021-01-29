using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;


namespace AgOpenGPS
{
    public class CTool
    {
        //create a new section and set left and right positions
        private readonly FormGPS mf;

        public List<CSection> Sections = new List<CSection>();

        public Vec2 HitchPos, ToolHitchPos, TankHitchPos;
        public Vec3 ToolWheelPos, TankWheelPos;

        public double HitchLength;
        public int ToolNum;

        public double ToolFarLeftSpeed = 0, ToolFarRightSpeed = 0;

        //points in world space that start and end of tool are in
        public Vec2 LeftPoint, RightPoint;

        public double ToolWheelLength, TankWheelLength, ToolHitchLength, TankHitchLength;
        public double ToolOffset, SlowSpeedCutoff, ToolWidth;

        public double LookAheadOffSetting, LookAheadOnSetting;
        public double TurnOffDelay, MappingOnDelay, MappingOffDelay;

        public double lookAheadDistanceOnPixelsLeft, lookAheadDistanceOnPixelsRight;
        public double lookAheadDistanceOffPixelsLeft, lookAheadDistanceOffPixelsRight;

        public bool isToolTrailing, isToolTBT;
        public bool isToolBehindPivot;

        //storage for the cos and sin of heading
        public double cosSectionHeading = 1.0, sinSectionHeading = 0.0;

        //used for super section off on
        public int toolMinUnappliedPixels;

        //how many individual sections
        public int numOfSections;
        public bool SuperSection;


        //read pixel values
        public int rpXPosition;
        public int rpWidth;

        //Constructor called by FormGPS
        public CTool(FormGPS _f, int num)
        {
            mf = _f;
            ToolNum = num;

            SetToolSettings(num);
        }

        public void SetToolSettings(int num)
        {
            ToolNum = num;
            numOfSections = Properties.Vehicle.Default.ToolSettings[ToolNum].Sections.Count;

            SetSections();

            ToolWheelPos = new Vec3(0, 0, 0);
            ToolHitchPos = new Vec2(0, 0);
            TankWheelPos = new Vec3(0, 0, 0);
            TankHitchPos = new Vec2(0, 0);
            HitchPos = new Vec2(0, 0);

            ToolWheelLength = Properties.Vehicle.Default.ToolSettings[ToolNum].ToolWheelLength;
            TankWheelLength = Properties.Vehicle.Default.ToolSettings[ToolNum].TankWheelLength;
            ToolHitchLength = Properties.Vehicle.Default.ToolSettings[ToolNum].ToolHitchLength;
            TankHitchLength = Properties.Vehicle.Default.ToolSettings[ToolNum].TankHitchLength;
            HitchLength = Properties.Vehicle.Default.ToolSettings[ToolNum].HitchLength;

            isToolBehindPivot = Properties.Vehicle.Default.ToolSettings[ToolNum].BehindPivot;
            isToolTrailing = Properties.Vehicle.Default.ToolSettings[ToolNum].Trailing;
            isToolTBT = Properties.Vehicle.Default.ToolSettings[ToolNum].TBT;

            LookAheadOnSetting = Properties.Vehicle.Default.ToolSettings[ToolNum].LookAheadOn;
            LookAheadOffSetting = Properties.Vehicle.Default.ToolSettings[ToolNum].LookAheadOff;

            TurnOffDelay = Properties.Vehicle.Default.ToolSettings[ToolNum].TurnOffDelay;

            MappingOnDelay = Properties.Vehicle.Default.ToolSettings[ToolNum].MappingOnDelay;
            MappingOffDelay = Properties.Vehicle.Default.ToolSettings[ToolNum].MappingOffDelay;

            toolMinUnappliedPixels = Properties.Vehicle.Default.ToolSettings[ToolNum].MinApplied;
            SlowSpeedCutoff = Properties.Vehicle.Default.ToolSettings[ToolNum].SlowSpeedCutoff;
        }

        public void RemoveSections()
        {
            for (int j = Sections.Count - 1; j >= 0; j--)
            {
                if (Sections[j].IsMappingOn)
                {
                    TurnMappingOff(j);
                }

                mf.Controls.Remove(Sections[j].SectionButton);
                Sections[j].SectionButton.Dispose();
                Sections.RemoveAt(j);
            }
        }

        public void SetSections()
        {
            for (int j = Sections.Count-1; j >= 0; j--)
            {
                if (Sections[j].IsMappingOn)
                {
                    TurnMappingOff(j);
                }

                if (j > numOfSections)
                {
                    mf.Controls.Remove(Sections[j].SectionButton);
                    Sections[j].SectionButton.Dispose();
                    Sections.RemoveAt(j);
                    mf.TotalSections--;
                }
                else if (j == numOfSections)
                {
                    mf.Controls.Remove(Sections[j].SectionButton);
                }
            }

            if (numOfSections > 0)
            {
                for (int j = 0; j <= numOfSections; j++)
                {
                    if (Sections.Count <= j) Sections.Add(new CSection());
                    if (j < numOfSections)
                    {
                        Sections[j].positionLeft = Properties.Vehicle.Default.ToolSettings[ToolNum].Sections[j][0];
                        Sections[j].positionRight = Sections[j].positionLeft + Properties.Vehicle.Default.ToolSettings[ToolNum].Sections[j][1];

                        Sections[j].rpSectionPosition = 375 + (int)Math.Round(Sections[j].positionLeft * 10, 0, MidpointRounding.AwayFromZero);
                        Sections[j].rpSectionWidth = (int)Math.Round((Sections[j].positionRight - Sections[j].positionLeft) * 10, 0, MidpointRounding.AwayFromZero);


                        if (!mf.Controls.Contains(Sections[j].SectionButton))
                        {
                            mf.TotalSections++;
                            Sections[j].BtnSectionState = mf.autoBtnState;

                            mf.Controls.Add(Sections[j].SectionButton);

                            Sections[j].SectionButton.BringToFront();
                            Sections[j].SectionButton.Text = (j + 1).ToString();
                            Sections[j].SectionButton.Name = (ToolNum + "," + j).ToString();
                            Sections[j].SectionButton.Click += new EventHandler(mf.btnSectionMan_Click);

                            Sections[j].SectionButton.Enabled = (mf.autoBtnState != 0);
                            Sections[j].SectionButton.FlatAppearance.BorderColor = SystemColors.ActiveCaptionText;
                            Sections[j].SectionButton.FlatStyle = FlatStyle.Flat;
                            Sections[j].SectionButton.TextAlign = ContentAlignment.MiddleCenter;
                            Sections[j].SectionButton.UseVisualStyleBackColor = false;

                            SectionButtonColor(j);
                        }
                        else
                        {
                            Sections[j].BtnSectionState = mf.autoBtnState;
                            Sections[j].SectionButton.Enabled = (mf.autoBtnState != 0);
                            SectionButtonColor(j);
                        }
                    }
                }

                //now do the full width section
                Sections[numOfSections].positionLeft = Sections[0].positionLeft;
                Sections[numOfSections].positionRight = Sections[numOfSections - 1].positionRight;

                rpXPosition = 375 + (int)Math.Round(Sections[0].positionLeft * 10, 0, MidpointRounding.AwayFromZero);
                rpWidth = (int)Math.Round((Sections[numOfSections - 1].positionRight - Sections[0].positionLeft) * 10, 0, MidpointRounding.AwayFromZero);
                ToolWidth = Sections[numOfSections - 1].positionRight - Sections[0].positionLeft;
            }
            else ToolWidth = 0;
        }

        public void SectionButtonColor(int j)
        {
            if (!mf.isJobStarted)
            {
                Sections[j].SectionButton.BackColor = mf.isDay ? Color.Silver : Color.Silver;
            }
            else if (Sections[j].BtnSectionState == FormGPS.btnStates.On)
                Sections[j].SectionButton.BackColor = mf.isDay ? Color.Yellow : Color.DarkGoldenrod;
            else if (Sections[j].BtnSectionState == FormGPS.btnStates.Auto)
                Sections[j].SectionButton.BackColor = mf.isDay ? Color.Lime : Color.ForestGreen;
            else
                Sections[j].SectionButton.BackColor = mf.isDay ? Color.Red : Color.Crimson;

            Sections[j].SectionButton.ForeColor = mf.isDay ? Color.Black : Color.White;
        }

        public void DrawTool()
        {
            if (isToolBehindPivot)
                GL.Translate(0.0, -HitchLength, 0.0);
            else
                GL.Translate(0.0, HitchLength, 0.0);

            if (numOfSections > 0)
            {
                //there is a trailing tow between hitch
                if (isToolTrailing)
                {
                    if (isToolTBT)
                    {
                        //rotate to tank heading
                        GL.Rotate(Glm.ToDegrees(mf.fixHeading - TankWheelPos.Heading), 0.0, 0.0, 1.0);

                        //draw the tank
                        GL.LineWidth(2f);
                        GL.Color3(0.7f, 0.7f, 0.97f);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex3(0, 0, 0);
                        GL.Vertex3(0, -TankWheelLength, 0);
                        GL.End();

                        //Hitch
                        GL.LineWidth(1f);
                        GL.Color3(0.37f, 0.37f, 0.97f);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex3(0, -TankWheelLength, 0);
                        GL.Vertex3(0, -TankWheelLength + -TankHitchLength, 0);
                        GL.End();

                        //pivot markers
                        GL.Color3(0.95f, 0.95f, 0f);
                        GL.PointSize(6f);
                        GL.Begin(PrimitiveType.Points);
                        GL.Vertex3(0.0, -TankWheelLength, 0.0);
                        if (TankHitchLength != 0)
                        {
                            GL.Color3(0.95f, 0.0f, 0.0f);
                            GL.Vertex3(0.0, -TankWheelLength + -TankHitchLength, 0.0);
                        }
                        GL.End();

                        //move down the tank hitch, unwind, rotate to section heading
                        GL.Translate(0.0, -TankWheelLength + -TankHitchLength, 0.0);
                    }

                    GL.Rotate(Glm.ToDegrees(TankWheelPos.Heading - ToolWheelPos.Heading), 0.0, 0.0, 1.0);

                    //draw the hitch
                    GL.LineWidth(2);
                    GL.Color3(0.7f, 0.7f, 0.97f);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(0.0, -ToolWheelLength, 0.0);
                    GL.Vertex3(0, 0, 0);
                    GL.End();

                    //Hitch
                    GL.LineWidth(1f);
                    GL.Color3(0.37f, 0.37f, 0.97f);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(0, -ToolWheelLength, 0);
                    GL.Vertex3(0, -ToolWheelLength + -ToolHitchLength, 0);
                    GL.End();

                    if (ToolHitchLength != 0)
                    {
                        //pivot marker
                        GL.Color3(0.95f, 0.95f, 0f);
                        GL.PointSize(6f);
                        GL.Begin(PrimitiveType.Points);
                        GL.Vertex3(0.0, -ToolWheelLength, 0.0);
                        GL.End();
                    }

                    GL.Translate(0.0, -ToolWheelLength + -ToolHitchLength, 0.0);
                }

                //look ahead lines
                GL.LineWidth(1);
                GL.Begin(PrimitiveType.Lines);

                //lookahead section on
                GL.Color3(0.20f, 0.7f, 0.2f);
                GL.Vertex3(Sections[0].positionLeft, lookAheadDistanceOnPixelsLeft * 0.1, 0);
                for (int j = 0; j < numOfSections; j++)
                {
                    if (j > 0 && Sections[j - 1].positionRight != Sections[j].positionLeft)
                    {
                        GL.Vertex3(Sections[j - 1].positionRight, (lookAheadDistanceOnPixelsLeft + (((lookAheadDistanceOnPixelsRight - lookAheadDistanceOnPixelsLeft) / (rpWidth * 0.1)) * (Sections[j - 1].positionRight - Sections[0].positionLeft))) * 0.1, 0);
                        GL.Vertex3(Sections[j].positionLeft, (lookAheadDistanceOnPixelsLeft + (((lookAheadDistanceOnPixelsRight - lookAheadDistanceOnPixelsLeft) / (rpWidth * 0.1)) * (Sections[j].positionLeft - Sections[0].positionLeft))) * 0.1, 0);
                    }
                }
                GL.Vertex3(Sections[numOfSections - 1].positionRight, lookAheadDistanceOnPixelsRight * 0.1, 0);

                //lookahead section off
                GL.Color3(0.70f, 0.2f, 0.2f);
                GL.Vertex3(Sections[0].positionLeft, lookAheadDistanceOffPixelsLeft * 0.1, 0);
                for (int j = 0; j < numOfSections; j++)
                {
                    if (j > 0 && Sections[j - 1].positionRight != Sections[j].positionLeft)
                    {
                        GL.Vertex3(Sections[j - 1].positionRight, (lookAheadDistanceOffPixelsLeft + (((lookAheadDistanceOffPixelsRight - lookAheadDistanceOffPixelsLeft) / (rpWidth * 0.1)) * (Sections[j - 1].positionRight - Sections[0].positionLeft))) * 0.1, 0);
                        GL.Vertex3(Sections[j].positionLeft, (lookAheadDistanceOffPixelsLeft + (((lookAheadDistanceOffPixelsRight - lookAheadDistanceOffPixelsLeft) / (rpWidth * 0.1)) * (Sections[j].positionLeft - Sections[0].positionLeft))) * 0.1, 0);
                    }
                }
                GL.Vertex3(Sections[numOfSections - 1].positionRight, lookAheadDistanceOffPixelsRight * 0.1, 0);

                if (mf.vehicle.BtnHydLiftOn)
                {
                    GL.Color3(0.70f, 0.2f, 0.72f);
                    GL.Vertex3(Sections[0].positionLeft, mf.vehicle.hydLiftLookAheadDistanceLeft * 0.1, 0);
                    for (int j = 0; j < numOfSections; j++)
                    {
                        if (j > 0 && Sections[j - 1].positionRight != Sections[j].positionLeft)
                        {
                            GL.Vertex3(Sections[j - 1].positionRight, (mf.vehicle.hydLiftLookAheadDistanceLeft + (((mf.vehicle.hydLiftLookAheadDistanceRight - mf.vehicle.hydLiftLookAheadDistanceLeft) / (rpWidth * 0.1)) * (Sections[j - 1].positionRight - Sections[0].positionLeft))) * 0.1, 0);
                            GL.Vertex3(Sections[j].positionLeft, (mf.vehicle.hydLiftLookAheadDistanceLeft + (((mf.vehicle.hydLiftLookAheadDistanceRight - mf.vehicle.hydLiftLookAheadDistanceLeft) / (rpWidth * 0.1)) * (Sections[j].positionLeft - Sections[0].positionLeft))) * 0.1, 0);
                        }
                    }
                    GL.Vertex3(Sections[numOfSections - 1].positionRight, mf.vehicle.hydLiftLookAheadDistanceRight * 0.1, 0);
                }

                GL.End();
            }

            //draw the sections
            GL.LineWidth(2);

            double hite = mf.camera.camSetDistance / -100;
            if (hite > 1.3) hite = 1.3;
            if (hite < 0.5) hite = 0.5;

            for (int j = 0; j < numOfSections; j++)
            {
                //if section is on, green, if off, red color
                if (Sections[j].IsSectionOn || SuperSection)
                {
                    if (Sections[j].BtnSectionState == FormGPS.btnStates.Auto)
                    {
                        GL.Color3(0.0f, 0.9f, 0.0f);
                        //if (mf.section[j].isMappingOn) GL.Color3(0.0f, 0.7f, 0.0f);
                        //else GL.Color3(0.70f, 0.0f, 0.90f);
                    }
                    else GL.Color3(0.97, 0.97, 0);
                }
                else
                {
                    //if (!mf.section[j].isMappingOn) GL.Color3(0.70f, 0.2f, 0.2f);
                    //else GL.Color3(0.00f, 0.250f, 0.90f);
                    GL.Color3(0.7f, 0.2f, 0.2f);
                }

                double mid = (Sections[j].positionLeft + Sections[j].positionRight) /2;

                GL.Begin(PrimitiveType.TriangleFan);
                {
                    GL.Vertex3(Sections[j].positionLeft, 0, 0);
                    GL.Vertex3(Sections[j].positionLeft, 0 -hite, 0);

                    GL.Vertex3(mid, -hite * 1.5, 0);

                    GL.Vertex3(Sections[j].positionRight, -hite, 0);
                    GL.Vertex3(Sections[j].positionRight, 0, 0);
                }
                GL.End();

                GL.Begin(PrimitiveType.LineLoop);
                {
                    GL.Color3(0.0, 0.0, 0.0);
                    GL.Vertex3(Sections[j].positionLeft, 0, 0);
                    GL.Vertex3(Sections[j].positionLeft, -hite, 0);

                    GL.Vertex3(mid, -hite * 1.5, 0);

                    GL.Vertex3(Sections[j].positionRight, -hite, 0);
                    GL.Vertex3(Sections[j].positionRight, 0, 0);
                }
                GL.End();
            }

            //GL.End();

            //draw section markers if close enough
            if (mf.camera.camSetDistance > 250)
            {
                GL.Color3(0.0f, 0.0f, 0.0f);
                //section markers
                GL.PointSize(3.0f);
                GL.Begin(PrimitiveType.Points);
                for (int j = 0; j < numOfSections - 1; j++)
                    GL.Vertex3(Sections[j].positionRight, 0, 0);
                GL.End();
            }

            if (isToolTrailing)
            {
                GL.Translate(0.0, -(-ToolWheelLength + -ToolHitchLength), 0.0);
                GL.Rotate(Glm.ToDegrees(TankWheelPos.Heading - ToolWheelPos.Heading), 0.0, 0.0, -1.0);

                if (isToolTBT)
                {
                    GL.Translate(0.0, -(-TankWheelLength + -TankHitchLength), 0.0);
                    GL.Rotate(Glm.ToDegrees(mf.fixHeading - TankWheelPos.Heading), 0.0, 0.0, -1.0);
                }
            }

            if (isToolBehindPivot)
                GL.Translate(0.0, HitchLength, 0.0);
            else
                GL.Translate(0.0, -HitchLength, 0.0);
        }

        public void TurnMappingOff(int Section)
        {
            AddMappingPoint(Section);

            Sections[Section].IsMappingOn = false;
            Sections[Section].numTriangles = 0;

            if (Sections[Section].triangleList.Count > 4)
            {
                //save the triangle list in a patch list to add to saving file
                mf.PatchSaveList.Add(Sections[Section].triangleList);
                mf.PatchDrawList.Add(Sections[Section].triangleList);
            }
            else
            {
                Sections[Section].triangleList.Clear();
            }
        }

        //every time a new fix, a new patch point from last point to this point
        //only need prev point on the first points of triangle strip that makes a box (2 triangles)

        public void AddMappingPoint(int Section)
        {
            //add two triangles for next step.
            //left side
            Vec2 diff = (RightPoint - LeftPoint) / ToolWidth;

            Vec2 Pos = LeftPoint + diff * (ToolWidth / 2 + Sections[Section].positionLeft);
            GL.Vertex3(Pos.Easting, Pos.Northing, 0);

            Vec3 point = new Vec3(Pos.Easting, Pos.Northing, 0);

            //add the point to List
            Sections[Section].triangleList.Add(point);

            //Right side
            Pos = LeftPoint + diff * (ToolWidth / 2 + Sections[Section].positionRight);
            Vec3 point2 = new Vec3(Pos.Easting, Pos.Northing, 0);

            //add the point to the list
            Sections[Section].triangleList.Add(point2);

            //count the triangle pairs
            Sections[Section].numTriangles++;

            //quick count
            int c = Sections[Section].triangleList.Count - 1;

            //when closing a job the triangle patches all are emptied but the section delay keeps going.
            //Prevented by quick check. 4 points plus colour
            if (c > 4)
            {
                //calculate area of these 2 new triangles - AbsoluteValue of (Ax(By-Cy) + Bx(Cy-Ay) + Cx(Ay-By)/2)
                {
                    double temp = (Sections[Section].triangleList[c].Easting * (Sections[Section].triangleList[c - 1].Northing - Sections[Section].triangleList[c - 2].Northing))
                              + (Sections[Section].triangleList[c - 1].Easting * (Sections[Section].triangleList[c - 2].Northing - Sections[Section].triangleList[c].Northing))
                                  + (Sections[Section].triangleList[c - 2].Easting * (Sections[Section].triangleList[c].Northing - Sections[Section].triangleList[c - 1].Northing));

                    temp = Math.Abs(temp / 2.0);
                    mf.fd.workedAreaTotal += temp;
                    mf.fd.workedAreaTotalUser += temp;

                    //temp = 0;
                    temp = (Sections[Section].triangleList[c - 1].Easting * (Sections[Section].triangleList[c - 2].Northing - Sections[Section].triangleList[c - 3].Northing))
                              + (Sections[Section].triangleList[c - 2].Easting * (Sections[Section].triangleList[c - 3].Northing - Sections[Section].triangleList[c - 1].Northing))
                                  + (Sections[Section].triangleList[c - 3].Easting * (Sections[Section].triangleList[c - 1].Northing - Sections[Section].triangleList[c - 2].Northing));

                    temp = Math.Abs(temp / 2.0);
                    mf.fd.workedAreaTotal += temp;
                    mf.fd.workedAreaTotalUser += temp;
                }
            }

            if (Sections[Section].numTriangles > 36)
            {
                Sections[Section].numTriangles = 0;

                //save the cutoff patch to be saved later
                mf.PatchSaveList.Add(Sections[Section].triangleList);

                mf.PatchDrawList.Add(Sections[Section].triangleList);

                Sections[Section].triangleList = new List<Vec3>();

                //Add Patch colour
                Color t1 = mf.sectionColorDay;
                Vec3 colur = new Vec3(t1.R, t1.G, t1.B);
                Sections[Section].triangleList.Add(colur);

                //add the points to List, yes its more points, but breaks up patches for culling
                Sections[Section].triangleList.Add(point);
                Sections[Section].triangleList.Add(point2);
            }
        }

        public void TurnMappingOn(int Section)
        {
            Sections[Section].numTriangles = 0;

            //do not tally square meters on inital point, that would be silly
            if (!Sections[Section].IsMappingOn)
            {
                //set the section bool to on
                Sections[Section].IsMappingOn = true;

                //starting a new patch chunk so create a new triangle list
                Sections[Section].triangleList = new List<Vec3>();
                Color t1 = mf.sectionColorDay;
                Vec3 colur = new Vec3(t1.R, t1.G, t1.B);
                Sections[Section].triangleList.Add(colur);

                //left side of triangle

                Vec2 diff = (RightPoint - LeftPoint) / ToolWidth;

                Vec2 Pos = LeftPoint + diff * (ToolWidth / 2 + Sections[Section].positionLeft);
                Vec3 point = new Vec3(Pos.Easting, Pos.Northing, 0);
                Sections[Section].triangleList.Add(point);

                //Right side of triangle
                Pos = LeftPoint + diff * (ToolWidth / 2 + Sections[Section].positionRight);
                point = new Vec3(Pos.Easting, Pos.Northing, 0);
                Sections[Section].triangleList.Add(point);
            }
        }
    }

    public class CSection
    {

        //list of patch data individual triangles
        public List<Vec3> triangleList = new List<Vec3>();

        //is this section on or off
        public bool IsSectionOn = false;
        public bool IsSectionRequiredOn = false;
        public bool SectionOnRequest = false;
        public int SectionOverlapTimer = 0;
        public Button SectionButton = new Button();

        //mapping
        public bool IsMappingOn = false;
        public bool MappingOnRequest = false;
        public int MappingOnTimer = 0;
        public int MappingOffTimer = 0;


        //the left side is always negative, right side is positive
        //so a section on the left side only would be -8, -4
        //in the center -4,4  on the right side only 4,8
        //reads from left to right
        //   ------========---------||----------========---------
        //        -8      -4      -1  1         4      8
        // in (meters)

        public double positionLeft = -4;
        public double positionRight = 4;

        //used by readpixel to determine color in pixel array
        public int rpSectionWidth = 0;
        public int rpSectionPosition = 0;

        //whether or not this section is in boundary, headland
        public int numTriangles = 0;

        //used to determine state of Manual section button - Off Auto On
        public FormGPS.btnStates BtnSectionState = FormGPS.btnStates.Off;
    }
}
