using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        public double GuidanceWidth, GuidanceOverlap, GuidanceOffset, WidthMinusOverlap;

        //flag for starting stop adding points
        public bool BtnGuidanceOn, isOkToAddPoints;
        public double distanceFromRefLine;

        public bool ResetABLine = false;
        public int HowManyPathsAway, OldHowManyPathsAway, SmoothCount;
        public bool isSmoothWindowOpen, isSameWay, OldisSameWay;

        public int A, B, CurrentLine = -1, CurrentEditLine = -1, tryoutcurve = -1, CurrentTramLine = -1;

        //the list of points of curve to drive on
        public List<Vec2> curList = new List<Vec2>();
        public List<Vec2> smooList = new List<Vec2>();
        public List<CGuidanceLine> Lines = new List<CGuidanceLine>();
        public bool isEditing;
        public List<List<List<Vec2>>> ExtraGuidanceLines = new List<List<List<Vec2>>>();

        public CGuidance(FormGPS _f)
        {
            mf = _f;
            GuidanceWidth = Properties.Vehicle.Default.GuidanceWidth;
            GuidanceOverlap = Properties.Vehicle.Default.GuidanceOverlap;
            WidthMinusOverlap = GuidanceWidth - GuidanceOverlap;
            GuidanceOffset = Properties.Vehicle.Default.GuidanceOffset;

            youTurnStartOffset = Properties.Vehicle.Default.set_youTurnDistance;
            rowSkipsWidth = Properties.Vehicle.Default.set_youSkipWidth;
            YouTurnType = Properties.Vehicle.Default.Youturn_Type;

            TramOffset = Properties.Settings.Default.setTram_offset;
            TramWidth = Properties.Settings.Default.setTram_eqWidth;
            TramWheelTrack = Properties.Settings.Default.setTram_wheelSpacing;
            TramWheelWidth = Properties.Settings.Default.Tram_wheelWidth;
            TramHalfWheelTrack = TramWheelTrack * 0.5;
            TramPasses = Properties.Settings.Default.setTram_passes;
            TramDisplayMode = 0;
        }

        public void DrawLine()
        {
            int ptCount;
            if (tryoutcurve > -1 && tryoutcurve < Lines.Count)
            {
                GL.LineWidth(mf.lineWidth * 2);
                GL.Color3(1.0f, 0.0f, 0.0f);
                if (Lines[tryoutcurve].Mode == Gmode.Boundary) GL.Begin(PrimitiveType.LineLoop);
                else GL.Begin(PrimitiveType.LineStrip);

                if (Lines[tryoutcurve].Mode == Gmode.AB || Lines[tryoutcurve].Mode == Gmode.Heading)
                {
                    double cosHeading = Math.Cos(-Lines[tryoutcurve].Heading);
                    double sinHeading = Math.Sin(-Lines[tryoutcurve].Heading); 

                    GL.Vertex3(Lines[tryoutcurve].Segments[0].Easting + sinHeading * mf.maxCrossFieldLength, Lines[tryoutcurve].Segments[0].Northing - cosHeading * mf.maxCrossFieldLength, 0);
                    GL.Vertex3(Lines[tryoutcurve].Segments[1].Easting - sinHeading * mf.maxCrossFieldLength, Lines[tryoutcurve].Segments[1].Northing + cosHeading * mf.maxCrossFieldLength, 0);
                }
                else
                {
                    for (int h = 0; h < Lines[tryoutcurve].Segments.Count; h++)
                    {
                        GL.Vertex3(Lines[tryoutcurve].Segments[h].Easting, Lines[tryoutcurve].Segments[h].Northing, 0);
                    }
                }
                GL.End();
                return;
            }
            else if (isSmoothWindowOpen)
            {
                ptCount = smooList.Count;
                if (smooList.Count == 0) return;

                GL.LineWidth(mf.lineWidth);
                GL.Color3(0.930f, 0.92f, 0.260f);
                GL.Begin(PrimitiveType.Lines);
                for (int h = 0; h < ptCount; h++) GL.Vertex3(smooList[h].Easting, smooList[h].Northing, 0);
                GL.End();
            }
            else if (CurrentEditLine < Lines.Count && CurrentEditLine > -1)//draw the last line to tractor
            {
                ptCount = Lines[CurrentEditLine].Segments.Count;
                if (ptCount > 0)
                {
                    GL.Color3(0.930f, 0.0692f, 0.260f);
                    GL.Begin(PrimitiveType.LineStrip);

                    if (Lines[CurrentEditLine].Mode == Gmode.AB || Lines[CurrentEditLine].Mode == Gmode.Heading)
                    {
                        double cosHeading = Math.Cos(-Lines[CurrentEditLine].Heading);
                        double sinHeading = Math.Sin(-Lines[CurrentEditLine].Heading);

                        GL.Vertex3(Lines[CurrentEditLine].Segments[0].Easting + sinHeading * mf.maxCrossFieldLength, Lines[CurrentEditLine].Segments[0].Northing - cosHeading * mf.maxCrossFieldLength, 0);
                        GL.Vertex3(Lines[CurrentEditLine].Segments[1].Easting - sinHeading * mf.maxCrossFieldLength, Lines[CurrentEditLine].Segments[1].Northing + cosHeading * mf.maxCrossFieldLength, 0);
                    }
                    else
                    {
                        for (int h = 0; h < ptCount; h++) GL.Vertex3(Lines[CurrentEditLine].Segments[h].Easting, Lines[CurrentEditLine].Segments[h].Northing, 0);

                        if (isEditing && ptCount > 0)
                        {
                            GL.Vertex3(Lines[CurrentEditLine].Segments[ptCount - 1].Easting, Lines[CurrentEditLine].Segments[ptCount - 1].Northing, 0);
                            Vec3 pivot = mf.pivotAxlePos;
                            pivot.Northing += Math.Sin(pivot.Heading) * -mf.Guidance.GuidanceOffset;
                            pivot.Easting += Math.Cos(pivot.Heading) * mf.Guidance.GuidanceOffset;
                            GL.Vertex3(pivot.Easting, pivot.Northing, 0);
                        }
                    }
                    GL.End();
                }
            }
            else
            {
                if (CurrentLine < Lines.Count && CurrentLine > -1)
                {
                    ptCount = Lines[CurrentLine].Segments.Count;
                    if (ptCount < 1) return;

                    GL.Color3(0.96, 0.2f, 0.2f);

                    //original line

                    if (Lines[CurrentLine].Mode == Gmode.Boundary) GL.Begin(PrimitiveType.LineLoop);
                    else GL.Begin(PrimitiveType.LineStrip);

                    if (Lines[CurrentLine].Mode == Gmode.AB || Lines[CurrentLine].Mode == Gmode.Heading)
                    {
                        double cosHeading = Math.Cos(-Lines[CurrentLine].Heading);
                        double sinHeading = Math.Sin(-Lines[CurrentLine].Heading);

                        GL.Vertex3(Lines[CurrentLine].Segments[0].Easting + sinHeading * mf.maxCrossFieldLength, Lines[CurrentLine].Segments[0].Northing - cosHeading * mf.maxCrossFieldLength, 0);
                        GL.Vertex3(Lines[CurrentLine].Segments[1].Easting - sinHeading * mf.maxCrossFieldLength, Lines[CurrentLine].Segments[1].Northing + cosHeading * mf.maxCrossFieldLength, 0);
                    }
                    else
                    {
                        for (int h = 0; h < ptCount; h++)
                        {
                            GL.Vertex3(Lines[CurrentLine].Segments[h].Easting, Lines[CurrentLine].Segments[h].Northing, 0);
                        }
                    }
                    GL.End();

                    if (mf.isSideGuideLines)
                    {
                        GL.Color3(0.56f, 0.650f, 0.650f);
                        GL.Enable(EnableCap.LineStipple);
                        GL.LineStipple(1, 0x0101);
                        GL.LineWidth(mf.lineWidth);

                        for (int i = 0; i < ExtraGuidanceLines.Count; i++)
                        {
                            for (int j = 0; j < ExtraGuidanceLines[i].Count; j++)
                            {
                                if (ExtraGuidanceLines[i][j].Count > 0)
                                {
                                    if (Lines[CurrentLine].Mode == Gmode.Boundary) GL.Begin(PrimitiveType.LineLoop);
                                    else GL.Begin(PrimitiveType.LineStrip);
                                    for (int h = 0; h < ExtraGuidanceLines[i][j].Count; h++) GL.Vertex3(ExtraGuidanceLines[i][j][h].Easting, ExtraGuidanceLines[i][j][h].Northing, 0);
                                    GL.End();
                                }
                            }
                        }
                        GL.Disable(EnableCap.LineStipple);
                    }

                    if (mf.font.isFontOn && ptCount > 410)
                    {
                        GL.Color3(0.40f, 0.90f, 0.95f);
                        mf.font.DrawText3D(Lines[CurrentLine].Segments[201].Easting, Lines[CurrentLine].Segments[201].Northing, "&A");
                        mf.font.DrawText3D(Lines[CurrentLine].Segments[Lines[CurrentLine].Segments.Count - 200].Easting, Lines[CurrentLine].Segments[Lines[CurrentLine].Segments.Count - 200].Northing, "&B");
                    }

                    ptCount = curList.Count;
                    if (ptCount > 1)
                    {
                        //OnDrawGizmos();
                        GL.LineWidth(mf.lineWidth);
                        GL.Color3(0.95f, 0.2f, 0.95f);
                        if (Lines[CurrentLine].Mode == Gmode.Boundary) GL.Begin(PrimitiveType.LineLoop);
                        else GL.Begin(PrimitiveType.LineStrip);
                        for (int h = 0; h < ptCount; h++) GL.Vertex3(curList[h].Easting, curList[h].Northing, 0);
                        GL.End();
                    }

                    if (mf.isPureDisplayOn && !mf.isStanleyUsed)
                    {
                        if (mf.ppRadius < 100 && mf.ppRadius > -100)
                        {
                            const int numSegments = 100;
                            double theta = Glm.twoPI / numSegments;
                            double c = Math.Cos(theta);//precalculate the sine and cosine
                            double s = Math.Sin(theta);
                            double x = mf.ppRadius;//we start at angle = 0
                            double y = 0;

                            GL.LineWidth(1);
                            GL.Color3(0.95f, 0.30f, 0.950f);
                            GL.Begin(PrimitiveType.LineLoop);
                            for (int ii = 0; ii < numSegments; ii++)
                            {
                                //glVertex2f(x + cx, y + cy);//output vertex
                                Vec2 Point2 = mf.radiusPoint;
                                GL.Vertex3(x + Point2.Easting, y + Point2.Northing, 0);//output vertex
                                double t = x;//apply the rotation matrix
                                x = (c * x) - (s * y);
                                y = (s * t) + (c * y);
                            }
                            GL.End();
                        }

                        //Draw lookahead Point
                        GL.PointSize(4.0f);
                        GL.Begin(PrimitiveType.Points);
                        GL.Color3(1.0f, 0.5f, 0.95f);
                        Vec2 Point = mf.GoalPoint;
                        GL.Vertex3(Point.Easting, Point.Northing, 0.0);
                        GL.Color3(1.0f, 1.5f, 0.95f);
                        GL.Vertex3(mf.rEast, mf.rNorth, 0.0);
                        GL.End();
                    }

                    mf.Guidance.DrawYouTurn();
                }
            }
            GL.PointSize(1.0f);
        }

        //for calculating for display the averaged new line
        public void SmoothAB(int smPts)
        {
            if (CurrentEditLine < Lines.Count && CurrentEditLine > -1)
            {
                //count the reference list of original curve
                int cnt = Lines[CurrentEditLine].Segments.Count;

                //just go back if not very long
                if (cnt < 10) return;

                //the temp array
                Vec2[] arr = new Vec2[cnt];

                //average them - center weighted average

                bool tt = Lines[CurrentEditLine].Mode != Gmode.Boundary;
                for (int i = 0; i < cnt; i++)
                {
                    int Start = (tt && i < smPts / 2) ? -i : -smPts / 2;
                    int Stop = tt && i > cnt - (smPts / 2) ? cnt - i: smPts / 2;
                    int cntd = Stop - Start;
                    for (int j = Start; j < Stop; j++)
                    {
                        int Idx = (j + i).Clamp(cnt);

                        arr[i].Easting += Lines[CurrentEditLine].Segments[Idx].Easting;
                        arr[i].Northing += Lines[CurrentEditLine].Segments[Idx].Northing;
                    }
                    arr[i].Easting /= cntd;
                    arr[i].Northing /= cntd;
                }

                //make a list to draw
                smooList.Clear();
                smooList.AddRange(arr);
            }
        }

        public void SwapHeading(int Idx)
        {
            if (Idx < Lines.Count && Idx > -1)
            {
                int cnt = Lines[Idx].Segments.Count;
                if (cnt > 0)
                {
                    Lines[Idx].Segments.Reverse();
                    Lines[Idx].Heading += Math.PI;
                    Lines[Idx].Heading %= Glm.twoPI;
                }
                if (Idx == CurrentTramLine && mf.Guidance.TramDisplayMode > 0) BuildTram();
                if (isSmoothWindowOpen) SmoothAB(SmoothCount * 2);
            }
        }

        //turning the visual line into the real reference line to use
        public void SaveSmoothList()
        {
            if (CurrentEditLine < Lines.Count && CurrentEditLine > -1)
            {
                int cnt = smooList.Count;
                if (cnt < 3) return;

                Lines[CurrentEditLine].Segments.Clear();
                Lines[CurrentEditLine].Segments.AddRange(smooList);
                ResetABLine = true;
            }
        }

        public void GetCurrentLine(Vec3 pivot, Vec3 steer)
        {
            double minDistance;

            double boundaryTriggerDistance = 1;
            if (CurrentLine < Lines.Count && CurrentLine > -1)
            {
                bool useSteer = mf.isStanleyUsed;

                if (!mf.vehicle.isSteerAxleAhead) useSteer = !useSteer;
                if (mf.vehicle.isReverse) useSteer = !useSteer;

                Vec3 point = useSteer ? steer : pivot;

                if (Lines[CurrentLine].Mode == Gmode.Spiral)
                {
                    minDistance = Glm.Distance(Lines[CurrentLine].Segments[0], point);

                    double RefDist = minDistance / WidthMinusOverlap;
                    if (RefDist < 0) HowManyPathsAway = (int)(RefDist - 0.5);
                    else HowManyPathsAway = (int)(RefDist + 0.5);

                    if (OldHowManyPathsAway != HowManyPathsAway || ResetABLine)
                    {
                        ResetABLine = false;
                        OldHowManyPathsAway = HowManyPathsAway;
                        if (HowManyPathsAway < 2) HowManyPathsAway = 2;

                        double s = WidthMinusOverlap / 2;

                        curList.Clear();
                        //double circumference = (glm.twoPI * s) / (boundaryTriggerDistance * 0.1);
                        double circumference;

                        for (double round = Glm.twoPI * (HowManyPathsAway - 2); round <= (Glm.twoPI * (HowManyPathsAway + 2) + 0.00001); round += (Glm.twoPI / circumference))
                        {
                            double x = s * (Math.Cos(round) + (round / Math.PI) * Math.Sin(round));
                            double y = s * (Math.Sin(round) - (round / Math.PI) * Math.Cos(round));

                            Vec2 pt = new Vec2(Lines[CurrentLine].Segments[0].Easting + x, Lines[CurrentLine].Segments[0].Northing + y);
                            curList.Add(pt);

                            double radius = Math.Sqrt(x * x + y * y);
                            circumference = (Glm.twoPI * radius) / (boundaryTriggerDistance);
                        }
                    }
                }
                else if (Lines[CurrentLine].Mode == Gmode.Circle)
                {
                    minDistance = Glm.Distance(Lines[CurrentLine].Segments[0], point);

                    double RefDist = minDistance / WidthMinusOverlap;
                    if (RefDist < 0) HowManyPathsAway = (int)(RefDist - 0.5);
                    else HowManyPathsAway = (int)(RefDist + 0.5);

                    if (OldHowManyPathsAway != HowManyPathsAway && HowManyPathsAway == 0)
                    {
                        OldHowManyPathsAway = HowManyPathsAway;
                        curList.Clear();
                    }
                    else if (OldHowManyPathsAway != HowManyPathsAway || ResetABLine)
                    {
                        ResetABLine = false;
                        if (HowManyPathsAway > 100) return;
                        OldHowManyPathsAway = HowManyPathsAway;

                        curList.Clear();

                        int aa = (int)((Glm.twoPI * WidthMinusOverlap * HowManyPathsAway) / boundaryTriggerDistance);

                        for (double round = 0; round <= Glm.twoPI + 0.00001; round += (Glm.twoPI) / aa)
                        {
                            Vec2 pt = new Vec2(Lines[CurrentLine].Segments[0].Easting + (Math.Sin(round) * WidthMinusOverlap * HowManyPathsAway), Lines[CurrentLine].Segments[0].Northing + (Math.Cos(round) * WidthMinusOverlap * HowManyPathsAway));
                            curList.Add(pt);
                        }
                    }
                }
                else
                {
                    if (Lines[CurrentLine].Segments.Count < 2) return;

                    double Dy, Dx;

                    if (Lines[CurrentLine].Mode == Gmode.Heading)
                    {
                        if (!mf.isAutoSteerBtnOn || ResetABLine)
                        {
                            isSameWay = Math.PI - Math.Abs(Math.Abs(point.Heading - Lines[CurrentLine].Heading) - Math.PI) < Glm.PIBy2;
                        }
                        double cosHeading = Math.Cos(Lines[CurrentLine].Heading);
                        double sinHeading = Math.Sin(Lines[CurrentLine].Heading);


                        Dx = 2 * cosHeading * mf.maxCrossFieldLength;
                        Dy = 2 * sinHeading * mf.maxCrossFieldLength;

                        Vec3 Start = new Vec3(Lines[CurrentLine].Segments[0].Easting + sinHeading * -mf.maxCrossFieldLength, Lines[CurrentLine].Segments[0].Northing + cosHeading * -mf.maxCrossFieldLength, 0);
                        Vec3 Stop = new Vec3(Lines[CurrentLine].Segments[1].Easting + sinHeading * mf.maxCrossFieldLength, Lines[CurrentLine].Segments[1].Northing + cosHeading * mf.maxCrossFieldLength, 0);

                        distanceFromRefLine = ((Dx * point.Easting) - (Dy * point.Northing) + (Stop.Easting * Start.Northing) - (Stop.Northing * Start.Easting)) / Math.Sqrt((Dx * Dx) + (Dy * Dy));

                    }
                    else
                    {
                        double minDistA = double.PositiveInfinity;
                        if (!mf.isAutoSteerBtnOn || ResetABLine)
                        {
                            int s = Lines[CurrentLine].Segments.Count - 1;
                            for (int t = 0; t < Lines[CurrentLine].Segments.Count; s = t++)
                            {
                                if (t == 0 && Lines[CurrentLine].Mode != Gmode.Boundary) continue;

                                double dist = pivot.FindDistanceToSegment(Lines[CurrentLine].Segments[s], Lines[CurrentLine].Segments[t]);

                                if (dist < minDistA)
                                {
                                    minDistA = dist;
                                    A = t;
                                    B = s;
                                }
                            }

                            if (double.IsInfinity(minDistA)) return;

                            if (A > B) { int C = A; A = B; B = C; }
                                if (Lines[CurrentLine].Mode == Gmode.Boundary && B == Lines[CurrentLine].Segments.Count - 1 && A == 0) { int C = A; A = B; B = C; }

                            if (A < Lines[CurrentLine].Segments.Count && B < Lines[CurrentLine].Segments.Count)
                            {
                                //get the distance from currently active AB line
                                Dx = Lines[CurrentLine].Segments[B].Northing - Lines[CurrentLine].Segments[A].Northing;
                                Dy = Lines[CurrentLine].Segments[B].Easting - Lines[CurrentLine].Segments[A].Easting;

                                if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) return;

                                double Heading = Math.Atan2(Dy, Dx);
                                //are we going same direction as stripList was created?
                                isSameWay = Math.PI - Math.Abs(Math.Abs(point.Heading - Heading) - Math.PI) < Glm.PIBy2;
                            }
                        }
                        if (A < Lines[CurrentLine].Segments.Count && B < Lines[CurrentLine].Segments.Count)
                        {
                            Dx = Lines[CurrentLine].Segments[B].Northing - Lines[CurrentLine].Segments[A].Northing;
                            Dy = Lines[CurrentLine].Segments[B].Easting - Lines[CurrentLine].Segments[A].Easting;
                            if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) return;
                            distanceFromRefLine = ((Dx * point.Easting) - (Dy * point.Northing) + (Lines[CurrentLine].Segments[B].Easting * Lines[CurrentLine].Segments[A].Northing) - (Lines[CurrentLine].Segments[B].Northing * Lines[CurrentLine].Segments[A].Easting)) / Math.Sqrt((Dx * Dx) + (Dy * Dy));
                        }
                    }

                    if (!mf.isAutoSteerBtnOn || ResetABLine)
                    {
                        double RefDist = (distanceFromRefLine + (isSameWay ? GuidanceOffset : -GuidanceOffset)) / WidthMinusOverlap;
                        if (RefDist < 0) HowManyPathsAway = (int)(RefDist - 0.5);
                        else HowManyPathsAway = (int)(RefDist + 0.5);
                    }

                    if ((GuidanceOffset != 0 && OldisSameWay != isSameWay) || HowManyPathsAway != OldHowManyPathsAway || ResetABLine)
                    {
                        if (mf.isSideGuideLines)
                        {
                            if (OldHowManyPathsAway != HowManyPathsAway || ResetABLine)
                            {
                                int Gcnt;

                                int Up = HowManyPathsAway - OldHowManyPathsAway;

                                if (Up < -5 || Up > 5) ExtraGuidanceLines.Clear();

                                int Count = ExtraGuidanceLines.Count;

                                if (Count < 6 || Up == 0)
                                {
                                    if (Count > 0) ExtraGuidanceLines.Clear();
                                    for (double i = -2.5; i < 3; i++)
                                    {
                                        ExtraGuidanceLines.Add(new List<List<Vec2>>());
                                        Gcnt = ExtraGuidanceLines.Count - 1;
                                        CalculateOffsetList(out List<List<Vec2>> ttt, WidthMinusOverlap * (HowManyPathsAway + i), false);
                                        ExtraGuidanceLines[Gcnt] = ttt;
                                    }
                                }
                                else if (Up < 0)
                                {
                                    for (int i = -1; i >= Up; i--)
                                    {
                                        ExtraGuidanceLines.RemoveAt(5);
                                        ExtraGuidanceLines.Insert(0, new List<List<Vec2>>());
                                        Gcnt = 0;
                                        CalculateOffsetList(out List<List<Vec2>> ttt, WidthMinusOverlap * (OldHowManyPathsAway - 2.5 + i), false);
                                        ExtraGuidanceLines[Gcnt] = ttt;
                                    }
                                }
                                else
                                {
                                    for (int i = 1; i <= Up; i++)
                                    {
                                        ExtraGuidanceLines.RemoveAt(0);
                                        ExtraGuidanceLines.Insert(5, new List<List<Vec2>>());
                                        Gcnt = 5;
                                        CalculateOffsetList(out List<List<Vec2>> ttt, WidthMinusOverlap * (OldHowManyPathsAway + 2.5 + i), false);
                                        ExtraGuidanceLines[Gcnt] = ttt;
                                    }
                                }
                            }
                        }
                        else ExtraGuidanceLines.Clear();

                        ResetABLine = false;
                        OldisSameWay = isSameWay;
                        OldHowManyPathsAway = HowManyPathsAway;

                        CalculateOffsetList(out List<List<Vec2>> tttt, WidthMinusOverlap * HowManyPathsAway + (isSameWay ? -GuidanceOffset : GuidanceOffset), true);

                        if (tttt.Count > 0)
                        curList = tttt[0];
                    }
                }
                mf.CalculateSteerAngle(ref curList, isSameWay, Lines[CurrentLine].Mode == Gmode.Boundary);
            }
            else
            {
                //invalid distance so tell AS module
                mf.distanceFromCurrentLine = 32000;
                mf.guidanceLineDistanceOff = 32000;
            }
        }

        public void CalculateOffsetList(out List<List<Vec2>> Output, double Offset, bool RoundCorner)
        {
            Output = new List<List<Vec2>>();

            if (Lines[CurrentLine].Mode == Gmode.AB || Lines[CurrentLine].Mode == Gmode.Heading)
            {
                double cosHeading = Math.Cos(Lines[CurrentLine].Heading);
                double sinHeading = Math.Sin(Lines[CurrentLine].Heading);
                Output.Add(new List<Vec2>());
                Output[0].Add(new Vec2(Lines[CurrentLine].Segments[0].Easting + sinHeading * -mf.maxCrossFieldLength + cosHeading * Offset, Lines[CurrentLine].Segments[0].Northing + cosHeading * -mf.maxCrossFieldLength + sinHeading * -Offset));
                Output[0].Add(new Vec2(Lines[CurrentLine].Segments[1].Easting + sinHeading * mf.maxCrossFieldLength + cosHeading * Offset, Lines[CurrentLine].Segments[1].Northing + cosHeading * mf.maxCrossFieldLength + sinHeading * -Offset));
            }
            else
            {
                if (Lines[CurrentLine].Segments.Count > 1)
                {
                    if (Math.Abs(Offset) > 0.01)
                    {
                        List<Vec2> OffsetPoints = Lines[CurrentLine].Segments.OffsetPolyline(Offset, CancellationToken.None, Lines[CurrentLine].Mode == Gmode.Boundary);

                        if (Lines[CurrentLine].Mode != Gmode.Boundary)
                            OffsetPoints.AddFirstLastPoint22(mf.maxCrossFieldLength);

                        Output = OffsetPoints.FixPolyline(Offset, CancellationToken.None, Lines[CurrentLine].Mode == Gmode.Boundary, in mf.bnd.Boundaries[0].Polygon.Points, false);
                    }
                    else
                    {
                        Output.Add(Lines[CurrentLine].Segments.ToList());
                    }


                    if (RoundCorner)
                    {
                        for (int s = 0; s < Output.Count; s++)
                        {
                            if (Output[s].Count < 3) return;
                            double distance = Glm.Distance(Output[s][0], Output[s][Output[s].Count - 1]);
                            bool loop = distance < 5 || Lines[CurrentLine].Mode == Gmode.Boundary;
                            Output[s].CalculateRoundedCorner(mf.vehicle.minTurningRadius, loop, 0.0436332, CancellationToken.None);
                        }
                    }
                }
            }
        }

        public void MoveLine(int Idx, double dist)
        {
            if (Idx < Lines.Count && Idx > -1)
            {
                Lines[Idx].Segments = Lines[Idx].Segments.OffsetPolyline(dist, CancellationToken.None, Lines[Idx].Mode == Gmode.Boundary);

                if (Idx == CurrentTramLine && mf.Guidance.TramDisplayMode > 0) BuildTram();

                if (isSmoothWindowOpen) SmoothAB(SmoothCount * 2);
            }
            ResetABLine = true;
        }

        public void AddFirstLastPoints()
        {
            int ptCnt = Lines[CurrentEditLine].Segments.Count;

            if (ptCnt > 2)
            {
                double x = 0, y = 0;

                double Distance = 0;
                int i = ptCnt-1;

                for (int j = ptCnt - 2; j > 0; i = j--)
                {
                    double lastDistance = Glm.Distance(Lines[CurrentEditLine].Segments[i], Lines[CurrentEditLine].Segments[j]);

                    if (Distance + lastDistance > 15)
                    {
                        x += (Lines[CurrentEditLine].Segments[j].Northing - Lines[CurrentEditLine].Segments[i].Northing) / lastDistance * (15 - Distance);
                        y += (Lines[CurrentEditLine].Segments[j].Easting - Lines[CurrentEditLine].Segments[i].Easting) / lastDistance * (15 - Distance);
                        Distance = 15 - Distance;
                        break;
                    }
                    x += (Lines[CurrentEditLine].Segments[j].Northing - Lines[CurrentEditLine].Segments[i].Northing);
                    y += (Lines[CurrentEditLine].Segments[j].Easting - Lines[CurrentEditLine].Segments[i].Easting);
                    Distance += lastDistance;
                }
                x /= Distance;
                y /= Distance;

                double EndHeading = Math.Atan2(y, x);

                Vec2 EndPoint = Lines[CurrentEditLine].Segments[ptCnt - 1];

                EndPoint.Easting -= Math.Sin(EndHeading) * 20;
                EndPoint.Northing -= Math.Cos(EndHeading) * 20;
                Lines[CurrentEditLine].Segments.Add(EndPoint);


                x = 0;
                y = 0;
                Distance = 0;
                i = 0;

                for (int j = 1; j < ptCnt; i = j++)
                {
                    double lastDistance = Glm.Distance(Lines[CurrentEditLine].Segments[i], Lines[CurrentEditLine].Segments[j]);

                    if (Distance + lastDistance > 15)
                    {
                        x += (Lines[CurrentEditLine].Segments[i].Northing - Lines[CurrentEditLine].Segments[j].Northing) / lastDistance * (15 - Distance);
                        y += (Lines[CurrentEditLine].Segments[i].Easting - Lines[CurrentEditLine].Segments[j].Easting) / lastDistance * (15 - Distance);
                        Distance = (15 - Distance);
                        break;
                    }
                    x += (Lines[CurrentEditLine].Segments[i].Northing - Lines[CurrentEditLine].Segments[j].Northing);
                    y += (Lines[CurrentEditLine].Segments[i].Easting - Lines[CurrentEditLine].Segments[j].Easting);
                    Distance += lastDistance;
                }
                x /= Distance;
                y /= Distance;
                double StartHeading = Math.Atan2(y, x);

                //and the beginning
                Vec2 StartPoint = Lines[CurrentEditLine].Segments[0];

                StartPoint.Easting += Math.Sin(StartHeading) * 20;
                StartPoint.Northing += Math.Cos(StartHeading) * 20;
                Lines[CurrentEditLine].Segments.Insert(0, StartPoint);
            }
        }
    }

    public enum Gmode { Spiral, Circle, AB, Heading, Curve, Boundary };

    public class CGuidanceLine
    {
        public List<Vec2> Segments = new List<Vec2>();
        public double Heading = 3;
        public string Name = "aa";
        public Gmode Mode;
    }
}
