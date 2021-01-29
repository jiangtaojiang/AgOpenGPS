using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CRecPathPt
    {
        public double Easting { get; set; }
        public double Northing { get; set; }
        public double Heading { get; set; }
        public double Speed { get; set; }
        public bool AutoBtnState { get; set; }

        //constructor
        public CRecPathPt(double _easting, double _northing, double _heading, double _speed,
                            bool _autoBtnState)
        {
            Easting = _easting;
            Northing = _northing;
            Heading = _heading;
            Speed = _speed;
            AutoBtnState = _autoBtnState;
        }
    }

    public class CRecordedPath
    {
        //constructor
        public CRecordedPath(FormGPS _f)
        {
            mf = _f;
        }

        //pointers to mainform controls
        private readonly FormGPS mf;

        //the recorded path from driving around
        public List<CRecPathPt> recList = new List<CRecPathPt>();

        public int recListCount;

        //the dubins path to get there
        public List<CRecPathPt> shuttleDubinsList = new List<CRecPathPt>();

        public int shuttleListCount;

        public List<Vec3> mazeList = new List<Vec3>();

        //list of vec3 points of Dubins shortest path between 2 points - To be converted to RecPt
        public List<Vec3> shortestDubinsList = new List<Vec3>();

        //generated reference line
        public Vec2 refPoint1 = new Vec2(1, 1), refPoint2 = new Vec2(2, 2);

        public double distanceFromRefLine, distanceFromCurrentLine, refLineSide = 1.0;
        private int A, B, C;
        public double abFixHeadingDelta, abHeading;
        public bool isABSameAsVehicleHeading = true, isOnRightSideCurrentLine = true;

        public int lastPointFound = -1, currentPositonIndex;

        //pure pursuit values
        public Vec3 steerAxlePosRP = new Vec3(0, 0, 0);

        public Vec2 goalPointRP = new Vec2(0, 0);
        public double steerAngleRP, rEastRP, rNorthRP;

        public bool isBtnFollowOn, isEndOfTheRecLine, isRecordOn;
        public bool isDrivingRecordedPath, isPausedDrivingRecordedPath, isFollowingRecPath, isFollowingDubinsHome;

        public bool StartDrivingRecordedPath()
        {
            //create the dubins path based on start and goal to start of recorded path
            A = B = C = 0;
            recListCount = recList.Count;
            if (recListCount < 5) return false;

            //technically all good if we get here so set all the flags
            isFollowingDubinsHome = false;
            isFollowingRecPath = true;
            isEndOfTheRecLine = false;
            currentPositonIndex = 0;
            isDrivingRecordedPath = true;

            isPausedDrivingRecordedPath = false;
            return true;
        }

        public bool trig;
        public double north;
        public int pathCount = 0;

        public void UpdatePosition()
        {
            if (isFollowingRecPath)
            {
                steerAxlePosRP = mf.steerAxlePos;

                StanleyRecPath(recListCount);

                //if end of the line then stop
                if (!isEndOfTheRecLine)
                {
                    mf.sim.stepDistance = recList[C].Speed / 17.86;
                    north = recList[C].Northing;

                    pathCount = recList.Count - C;

                    //section control - only if different click the button
                    bool autoBtn = (mf.autoBtnState == FormGPS.btnStates.Auto);
                    trig = autoBtn;
                    if (autoBtn != recList[C].AutoBtnState) mf.btnAutoSection.PerformClick();
                }
                else
                {
                    StopDrivingRecordedPath();
                    return;
                }
            }

            if (isFollowingDubinsHome)
            {
                int cnt = shuttleDubinsList.Count;
                pathCount = cnt - B;
                if (pathCount < 3)
                {
                    StopDrivingRecordedPath();
                    return;
                }

                mf.sim.stepDistance = shuttleDubinsList[C].Speed / 17.86;
                steerAxlePosRP = mf.steerAxlePos;

                StanleyDubinsPath(shuttleListCount);
            }

            //if paused, set the sim to 0
            if (isPausedDrivingRecordedPath) mf.sim.stepDistance = 0;
        }

        public void StopDrivingRecordedPath()
        {
            isFollowingDubinsHome = false;
            isFollowingRecPath = false;
            shuttleDubinsList.Clear();
            shortestDubinsList.Clear();
            mf.sim.stepDistance = 0;

            mf.sim.reverse = false;
            mf.btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;
            isDrivingRecordedPath = false;
            mf.goPathMenu.Image = Properties.Resources.AutoGo;
            isPausedDrivingRecordedPath = false;
        }

        private void StanleyRecPath(int ptCount)
        {
            //find the closest 2 points to current fix
            double minDistA = 9999999999;

            //set the search range close to current position
            int top = currentPositonIndex + 5;
            if (top > ptCount) top = ptCount;

            double dist;
            for (int t = currentPositonIndex; t < top; t++)
            {
                dist = ((steerAxlePosRP.Easting - recList[t].Easting) * (steerAxlePosRP.Easting - recList[t].Easting))
                                + ((steerAxlePosRP.Northing - recList[t].Northing) * (steerAxlePosRP.Northing - recList[t].Northing));
                if (dist < minDistA)
                {
                    minDistA = dist;
                    A = t;
                }
            }

            //Save the closest point
            C = A;

            //next point is the next in list
            B = A + 1;
            if (B == ptCount)
            {
                //don't go past the end of the list - "end of the line" trigger
                A--;
                B--;
                isEndOfTheRecLine = true;
            }

            //save current position
            currentPositonIndex = A;

            //get the distance from currently active AB line
            double dx = recList[B].Easting - recList[A].Easting;
            double dz = recList[B].Northing - recList[A].Northing;

            if (Math.Abs(dx) < Double.Epsilon && Math.Abs(dz) < Double.Epsilon) return;

            abHeading = Math.Atan2(dx, dz);

            //how far from current AB Line is fix
            distanceFromCurrentLine =
                ((dz * steerAxlePosRP.Easting) - (dx * steerAxlePosRP.Northing) + (recList[B].Easting
                        * recList[A].Northing) - (recList[B].Northing * recList[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            //are we on the right side or not
            isOnRightSideCurrentLine = distanceFromCurrentLine > 0;

            // calc point on ABLine closest to current position
            double U = (((steerAxlePosRP.Easting - recList[A].Easting) * dx)
                        + ((steerAxlePosRP.Northing - recList[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            rEastRP = recList[A].Easting + (U * dx);
            rNorthRP = recList[A].Northing + (U * dz);

            //the first part of stanley is to extract heading error
            double abFixHeadingDelta = (steerAxlePosRP.Heading - abHeading);

            //Fix the circular error - get it from -Pi/2 to Pi/2
            if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -Math.PI) abFixHeadingDelta += Math.PI;
            if (abFixHeadingDelta > Glm.PIBy2) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -Glm.PIBy2) abFixHeadingDelta += Math.PI;

            //normally set to 1, less then unity gives less heading error.
            abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
            if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
            if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

            //the non linear distance error part of stanley
            steerAngleRP = Math.Atan((distanceFromCurrentLine * mf.vehicle.stanleyGain) / ((mf.pn.speed * 0.277777) + 1));

            //clamp it to max 42 degrees
            if (steerAngleRP > 0.74) steerAngleRP = 0.74;
            if (steerAngleRP < -0.74) steerAngleRP = -0.74;

            //add them up and clamp to max in vehicle settings
            steerAngleRP = Glm.ToDegrees((steerAngleRP + abFixHeadingDelta) * -1.0);
            if (steerAngleRP < -mf.vehicle.maxSteerAngle) steerAngleRP = -mf.vehicle.maxSteerAngle;
            if (steerAngleRP > mf.vehicle.maxSteerAngle) steerAngleRP = mf.vehicle.maxSteerAngle;

            //Convert to millimeters and round properly to above/below .5
            distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);

            //every guidance method dumps into these that are used and sent everywhere, last one wins
            mf.guidanceLineDistanceOff = mf.distanceDisplay = (short)distanceFromCurrentLine;
            mf.guidanceLineSteerAngle = (short)(steerAngleRP * 100);
        }

        private void StanleyDubinsPath(int ptCount)
        {
            //distanceFromCurrentLine = 9999;
            //find the closest 2 points to current fix
            double minDistA = 9999999999;
            for (int t = 0; t < ptCount; t++)
            {
                double dist = ((steerAxlePosRP.Easting - shuttleDubinsList[t].Easting) * (steerAxlePosRP.Easting - shuttleDubinsList[t].Easting))
                                + ((steerAxlePosRP.Northing - shuttleDubinsList[t].Northing) * (steerAxlePosRP.Northing - shuttleDubinsList[t].Northing));
                if (dist < minDistA)
                {
                    minDistA = dist;
                    A = t;
                }
            }

            //save the closest point
            C = A;
            //next point is the next in list
            B = A + 1;
            if (B == ptCount) { A--; B--; }                //don't go past the end of the list - "end of the line" trigger

            //get the distance from currently active AB line
            //x2-x1
            double dx = shuttleDubinsList[B].Easting - shuttleDubinsList[A].Easting;
            //z2-z1
            double dz = shuttleDubinsList[B].Northing - shuttleDubinsList[A].Northing;

            if (Math.Abs(dx) < Double.Epsilon && Math.Abs(dz) < Double.Epsilon) return;

            //abHeading = Math.Atan2(dz, dx);
            abHeading = shuttleDubinsList[A].Heading;

            //how far from current AB Line is fix
            distanceFromCurrentLine = ((dz * steerAxlePosRP.Easting) - (dx * steerAxlePosRP
                .Northing) + (shuttleDubinsList[B].Easting
                        * shuttleDubinsList[A].Northing) - (shuttleDubinsList[B].Northing * shuttleDubinsList[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            //are we on the right side or not
            isOnRightSideCurrentLine = distanceFromCurrentLine > 0;

            // calc point on ABLine closest to current position
            double U = (((steerAxlePosRP.Easting - shuttleDubinsList[A].Easting) * dx)
                        + ((steerAxlePosRP.Northing - shuttleDubinsList[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            rEastRP = shuttleDubinsList[A].Easting + (U * dx);
            rNorthRP = shuttleDubinsList[A].Northing + (U * dz);

            //the first part of stanley is to extract heading error
            double abFixHeadingDelta = (steerAxlePosRP.Heading - abHeading);

            //Fix the circular error - get it from -Pi/2 to Pi/2
            if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -Math.PI) abFixHeadingDelta += Math.PI;
            if (abFixHeadingDelta > Glm.PIBy2) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -Glm.PIBy2) abFixHeadingDelta += Math.PI;

            //normally set to 1, less then unity gives less heading error.
            abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
            if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
            if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

            //the non linear distance error part of stanley
            steerAngleRP = Math.Atan((distanceFromCurrentLine * mf.vehicle.stanleyGain) / ((mf.pn.speed * 0.277777) + 1));

            //clamp it to max 42 degrees
            if (steerAngleRP > 0.74) steerAngleRP = 0.74;
            if (steerAngleRP < -0.74) steerAngleRP = -0.74;

            //add them up and clamp to max in vehicle settings
            steerAngleRP = Glm.ToDegrees((steerAngleRP + abFixHeadingDelta) * -1.0);
            if (steerAngleRP < -mf.vehicle.maxSteerAngle) steerAngleRP = -mf.vehicle.maxSteerAngle;
            if (steerAngleRP > mf.vehicle.maxSteerAngle) steerAngleRP = mf.vehicle.maxSteerAngle;

            //Convert to millimeters and round properly to above/below .5
            distanceFromCurrentLine = Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);

            //every guidance method dumps into these that are used and sent everywhere, last one wins
            mf.guidanceLineDistanceOff = mf.distanceDisplay = (short)distanceFromCurrentLine;
            mf.guidanceLineSteerAngle = (short)(steerAngleRP * 100);
        }

        public void DrawRecordedLine()
        {
            int ptCount = recList.Count;
            if (ptCount < 1) return;
            GL.LineWidth(1);
            GL.Color3(0.98f, 0.92f, 0.460f);
            GL.Begin(PrimitiveType.LineStrip);
            for (int h = 0; h < ptCount; h++) GL.Vertex3(recList[h].Easting, recList[h].Northing, 0);
            GL.End();

            if (mf.isPureDisplayOn)
            {
                //Draw lookahead Point
                GL.PointSize(8.0f);
                GL.Begin(PrimitiveType.Points);

                //GL.Color(1.0f, 1.0f, 0.25f);
                //GL.Vertex(rEast, rNorth, 0.0);

                GL.Color3(1.0f, 0.5f, 0.95f);
                GL.Vertex3(rEastRP, rNorthRP, 0.0);
                GL.End();
                GL.PointSize(1.0f);
            }
        }

        public void DrawDubins()
        {
            if (shuttleDubinsList.Count > 1)
            {
                //GL.LineWidth(2);
                GL.PointSize(2);
                GL.Color3(0.298f, 0.96f, 0.2960f);
                GL.Begin(PrimitiveType.Points);
                for (int h = 0; h < shuttleDubinsList.Count; h++)
                    GL.Vertex3(shuttleDubinsList[h].Easting, shuttleDubinsList[h].Northing, 0);
                GL.End();
            }
        }
    }
}