using System;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Graphics.OpenGL;

namespace AgOpenGPS
{
    public class TramPolygon
    {
        public List<Vec2> Center = new List<Vec2>();

        public int BufferPointsLeft = int.MinValue, BufferPointsRight = int.MinValue, BufferPointsCntLeft = 0, BufferPointsCntRight = 0;

        public void DrawPolygon()
        {
            if (BufferPointsLeft != int.MinValue)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPointsLeft);
                GL.VertexPointer(2, VertexPointerType.Double, 16, IntPtr.Zero);
                GL.EnableClientState(ArrayCap.VertexArray);
                GL.DrawArrays(PrimitiveType.TriangleStrip, 0, BufferPointsCntLeft);
            }

            if (BufferPointsRight != int.MinValue)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPointsRight);
                GL.VertexPointer(2, VertexPointerType.Double, 16, IntPtr.Zero);
                GL.EnableClientState(ArrayCap.VertexArray);
                GL.DrawArrays(PrimitiveType.TriangleStrip, 0, BufferPointsCntRight);
            }
        }
    }

    public partial class CGuidance
    {
        public List<TramPolygon> TramList = new List<TramPolygon>();
        public List<TramPolygon> BoundaryTram = new List<TramPolygon>();
        
        //tram settings
        public double TramWheelTrack, TramWheelWidth;
        public double TramWidth, TramOffset;
        public double TramHalfWheelTrack;
        public int TramPasses;
        public bool ResetBoundaryTram = true;

        // 0 off, 1 All, 2, Lines, 3 Outer
        public int TramDisplayMode;

        public void DrawTram(bool Force)
        {
            if (Force || TramDisplayMode > 0)
            {
                if (TramDisplayMode != 2 || Force)
                {
                    for (int b = 0; b < BoundaryTram.Count; b++)
                        BoundaryTram[b].DrawPolygon();
                }

                if (TramDisplayMode != 3 || Force)
                {
                    for (int a = 0; a < TramList.Count; a++)
                        TramList[a].DrawPolygon();
                }

                //draw tram numbers at end and beggining of line
                if (!Force && mf.font.isFontOn && TramDisplayMode != 3)
                {
                    for (int i = 0; i < TramList.Count; i++)
                    {
                        GL.Color4(0.8630f, 0.93692f, 0.8260f, 0.752);
                        if (TramList[i].Center.Count > 1)
                        {
                            int End = TramList[i].Center.Count - 2;
                            mf.font.DrawText3D(TramList[i].Center[End].Easting, TramList[i].Center[End].Northing, i.ToString());
                            mf.font.DrawText3D(TramList[i].Center[0].Easting, TramList[i].Center[0].Northing, i.ToString());
                        }
                    }
                }
            }
        }

        public void CreateBndTramRef()
        {
            ResetBoundaryTram = false;
            BoundaryTram.Clear();
            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                int ChangeDirection = i == 0 ? 1 : -1;

                double Offset = (TramWidth * 0.5 + TramOffset) * ChangeDirection;
                
                List<Vec2> OffsetPoints = mf.bnd.Boundaries[i].Polygon.Points.OffsetPolyline(Offset, CancellationToken.None, true);
                List<List<Vec2>> Build = OffsetPoints.FixPolyline(Offset, CancellationToken.None, true, in mf.bnd.Boundaries[i].Polygon.Points, true);

                for (int j = 0; j < Build.Count; j++)
                {
                    Build[j].CalculateRoundedCorner(mf.vehicle.minTurningRadius, true, 0.0436332, CancellationToken.None);

                    if (Build[j].Count > 1)
                    {
                        BoundaryTram.Add(new TramPolygon());
                        Vec2 Point;

                        List<Vec2> Left = new List<Vec2>();
                        List<Vec2> Right = new List<Vec2>();


                        double Dx, Dy, Heading, CosHeading, SinHeading;
                        int k = Build[j].Count - 1;
                        for (int l = 0; l < Build[j].Count; k = l++)
                        {

                            //get the distance from currently active AB line
                            Dx = Build[j][l].Northing - Build[j][k].Northing;
                            Dy = Build[j][l].Easting - Build[j][k].Easting;
                            if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) continue;

                            Heading = Math.Atan2(Dy, Dx);
                            CosHeading = Math.Cos(Heading);
                            SinHeading = Math.Sin(Heading);

                            Point = Build[j][l];
                            BoundaryTram[BoundaryTram.Count - 1].Center.Add(new Vec2(Point.Easting, Point.Northing));

                            Point.Northing += SinHeading * (TramHalfWheelTrack + TramWheelWidth / 2);
                            Point.Easting += CosHeading * (-TramHalfWheelTrack - TramWheelWidth / 2);
                            Left.Add(new Vec2(Point.Easting, Point.Northing));
                            Point.Northing += SinHeading * -TramWheelWidth;
                            Point.Easting += CosHeading * TramWheelWidth;
                            Left.Add(new Vec2(Point.Easting, Point.Northing));


                            Point = Build[j][l];
                            Point.Northing += SinHeading * (-TramHalfWheelTrack + TramWheelWidth / 2);
                            Point.Easting += CosHeading * (TramHalfWheelTrack - TramWheelWidth / 2);
                            Right.Add(new Vec2(Point.Easting, Point.Northing));
                            Point.Northing += SinHeading * -TramWheelWidth;
                            Point.Easting += CosHeading * TramWheelWidth;
                            Right.Add(new Vec2(Point.Easting, Point.Northing));
                        }
                        if (Left.Count > 1)
                        {
                            Left.Add(Left[0]);
                            Left.Add(Left[1]);
                            if (BoundaryTram[BoundaryTram.Count - 1].BufferPointsLeft == int.MinValue) GL.GenBuffers(1, out BoundaryTram[BoundaryTram.Count - 1].BufferPointsLeft);
                            GL.BindBuffer(BufferTarget.ArrayBuffer, BoundaryTram[BoundaryTram.Count - 1].BufferPointsLeft);
                            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Left.Count * 16), Left.ToArray(), BufferUsageHint.DynamicDraw);

                            BoundaryTram[BoundaryTram.Count - 1].BufferPointsCntLeft = Left.Count;
                        }
                        if (Right.Count > 1)
                        {
                            Right.Add(Right[0]);
                            Right.Add(Right[1]);
                            if (BoundaryTram[BoundaryTram.Count - 1].BufferPointsRight == int.MinValue) GL.GenBuffers(1, out BoundaryTram[BoundaryTram.Count - 1].BufferPointsRight);
                            GL.BindBuffer(BufferTarget.ArrayBuffer, BoundaryTram[BoundaryTram.Count - 1].BufferPointsRight);
                            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Right.Count * 16), Right.ToArray(), BufferUsageHint.DynamicDraw);

                            BoundaryTram[BoundaryTram.Count - 1].BufferPointsCntRight = Right.Count;
                        }
                    }
                }
            }
        }

        public void BuildTram()
        {
            TramList.Clear();
            if (ResetBoundaryTram && mf.bnd.Boundaries.Count > 0)
                CreateBndTramRef();

            if (CurrentTramLine > -1 && CurrentTramLine < Lines.Count)
            {
                if (Lines[CurrentTramLine].Mode == Gmode.AB || Lines[CurrentTramLine].Mode == Gmode.Heading)
                    BuildTram2();
                else
                {
                    List<List<Vec2>> Build = new List<List<Vec2>>();
                    int count = TramPasses < 0 ? -1 : 1;
                    for (double i = TramPasses < 0 ? -0.5 : 0.5; TramPasses < 0 ? i > TramPasses : i < TramPasses; i += count)
                    {
                        double Offset = (TramWidth * i) - WidthMinusOverlap / 2 + TramOffset;

                        List<Vec2> OffsetPoints = Lines[CurrentTramLine].Segments.OffsetPolyline(Offset, CancellationToken.None, Lines[CurrentTramLine].Mode == Gmode.Boundary);
                        if (Lines[CurrentTramLine].Mode != Gmode.Boundary)
                            OffsetPoints.AddFirstLastPoint22(mf.maxCrossFieldLength);

                        int BuildStartCnt = Build.Count;
                        if (BoundaryTram.Count > 0)
                            Build.AddRange(OffsetPoints.FixPolyline(Offset, CancellationToken.None, Lines[CurrentTramLine].Mode == Gmode.Boundary, in BoundaryTram[0].Center));
                        else
                            Build.AddRange(OffsetPoints.FixPolyline(Offset, CancellationToken.None, Lines[CurrentTramLine].Mode == Gmode.Boundary, null));

                        for (int s = BuildStartCnt; s < Build.Count; s++)
                        {
                            if (Build[s].Count < 1) return;

                            Build[s].CalculateRoundedCorner(mf.vehicle.minTurningRadius, Lines[CurrentTramLine].Mode == Gmode.Boundary, 0.0436332, CancellationToken.None);
                        }
                    }

                    Vec2 Point;
                    for (int j = 0; j < Build.Count; j++)
                    {
                        if (Build[j].Count > 1)
                        {
                            TramList.Add(new TramPolygon());

                            List<Vec2> Left = new List<Vec2>();
                            List<Vec2> Right = new List<Vec2>();

                            double Dx, Dy, Heading, CosHeading, SinHeading;
                            int k = Build[j].Count - 1;
                            for (int l = 0; l < Build[j].Count; k = l++)
                            {
                                if (l == 0 && Lines[CurrentTramLine].Mode != Gmode.Boundary)
                                {
                                    Dx = Build[j][l + 1].Northing - Build[j][l].Northing;
                                    Dy = Build[j][l + 1].Easting - Build[j][l].Easting;
                                }
                                else
                                {
                                    Dx = Build[j][l].Northing - Build[j][k].Northing;
                                    Dy = Build[j][l].Easting - Build[j][k].Easting;
                                }
                                if (Math.Abs(Dy) < double.Epsilon && Math.Abs(Dx) < double.Epsilon) continue;

                                Heading = Math.Atan2(Dy, Dx);
                                CosHeading = Math.Cos(Heading);
                                SinHeading = Math.Sin(Heading);



                                Point = Build[j][l];
                                Point.Northing += SinHeading * (-TramHalfWheelTrack + TramWheelWidth / 2);
                                Point.Easting += CosHeading * (TramHalfWheelTrack - TramWheelWidth / 2);
                                Left.Add(new Vec2(Point.Easting, Point.Northing));
                                Point.Northing += SinHeading * -TramWheelWidth;
                                Point.Easting += CosHeading * TramWheelWidth;
                                Left.Add(new Vec2(Point.Easting, Point.Northing));

                                Point = Build[j][l];
                                Point.Northing += SinHeading * (TramHalfWheelTrack + TramWheelWidth / 2);
                                Point.Easting += CosHeading * (-TramHalfWheelTrack - TramWheelWidth / 2);
                                Right.Add(new Vec2(Point.Easting, Point.Northing));
                                Point.Northing += SinHeading * -TramWheelWidth;
                                Point.Easting += CosHeading * TramWheelWidth;
                                Right.Add(new Vec2(Point.Easting, Point.Northing));
                            }

                            if (Lines[CurrentTramLine].Mode == Gmode.Boundary && Left.Count > 2)
                            {
                                Left.Add(Left[0]);
                                Left.Add(Left[1]);
                            }
                            if (Lines[CurrentTramLine].Mode == Gmode.Boundary && Right.Count > 2)
                            {
                                Right.Add(Right[0]);
                                Right.Add(Right[1]);
                            }

                            if (Left.Count > 1)
                            {
                                if (TramList[TramList.Count - 1].BufferPointsLeft == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsLeft);
                                GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsLeft);
                                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Left.Count * 16), Left.ToArray(), BufferUsageHint.DynamicDraw);

                                TramList[TramList.Count - 1].BufferPointsCntLeft = Left.Count;
                            }
                            if (Right.Count > 1)
                            {
                                if (TramList[TramList.Count - 1].BufferPointsRight == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsRight);
                                GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsRight);
                                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Right.Count * 16), Right.ToArray(), BufferUsageHint.DynamicDraw);

                                TramList[TramList.Count - 1].BufferPointsCntRight = Right.Count;
                            }
                        }
                    }
                }
            }
        }

        public void BuildTram2()
        {
            double hsin = Math.Sin(Lines[CurrentTramLine].Heading);
            double hcos = Math.Cos(Lines[CurrentTramLine].Heading);

            int tramcount = BoundaryTram.Count;

            for (double i = 0.5; i < TramPasses; i++)
            {
                double Offset = (TramWidth * i) - WidthMinusOverlap / 2 - TramHalfWheelTrack + TramOffset;

                Vec2 pos1A = new Vec2(Lines[CurrentTramLine].Segments[0]);
                pos1A.Northing += hcos * mf.maxCrossFieldLength + hsin * -Offset;
                pos1A.Easting += hsin * mf.maxCrossFieldLength + hcos * Offset;

                Vec2 pos1B = new Vec2(Lines[CurrentTramLine].Segments[1]);
                pos1B.Northing += hcos * -mf.maxCrossFieldLength + hsin * -Offset;
                pos1B.Easting += hsin * -mf.maxCrossFieldLength + hcos * Offset;

                Vec2 pos2A = pos1A;
                pos2A.Northing += hsin * -TramWheelTrack;
                pos2A.Easting += hcos * TramWheelTrack;

                Vec2 pos2B = pos1B;
                pos2B.Northing += hsin * -TramWheelTrack;
                pos2B.Easting += hcos * TramWheelTrack;


                if (mf.bnd.Boundaries.Count > 0 && BoundaryTram[0].Center.Count > 2)
                {
                    List<Vec4> Crossings1 = new List<Vec4>();
                    Vec2 crossing = new Vec2();

                    for (int m = 0; m < tramcount; m++)
                    {
                        Crossings1.FindCrossingPoints(BoundaryTram[m].Center, pos1A, pos1B, 0);
                    }

                    if (Crossings1.Count > 1)
                    {
                        List<Vec4> Crossings2 = new List<Vec4>();
                        for (int m = 0; m < tramcount; m++)
                        {
                            Crossings2.FindCrossingPoints(BoundaryTram[m].Center, pos2A, pos2B, 0);
                        }

                        if (Crossings2.Count > 1)
                        {
                            Crossings1.Sort((x, y) => x.Time.CompareTo(y.Time));
                            Crossings2.Sort((x, y) => x.Time.CompareTo(y.Time));

                            if (Crossings1.Count - 1 > Crossings2.Count)
                            {
                                for (int j = 0; j + 1 < Crossings2.Count; j += 2)
                                {
                                    for (int l = j + 1; l + 1 < Crossings1.Count; l += 2)
                                    {
                                        if (Crossings2[j].Time < Crossings1[l].Time && Crossings1[l + 1].Time < Crossings2[j + 1].Time)
                                        {
                                            crossing = new Vec2(Crossings1[l + 1].Easting, Crossings1[l + 1].Northing);
                                            //crossing.Northing += hsin * TramWheelTrack;
                                            //crossing.Easting += hcos * TramWheelTrack;
                                            Crossings2.Insert(j + 1, new Vec4(crossing.Easting, crossing.Northing, 0, Crossings1[l].Time, 0));
                                            crossing = new Vec2(Crossings1[l].Easting, Crossings1[l].Northing);
                                            //crossing.Northing += hsin * TramWheelTrack;
                                            //crossing.Easting += hcos * TramWheelTrack;
                                            Crossings2.Insert(j + 1, new Vec4(crossing.Easting, crossing.Northing, 0, Crossings1[l].Time, 0));
                                        }
                                        else if (Crossings2[j + 1].Time < Crossings1[j].Time && Crossings2[j + 1].Time < Crossings1[j + 1].Time)
                                        {
                                            Crossings1.RemoveAt(j);
                                            Crossings1.RemoveAt(j);
                                        }
                                        else if (Crossings2[j + 1].Time > Crossings1[j].Time && Crossings2[j + 1].Time > Crossings1[j + 1].Time)
                                        {
                                            Crossings1.RemoveAt(j);
                                            Crossings1.RemoveAt(j);
                                        }

                                    }
                                }
                                Crossings2.Sort((x, y) => x.Time.CompareTo(y.Time));
                            }
                            else if (Crossings2.Count - 1 > Crossings1.Count)
                            {
                                for (int j = 0; j + 1 < Crossings1.Count; j += 2)
                                {
                                    for (int l = j + 1; l + 1 < Crossings2.Count; l += 2)
                                    {
                                        if (Crossings1[j].Time < Crossings2[l].Time && Crossings2[l + 1].Time < Crossings1[j + 1].Time)
                                        {
                                            crossing = new Vec2(Crossings2[l + 1].Easting, Crossings2[l + 1].Northing);
                                            //crossing.Northing += hsin * TramWheelTrack;
                                            //crossing.Easting += hcos * -TramWheelTrack;
                                            Crossings1.Insert(j + 1, new Vec4(crossing.Easting, crossing.Northing, 0, Crossings2[l].Time, 0));
                                            crossing = new Vec2(Crossings2[l].Easting, Crossings2[l].Northing);
                                            //crossing.Northing += hsin * TramWheelTrack;
                                            //crossing.Easting += hcos * -TramWheelTrack;
                                            Crossings1.Insert(j + 1, new Vec4(crossing.Easting, crossing.Northing, 0, Crossings2[l].Time, 0));

                                        }
                                        else if (Crossings1[j + 1].Time < Crossings2[j].Time && Crossings1[j + 1].Time < Crossings2[j + 1].Time)
                                        {
                                            Crossings2.RemoveAt(j);
                                            Crossings2.RemoveAt(j);
                                        }
                                        else if (Crossings1[j + 1].Time > Crossings2[j].Time && Crossings1[j + 1].Time > Crossings2[j + 1].Time)
                                        {
                                            Crossings2.RemoveAt(j);
                                            Crossings2.RemoveAt(j);
                                        }
                                    }
                                }
                                Crossings1.Sort((x, y) => x.Time.CompareTo(y.Time));
                            }
                            for (int j = 0; j + 1 < Crossings1.Count; j += 2)
                            {
                                if (j + 1 < Crossings2.Count)
                                {


                                    TramList.Add(new TramPolygon());
                                    List<Vec2> Left = new List<Vec2>();
                                    List<Vec2> Right = new List<Vec2>();

                                    //left of left tram
                                    crossing = new Vec2(Crossings1[j].Easting, Crossings1[j].Northing);
                                    crossing.Northing += hsin * -(TramWheelWidth / 2);//+ hcos * -TramHalfWheelTrack;
                                    crossing.Easting += hcos * (TramWheelWidth / 2);//+ hsin * -TramHalfWheelTrack;
                                    Left.Add(crossing);

                                    //right of left tram
                                    crossing.Northing += hsin * TramWheelWidth;
                                    crossing.Easting += hcos * -TramWheelWidth;
                                    Left.Add(crossing);

                                    //left of left tram
                                    crossing = new Vec2(Crossings1[j + 1].Easting, Crossings1[j + 1].Northing);
                                    crossing.Northing += hsin * -(TramWheelWidth / 2);//+ hcos * -TramHalfWheelTrack;
                                    crossing.Easting += hcos * (TramWheelWidth / 2);//+ hsin * -TramHalfWheelTrack;
                                    Left.Add(crossing);
                                    //right of left tram
                                    crossing.Northing += hsin * TramWheelWidth;
                                    crossing.Easting += hcos * -TramWheelWidth;
                                    Left.Add(crossing);


                                    //left of right tram
                                    crossing = new Vec2(Crossings2[j].Easting, Crossings2[j].Northing);
                                    crossing.Northing += hsin * -(TramWheelWidth / 2);//+ hcos * -TramHalfWheelTrack;
                                    crossing.Easting += hcos * (TramWheelWidth / 2);//+ hsin * -TramHalfWheelTrack;
                                    Right.Add(crossing);
                                    //right of right tram
                                    crossing.Northing += hsin * TramWheelWidth;
                                    crossing.Easting += hcos * -TramWheelWidth;
                                    Right.Add(crossing);



                                    //left of right tram
                                    crossing = new Vec2(Crossings2[j + 1].Easting, Crossings2[j + 1].Northing);
                                    crossing.Northing += hsin * -(TramWheelWidth / 2);//+ hcos * -TramHalfWheelTrack;
                                    crossing.Easting += hcos * (TramWheelWidth / 2);//+ hsin * -TramHalfWheelTrack;
                                    Right.Add(crossing);
                                    //right of right tram
                                    crossing.Northing += hsin * TramWheelWidth;
                                    crossing.Easting += hcos * -TramWheelWidth;
                                    Right.Add(crossing);

                                    if (Left.Count > 1)
                                    {
                                        if (TramList[TramList.Count - 1].BufferPointsLeft == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsLeft);
                                        GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsLeft);
                                        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Left.Count * 16), Left.ToArray(), BufferUsageHint.DynamicDraw);

                                        TramList[TramList.Count - 1].BufferPointsCntLeft = Left.Count;
                                    }
                                    if (Right.Count > 1)
                                    {
                                        if (TramList[TramList.Count - 1].BufferPointsRight == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsRight);
                                        GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsRight);
                                        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Right.Count * 16), Right.ToArray(), BufferUsageHint.DynamicDraw);

                                        TramList[TramList.Count - 1].BufferPointsCntRight = Right.Count;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    TramList.Add(new TramPolygon());

                    List<Vec2> Left = new List<Vec2>();
                    List<Vec2> Right = new List<Vec2>();
                    //left of left tram
                    pos1A.Northing += hcos * -TramHalfWheelTrack + hsin * (TramWheelWidth / 2);
                    pos1A.Easting += hsin * -TramHalfWheelTrack + hcos * -(TramWheelWidth / 2);
                    Left.Add(pos1A);
                    //right of left tram
                    pos1A.Northing += hsin * -TramWheelWidth;
                    pos1A.Easting += hcos * TramWheelWidth;
                    Left.Add(pos1A);

                    //left of left tram
                    pos1B.Northing += hcos * TramHalfWheelTrack + hsin * (TramWheelWidth / 2);
                    pos1B.Easting += hsin * TramHalfWheelTrack + hcos * -(TramWheelWidth / 2);
                    Left.Add(pos1B);
                    //right of left tram
                    pos1B.Northing += hsin * -TramWheelWidth;
                    pos1B.Easting += hcos * TramWheelWidth;
                    Left.Add(pos1B);


                    //right of right tram
                    pos2A.Northing += hcos * -TramHalfWheelTrack + hsin * -(TramWheelWidth / 2);
                    pos2A.Easting += hsin * -TramHalfWheelTrack + hcos * (TramWheelWidth / 2);
                    Right.Add(pos2A);
                    //left of right tram
                    pos2A.Northing += hsin * TramWheelWidth;
                    pos2A.Easting += hcos * -TramWheelWidth;
                    Right.Add(pos2A);

                    //right of right tram
                    pos2B.Northing += hcos * TramHalfWheelTrack + hsin * -(TramWheelWidth / 2);
                    pos2B.Easting += hsin * TramHalfWheelTrack + hcos * (TramWheelWidth / 2);
                    Right.Add(pos2B);
                    //left of right tram
                    pos2B.Northing += hsin * TramWheelWidth;
                    pos2B.Easting += hcos * -TramWheelWidth;
                    Right.Add(pos2B);

                    if (Left.Count > 1)
                    {
                        if (TramList[TramList.Count - 1].BufferPointsLeft == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsLeft);
                        GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsLeft);
                        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Left.Count * 16), Left.ToArray(), BufferUsageHint.DynamicDraw);

                        TramList[TramList.Count - 1].BufferPointsCntLeft = Left.Count;
                    }
                    if (Right.Count > 1)
                    {
                        if (TramList[TramList.Count - 1].BufferPointsRight == int.MinValue) GL.GenBuffers(1, out TramList[TramList.Count - 1].BufferPointsRight);
                        GL.BindBuffer(BufferTarget.ArrayBuffer, TramList[TramList.Count - 1].BufferPointsRight);
                        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Right.Count * 16), Right.ToArray(), BufferUsageHint.DynamicDraw);

                        TramList[TramList.Count - 1].BufferPointsCntRight = Right.Count;
                    }
                }
            }
        }
    }
}
