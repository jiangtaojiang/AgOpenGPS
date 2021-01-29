using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        // the list of possible bounds points
        public List<Vec2> turnClosestList = new List<Vec2>();

        public List<List<Vec3>> BoxList = new List<List<Vec3>>();

        public int turnSelected = 0, closestTurnNum;
        //generated box for finding closest point
        public Vec2 boxA = new Vec2(9000, 9000), boxB = new Vec2(9000, 9002);

        public Vec2 boxC = new Vec2(9001, 9001), boxD = new Vec2(9002, 9003);

        //point at the farthest turn segment from pivotAxle
        public Vec3 closestTurnPt = new Vec3();

        /// <summary>/// triggered right after youTurnTriggerPoint is set /// </summary>
        public bool isYouTurnTriggered;

        /// <summary>  /// turning right or left?/// </summary>
        public bool isYouTurnRight;
        public bool SwapYouTurn = true;
        //controlled by user in GUI to en/dis able
        public bool isRecordingCustomYouTurn;

        /// <summary> /// Is the youturn button enabled? /// </summary>
        public bool isYouTurnBtnOn;

        //Patterns or Dubins
        public byte YouTurnType;

        public double distanceTurnBeforeLine = 0;
        public double ytLength = 0;

        public int rowSkipsWidth = 1;

        /// <summary>  /// distance from headland as offset where to start turn shape /// </summary>
        public int youTurnStartOffset;

        public bool isTurnCreationTooClose = false, isTurnCreationNotCrossingError = false;

        //pure pursuit values
        private Vec4 ExitPoint = new Vec4(0, 0, 0, 0, 0), EntryPoint = new Vec4(0, 0, 0, 0, 0);
        private List<Vec2> OffsetList = new List<Vec2>();

        public double rEastYT, rNorthYT;

        //list of points for scaled and rotated YouTurn line, used for pattern, dubins, abcurve, abline
        public List<Vec2> ytList = new List<Vec2>();

        //list of points read from file, this is the actual pattern from a bunch of sources possible
        public List<Vec2> youFileList = new List<Vec2>();

        //to try and pull a UTurn back in bounds
        public double turnDistanceAdjuster;

        //is UTurn pattern in or out of bounds
        public bool isOutOfBounds = false;

        //sequence of operations of finding the next turn 0 to 3
        public int youTurnPhase, curListCount;
        public double onA;

        public Vec4 crossingCurvePointA = new Vec4(), crossingCurvePointB = new Vec4();
        public Vec2 crossingTurnLinePointA = new Vec2(), crossingTurnLinePointB = new Vec2();

        //Finds the point where an AB Curve crosses the turn line
        public bool FindCurveTurnPoints()
        {
            crossingCurvePointA.Easting = -20000;

            //find closet AB Curve point that will cross and go out of bounds
            curListCount = curList.Count;
            if (curListCount < 3) return false;

            int Count = isSameWay ? 1 : -1;
            bool Loop = true;
            int A = 0, B = 0, Index = -1;

            for (int j = mf.currentLocationIndexA; (isSameWay ? j < mf.currentLocationIndexA : j > mf.currentLocationIndexA) || Loop; j += Count)
            {
                if (isSameWay && j == curListCount)
                {
                    if (Lines[CurrentLine].Mode != Gmode.Spiral && Lines[CurrentLine].Mode != Gmode.Circle && Lines[CurrentLine].Mode != Gmode.Boundary)
                        break;
                    j = 0;
                    Loop = false;
                }
                else if (!isSameWay && j == -1)
                {
                    if (Lines[CurrentLine].Mode != Gmode.Spiral && Lines[CurrentLine].Mode != Gmode.Circle && Lines[CurrentLine].Mode != Gmode.Boundary)
                        break;
                    j = curListCount - 1;
                    Loop = false;
                }

                if (!mf.bnd.Boundaries[0].IsPointInTurnArea(curList[j]))
                {
                    if (isSameWay)
                    {
                        A = (j - 1).Clamp(curList.Count);
                        B = j;
                    }
                    else
                    {
                        A = j;
                        B = (j + 1).Clamp(curList.Count);
                    }
                    Index = 0;
                    break;
                }

                for (int k = 1; k < mf.bnd.Boundaries.Count; k++)
                {
                    //make sure not inside a non drivethru boundary
                    if (mf.bnd.Boundaries[k].isDriveThru || mf.bnd.Boundaries[k].isDriveAround) continue;
                    if (mf.bnd.Boundaries[k].IsPointInTurnArea(curList[j]))
                    {
                        if (isSameWay)
                        {
                            A = (j - 1).Clamp(curList.Count);
                            B = j;
                        }
                        else
                        {
                            A = j;
                            B = (j + 1).Clamp(curList.Count);
                        }
                        Index = k;
                        goto CrossingFound;
                    }
                }
            }

            //escape for multiple for's
            CrossingFound:;

            if (Index == -1)
            {
                if (Lines[CurrentLine].Mode != Gmode.Spiral && Lines[CurrentLine].Mode != Gmode.Circle && Lines[CurrentLine].Mode != Gmode.Boundary)
                    isTurnCreationNotCrossingError = true;
                else
                {
                    youTurnPhase = 4;
                }
                return false;
            }

            int i = mf.bnd.Boundaries[Index].turnLine.Count-1;
            for (int j = 0; j < mf.bnd.Boundaries[Index].turnLine.Count; i = j++)
            {
                if (DanielP.GetLineIntersection(curList[A], curList[B], mf.bnd.Boundaries[Index].turnLine[i], mf.bnd.Boundaries[Index].turnLine[j], out _, out _))
                {
                    crossingTurnLinePointA.Easting = mf.bnd.Boundaries[Index].turnLine[i].Easting;
                    crossingTurnLinePointA.Northing = mf.bnd.Boundaries[Index].turnLine[i].Northing;
                    crossingTurnLinePointB.Easting = mf.bnd.Boundaries[Index].turnLine[j].Easting;
                    crossingTurnLinePointB.Northing = mf.bnd.Boundaries[Index].turnLine[j].Northing;
                    break;
                }
            }
            return true;
        }

        public void AddSequenceLines(double head, bool Straight = false)
        {
            Vec2 pt;
            double Hcos = Math.Cos(head);
            double Hsin = Math.Sin(head);

            double distancePivotToTurnLine;
            for (int i = 0; i < youTurnStartOffset+1; i++)
            {
                if (i < youTurnStartOffset)
                {
                    pt.Easting = ytList[0].Easting - Hsin;
                    pt.Northing = ytList[0].Northing - Hcos;
                    ytList.Insert(0, pt);
                }

                distancePivotToTurnLine = Glm.Distance(ytList[0], mf.pivotAxlePos);
                if (distancePivotToTurnLine > 3)
                {
                    isTurnCreationTooClose = false;
                }
                else
                {
                    isTurnCreationTooClose = true;
                    break;
                }

                if (Straight)
                {
                    pt.Easting = ytList[ytList.Count - 1].Easting + Hsin;
                    pt.Northing = ytList[ytList.Count - 1].Northing + Hcos;
                }
                else
                {
                    pt.Easting = ytList[ytList.Count - 1].Easting - Hsin;
                    pt.Northing = ytList[ytList.Count - 1].Northing - Hcos;
                }
                ytList.Add(pt);
            }
            ytLength = 0;
            for (int i = 0; i + 2 < ytList.Count; i++)
            {
                ytLength += Glm.Distance(ytList[i], ytList[i+1]);
            }
        }

        //list of points of collision path avoidance
        public List<Vec3> mazeList = new List<Vec3>();

        public bool BuildGuidanceYouTurn()
        {
            List<Vec4> Crossings = new List<Vec4>();
            bool isCountingUp = isSameWay;
            if (youTurnPhase == 0)
            {
                Vec2 Start = new Vec2(mf.rEast, mf.rNorth);
                Vec2 End;
                bool Loop = true;
                //check if outside a border
                for (int j = mf.B; (isCountingUp && j < mf.B) || (!isCountingUp && j > mf.B) || Loop; j = (isCountingUp ? ++j : --j))
                {
                    if (isCountingUp && j == curList.Count)
                    {
                        if (Lines[CurrentLine].Mode != Gmode.Circle && Lines[CurrentLine].Mode != Gmode.Boundary)
                            break;
                        j = 0;
                        Loop = false;
                    }
                    else if (!isCountingUp && j == -1)
                    {
                        if (Lines[CurrentLine].Mode != Gmode.Circle && Lines[CurrentLine].Mode != Gmode.Boundary)
                            break;
                        j = curList.Count - 1;
                        Loop = false;
                    }
                    End = curList[j];

                    for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
                    {
                        if (mf.bnd.Boundaries[i].isDriveThru) continue;
                        Crossings.FindCrossingPoints(mf.bnd.Boundaries[i].turnLine, Start, End, i);
                    }
                    if (Crossings.Count > 0) break;//we only care for the closest one;
                    Start = End;
                }

                if (Crossings.Count > 0)
                {
                    Crossings.Sort((x, y) => x.Time.CompareTo(y.Time));//Now we have te closest crossing! most of the time its just 1;
                    ExitPoint = Crossings[0];
                    youTurnPhase = 1;
                }
                else if (Lines[CurrentLine].Mode == Gmode.Boundary || Lines[CurrentLine].Mode == Gmode.Circle)
                {
                    youTurnPhase = 4;
                }
                else if (Lines[CurrentLine].Mode == Gmode.Spiral)
                {
                    youTurnPhase = 4;
                    //needs to recalculate if line changes
                }
                else
                {
                    isTurnCreationNotCrossingError = true;
                    youTurnPhase = -1;
                }
            }
            else if (youTurnPhase == 1)
            {
                OffsetList.Clear();

                CalculateOffsetList(out List<List<Vec2>> ttt, WidthMinusOverlap * (HowManyPathsAway + rowSkipsWidth * (isSameWay ? (isYouTurnRight? 1 : -1) : (isYouTurnRight ? -1 : 1))) + (isSameWay ? GuidanceOffset : -GuidanceOffset), true);

                OffsetList = ttt[0];

                double turnOffset = (WidthMinusOverlap * rowSkipsWidth) + (isYouTurnRight ? GuidanceOffset * 2.0 : -GuidanceOffset * 2.0);

                bool TurnRight = (turnOffset < 0) ? !isYouTurnRight : isYouTurnRight;
                if (ExitPoint.Index != 0) TurnRight = !TurnRight;

                int Idx = (int)(ExitPoint.Heading + (TurnRight ? 0.5 : -0.5));
                int StartInt = Idx;

                Idx = (Idx + (TurnRight ? 1 : -1)).Clamp(mf.bnd.Boundaries[ExitPoint.Index].turnLine.Count);


                Vec2 Start = new Vec2(mf.bnd.Boundaries[ExitPoint.Index].turnLine[StartInt].Easting, mf.bnd.Boundaries[ExitPoint.Index].turnLine[StartInt].Northing);
                Vec2 End;
                bool Loop = true;
                //check if outside a border
                for (int j = Idx; (TurnRight && j < StartInt) || (!TurnRight && j > StartInt) || Loop; j = (TurnRight ? ++j : --j))
                {
                    if (TurnRight && j == mf.bnd.Boundaries[ExitPoint.Index].turnLine.Count)
                    {
                        j = 0;
                        Loop = false;
                    }
                    else if (!TurnRight && j == -1)
                    {
                        j = mf.bnd.Boundaries[ExitPoint.Index].turnLine.Count - 1;
                        Loop = false;
                    }
                    End = mf.bnd.Boundaries[ExitPoint.Index].turnLine[j];

                    int L = 0;
                    for (int K = 1; K < OffsetList.Count; L = K++)
                    {
                        if (DanielP.GetLineIntersection(Start, End, OffsetList[K], OffsetList[L], out Vec2 Crossing, out double Time))
                        {
                            Crossings.Add(new Vec4(Crossing.Easting, Crossing.Northing, (K + L) / 2.0, Time, 0));
                        }
                    }

                    if (Crossings.Count > 0) break;//we only care for the closest one;
                    Start = End;
                }

                if (Crossings.Count > 0)
                {
                    Crossings.Sort((x, y) => x.Time.CompareTo(y.Time));//Now we have te closest crossing! most of the time its just 1;
                    EntryPoint = Crossings[0];
                    youTurnPhase = 2;
                    if (EntryPoint.Index == 0)
                    {
                    }
                }
                else
                {
                    isTurnCreationNotCrossingError = true;
                    youTurnPhase = -1;
                }
                youTurnPhase = 2;
            }
            else if (youTurnPhase == 2)
            {


            }
            else if (youTurnPhase == 3)
            {

                youTurnPhase = 4;
            }


            return false;
        }

        public bool FindClosestTurnPoint(bool isYouTurnRight, Vec2 fromPt, double headAB)
        {
            //initial scan is straight ahead of pivot point of vehicle to find the right turnLine/boundary

            int closestTurnNum = int.MinValue;

            double CosHead = Math.Cos(headAB);
            double SinHead = Math.Sin(headAB);

            List<Vec4> Crossings1 = new List<Vec4>();

            Vec2 rayPt = fromPt;
            rayPt.Northing += CosHead * mf.maxCrossFieldLength;
            rayPt.Easting += SinHead * mf.maxCrossFieldLength;

            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                Crossings1.FindCrossingPoints(mf.bnd.Boundaries[i].turnLine, fromPt, rayPt, i);
            }

            if (Crossings1.Count > 0)
            {
                Crossings1.Sort((x, y) => x.Time.CompareTo(y.Time));


                rayPt.Easting = Crossings1[0].Easting;
                rayPt.Northing = Crossings1[0].Northing;
                closestTurnNum = Crossings1[0].Index;

                crossingTurnLinePointA = mf.bnd.Boundaries[Crossings1[0].Index].turnLine[((int)(Crossings1[0].Heading - 0.5)).Clamp(mf.bnd.Boundaries[Crossings1[0].Index].turnLine.Count)];
                crossingTurnLinePointB = mf.bnd.Boundaries[Crossings1[0].Index].turnLine[((int)(Crossings1[0].Heading + 0.5)).Clamp(mf.bnd.Boundaries[Crossings1[0].Index].turnLine.Count)];

                return true;
            }
            else return false;
        }

        public bool FindClosestTurnPointinBox(bool isYouTurnRight, Vec3 fromPt)
        {
            double CosHead = Math.Cos(fromPt.Heading);
            double SinHead = Math.Sin(fromPt.Heading);

            double scanWidthL, scanWidthR;

            if (isYouTurnRight)
            {
                scanWidthL = -(mf.Guidance.GuidanceWidth * 0.5 - mf.Guidance.GuidanceOffset);
                scanWidthR = mf.Guidance.GuidanceWidth * (0.5 + mf.Guidance.rowSkipsWidth) + mf.Guidance.GuidanceOffset;
            }
            else
            {
                scanWidthL = -(mf.Guidance.GuidanceWidth * (0.5 + mf.Guidance.rowSkipsWidth) - mf.Guidance.GuidanceOffset);
                scanWidthR = (mf.Guidance.GuidanceWidth * 0.5 + mf.Guidance.GuidanceOffset);
            }

            double NorthingMax = boxA.Northing = fromPt.Northing + SinHead * -scanWidthL;
            double EastingMax = boxA.Easting = fromPt.Easting + CosHead * scanWidthL;
            double NorthingMin = NorthingMax;
            double EastingMin = EastingMax;

            boxB.Northing = fromPt.Northing + SinHead * -scanWidthR;


            if (boxB.Northing > NorthingMax) NorthingMax = boxB.Northing;
            if (boxB.Northing < NorthingMin) NorthingMin = boxB.Northing;
            boxB.Easting = fromPt.Easting + CosHead * scanWidthR;
            if (boxB.Easting > EastingMax) EastingMax = boxB.Easting;
            if (boxB.Easting < EastingMin) EastingMin = boxB.Easting;
            boxC.Northing = boxB.Northing + CosHead * mf.maxCrossFieldLength;
            if (boxC.Northing > NorthingMax) NorthingMax = boxC.Northing;
            if (boxC.Northing < NorthingMin) NorthingMin = boxC.Northing;
            boxC.Easting = boxB.Easting + SinHead * mf.maxCrossFieldLength;
            if (boxC.Easting > EastingMax) EastingMax = boxC.Easting;
            if (boxC.Easting < EastingMin) EastingMin = boxC.Easting;

            boxD.Northing = boxA.Northing + CosHead * mf.maxCrossFieldLength;
            if (boxD.Northing > NorthingMax) NorthingMax = boxD.Northing;
            if (boxD.Northing < NorthingMin) NorthingMin = boxD.Northing;
            boxD.Easting = boxA.Easting + SinHead * mf.maxCrossFieldLength;
            if (boxD.Easting > EastingMax) EastingMax = boxD.Easting;
            if (boxD.Easting < EastingMin) EastingMin = boxD.Easting;


            Vec2 BoxBA = boxB - boxA;
            Vec2 BoxDC = boxD - boxC;
            Vec2 BoxCB = boxC - boxB;
            Vec2 BoxAD = boxA - boxD;

            BoxList.Clear();
            Vec3 inBox;
            int lasti = -1;
            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                if (mf.bnd.Boundaries.Count > i)
                {
                    int ptCount = mf.bnd.Boundaries[i].turnLine.Count;
                    for (int p = 0; p < ptCount; p++)
                    {
                        if (mf.bnd.Boundaries[i].turnLine[p].Northing > NorthingMax || mf.bnd.Boundaries[i].turnLine[p].Northing < NorthingMin) continue;
                        if (mf.bnd.Boundaries[i].turnLine[p].Easting > EastingMax || mf.bnd.Boundaries[i].turnLine[p].Easting < EastingMin) continue;

                        if (((BoxBA.Easting * (mf.bnd.Boundaries[i].turnLine[p].Northing - boxA.Northing))
                                - (BoxBA.Northing * (mf.bnd.Boundaries[i].turnLine[p].Easting - boxA.Easting))) < 0) continue;

                        if (((BoxDC.Easting * (mf.bnd.Boundaries[i].turnLine[p].Northing - boxC.Northing))
                                - (BoxDC.Northing * (mf.bnd.Boundaries[i].turnLine[p].Easting - boxC.Easting))) < 0) continue;

                        if (((BoxCB.Easting * (mf.bnd.Boundaries[i].turnLine[p].Northing - boxB.Northing))
                                - (BoxCB.Northing * (mf.bnd.Boundaries[i].turnLine[p].Easting - boxB.Easting))) < 0) continue;

                        if (((BoxAD.Easting * (mf.bnd.Boundaries[i].turnLine[p].Northing - boxD.Northing))
                                - (BoxAD.Northing * (mf.bnd.Boundaries[i].turnLine[p].Easting - boxD.Easting))) < 0) continue;

                        inBox.Northing = mf.bnd.Boundaries[i].turnLine[p].Northing;
                        inBox.Easting = mf.bnd.Boundaries[i].turnLine[p].Easting;
                        inBox.Heading = p;

                        if (i != lasti)
                        {
                            BoxList.Add(new List<Vec3>());
                            lasti = i;
                        }
                        BoxList[BoxList.Count - 1].Add(inBox);
                    }
                }
            }
            return BoxList.Count > 0;
        }

        public bool BuildABLineCurveYouTurn()
        {
            if (CurrentLine < Lines.Count && CurrentLine > -1)
            {
                double head = Lines[CurrentLine].Heading;
                if (!isSameWay) head += Math.PI;


                double Hcos = Math.Cos(head);
                double Hsin = Math.Sin(head);
                List<Vec4> Crossings = new List<Vec4>();

                Vec2 Start = new Vec2(mf.rEast, mf.rNorth);
                Vec2 End = Start;

                End.Northing += Hcos * mf.maxCrossFieldLength;
                End.Easting += Hsin * mf.maxCrossFieldLength;

                double turnOffset = (WidthMinusOverlap * rowSkipsWidth) + (isYouTurnRight ? GuidanceOffset * 2.0 : -GuidanceOffset * 2.0);

                if (Math.Abs(turnOffset) > mf.vehicle.minTurningRadius * 2)
                {
                    for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
                    {
                        if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                        if (mf.bnd.Boundaries.Count > i)
                        {
                            Crossings.FindCrossingPoints(mf.bnd.Boundaries[i].turnLine, Start, End, i);
                        }
                    }
                    ytList.Clear();

                    if (Crossings.Count > 0)
                    {
                        Crossings.Sort((x, y) => x.Time.CompareTo(y.Time));

                        bool TurnRight = (turnOffset < 0) ? !isYouTurnRight : isYouTurnRight;


                        int Idx = ((int)(Crossings[0].Heading + (TurnRight ? 0.5 : -0.5))).Clamp(mf.bnd.Boundaries[Crossings[0].Index].turnLine.Count);
                        int StartInt = Idx;
                        int count = TurnRight ? 1 : -1;
                        if (Crossings[0].Index != 0) count *= -1;

                        if (turnOffset < 0) turnOffset *= -1;
                        if (!TurnRight) turnOffset *= -1;

                        Vec2 Start2;
                        Start2.Northing = Start.Northing + Hsin * -turnOffset - Hcos * mf.maxCrossFieldLength;
                        Start2.Easting = Start.Easting + Hcos * turnOffset - Hsin * mf.maxCrossFieldLength;

                        Vec2 End2;
                        End2.Northing = End.Northing + Hsin * -turnOffset;
                        End2.Easting = End.Easting + Hcos * turnOffset;

                        Vec2 StartEnd2 = Start2 - End2;
                        Vec2 StartEnd = Start - End;

                        ytList.Add(Start);
                        ytList.Add(new Vec2(Crossings[0].Easting, Crossings[0].Northing));

                        int CDLine = 0;
                        int ABLine = 0;



                        while (TurnRight ? CDLine <= 0 && ABLine >= 0 : CDLine >= 0 && ABLine <= 0)
                        {

                            ytList.Add(mf.bnd.Boundaries[Crossings[0].Index].turnLine[Idx]);

                            ABLine = Math.Sign((StartEnd.Easting * (mf.bnd.Boundaries[Crossings[0].Index].turnLine[Idx].Northing - End.Northing))
                               - (StartEnd.Northing * (mf.bnd.Boundaries[Crossings[0].Index].turnLine[Idx].Easting - End.Easting)));

                            //offset
                            CDLine = Math.Sign((StartEnd2.Easting * (mf.bnd.Boundaries[Crossings[0].Index].turnLine[Idx].Northing - End2.Northing))
                                - (StartEnd2.Northing * (mf.bnd.Boundaries[Crossings[0].Index].turnLine[Idx].Easting - End2.Easting)));

                            
                            Idx = (Idx + count).Clamp(mf.bnd.Boundaries[Crossings[0].Index].turnLine.Count);


                            if (Idx == StartInt)
                            {
                                ytList.Clear();
                                return YtListClear();
                            }
                        }

                        if (TurnRight ? ABLine < 0 : ABLine > 0)
                        {
                            if (Crossings.Count > 1 && Crossings[0].Index == Crossings[1].Index)
                            {
                                if (DanielP.GetLineIntersection(Start, End, ytList[ytList.Count - 2], ytList[ytList.Count - 1], out Vec2 Crossing2, out _))
                                {
                                    ytList.RemoveAt(ytList.Count - 1);
                                    ytList.Add(Crossing2);

                                    Crossing2.Northing += (Math.Cos(head) * WidthMinusOverlap * 15);
                                    Crossing2.Easting += (Math.Sin(head) * WidthMinusOverlap * 15);
                                    ytList.Add(Crossing2);
                                    SwapYouTurn = false;
                                }
                                else return YtListClear();
                            }
                            else return YtListClear();
                        }
                        else
                        {
                            if (DanielP.GetLineIntersection(Start2, End2, ytList[ytList.Count - 2], ytList[ytList.Count - 1], out Vec2 Crossing, out _))
                            {
                                ytList.RemoveAt(ytList.Count - 1);

                                ytList.Add(Crossing);

                                Crossing.Northing -= (Math.Cos(head) * WidthMinusOverlap * 15);
                                Crossing.Easting -= (Math.Sin(head) * WidthMinusOverlap * 15);
                                ytList.Add(Crossing);
                                SwapYouTurn = true;
                            }
                            else return YtListClear();
                        }


                        Start = new Vec2(ytList[ytList.Count - 2].Easting, ytList[ytList.Count - 2].Northing);

                        ytList.CalculateRoundedCorner(mf.vehicle.minTurningRadius, false, 0.0836332, CancellationToken.None);
                        ytList.RemoveAt(0);
                        ytList.RemoveAt(ytList.Count - 1);

                        count = ytList.Count;
                        if (count == 0) return YtListClear();

                        AddSequenceLines(head, !SwapYouTurn);
                        
                        if (ytList.Count == 0) return YtListClear();
                        
                        youTurnPhase = 4;

                        return true;
                    }
                    return YtListClear();
                }
                else
                {
                    return BuildABLineDubinsYouTurn(isYouTurnRight);
                }
            }
            return YtListClear();
        }

        public bool YtListClear()
        {
            ytList.Clear();
            return false;
        }

        public bool BuildABLineDubinsYouTurn(bool isTurnRight)
        {
            if (CurrentLine < Lines.Count && CurrentLine > -1)
            {
                double head = Lines[CurrentLine].Heading;
                if (!isSameWay) head += Math.PI;

                double Hcos = Math.Cos(head);
                double Hsin = Math.Sin(head);

                if (youTurnPhase == 0)
                {
                    //grab the pure pursuit point right on ABLine
                    Vec2 onPurePoint = new Vec2(mf.rEast, mf.rNorth);

                    //how far are we from any turn boundary
                    if (!FindClosestTurnPoint(isYouTurnRight, onPurePoint, head))
                    {
                        //Full emergency stop code goes here, it thinks its auto turn, but its not!
                        mf.distancePivotToTurnLine = -3333;
                        return false;
                    }
                    
                    mf.distancePivotToTurnLine = Glm.Distance(crossingTurnLinePointA, mf.pivotAxlePos);

                    double boundaryAngleOffPerpendicular = (crossingTurnLinePointA.Heading(crossingTurnLinePointB) - head) - Glm.PIBy2;

                    if (boundaryAngleOffPerpendicular > Math.PI) boundaryAngleOffPerpendicular -= Glm.twoPI;
                    if (boundaryAngleOffPerpendicular < -Math.PI) boundaryAngleOffPerpendicular += Glm.twoPI;

                    CDubins dubYouTurnPath = new CDubins();
                    CDubins.turningRadius = mf.vehicle.minTurningRadius;

                    double turnOffset = (WidthMinusOverlap * rowSkipsWidth) + (isTurnRight ? GuidanceOffset * 2.0 : -GuidanceOffset * 2.0);


                    double turnRadius = turnOffset * Math.Tan(boundaryAngleOffPerpendicular);

                    if (turnOffset < mf.vehicle.minTurningRadius * 2) turnRadius = 0;

                    //start point of Dubins
                    rEastYT = mf.rEast + Hsin * mf.distancePivotToTurnLine;
                    rNorthYT = mf.rNorth + Hcos * mf.distancePivotToTurnLine;
                    Vec3 Start = new Vec3(rEastYT, rNorthYT, head);
                    Vec3 Goal = new Vec3();

                    if (isTurnRight)
                    {
                        Goal.Northing = rNorthYT + Hsin * -turnOffset + Hcos * -turnRadius;
                        Goal.Easting = rEastYT + Hcos * turnOffset + Hsin * -turnRadius;
                        Goal.Heading = head - Math.PI;
                    }
                    else
                    {
                        Goal.Northing = rNorthYT + Hsin * turnOffset + Hcos * turnRadius;
                        Goal.Easting = rEastYT + Hcos * -turnOffset + Hsin * turnRadius;
                        Goal.Heading = head - Math.PI;
                    }

                    //generate the turn points
                    ytList = dubYouTurnPath.GenerateDubins(Start, Goal);
                    int count = ytList.Count;
                    if (count == 0) return false;

                    AddSequenceLines(head);
                    if (ytList.Count == 0) return false;
                    else youTurnPhase = 1;
                }

                if (youTurnPhase == 4) return true;

                // Phase 0 - back up the turn till it is out of bounds.
                // Phase 1 - move it forward till out of bounds.
                // Phase 2 - move forward couple meters away from turn line.
                // Phase 3 - ytList is made, waiting to get close enough to it

                isOutOfBounds = false;
                int cnt = ytList.Count;
                Vec2 arr2;

                if (youTurnPhase < 2)
                {
                    //the temp array
                    mf.distancePivotToTurnLine = Glm.Distance(ytList[0], mf.pivotAxlePos);

                    for (int i = 0; i < cnt; i++)
                    {
                        arr2 = ytList[i];
                        arr2.Northing -= Hcos;
                        arr2.Easting -= Hsin;
                        ytList[i] = arr2;
                    }

                }

                if (youTurnPhase > 0)
                {
                    for (int j = 0; j < cnt; j += 2)
                    {
                        if (!mf.bnd.Boundaries[0].IsPointInTurnArea(ytList[j])) isOutOfBounds = true;
                        if (isOutOfBounds) break;

                        for (int i = 1; i < mf.bnd.Boundaries.Count; i++)
                        {
                            //make sure not inside a non drivethru boundary
                            if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                            if (mf.bnd.Boundaries[i].IsPointInTurnArea(ytList[j]))
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }
                        if (isOutOfBounds) break;
                    }

                    if (!isOutOfBounds)
                    {
                        if (GuidanceOffset != 0)
                        {
                            if (isTurnRight)
                            {
                                for (int i = 0; i < cnt; i++)
                                {
                                    arr2 = ytList[i];
                                    arr2.Northing += Hcos * -GuidanceOffset;
                                    arr2.Easting += Hsin * -GuidanceOffset;
                                    ytList[i] = arr2;
                                }
                            }
                        }

                        youTurnPhase = 4;
                    }
                    else
                    {
                        youTurnPhase = 1;
                        //turn keeps approaching vehicle and running out of space - end of field?
                        if (isOutOfBounds && mf.distancePivotToTurnLine > 3)
                        {
                            isTurnCreationTooClose = false;
                        }
                        else
                        {
                            isTurnCreationTooClose = true;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public bool BuildABLinePatternYouTurn(bool isTurnRight)
        {
            if (CurrentLine < Lines.Count && CurrentLine > -1)
            {
                double headAB = Lines[CurrentLine].Heading;
                if (!isSameWay) headAB += Math.PI;

                //grab the pure pursuit point right on ABLine
                Vec2 onPurePoint = new Vec2(mf.rEast, mf.rNorth);

                //how far are we from any turn boundary
                if(!FindClosestTurnPoint(isYouTurnRight, onPurePoint, headAB))
                {
                    //Full emergency stop code goes here, it thinks its auto turn, but its not!
                    mf.distancePivotToTurnLine = -3333;
                }
                mf.distancePivotToTurnLine = Glm.Distance(crossingTurnLinePointA, mf.pivotAxlePos);

                distanceTurnBeforeLine = turnDistanceAdjuster;

                ytList.Clear();

                //point on AB line closest to pivot axle point from ABLine PurePursuit
                rEastYT = mf.rEast;
                rNorthYT = mf.rNorth;

                //grab the vehicle widths and offsets
                double toolOffset = GuidanceOffset * 2.0;

                double turnOffset = ((WidthMinusOverlap * rowSkipsWidth) + (isTurnRight ? toolOffset: -toolOffset));

                //Pattern Turn
                int numShapePoints = youFileList.Count;

                if (numShapePoints < 2) return false;

                Vec2[] pt = new Vec2[numShapePoints];

                //Now put the shape into an array since lists are immutable
                for (int i = 0; i < numShapePoints; i++)
                {
                    pt[i].Easting = youFileList[i].Easting;
                    pt[i].Northing = youFileList[i].Northing;
                }

                //start of path on the origin. Mirror the shape if left turn
                if (!isTurnRight)
                {
                    for (int i = 0; i < pt.Length; i++) pt[i].Easting *= -1;
                }

                //scaling - Drawing is 10m wide so find ratio of tool width
                double scale = turnOffset * 0.1;
                for (int i = 0; i < pt.Length; i++)
                {
                    pt[i].Easting *= scale;
                    pt[i].Northing *= scale;
                }

                double _turnDiagDistance = mf.distancePivotToTurnLine - distanceTurnBeforeLine;

                //move the start forward
                if (youTurnPhase < 2)
                {
                    rEastYT += (Math.Sin(headAB) * (_turnDiagDistance - turnOffset));
                    rNorthYT += (Math.Cos(headAB) * (_turnDiagDistance - turnOffset));
                }
                else
                {
                    _turnDiagDistance -= 2;
                    turnDistanceAdjuster += 5;
                    rEastYT += (Math.Sin(headAB) * (_turnDiagDistance - turnOffset));
                    rNorthYT += (Math.Cos(headAB) * (_turnDiagDistance - turnOffset));
                    youTurnPhase = 4;
                }

                //rotate pattern to match AB Line heading
                double xr, yr;
                for (int i = 0; i < pt.Length - 1; i++)
                {
                    xr = (Math.Cos(-headAB) * pt[i].Easting) - (Math.Sin(-headAB) * pt[i].Northing) + rEastYT;
                    yr = (Math.Sin(-headAB) * pt[i].Easting) + (Math.Cos(-headAB) * pt[i].Northing) + rNorthYT;

                    pt[i].Easting = xr;
                    pt[i].Northing = yr;
                    ytList.Add(pt[i]);
                }
                xr = (Math.Cos(-headAB) * pt[pt.Length - 1].Easting) - (Math.Sin(-headAB) * pt[pt.Length - 1].Northing) + rEastYT;
                yr = (Math.Sin(-headAB) * pt[pt.Length - 1].Easting) + (Math.Cos(-headAB) * pt[pt.Length - 1].Northing) + rNorthYT;

                pt[pt.Length - 1].Easting = xr;
                pt[pt.Length - 1].Northing = yr;
                ytList.Add(pt[pt.Length - 1]);

                //pattern all made now is it outside a boundary
                //now check to make sure we are not in an inner turn boundary - drive thru is ok
                int count = ytList.Count;
                if (count == 0) return false;
                isOutOfBounds = false;

                headAB += Math.PI;

                Vec2 ptt;
                for (int a = 0; a < youTurnStartOffset; a++)
                {
                    ptt.Easting = ytList[0].Easting + (Math.Sin(headAB));
                    ptt.Northing = ytList[0].Northing + (Math.Cos(headAB));
                    ytList.Insert(0, ptt);
                }

                count = ytList.Count;

                for (int i = 1; i <= youTurnStartOffset; i++)
                {
                    ptt.Easting = ytList[count - 1].Easting + (Math.Sin(headAB) * i);
                    ptt.Northing = ytList[count - 1].Northing + (Math.Cos(headAB) * i);
                    ytList.Add(ptt);
                }

                double distancePivotToTurnLine;
                count = ytList.Count;
                for (int i = 0; i < count; i += 2)
                {
                    distancePivotToTurnLine = Glm.Distance(ytList[i], mf.pivotAxlePos);
                    if (distancePivotToTurnLine > 3)
                    {
                        isTurnCreationTooClose = false;
                    }
                    else
                    {
                        isTurnCreationTooClose = true;
                        break;
                    }
                }

                // Phase 0 - back up the turn till it is out of bounds.
                // Phase 1 - move it forward till out of bounds.
                // Phase 2 - move forward couple meters away from turn line.

                for (int j = 0; j < count; j += 2)
                {
                    if (!mf.bnd.Boundaries[0].IsPointInTurnArea(ytList[j])) isOutOfBounds = true;
                    if (isOutOfBounds) break;

                    for (int i = 1; i < mf.bnd.Boundaries.Count; i++)
                    {
                        //make sure not inside a non drivethru boundary
                        if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                        if (mf.bnd.Boundaries[i].IsPointInTurnArea(ytList[j]))
                        {
                            isOutOfBounds = true;
                            break;
                        }
                    }
                    if (isOutOfBounds) break;
                }

                if (youTurnPhase == 0)
                {
                    turnDistanceAdjuster -= 2;
                    if (isOutOfBounds) youTurnPhase = 1;
                }
                else
                {
                    if (!isOutOfBounds)
                    {
                        youTurnPhase = 4;
                    }
                    else
                    {
                        //turn keeps approaching vehicle and running out of space - end of field?
                        if (isOutOfBounds && _turnDiagDistance > 3)
                        {
                            turnDistanceAdjuster += 2;
                            isTurnCreationTooClose = false;
                        }
                        else
                        {
                            isTurnCreationTooClose = true;
                        }
                    }
                }
                return isOutOfBounds;
            }
            return false;
        }

        public bool BuildCurvePatternYouTurn(bool isTurnRight, Vec3 pivotPos)
        {
            if (youTurnPhase > 0)
            {
                ytList.Clear();

                double head = crossingCurvePointA.Heading;
                if (!isSameWay) head += Math.PI;

                double toolOffset = GuidanceOffset * 2.0;
                double turnOffset = isTurnRight ? WidthMinusOverlap + toolOffset : WidthMinusOverlap - toolOffset;

                //Pattern Turn
                int numShapePoints = youFileList.Count;
                Vec2[] pt = new Vec2[numShapePoints];

                //Now put the shape into an array since lists are immutable
                for (int i = 0; i < numShapePoints; i++)
                {
                    pt[i].Easting = youFileList[i].Easting;
                    pt[i].Northing = youFileList[i].Northing;
                }

                //start of path on the origin. Mirror the shape if left turn
                if (!isTurnRight)
                {
                    for (int i = 0; i < pt.Length; i++) pt[i].Easting *= -1;
                }

                //scaling - Drawing is 10m wide so find ratio of tool width
                double scale = turnOffset * 0.1;
                for (int i = 0; i < pt.Length; i++)
                {
                    pt[i].Easting *= scale * rowSkipsWidth;
                    pt[i].Northing *= scale * rowSkipsWidth;
                }

                //rotate pattern to match AB Line heading
                double xr, yr;
                for (int i = 0; i < pt.Length - 1; i++)
                {
                    xr = (Math.Cos(-head) * pt[i].Easting) - (Math.Sin(-head) * pt[i].Northing) + crossingCurvePointA.Easting;
                    yr = (Math.Sin(-head) * pt[i].Easting) + (Math.Cos(-head) * pt[i].Northing) + crossingCurvePointA.Northing;

                    pt[i].Easting = xr;
                    pt[i].Northing = yr;
                    ytList.Add(pt[i]);
                }
                xr = (Math.Cos(-head) * pt[pt.Length - 1].Easting) - (Math.Sin(-head) * pt[pt.Length - 1].Northing) + crossingCurvePointA.Easting;
                yr = (Math.Sin(-head) * pt[pt.Length - 1].Easting) + (Math.Cos(-head) * pt[pt.Length - 1].Northing) + crossingCurvePointA.Northing;

                pt[pt.Length - 1].Easting = xr;
                pt[pt.Length - 1].Northing = yr;
                ytList.Add(pt[pt.Length - 1]);

                //pattern all made now is it outside a boundary
                head -= Math.PI;

                Vec2 ptt;
                for (int a = 0; a < youTurnStartOffset; a++)
                {
                    ptt.Easting = ytList[0].Easting + (Math.Sin(head));
                    ptt.Northing = ytList[0].Northing + (Math.Cos(head));
                    ytList.Insert(0, ptt);
                }

                int count = ytList.Count;

                for (int i = 1; i <= youTurnStartOffset; i++)
                {
                    ptt.Easting = ytList[count - 1].Easting + (Math.Sin(head) * i);
                    ptt.Northing = ytList[count - 1].Northing + (Math.Cos(head) * i);
                    ytList.Add(ptt);
                }

                double distancePivotToTurnLine;
                count = ytList.Count;
                for (int i = 0; i < count; i += 2)
                {
                    distancePivotToTurnLine = Glm.Distance(ytList[i], mf.pivotAxlePos);
                    if (distancePivotToTurnLine > 3)
                    {
                        isTurnCreationTooClose = false;
                    }
                    else
                    {
                        isTurnCreationTooClose = true;
                        break;
                    }
                }
            }

            switch (youTurnPhase)
            {
                case 0: //find the crossing points
                    if (FindCurveTurnPoints()) youTurnPhase = 1;
                    else
                    {
                        if (Lines[CurrentLine].Mode == Gmode.Spiral || Lines[CurrentLine].Mode == Gmode.Circle || Lines[CurrentLine].Mode == Gmode.Boundary)
                        {
                            isTurnCreationNotCrossingError = false;
                        }
                        else isTurnCreationNotCrossingError = true;
                    }
                    ytList.Clear();
                    break;

                case 1:
                    //now check to make sure turn is not in an inner turn boundary - drive thru is ok
                    int count = ytList.Count;
                    if (count == 0) return false;
                    isOutOfBounds = false;

                    //Out of bounds?
                    for (int j = 0; j < count; j += 2)
                    {
                        if (!mf.bnd.Boundaries[0].IsPointInTurnArea(ytList[j])) isOutOfBounds = true;
                        if (isOutOfBounds) break;

                        for (int i = 1; i < mf.bnd.Boundaries.Count; i++)
                        {
                            //make sure not inside a non drivethru boundary
                            if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                            if (mf.bnd.Boundaries[i].IsPointInTurnArea(ytList[j]))
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }
                        if (isOutOfBounds) break;
                    }

                    //first check if not out of bounds, add a bit more to clear turn line, set to phase 2
                    if (!isOutOfBounds)
                    {
                        youTurnPhase = 4;
                        return true;
                    }

                    //keep moving infield till pattern is all inside
                    if (isSameWay)
                    {
                        crossingCurvePointA.Index--;
                        crossingCurvePointB.Index--;
                        if (crossingCurvePointA.Index < 0) crossingCurvePointA.Index = 0;
                        if (crossingCurvePointB.Index < 1) crossingCurvePointB.Index = 1;
                    }
                    else
                    {
                        crossingCurvePointA.Index++;
                        crossingCurvePointB.Index++;
                        if (crossingCurvePointA.Index + 1 > curListCount)
                            crossingCurvePointA.Index = curListCount - 1;
                        if (crossingCurvePointB.Index + 2 > curListCount)
                            crossingCurvePointB.Index = curListCount - 2;
                    }
                    crossingCurvePointA.Easting = curList[crossingCurvePointA.Index].Easting;
                    crossingCurvePointA.Northing = curList[crossingCurvePointA.Index].Northing;
                    crossingCurvePointA.Heading = curList[crossingCurvePointA.Index].Heading(curList[crossingCurvePointB.Index]);

                    double tooClose = Glm.Distance(ytList[0], pivotPos);
                    isTurnCreationTooClose = tooClose < 3;
                    break;
            }
            return true;
        }

        public bool BuildCurveDubinsYouTurn(bool isTurnRight, Vec3 pivotPos)
        {
            double head = crossingCurvePointA.Heading;
            if (!isSameWay) head += Math.PI;

            if (youTurnPhase == 1)
            {
                double boundaryAngleOffPerpendicular =  (crossingTurnLinePointA.Heading(crossingTurnLinePointB) - head) - Glm.PIBy2;
                if (boundaryAngleOffPerpendicular > Math.PI) boundaryAngleOffPerpendicular -= Glm.twoPI;
                if (boundaryAngleOffPerpendicular < -Math.PI) boundaryAngleOffPerpendicular += Glm.twoPI;

                CDubins dubYouTurnPath = new CDubins();
                CDubins.turningRadius = mf.vehicle.minTurningRadius;

                double turnOffset = (WidthMinusOverlap * rowSkipsWidth) - (isTurnRight ? -GuidanceOffset * 2.0 : GuidanceOffset * 2.0);


                double turnRadius = turnOffset * Math.Tan(boundaryAngleOffPerpendicular);
                if (Math.Abs(turnRadius) > 100) turnRadius = 0;
                if (turnOffset < mf.vehicle.minTurningRadius * 2) turnRadius = 0;

                var start = new Vec3(crossingCurvePointA.Easting, crossingCurvePointA.Northing, head);

                var goal = new Vec3();
                if (isTurnRight)
                {
                    goal.Northing = crossingCurvePointA.Northing + Math.Sin(head) * -turnOffset + Math.Cos(head) * -turnRadius;
                    goal.Easting = crossingCurvePointA.Easting + Math.Cos(head) * turnOffset + Math.Sin(head) * -turnRadius;
                    goal.Heading = head - Math.PI;
                }
                else
                {
                    goal.Northing = crossingCurvePointA.Northing + Math.Sin(head) * turnOffset + Math.Cos(head) * turnRadius;
                    goal.Easting = crossingCurvePointA.Easting + Math.Cos(head) * -turnOffset + Math.Sin(head) * turnRadius;
                    goal.Heading = head + Math.PI;
                }

                //generate the turn points
                ytList = dubYouTurnPath.GenerateDubins(start, goal);
                int count = ytList.Count;
                if (count == 0) return false;

                //these are the lead in lead out lines that add to the turn
                AddSequenceLines(head);
            }

            switch (youTurnPhase)
            {
                case 0: //find the crossing points
                    if (FindCurveTurnPoints()) youTurnPhase = 1;
                    ytList.Clear();
                    break;

                case 1:
                    //now check to make sure we are not in an inner turn boundary - drive thru is ok
                    int count = ytList.Count;
                    if (count == 0) return false;

                    //Are we out of bounds?
                    isOutOfBounds = false;
                    for (int j = 0; j < count; j += 2)
                    {
                        if (!mf.bnd.Boundaries[0].IsPointInTurnArea(ytList[j]))
                        {
                            isOutOfBounds = true;
                            break;
                        }

                        for (int i = 1; i < mf.bnd.Boundaries.Count; i++)
                        {
                            //make sure not inside a non drivethru boundary
                            if (mf.bnd.Boundaries[i].isDriveThru || mf.bnd.Boundaries[i].isDriveAround) continue;
                            if (mf.bnd.Boundaries[i].IsPointInTurnArea(ytList[j]))
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }
                        if (isOutOfBounds) break;
                    }

                    //first check if not out of bounds, add a bit more to clear turn line, set to phase 2
                    if (!isOutOfBounds)
                    {
                        if (GuidanceOffset != 0)
                        {
                            double turnOffset = (WidthMinusOverlap * rowSkipsWidth) - (isTurnRight ? -GuidanceOffset * 2.0 : GuidanceOffset * 2.0);
                            if (turnOffset < 0) isTurnRight = !isTurnRight;
                            if (isTurnRight)
                            {
                                double cosHead = Math.Cos(crossingCurvePointA.Heading);
                                double sinHead = Math.Sin(crossingCurvePointA.Heading);

                                for (int i = 0; i < count; i++)
                                {
                                    Vec2 arr2 = ytList[i];
                                    arr2.Northing += cosHead * GuidanceOffset;
                                    arr2.Easting += sinHead * GuidanceOffset;
                                    ytList[i] = arr2;
                                }
                            }
                        }

                        youTurnPhase = 4;
                        return true;
                    }

                    //keep moving infield till pattern is all inside
                    if (isSameWay)
                    {
                        crossingCurvePointA.Index--;
                        crossingCurvePointB.Index--;
                        if (crossingCurvePointA.Index < 0) crossingCurvePointA.Index = 0;
                        if (crossingCurvePointB.Index < 1) crossingCurvePointB.Index = 1;
                    }
                    else
                    {
                        crossingCurvePointA.Index++;
                        crossingCurvePointB.Index++;
                        if (crossingCurvePointA.Index+1 > curListCount)
                            crossingCurvePointA.Index = curListCount - 1;
                        if (crossingCurvePointB.Index+2 > curListCount)
                            crossingCurvePointB.Index = curListCount - 2;
                    }
                    crossingCurvePointA.Easting = curList[crossingCurvePointA.Index].Easting;
                    crossingCurvePointA.Northing = curList[crossingCurvePointA.Index].Northing;
                    crossingCurvePointA.Heading = curList[crossingCurvePointA.Index].Heading(curList[crossingCurvePointB.Index]);

                    double tooClose = Glm.Distance(ytList[0], pivotPos);
                    isTurnCreationTooClose = tooClose < 3;
                    break;
            }
            return true;
        }

        //called to initiate turn
        public void YouTurnTrigger()
        {
            //trigger pulled
            isYouTurnTriggered = true;
            mf.seq.isSequenceTriggered = true;

            if (SwapYouTurn)
            {
                if (isSameWay)
                {
                    if (isYouTurnRight) HowManyPathsAway += rowSkipsWidth;
                    else HowManyPathsAway -= rowSkipsWidth;
                }
                else
                {
                    if (isYouTurnRight) HowManyPathsAway -= rowSkipsWidth;
                    else HowManyPathsAway += rowSkipsWidth;
                }
                isSameWay = !isSameWay;
            }
        }

        //Normal copmpletion of youturn
        public void CompleteYouTurn()
        {
            //just do the opposite of last turn
            if (SwapYouTurn) isYouTurnRight = !isYouTurnRight;
            SwapYouTurn = true;
            isYouTurnTriggered = false;
            ResetCreatedYouTurn();
            mf.seq.ResetSequenceEventTriggers();
            mf.seq.isSequenceTriggered = false;
            mf.isBoundAlarming = false;
        }

        //something went seriously wrong so reset everything
        public void ResetYouTurn()
        {
            //fix you turn
            SwapYouTurn = true;
            isYouTurnTriggered = false;
            ResetCreatedYouTurn();
            mf.isBoundAlarming = false;
            isTurnCreationTooClose = false;
            isTurnCreationNotCrossingError = false;

            //reset sequence
            mf.seq.ResetSequenceEventTriggers();
            mf.seq.isSequenceTriggered = false;
        }

        public void ResetCreatedYouTurn()
        {
            SwapYouTurn = true;
            turnDistanceAdjuster = 0;
            youTurnPhase = -1;
            ytList.Clear();
        }

        //get list of points from txt shape file
        public void LoadYouTurnShapeFromData(string Data)
        {
            try
            {
                string[] Text = Data.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                youFileList.Clear();
                Vec2 coords = new Vec2();
                for (int v = 0; v < Text.Length; v++)
                {
                    string[] words = Text[v].Split(',');
                    coords.Easting = double.Parse(words[0], CultureInfo.InvariantCulture);
                    coords.Northing = double.Parse(words[1], CultureInfo.InvariantCulture);
                    youFileList.Add(coords);
                }
            }
            catch (Exception e)
            {
                mf.TimedMessageBox(2000, "YouTurn File is Corrupt", "But Field is Loaded");
                mf.WriteErrorLog("YouTurn File is Corrupt " + e);
            }
        }

        //Resets the drawn YOuTurn and set diagPhase to 0
        //build the points and path of youturn to be scaled and transformed
        public void BuildManualYouTurn(bool isTurnRight, bool isTurnButtonTriggered)
        {
            isYouTurnTriggered = true;
            double Heading;
            //point on AB line closest to pivot axle point from ABLine PurePursuit
            if (CurrentLine < Lines.Count && CurrentLine > -1)
            {
                if (curList.Count < 2) return;

                rEastYT = mf.rEast;
                rNorthYT = mf.rNorth;

                //get the distance from currently active AB line
                double Dx = curList[mf.currentLocationIndexB].Northing - curList[mf.currentLocationIndexA].Northing;
                double Dy = curList[mf.currentLocationIndexB].Easting - curList[mf.currentLocationIndexA].Easting;

                if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) return;

                Heading = Math.Atan2(Dy, Dx);

                isSameWay = !isSameWay;

                if (!isSameWay)
                {
                    if (isTurnRight) HowManyPathsAway += rowSkipsWidth;
                    else HowManyPathsAway -= rowSkipsWidth;
                }
                else
                {
                    if (isTurnRight) HowManyPathsAway -= rowSkipsWidth;
                    else HowManyPathsAway += rowSkipsWidth;
                }
            }
            else return;

            double toolOffset = GuidanceOffset * 2.0;
            double turnOffset;

            //turning right
            if (isTurnRight) turnOffset = WidthMinusOverlap * rowSkipsWidth + toolOffset;
            else turnOffset = WidthMinusOverlap * rowSkipsWidth - toolOffset;

            CDubins dubYouTurnPath = new CDubins();
            CDubins.turningRadius = mf.vehicle.minTurningRadius;

            //if its straight across it makes 2 loops instead so goal is a little lower then start
            if (isSameWay) Heading += 3.14;
            else Heading -= 0.01;

            //move the start forward 2 meters, this point is critical to formation of uturn
            rEastYT += (Math.Sin(Heading) * 2);
            rNorthYT += (Math.Cos(Heading) * 2);

            //now we have our start point
            var start = new Vec3(rEastYT, rNorthYT, Heading);
            var goal = new Vec3();

            //now we go the other way to turn round
            Heading -= Math.PI;
            if (Heading < 0) Heading += Glm.twoPI;

            //set up the goal point for Dubins
            goal.Heading = Heading;
            if (isTurnButtonTriggered)
            {
                if (isTurnRight)
                {
                    goal.Easting = rEastYT - (Math.Cos(-Heading) * turnOffset);
                    goal.Northing = rNorthYT - (Math.Sin(-Heading) * turnOffset);
                }
                else
                {
                    goal.Easting = rEastYT + (Math.Cos(-Heading) * turnOffset);
                    goal.Northing = rNorthYT + (Math.Sin(-Heading) * turnOffset);
                }
            }

            //generate the turn points
            ytList = dubYouTurnPath.GenerateDubins(start, goal);

            Vec2 pt;
            for (int a = 0; a < 3; a++)
            {
                pt.Easting = ytList[0].Easting + (Math.Sin(Heading));
                pt.Northing = ytList[0].Northing + (Math.Cos(Heading));
                ytList.Insert(0, pt);
            }

            int count = ytList.Count;

            for (int i = 1; i <= 7; i++)
            {
                pt.Easting = ytList[count - 1].Easting + (Math.Sin(Heading) * i);
                pt.Northing = ytList[count - 1].Northing + (Math.Cos(Heading) * i);
                ytList.Add(pt);
            }
        }

        public void DrawYouTurn()
        {
            int ptCount = ytList.Count;
            if (ptCount > 0)
            {
                GL.PointSize(mf.lineWidth);

                if (isYouTurnTriggered) GL.Color3(0.95f, 0.95f, 0.25f);
                else if (isOutOfBounds) GL.Color3(0.9495f, 0.395f, 0.325f);
                else GL.Color3(0.395f, 0.925f, 0.30f);

                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i < ptCount; i++)
                {
                    GL.Vertex3(ytList[i].Easting, ytList[i].Northing, 0);
                }
                GL.End();
            }
            /*
            ptCount = OffsetList.Count;
            if (ptCount > 0)
            {
                GL.PointSize(mf.lineWidth);

                if (isYouTurnTriggered) GL.Color3(0.95f, 0.95f, 0.25f);
                else if (isOutOfBounds) GL.Color3(0.9495f, 0.395f, 0.325f);
                else GL.Color3(0.395f, 0.925f, 0.30f);

                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i < ptCount; i++)
                {
                    GL.Vertex3(OffsetList[i].Easting, OffsetList[i].Northing, 0);
                }
                GL.End();
            }
            
            GL.PointSize(5);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(ExitPoint.Easting, ExitPoint.Northing, 0);
            GL.Vertex3(EntryPoint.Easting, EntryPoint.Northing, 0);
            GL.End();
            */
        }
    }
}