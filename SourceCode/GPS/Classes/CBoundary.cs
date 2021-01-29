using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AgOpenGPS
{
    public partial class CBoundary
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        public List<CBoundaryLines> Boundaries = new List<CBoundaryLines>();

        public List<Vec2> bndBeingMadePts = new List<Vec2>();

        public double createBndOffset;
        public bool isBndBeingMade;

        public bool isDrawRightSide = true, isOkToAddPoints = false;

        public int boundarySelected = -1, closestBoundaryNum;

        public bool BtnHeadLand = false;
        public bool isToolUp = true;

        //constructor
        public CBoundary(FormGPS _f)
        {
            mf = _f;
        }

        public void DrawBoundaryLines()
        {
            for (int i = 0; i < Boundaries.Count; i++)
            {
                if (boundarySelected == i) GL.Color3(1.0f, 0.0f, 0.0f);
                else GL.Color3(0.95f, 0.5f, 0.250f);

                if (Boundaries[i].Northingmin <= mf.worldGrid.NorthingMax && Boundaries[i].Northingmax >= mf.worldGrid.NorthingMin)
                {
                    if (Boundaries[i].Eastingmin <= mf.worldGrid.EastingMax && Boundaries[i].Eastingmax >= mf.worldGrid.EastingMin)
                    {
                        Boundaries[i].Polygon.DrawPolygon(false);
                    }
                }
            }

            if (bndBeingMadePts.Count > 0)
            {
                //the boundary so far
                Vec3 pivot = mf.pivotAxlePos;
                GL.LineWidth(1);
                GL.Color3(0.825f, 0.22f, 0.90f);
                GL.Begin(PrimitiveType.LineStrip);
                for (int h = 0; h < bndBeingMadePts.Count; h++) GL.Vertex3(bndBeingMadePts[h].Easting, bndBeingMadePts[h].Northing, 0);
                GL.End();
                GL.Color3(0.295f, 0.972f, 0.290f);

                //line from last point to pivot marker
                GL.Color3(0.825f, 0.842f, 0.0f);
                GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x0700);
                GL.Begin(PrimitiveType.LineStrip);
                if (isDrawRightSide)
                {
                    GL.Vertex3(bndBeingMadePts[0].Easting, bndBeingMadePts[0].Northing, 0);
                    GL.Vertex3(pivot.Easting + Math.Cos(pivot.Heading) * createBndOffset, pivot.Northing + Math.Sin(pivot.Heading) * -createBndOffset, 0);
                    GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].Easting, bndBeingMadePts[bndBeingMadePts.Count - 1].Northing, 0);
                }
                else
                {
                    GL.Vertex3(bndBeingMadePts[0].Easting, bndBeingMadePts[0].Northing, 0);
                    GL.Vertex3(pivot.Easting + (Math.Cos(pivot.Heading) * -createBndOffset), pivot.Northing + (Math.Sin(pivot.Heading) * createBndOffset), 0);
                    GL.Vertex3(bndBeingMadePts[bndBeingMadePts.Count - 1].Easting, bndBeingMadePts[bndBeingMadePts.Count - 1].Northing, 0);
                }
                GL.End();
                GL.Disable(EnableCap.LineStipple);

                //boundary points
                GL.Color3(0.0f, 0.95f, 0.95f);
                GL.PointSize(6.0f);
                GL.Begin(PrimitiveType.Points);
                for (int h = 0; h < bndBeingMadePts.Count; h++) GL.Vertex3(bndBeingMadePts[h].Easting, bndBeingMadePts[h].Northing, 0);
                GL.End();
            }
        }

        public void DrawGeoFenceLines()
        {
            for (int i = 0; i < Boundaries.Count; i++)
            {
                GL.Color3(0.96555f, 0.1232f, 0.50f);

                if (Boundaries[i].Northingmin < mf.worldGrid.NorthingMax && Boundaries[i].Northingmax > mf.worldGrid.NorthingMin)
                {
                    if (Boundaries[i].Eastingmin < mf.worldGrid.EastingMax && Boundaries[i].Eastingmax > mf.worldGrid.EastingMin)
                    {
                        GL.Begin(PrimitiveType.LineLoop);
                        for (int h = 0; h < Boundaries[i].geoFenceLine.Count; h++) GL.Vertex3(Boundaries[i].geoFenceLine[h].Easting, Boundaries[i].geoFenceLine[h].Northing, 0);
                        GL.End();
                    }
                }
            }
        }

        public void DrawTurnLines()
        {
            GL.LineWidth(mf.lineWidth);
            GL.Color3(0.3555f, 0.6232f, 0.20f);
            //GL.PointSize(2);

            for (int i = 0; i < Boundaries.Count; i++)
            {
                if (Boundaries[i].isDriveAround || Boundaries[i].isDriveThru) continue;

                ////draw the turn line oject
                if (Boundaries[i].turnLine.Count < 1) return;

                if (Boundaries[i].Northingmin > mf.worldGrid.NorthingMax || Boundaries[i].Northingmax < mf.worldGrid.NorthingMin) continue;
                if (Boundaries[i].Eastingmin > mf.worldGrid.EastingMax || Boundaries[i].Eastingmax < mf.worldGrid.EastingMin) continue;

                GL.Begin(PrimitiveType.LineLoop);
                for (int h = 0; h < Boundaries[i].turnLine.Count; h++) GL.Vertex3(Boundaries[i].turnLine[h].Easting, Boundaries[i].turnLine[h].Northing, 0);
                GL.End();
            }
        }
    }



    public class Polygon
    {
        public List<Vec2> Points = new List<Vec2>();
        public List<int> Indexer = new List<int>();

        public bool ResetPoints, ResetIndexer;
        public int BufferPoints = int.MinValue, BufferIndex = int.MinValue, BufferPointsCnt = 0, BufferIndexCnt = 0;
        
        public void DrawPolygon(bool Triangles)
        {
            if (BufferPoints == int.MinValue || ResetPoints)
            {
                if (BufferPoints == int.MinValue) GL.GenBuffers(1, out BufferPoints);
                GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Points.Count * 16), Points.ToArray(), BufferUsageHint.StaticDraw);
                BufferPointsCnt = Points.Count;
                ResetPoints = false;
            }

            if (Triangles && BufferIndex == int.MinValue || ResetIndexer)
            {
                if (BufferIndex == int.MinValue) GL.GenBuffers(1, out BufferIndex);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(Indexer.Count * 4), Indexer.ToArray(), BufferUsageHint.StaticDraw);

                BufferIndexCnt = Indexer.Count;
                ResetIndexer = false;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferPoints);
            GL.VertexPointer(2, VertexPointerType.Double, 16, IntPtr.Zero);
            GL.EnableClientState(ArrayCap.VertexArray);

            if (Triangles)
            {
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferIndex);
                GL.DrawElements(PrimitiveType.Triangles, BufferIndexCnt, DrawElementsType.UnsignedInt, IntPtr.Zero);
            }
            else
            {
                GL.DrawArrays(PrimitiveType.LineLoop, 0, BufferPointsCnt);
            }
        }
    }

    public class CBoundaryLines
    {
        public Polygon Polygon = new Polygon();
        public List<Polygon> HeadLand = new List<Polygon>();
        public List<Polygon> Template = new List<Polygon>();


        public List<Vec2> geoFenceLine = new List<Vec2>();
        public List<Vec2> GeoCalcList = new List<Vec2>();

        public List<Vec2> turnLine = new List<Vec2>();
        public List<Vec2> CalcList = new List<Vec2>();

        public double Northingmin, Northingmax, Eastingmin, Eastingmax;

        //area variable
        public double Area = double.PositiveInfinity;

        //boundary variables
        public bool isDriveAround, isDriveThru;

        public void FixBoundaryLine(CancellationToken ct)
        {
            double area = Math.Abs(Polygon.Points.PolygonArea(ct, true) / 2.0);
            if (!ct.IsCancellationRequested) Area = area;

            double Multiplier = Math.Max(1, Math.Min((Area / 10000) / 10000, 10));
            double MinDist = 2 * Multiplier;
            double distance;

            int k = Polygon.Points.Count - 1;
            for (int l = 0; l < Polygon.Points.Count; k = l++)
            {
                if (k < 0) k = Polygon.Points.Count - 1;
                //make sure distance isn't too small between points on turnLine
                distance = Glm.Distance(Polygon.Points[k], Polygon.Points[l]);
                if (distance < MinDist)
                {
                    Polygon.Points.RemoveAt(k);
                    l--;
                    Polygon.ResetPoints = true;
                }
            }
        }

        public void BoundaryMinMax(CancellationToken ct)
        {
            if (Polygon.Points.Count > 0)
            {
                Northingmin = Northingmax = Polygon.Points[0].Northing;
                Eastingmin = Eastingmax = Polygon.Points[0].Easting;
                for (int j = 0; j < Polygon.Points.Count; j++)
                {
                    if (ct.IsCancellationRequested) break;
                    if (Northingmin > Polygon.Points[j].Northing) Northingmin = Polygon.Points[j].Northing;
                    if (Northingmax < Polygon.Points[j].Northing) Northingmax = Polygon.Points[j].Northing;
                    if (Eastingmin > Polygon.Points[j].Easting) Eastingmin = Polygon.Points[j].Easting;
                    if (Eastingmax < Polygon.Points[j].Easting) Eastingmax = Polygon.Points[j].Easting;
                }
            }
        }

        public bool IsPointInTurnArea(Vec2 TestPoint)
        {
            if (CalcList.Count < 3) return false;
            int j = turnLine.Count - 1;
            bool oddNodes = false;

            if (TestPoint.Northing > Northingmin || TestPoint.Northing < Northingmax || TestPoint.Easting > Eastingmin || TestPoint.Easting < Eastingmax)
            {
                //test against the constant and multiples list the test point
                for (int i = 0; i < turnLine.Count; j = i++)
                {
                    if ((turnLine[i].Northing < TestPoint.Northing && turnLine[j].Northing >= TestPoint.Northing)
                    || (turnLine[j].Northing < TestPoint.Northing && turnLine[i].Northing >= TestPoint.Northing))
                    {
                        oddNodes ^= ((TestPoint.Northing * CalcList[i].Northing) + CalcList[i].Easting < TestPoint.Easting);
                    }
                }
            }
            return oddNodes; //true means inside.
        }

        public bool IsPointInGeoFenceArea(Vec3 TestPoint)
        {
            if (GeoCalcList.Count < 3 || GeoCalcList.Count < geoFenceLine.Count) return false;

            int j = geoFenceLine.Count - 1;
            bool oddNodes = false;

            if (TestPoint.Northing > Northingmin || TestPoint.Northing < Northingmax || TestPoint.Easting > Eastingmin || TestPoint.Easting < Eastingmax)
            {
                //test against the constant and multiples list the test point
                for (int i = 0; i < geoFenceLine.Count; j = i++)
                {
                    if ((geoFenceLine[i].Northing < TestPoint.Northing && geoFenceLine[j].Northing >= TestPoint.Northing)
                    || (geoFenceLine[j].Northing < TestPoint.Northing && geoFenceLine[i].Northing >= TestPoint.Northing))
                    {
                        oddNodes ^= ((TestPoint.Northing * GeoCalcList[i].Northing) + GeoCalcList[i].Easting < TestPoint.Easting);
                    }
                }
            }
            return oddNodes; //true means inside.
        }
    }
}