using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public static class DanielP
    {
        public static bool GetLineIntersection(Vec2 PointAA, Vec2 PointAB, Vec2 PointBA, Vec2 PointBB, out Vec2 Crossing, out double TimeA, bool Limit = false)
        {
            TimeA = -1;
            Crossing = new Vec2();
            double denominator = (PointAB.Northing - PointAA.Northing) * (PointBB.Easting - PointBA.Easting) - (PointBB.Northing - PointBA.Northing) * (PointAB.Easting - PointAA.Easting);

            if (denominator != 0.0)
            {
                TimeA = ((PointBB.Northing - PointBA.Northing) * (PointAA.Easting - PointBA.Easting) - (PointAA.Northing - PointBA.Northing) * (PointBB.Easting - PointBA.Easting)) / denominator;

                if (Limit || (TimeA > 0.0 && TimeA < 1.0))
                {
                    double TimeB = ((PointAB.Northing - PointAA.Northing) * (PointAA.Easting - PointBA.Easting) - (PointAA.Northing - PointBA.Northing) * (PointAB.Easting - PointAA.Easting)) / denominator;
                    if (Limit || (TimeB > 0.0 && TimeB < 1.0))
                    {
                        Crossing = PointAA + (PointAB - PointAA) * TimeA;
                        return true;
                    }
                    else return false;
                }
                else return false;
            }
            else return false;
        }

        public static double PolygonArea(this List<Vec2> tt, CancellationToken ct, bool ForceCW = false)
        {
            double Area = 0;
            int j = tt.Count - 1;
            for (int i = 0; i < tt.Count; j = i++)
            {
                if (ct.IsCancellationRequested) break;
                Area += (tt[i].Northing - tt[j].Northing) * (tt[i].Easting + tt[j].Easting);
            }
            if (ForceCW && Area > 0)
            {
                tt.Reverse();//force Clockwise rotation
            }
            return Area;
        }

        public class VertexPoint
        {
            public int Idx;
            public Vec2 Coords;
            public VertexPoint Next;
            public VertexPoint Prev;
            public VertexPoint Crossing;
            //ClockWise or Crossing;
            public bool Data = false;

            public VertexPoint(Vec2 coords, bool intersection = false, int idx = 0)
            {
                Coords = coords;
                Data = intersection;
                Idx = idx;
            }
        }

        public static List<List<Vec2>> ClipPolyLine(this List<Vec2> Points, List<Vec2> clipPoints, bool Loop, CancellationToken ct)
        {
            List<List<Vec2>> FinalPolyLine = new List<List<Vec2>>();
            List<VertexPoint> PolyLine = PolyLineStructure(Points);

            List<VertexPoint> Crossings = new List<VertexPoint>();
            List<VertexPoint> Polygons = new List<VertexPoint>();
            if (PolyLine.Count < 3) return FinalPolyLine;
            VertexPoint CurrentVertex = PolyLine[0];
            VertexPoint StopVertex;
            if (Loop) StopVertex = CurrentVertex;
            else StopVertex = CurrentVertex.Prev;

            int IntersectionCount = 0;
            int safety = 0;
            bool start = true;
            while (true)
            {
                if (ct.IsCancellationRequested) break;
                if (!start && CurrentVertex == StopVertex) break;
                start = false;

                VertexPoint SecondVertex = CurrentVertex.Next;

                int sectcnt = 0;
                int safety2 = 0;
                bool start2 = true;
                while (true)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!start2 && SecondVertex == StopVertex) break;
                    start2 = false;

                    if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, SecondVertex.Coords, SecondVertex.Next.Coords, out Vec2 intersectionPoint2D, out _))
                    {
                        sectcnt++;
                        IntersectionCount++;

                        VertexPoint AA = InsertCrossing(intersectionPoint2D, CurrentVertex);
                        VertexPoint BB = InsertCrossing(intersectionPoint2D, SecondVertex);

                        AA.Crossing = BB;
                        BB.Crossing = AA;
                    }
                    SecondVertex = SecondVertex.Next;

                    if (safety2++ > PolyLine.Count * 1.2) break;
                }
                for (int i = 0; i <= sectcnt; i++) CurrentVertex = CurrentVertex.Next;

                if (safety++ > PolyLine.Count * 1.2) break;
            }
            if (IntersectionCount > 0)
            {
                CurrentVertex = PolyLine[0];
                StopVertex = CurrentVertex;

                bool Searching = true;
                start = true;
                safety = 0;

                while (Crossings.Count > 0 || Searching)
                {
                    if (ct.IsCancellationRequested) break;
                    if (Crossings.Count > 0)
                    {
                        start = true;
                        CurrentVertex = Crossings[0];
                        StopVertex = CurrentVertex;
                        Crossings.RemoveAt(0);
                    }

                    while (true)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!start && CurrentVertex == StopVertex)
                        {
                            Polygons.Add(CurrentVertex);
                            Searching = false;
                            break;
                        }

                        start = false;
                        if (CurrentVertex.Data)
                        {
                            if (Loop) Crossings.Add(CurrentVertex.Next);
                            safety = 0;
                            VertexPoint CC = CurrentVertex.Crossing.Next;
                            CurrentVertex.Crossing.Next = CurrentVertex.Next;
                            CurrentVertex.Next.Prev = CurrentVertex.Crossing;
                            CurrentVertex.Crossing.Data = false;
                            CurrentVertex.Crossing.Crossing = null;
                            CurrentVertex.Next = CC;
                            CurrentVertex.Next.Prev = CurrentVertex;
                            CurrentVertex.Data = false;
                            CurrentVertex.Crossing = null;
                        }
                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count * 1.2) break;
                    }
                }
            }
            else Polygons.Add(PolyLine[0]);

            if (!Loop)
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    CurrentVertex = Polygons[i];
                    StopVertex = CurrentVertex.Prev;
                    bool isInside;
                    if (clipPoints != null && clipPoints.Count > 2)
                        isInside = clipPoints.PointInPolygon(CurrentVertex.Coords);
                    else
                        isInside = true;

                    if (isInside) FinalPolyLine.Add(new List<Vec2>());

                    safety = 0;
                    start = true;
                    while (true)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!start && CurrentVertex == StopVertex) break;
                        start = false;

                        if (isInside)
                        {
                            FinalPolyLine[FinalPolyLine.Count - 1].Add(CurrentVertex.Coords);
                        }

                        if (clipPoints != null && clipPoints.Count > 2)
                        {
                            int j = clipPoints.Count - 1;
                            for (int k = 0; k < clipPoints.Count; j = k++)
                            {
                                if (ct.IsCancellationRequested) break;

                                if (GetLineIntersection(CurrentVertex.Coords, CurrentVertex.Next.Coords, clipPoints[j], clipPoints[k], out Vec2 Crossing, out _))
                                {
                                    if (isInside && FinalPolyLine.Count > 0) FinalPolyLine[FinalPolyLine.Count - 1].Add(Crossing);
                                    if (isInside = !isInside)
                                    {
                                        FinalPolyLine.Add(new List<Vec2>());
                                        FinalPolyLine[FinalPolyLine.Count - 1].Add(Crossing);
                                    }
                                    break;
                                }
                            }
                        }

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count * 1.2) break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Polygons.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    FinalPolyLine.Add(new List<Vec2>());

                    start = true;
                    CurrentVertex = Polygons[i];

                    if (Loop) StopVertex = CurrentVertex;
                    else StopVertex = CurrentVertex.Prev;
                    safety = 0;
                    while (true)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!start && CurrentVertex == StopVertex)
                            break;
                        start = false;

                        FinalPolyLine[i].Add(CurrentVertex.Coords);

                        CurrentVertex = CurrentVertex.Next;
                        if (safety++ > PolyLine.Count) break;
                    }
                }
            }


            //Experimental!
            if (Loop)
            {
                List<Vec2> test = new List<Vec2>();

                for (int i = 0; i < FinalPolyLine.Count; i++)
                {
                    test.Add(new Vec2(-1000, i));
                    bool inside;
                    for (int j = 2; j < FinalPolyLine[i].Count; j++)
                    {
                        if (!IsTriangleOrientedClockwise(FinalPolyLine[i][j - 2], FinalPolyLine[i][j - 1], FinalPolyLine[i][j]))
                        {
                            inside = false;

                            for (int k = 0; k < FinalPolyLine.Count; k++)
                            {
                                for (int l = 0; l < FinalPolyLine[k].Count; l++)
                                {
                                    if (IsPointInTriangle(FinalPolyLine[i][j - 2], FinalPolyLine[i][j - 1], FinalPolyLine[i][j], FinalPolyLine[k][l]))
                                    {
                                        inside = true;
                                        break;
                                    }
                                }
                                if (inside) break;
                            }
                            if (!inside)
                            {
                                int winding_number = 0;

                                double a = (FinalPolyLine[i][j - 2].Northing + FinalPolyLine[i][j - 1].Northing + FinalPolyLine[i][j].Northing) / 3.0;
                                double b = (FinalPolyLine[i][j - 2].Easting + FinalPolyLine[i][j - 1].Easting + FinalPolyLine[i][j].Easting) / 3.0;

                                Vec3 test3 = new Vec3(b, a, 0);

                                for (int k = 0; k < FinalPolyLine.Count; k++)
                                {
                                    int l = FinalPolyLine[k].Count - 1;
                                    for (int m = 0; m < FinalPolyLine[k].Count; l = m++)
                                    {
                                        if (FinalPolyLine[k][l].Easting <= test3.Easting && FinalPolyLine[k][m].Easting > test3.Easting)
                                        {
                                            if ((FinalPolyLine[k][m].Northing - FinalPolyLine[k][l].Northing) * (test3.Easting - FinalPolyLine[k][l].Easting) -
                                            (test3.Northing - FinalPolyLine[k][l].Northing) * (FinalPolyLine[k][m].Easting - FinalPolyLine[k][l].Easting) > 0)
                                            {
                                                ++winding_number;
                                            }
                                        }
                                        else
                                        {
                                            if (FinalPolyLine[k][l].Easting > test3.Easting && FinalPolyLine[k][m].Easting <= test3.Easting)
                                            {
                                                if ((FinalPolyLine[k][m].Northing - FinalPolyLine[k][l].Northing) * (test3.Easting - FinalPolyLine[k][l].Easting) -
                                                (test3.Northing - FinalPolyLine[k][l].Northing) * (FinalPolyLine[k][m].Easting - FinalPolyLine[k][l].Easting) < 0)
                                                {
                                                    --winding_number;
                                                }
                                            }
                                        }
                                    }
                                }
                                test[i] = new Vec2(winding_number, i);
                                break;
                            }
                        }
                    }
                }

                for (int i = 0; i < FinalPolyLine.Count; i++)
                {
                    if ((int)test[i].Easting < 1)
                    {
                        test.RemoveAt(i);
                        FinalPolyLine.RemoveAt(i);
                        i--;
                    }
                }
            }

            return FinalPolyLine;
        }

        public static List<VertexPoint> PolyLineStructure(List<Vec2> polyLine)
        {
            List<VertexPoint> PolyLine = new List<VertexPoint>();

            for (int i = 0; i < polyLine.Count; i++)
            {
                PolyLine.Add(new VertexPoint(polyLine[i], false, i));
            }

            for (int i = 0; i < PolyLine.Count; i++)
            {
                int Next = (i + 1).Clamp(PolyLine.Count);
                int Prev = (i - 1).Clamp(PolyLine.Count);

                PolyLine[i].Next = PolyLine[Next];
                PolyLine[i].Prev = PolyLine[Prev];
            }

            return PolyLine;
        }

        public static VertexPoint InsertCrossing(Vec2 intersectionPoint, VertexPoint currentVertex)
        {
            VertexPoint IntersectionCrossing = new VertexPoint(intersectionPoint, true)
            {
                Next = currentVertex.Next,
                Prev = currentVertex
            };
            currentVertex.Next.Prev = IntersectionCrossing;
            currentVertex.Next = IntersectionCrossing;
            return IntersectionCrossing;
        }

        public static bool PointInPolygon(this List<Vec2> Polygon, Vec2 pointAA)
        {
            Vec2 PointAB = new Vec2(0.0, 200000.0);

            int NumCrossings = 0;

            for (int i = 0; i < Polygon.Count; i++)
            {
                Vec2 PointBB = Polygon[(i + 1).Clamp(Polygon.Count)];

                if (GetLineIntersection(pointAA, PointAB, Polygon[i], PointBB, out _, out _))
                    NumCrossings += 1;
            }
            return NumCrossings % 2 == 1;
        }

        public static double LimitToRange(this double Value, double Min, double Max)
        {
            if (Value < Min) Value = Min;
            else if (Value > Max) Value = Max;
            return Value;
        }

        public static int Clamp(this int Idx, int Size)
        {
            return (Size + Idx) % Size;
        }

        public static double Heading(this Vec2 Point1, Vec2 Point2)
        {
            double Dx = Point2.Northing - Point1.Northing;
            double Dy = Point2.Easting - Point1.Easting;
            return Math.Atan2(Dy, Dx);
        }

        public static void AddFirstLastPoint22(this List<Vec2> OffsetPoints, double Length)
        {
            if (OffsetPoints.Count > 2)
            {
                double Heading = OffsetPoints[0].Heading(OffsetPoints[1]);
                double cosHeading = Math.Cos(Heading);
                double sinHeading = Math.Sin(Heading);
                OffsetPoints.Insert(0, new Vec2(OffsetPoints[0].Easting + sinHeading * -Length, OffsetPoints[0].Northing + cosHeading * -Length));

                Heading = OffsetPoints[OffsetPoints.Count - 2].Heading(OffsetPoints[OffsetPoints.Count - 1]);
                cosHeading = Math.Cos(Heading);
                sinHeading = Math.Sin(Heading);
                OffsetPoints.Add(new Vec2(OffsetPoints[OffsetPoints.Count - 1].Easting + sinHeading * Length, OffsetPoints[OffsetPoints.Count - 1].Northing + cosHeading * Length));
            }
        }

        public static double FindDistanceToSegment(this Vec3 pt, Vec2 p1, Vec2 p2)
        {
            double dx = p2.Northing - p1.Northing;
            double dy = p2.Easting - p1.Easting;
            if ((dx == 0) && (dy == 0))
            {
                dx = pt.Northing - p1.Northing;
                dy = pt.Easting - p1.Easting;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double Time = ((pt.Northing - p1.Northing) * dx + (pt.Easting - p1.Easting) * dy) / (dx * dx + dy * dy);

            if (Time < 0)
            {
                dx = pt.Northing - p1.Northing;
                dy = pt.Easting - p1.Easting;
            }
            else if (Time > 1)
            {
                dx = pt.Northing - p2.Northing;
                dy = pt.Easting - p2.Easting;
            }
            else
            {
                dx = pt.Northing - (p1.Northing + Time * dx);
                dy = pt.Easting - (p1.Easting + Time * dy);
            }
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double FindDistanceToSegment(this Vec2 pt, Vec2 p1, Vec2 p2)
        {
            double dx = p2.Northing - p1.Northing;
            double dy = p2.Easting - p1.Easting;
            if ((dx == 0) && (dy == 0))
            {
                dx = pt.Northing - p1.Northing;
                dy = pt.Easting - p1.Easting;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double Time = ((pt.Northing - p1.Northing) * dx + (pt.Easting - p1.Easting) * dy) / (dx * dx + dy * dy);

            if (Time < 0)
            {
                dx = pt.Northing - p1.Northing;
                dy = pt.Easting - p1.Easting;
            }
            else if (Time > 1)
            {
                dx = pt.Northing - p2.Northing;
                dy = pt.Easting - p2.Easting;
            }
            else
            {
                dx = pt.Northing - (p1.Northing + Time * dx);
                dy = pt.Easting - (p1.Easting + Time * dy);
            }
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static bool CheckValue(this TextBox Tbox, ref double value, double Minimum, double Maximum)
        {
            if (value < Minimum)
            {
                value = Minimum;
                Tbox.BackColor = System.Drawing.Color.OrangeRed;

                MessageBox.Show("Serious Settings Problem with - " + Tbox.Name
                    + " \n\rMinimum has been exceeded\n\rDouble check ALL your Settings and \n\rFix it and Resave Vehicle File",
                "Critical Settings Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return true;
            }
            else if (value > Maximum)
            {
                value = Maximum;
                Tbox.BackColor = System.Drawing.Color.OrangeRed;
                MessageBox.Show("Serious Settings Problem with - " + Tbox.Name
                    + " \n\rMaximum has been exceeded\n\rDouble check ALL your Settings and \n\rFix it and Resave Vehicle File",
                "Critical Settings Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return true;
            }

            //value is ok
            return false;
        }

        public static bool CheckValue(this TextBox Tbox, ref int value, int Minimum, int Maximum)
        {
            if (value < Minimum)
            {
                value = Minimum;
                Tbox.BackColor = System.Drawing.Color.OrangeRed;

                MessageBox.Show("Serious Settings Problem with - " + Tbox.Name
                    + " \n\rMinimum has been exceeded\n\rDouble check ALL your Settings and \n\rFix it and Resave Vehicle File",
                "Critical Settings Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return true;
            }
            else if (value > Maximum)
            {
                value = Maximum;
                Tbox.BackColor = System.Drawing.Color.OrangeRed;
                MessageBox.Show("Serious Settings Problem with - " + Tbox.Name
                    + " \n\rMaximum has been exceeded\n\rDouble check ALL your Settings and \n\rFix it and Resave Vehicle File",
                "Critical Settings Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return true;
            }

            //value is ok
            return false;
        }

        public static List<Vec2> OffsetPolyline(this List<Vec2> Points, double Distance, CancellationToken ct, bool Loop, int Start = -1, int End = -1)
        {
            List<Vec2> OffsetPoints = new List<Vec2>();
            int A, C;
            double Dx, Dy, Dx1, Dy1, Dx2, Dy2, Length, Northing, Easting;
            for (int i = 0; i < Points.Count; i++)
            {
                if (Start == -1 || (Start > End && (i >= Start || i <= End)) || (Start < End && i >= Start && i <= End))
                {
                    if (ct.IsCancellationRequested) break;
                    A = (i == 0) ? Points.Count - 1 : i - 1;
                    C = (i + 1 == Points.Count) ? 0 : i + 1;

                    if (!Loop && i == 0)
                    {
                        Dx = Points[C].Northing - Points[i].Northing;
                        Dy = Points[C].Easting - Points[i].Easting;
                        Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                        Northing = Distance * (-Dy / Length);
                        Easting = Distance * (Dx / Length);
                        OffsetPoints.Add(new Vec2(Points[i].Easting + Easting, Points[i].Northing + Northing));
                    }
                    else if (!Loop && i == Points.Count - 1)
                    {
                        Dx = Points[i].Northing - Points[A].Northing;
                        Dy = Points[i].Easting - Points[A].Easting;
                        Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                        Northing = Distance * (-Dy / Length);
                        Easting = Distance * (Dx / Length);
                        OffsetPoints.Add(new Vec2(Points[i].Easting + Easting, Points[i].Northing + Northing));
                    }
                    else
                    {
                        Dx1 = Points[i].Northing - Points[A].Northing;
                        Dy1 = Points[i].Easting - Points[A].Easting;
                        Dx2 = Points[i].Northing - Points[C].Northing;
                        Dy2 = Points[i].Easting - Points[C].Easting;
                        double angle = Math.Atan2(Dy1, Dx1) - Math.Atan2(Dy2, Dx2);

                        if (angle < 0) angle += Glm.twoPI;
                        if (angle > Glm.twoPI) angle -= Glm.twoPI;
                        angle /= 2;

                        double tan = Math.Abs(Math.Tan(angle));

                        double segment = Distance / tan;
                        var p1Cross = Points[i].GetProportionPoint(segment, GetLength(Dx1, Dy1), Dx1, Dy1);
                        var p2Cross = Points[i].GetProportionPoint(segment, GetLength(Dx2, Dy2), Dx2, Dy2);

                        Dx = Points[i].Northing * 2 - p1Cross.Northing - p2Cross.Northing;
                        Dy = Points[i].Easting * 2 - p1Cross.Easting - p2Cross.Easting;

                        if (Dx1 == 0 && Dy1 == 0 || Dx2 == 0 && Dy2 == 0 || Dx == 0 && Dy == 0) continue;

                        double L = GetLength(Dx, Dy);
                        double d = GetLength(segment, Distance);

                        Vec2 circlePoint = Points[i].GetProportionPoint(Math.Abs(angle) > Glm.PIBy2 ? -d : d, L, Dx, Dy);
                        OffsetPoints.Add(new Vec2(circlePoint.Easting, circlePoint.Northing));
                    }
                }
                else if (i == Start || i == End)//folow line
                {
                    int num = Start < End ? -1 : 1;
                    if (i == End) num *= -1;
                    if (Start > End) num *= -1;

                    int j = (i - num).Clamp(Points.Count);

                    Dx = Points[num > 0 ? i : j].Northing - Points[num > 0 ? j : i].Northing;
                    Dy = Points[num > 0 ? i : j].Easting - Points[num > 0 ? j : i].Easting;

                    Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                    Vec2 Point = new Vec2(Points[i].Easting + Distance * (Dx / Length), Points[i].Northing + Distance * (-Dy / Length));
                    Vec2 Point2 = new Vec2(Points[j].Easting + Distance * (Dx / Length), Points[j].Northing + Distance * (-Dy / Length));

                    if (GetLineIntersection(Point2, Point, Points[i], Points[(i + num).Clamp(Points.Count)], out Vec2 Crossing, out _))
                        OffsetPoints.Add(Crossing);
                    else
                        OffsetPoints.Add(Point);
                }
                else
                    OffsetPoints.Add(Points[i]);
            }
            return OffsetPoints;
        }

        public static List<List<Vec2>> FixPolyline(this List<Vec2> Points, double Distance, CancellationToken ct, bool Loop, in List<Vec2> Points2, bool Delete = false)
        {
            List<List<Vec2>> OffsetPoints = Points.ClipPolyLine(Loop ? null : Points2, Loop, ct);

            //return OffsetPoints;
            for (int k = 0; k < OffsetPoints.Count; k++)
            {
                double distance;
                int l = OffsetPoints[k].Count - 1;
                for (int m = 0; m < OffsetPoints[k].Count; l = m++)
                {
                    if (m == 0) l = OffsetPoints[k].Count - 1;

                    distance = Glm.Distance(OffsetPoints[k][l], OffsetPoints[k][m]);
                    if (distance > 0.5)
                    {
                        if (Delete)
                        {
                            int n = Points2.Count - 1;
                            for (int o = 0; o < Points2.Count; n = o++)
                            {
                                distance = FindDistanceToSegment(OffsetPoints[k][m], Points2[n], Points2[o]);
                                if (distance < (Distance - 0.001))
                                {
                                    OffsetPoints[k].RemoveAt(m);
                                    m--;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        OffsetPoints[k].RemoveAt(m);
                        m--;
                    }
                }
            }
            return OffsetPoints;
        }

        public static void CalculateCalcListPolygon(this List<Vec2> OffsetPoints, CancellationToken ct, out List<Vec2> CalcList)
        {
            CalcList = new List<Vec2>();
            if (OffsetPoints.Count > 3)
            {
                int j = OffsetPoints.Count - 1;
                Vec2 constantMultiple = new Vec2(0, 0);

                for (int i = 0; i < OffsetPoints.Count; j = i++)
                {
                    if (ct.IsCancellationRequested) break;

                    //check for divide by zero
                    if (Math.Abs(OffsetPoints[i].Northing - OffsetPoints[j].Northing) < double.Epsilon)
                    {
                        constantMultiple.Easting = OffsetPoints[i].Easting;
                        constantMultiple.Northing = 0;
                        CalcList.Add(constantMultiple);
                    }
                    else
                    {
                        //determine constant and multiple and add to list
                        constantMultiple.Easting = OffsetPoints[i].Easting - ((OffsetPoints[i].Northing * OffsetPoints[j].Easting)
                                        / (OffsetPoints[j].Northing - OffsetPoints[i].Northing)) + ((OffsetPoints[i].Northing * OffsetPoints[i].Easting)
                                            / (OffsetPoints[j].Northing - OffsetPoints[i].Northing));
                        constantMultiple.Northing = (OffsetPoints[j].Easting - OffsetPoints[i].Easting) / (OffsetPoints[j].Northing - OffsetPoints[i].Northing);
                        CalcList.Add(constantMultiple);
                    }
                }
            }
        }

        public static void CalculateRoundedCorner(this List<Vec2> Points, double Radius, bool Loop, double MaxAngle, CancellationToken ct)
        {
            double tt = Math.Asin(0.5 / Radius);
            if (!double.IsNaN(tt)) MaxAngle = Math.Min(tt, MaxAngle);

            int A, C, oldA, OldC;
            double radius = Radius;

            for (int B = 0; B < Points.Count; B++)
            {
                if (ct.IsCancellationRequested) break;
                if (!Loop && (B == 0 || B + 1 == Points.Count)) continue;
                A = (B == 0) ? Points.Count - 1 : B - 1;
                C = (B + 1 == Points.Count) ? 0 : B + 1;
                bool stop = false;
                double dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0, angle = 0, segment = 0, length1 = 0, length2 = 0;
                while (true)
                {
                    Vec2 tt3 = Points[B];
                    if (ct.IsCancellationRequested) break;
                    if (GetLineIntersection(Points[A], Points[(A + 1).Clamp(Points.Count)], Points[C], Points[(C - 1).Clamp(Points.Count)], out Vec2 Crossing, out double Time, true))
                    {
                        if (Time > -100 && Time < 100)
                            tt3 = Crossing;
                    }

                    dx1 = tt3.Northing - Points[A].Northing;
                    dy1 = tt3.Easting - Points[A].Easting;
                    dx2 = tt3.Northing - Points[C].Northing;
                    dy2 = tt3.Easting - Points[C].Easting;

                    angle = (Math.Atan2(dy1, dx1) - Math.Atan2(dy2, dx2));

                    if (angle < 0) angle += Glm.twoPI;
                    if (angle > Glm.twoPI) angle -= Glm.twoPI;
                    angle /= 2;

                    if ((Math.Abs(angle) > Glm.PIBy2 - MaxAngle && Math.Abs(angle) < Glm.PIBy2 + MaxAngle) || (Math.Abs(angle) > Math.PI - MaxAngle && Math.Abs(angle) < Math.PI + MaxAngle))
                    {
                        if (C - A > 2)//Check why this is somethimes wrong!
                        {
                            //while (C - 1 > A)
                            {
                                //C = C == 0 ? Points.Count - 1 : C - 1;
                                //Points.RemoveAt(C);
                            }
                        }
                        stop = true;
                        break;
                    }
                    double tan = Math.Abs(Math.Tan(angle));

                    segment = radius / tan;
                    length1 = GetLength(dx1, dy1);
                    length2 = GetLength(dx2, dy2);
                    oldA = A;
                    OldC = C;
                    if (segment > length1)
                    {
                        if (Loop || (!Loop && A > 0)) A = (A == 0) ? Points.Count - 1 : A - 1;

                        if (A == C)
                        {
                            stop = true;
                            break;
                        }
                    }
                    if (segment > length2)
                    {
                        if (Loop || (!Loop && C < Points.Count - 1)) C = (C + 1 == Points.Count) ? 0 : C + 1;
                        if (C == A)
                        {
                            stop = true;
                            break;
                        }
                    }
                    else if (segment < length1)
                    {
                        Points[B] = tt3;
                        break;
                    }

                    if (!Loop && A == 0 && C == Points.Count - 1 || (oldA == A && OldC == C))
                    {
                        stop = true;
                        break;
                    }
                }
                if (ct.IsCancellationRequested) break;
                if (stop) continue;

                var p1Cross = Points[B].GetProportionPoint(segment, length1, dx1, dy1);
                var p2Cross = Points[B].GetProportionPoint(segment, length2, dx2, dy2);

                bool reverse = false;
                if (Math.Abs(angle) > Glm.PIBy2)
                {
                    Vec2 test = p1Cross;
                    p1Cross = p2Cross;
                    p2Cross = test;
                    reverse = true;
                }

                double dx = Points[B].Northing * 2 - p1Cross.Northing - p2Cross.Northing;
                double dy = Points[B].Easting * 2 - p1Cross.Easting - p2Cross.Easting;

                if (dx1 == 0 && dy1 == 0 || dx2 == 0 && dy2 == 0 || dx == 0 && dy == 0) continue;

                Vec2 circlePoint;

                double L = GetLength(dx, dy);
                double d = GetLength(segment, radius);

                circlePoint = Points[B].GetProportionPoint(d, L, dx, dy);

                var startAngle = Math.Atan2(p1Cross.Easting - circlePoint.Easting, p1Cross.Northing - circlePoint.Northing);
                var endAngle = Math.Atan2(p2Cross.Easting - circlePoint.Easting, p2Cross.Northing - circlePoint.Northing);

                if (startAngle < 0) startAngle += Glm.twoPI;
                if (endAngle < 0) endAngle += Glm.twoPI;

                bool Looping = (A > C);
                while (C - 1 > A || Looping)
                {
                    if (ct.IsCancellationRequested) break;
                    if (C == 0)
                    {
                        if (A == Points.Count - 1) break;
                        Looping = false;
                    }

                    C = C == 0 ? Points.Count - 1 : C - 1;

                    if (A > C) A--;

                    Points.RemoveAt(C);
                }

                B = A > B ? -1 : A;

                double sweepAngle;

                if (((Glm.twoPI - endAngle + startAngle) % Glm.twoPI) < ((Glm.twoPI - startAngle + endAngle) % Glm.twoPI))
                    sweepAngle = (Glm.twoPI - endAngle + startAngle) % Glm.twoPI;
                else
                    sweepAngle = (Glm.twoPI - startAngle + endAngle) % Glm.twoPI;

                int sign = Math.Sign(sweepAngle);

                if (reverse)
                {
                    sign = -sign;
                    startAngle = endAngle;
                }

                int pointsCount = (int)Math.Round(Math.Abs(sweepAngle / MaxAngle));

                double degreeFactor = sweepAngle / pointsCount;

                Vec2[] points = new Vec2[pointsCount];

                for (int j = 0; j < pointsCount; ++j)
                {
                    if (ct.IsCancellationRequested) break;
                    var pointX = circlePoint.Northing + Math.Cos(startAngle + sign * (j + 1) * degreeFactor) * radius;
                    var pointY = circlePoint.Easting + Math.Sin(startAngle + sign * (j + 1) * degreeFactor) * radius;
                    points[j] = new Vec2(pointY, pointX);
                }
                Points.InsertRange(B + 1, points);

                B += points.Length;
            }
        }

        public static void FindCrossingPoints(this List<Vec4> Crossings, List<Vec2> Polygon, Vec2 Point1, Vec2 Point2, int Index)
        {
            if (Polygon.Count > 2)
            {
                int k = Polygon.Count - 2;
                for (int j = 0; j < Polygon.Count - 2; j +=2)
                {
                    if (GetLineIntersection(Point1, Point2, Polygon[j], Polygon[k], out Vec2 Crossing, out double Time))
                    {
                        int tt = (k == Polygon.Count - 1) ? -1 : k;
                        Crossings.Add(new Vec4(Crossing.Easting, Crossing.Northing, (j + tt) / 2.0, Time, Index));
                    }
                    k = j;
                }
            }
        }

        public static double GetLength(double dx, double dy)
        {
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vec2 GetProportionPoint(this Vec2 point, double segment, double length, double dx, double dy)
        {
            double factor = segment / length;
            return new Vec2(point.Easting - dy * factor, point.Northing - dx * factor);
        }

        public static List<int> TriangulatePolygon(this List<Vec2> Points, CancellationToken ct)
        {
            List<int> Indexer = new List<int>();

            if (Points.Count < 3) return Indexer;

            List<VertexPoint> Vertices = PolyLineStructure(Points);

            for (int i = 0; i < Vertices.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                CheckClockwise(Vertices[i]);
            }

            for (int i = 0; i < Vertices.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                if (!Vertices[i].Data) IsEar(Vertices[i], Vertices, ct);
            }

            VertexPoint LastVertex = Vertices[0].Prev;
            VertexPoint CurrentVertex = Vertices[0];

            while (true)
            {
                if (ct.IsCancellationRequested) break;
                CurrentVertex = CurrentVertex.Next;
                if (CurrentVertex.Crossing != null)
                {
                    LastVertex = CurrentVertex.Prev;
                    Indexer.Add(CurrentVertex.Data ? CurrentVertex.Prev.Idx : CurrentVertex.Idx);
                    Indexer.Add(CurrentVertex.Data ? CurrentVertex.Idx : CurrentVertex.Prev.Idx);
                    Indexer.Add(CurrentVertex.Next.Idx);

                    CurrentVertex.Prev.Next = CurrentVertex.Next;
                    CurrentVertex.Next.Prev = CurrentVertex.Prev;

                    CheckClockwise(CurrentVertex.Prev);
                    CheckClockwise(CurrentVertex.Next);

                    Vertices.Remove(CurrentVertex);
                    CurrentVertex.Prev.Crossing = null;
                    CurrentVertex.Next.Crossing = null;
                    if (!CurrentVertex.Prev.Data) IsEar(CurrentVertex.Prev, Vertices, ct);
                    if (!CurrentVertex.Next.Data) IsEar(CurrentVertex.Next, Vertices, ct);
                }
                if (LastVertex == CurrentVertex) break;
            }
            return Indexer;
        }

        public static void CheckClockwise(VertexPoint v)
        {
            if (IsTriangleOrientedClockwise(v.Prev.Coords, v.Coords, v.Next.Coords))
                v.Data = true;
            else
                v.Data = false;
        }

        public static bool IsTriangleOrientedClockwise(Vec2 p1, Vec2 p2, Vec2 p3)
        {
            double determinant = p1.Northing * p2.Easting + p3.Northing * p1.Easting + p2.Northing * p3.Easting - p1.Northing * p3.Easting - p3.Northing * p2.Easting - p2.Northing * p1.Easting;

            if (determinant > 0.0)
                return false;
            else
                return true;
        }

        public static void IsEar(VertexPoint Point, List<VertexPoint> vertices, CancellationToken ct)
        {
            bool hasPointInside = false;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                if (vertices[i].Data)
                {
                    if (IsPointInTriangle(Point.Prev.Coords, Point.Coords, Point.Next.Coords, vertices[i].Coords))
                    {
                        hasPointInside = true;
                        break;
                    }
                }
            }

            if (!hasPointInside)
            {
                Point.Crossing = Point;
            }
        }

        public static bool IsPointInTriangle(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p)
        {
            double Denominator = ((p2.Easting - p3.Easting) * (p1.Northing - p3.Northing) + (p3.Northing - p2.Northing) * (p1.Easting - p3.Easting));
            double a = ((p2.Easting - p3.Easting) * (p.Northing - p3.Northing) + (p3.Northing - p2.Northing) * (p.Easting - p3.Easting)) / Denominator;

            if (a > 0.0 && a < 1.0)
            {
                double b = ((p3.Easting - p1.Easting) * (p.Northing - p3.Northing) + (p1.Northing - p3.Northing) * (p.Easting - p3.Easting)) / Denominator;
                if (b > 0.0 && b < 1.0)
                {
                    double c = 1 - a - b;
                    if (c > 0.0 && c < 1.0)
                        return true;
                }
            }
            return false;
        }
    }

    public static class Glm
    {
        //Regex file expression
        public static string fileRegex = "(^(PRN|AUX|NUL|CON|COM[1-9]|LPT[1-9]|(\\.+)$)(\\..*)?$)|(([\\x00-\\x1f\\\\?*:\";‌​|/<>])+)|([\\.]+)";

        //inches to meters
        public static double in2m = 0.02539999999997;

        //meters to inches
        public static double m2in = 39.37007874019995;

        //meters to feet
        public static double m2ft = 3.28084;

        //Meters to Acres
        public static double m2ac = 0.000247105;

        //Meters to Hectare
        public static double m2ha = 0.0001;

        //the pi's
        public static double twoPI = 6.28318530717958647692;

        public static double PIBy2 = 1.57079632679489661923;

        //Degrees Radians Conversions
        public static double ToDegrees(double radians)
        {
            return radians * 57.295779513082325225835265587528;
        }

        public static double ToRadians(double degrees)
        {
            return degrees * 0.01745329251994329576923690768489;
        }

        //Distance calcs of all kinds
        public static double Distance(double east1, double north1, double east2, double north2)
        {
            return Math.Sqrt(Math.Pow(east1 - east2, 2) + Math.Pow(north1 - north2, 2));
        }

        public static double Distance(Vec2 first, Vec2 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec2 first, Vec3 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec3 first, Vec3 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec3 first, double east, double north)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - east, 2)
                + Math.Pow(first.Northing - north, 2));
        }

        public static double Distance(Vec2 first, double east, double north)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - east, 2)
                + Math.Pow(first.Northing - north, 2));
        }

        //not normalized distance, no square root
        public static double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            return Math.Pow(easting1 - easting2, 2) + Math.Pow(northing1 - northing2, 2);
        }
    }
}