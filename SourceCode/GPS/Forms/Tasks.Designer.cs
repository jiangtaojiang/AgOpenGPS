using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public enum TaskName { OpenJob, CloseJob, Save, Delete, FixBnd, FixHead, HeadLand, Boundary, TurnLine, GeoFence, Triangulate, MinMax };

    public enum HeadLandTaskName { Left, Right, Up, Down, Offset, Save };

    public class TaskClass
    {
        public Task Task;
        public CBoundaryLines Idx;
        public TaskName TaskName;
        public CancellationTokenSource Token;

        public TaskClass(Task task, CBoundaryLines idx, TaskName taskname, CancellationTokenSource token)
        {
            Task = task;
            Idx = idx;
            TaskName = taskname;
            Token = token;
        }
    }

    public partial class FormGPS
    {
        public List<TaskClass> TaskList = new List<TaskClass>();

        public void StartTasks(CBoundaryLines Boundary, int k, TaskName TaskName)
        {
            List<Task> tasks = new List<Task>();
            CancellationTokenSource newtoken = new CancellationTokenSource();
            if (TaskName == TaskName.HeadLand || TaskName == TaskName.TurnLine || TaskName == TaskName.GeoFence)
            {
                for (int j = 0; j < TaskList.Count; j++)
                {
                    if (TaskList[j].Task.IsCompleted)
                    {
                        TaskList.RemoveAt(j);
                        j--;
                    }
                    else if (TaskList[j].TaskName == TaskName.OpenJob || TaskList[j].TaskName == TaskName.CloseJob || TaskList[j].TaskName == TaskName.Save)
                    {
                        tasks.Add(TaskList[j].Task);
                    }
                    else if (TaskList[j].Idx == Boundary)
                    {
                        if (TaskList[j].TaskName == TaskName.Delete)
                        {
                            return;
                        }
                        else if (TaskName == TaskName.GeoFence || TaskName == TaskName.TurnLine)
                        {
                            if (TaskList[j].TaskName == TaskName.FixBnd)
                            {
                                tasks.Add(TaskList[j].Task);
                            }
                            else if (TaskList[j].TaskName == TaskName)
                            {
                                TaskList[j].Token.Cancel();
                                tasks.Add(TaskList[j].Task);
                            }
                        }
                        else
                        {
                            if (TaskList[j].TaskName == TaskName.FixHead)
                            {
                                tasks.Add(TaskList[j].Task);
                            }
                            else if (TaskList[j].TaskName == TaskName)
                            {
                                TaskList[j].Token.Cancel();
                                tasks.Add(TaskList[j].Task);
                            }
                        }
                    }
                }
                if (TaskName == TaskName.HeadLand)
                {
                    Task NewTask = Task_TriangulateHeadland(Boundary, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.HeadLand, newtoken));
                }
                else if (TaskName == TaskName.TurnLine)
                {
                    Task NewTask = Task_BuildTurnLine(Boundary, k == 0 ? Properties.Vehicle.Default.UturnTriggerDistance : -Properties.Vehicle.Default.UturnTriggerDistance, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.TurnLine, newtoken));
                }
                else if (TaskName == TaskName.GeoFence)
                {
                    Task NewTask = Task_BuildGeoFenceLine(Boundary, k == 0 ? Properties.Vehicle.Default.GeoFenceOffset : -Properties.Vehicle.Default.GeoFenceOffset, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.GeoFence, newtoken));
                }
            }
            else if (TaskName == TaskName.Save || TaskName == TaskName.CloseJob || TaskName == TaskName.Delete)
            {
                for (int j = 0; j < TaskList.Count; j++)
                {
                    if (TaskList[j].Task.IsCompleted)
                    {
                        TaskList.RemoveAt(j);
                        j--;
                    }
                    else if (TaskList[j].TaskName == TaskName.OpenJob || TaskList[j].TaskName == TaskName.CloseJob || TaskList[j].TaskName == TaskName.Save || TaskList[j].TaskName == TaskName.FixBnd || TaskList[j].TaskName == TaskName.FixHead || TaskList[j].TaskName == TaskName.Delete)
                    {
                        tasks.Add(TaskList[j].Task);
                    }
                    else if (TaskName != TaskName.Save)
                    {
                        tasks.Add(TaskList[j].Task);
                        if (TaskName == TaskName.CloseJob || (TaskName == TaskName.Delete && TaskList[j].Idx == Boundary))
                            TaskList[j].Token.Cancel();
                    }
                }

                if (TaskName == TaskName.Save)
                {
                    Task NewTask = Task_Save(k, tasks, newtoken.Token);

                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.Save, newtoken));
                }
                else if (TaskName == TaskName.CloseJob)
                {
                    Task NewTask = Task_JobClose(Boundary, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.CloseJob, newtoken));
                }
                else if (TaskName == TaskName.Delete)
                {
                    Task NewTask = Task_Delete(Boundary, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.Delete, newtoken));
                }
            }
            else
            {
                for (int j = 0; j < TaskList.Count; j++)
                {
                    if (TaskList[j].Task.IsCompleted)
                    {
                        TaskList.RemoveAt(j);
                        j--;
                    }
                    else
                    {
                        if (TaskList[j].TaskName == TaskName.OpenJob || TaskList[j].TaskName == TaskName.CloseJob || TaskList[j].TaskName == TaskName.Save)
                        {
                            tasks.Add(TaskList[j].Task);
                        }
                        else if (TaskList[j].Idx == Boundary)
                        {
                            if (TaskList[j].TaskName == TaskName.Delete)
                            {
                                return;
                            }
                            if (TaskList[j].TaskName == TaskName.FixBnd)
                            {
                                tasks.Add(TaskList[j].Task);
                            }
                            else if (TaskList[j].TaskName != TaskName.FixHead && TaskList[j].TaskName != TaskName.HeadLand)
                            {
                                tasks.Add(TaskList[j].Task);
                                TaskList[j].Token.Cancel();
                            }
                        }
                    }
                }

                if (double.IsInfinity(Boundary.Area))
                {
                    Task NewTask2 = Task_FixBoundaryLine(Boundary, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask2, Boundary, TaskName.FixBnd, newtoken));
                    tasks.Add(NewTask2);
                }

                newtoken = new CancellationTokenSource();
                Task NewTask = Task_BoundaryArea(Boundary, tasks, newtoken.Token);
                TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.MinMax, newtoken));

                if (Boundary.Polygon.Indexer.Count == 0)
                {
                    newtoken = new CancellationTokenSource();
                    NewTask = Task_TriangulateBoundary(Boundary, tasks, newtoken.Token);
                    TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.Triangulate, newtoken));
                }

                newtoken = new CancellationTokenSource();
                NewTask = Task_BuildGeoFenceLine(Boundary, k == 0 ? Properties.Vehicle.Default.GeoFenceOffset : -Properties.Vehicle.Default.GeoFenceOffset, tasks, newtoken.Token);
                TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.GeoFence, newtoken));

                newtoken = new CancellationTokenSource();
                NewTask = Task_BuildTurnLine(Boundary, k == 0 ? Properties.Vehicle.Default.UturnTriggerDistance : -Properties.Vehicle.Default.UturnTriggerDistance, tasks, newtoken.Token);
                TaskList.Add(new TaskClass(NewTask, Boundary, TaskName.TurnLine, newtoken));
            }
        }

        public async Task Task_FixBoundaryLine(CBoundaryLines BoundaryLine, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);
            await Task.Run(() =>
            {
                BoundaryLine.FixBoundaryLine(ct);
            });
            Guidance.ResetBoundaryTram = true;
        }

        public async Task Task_BoundaryArea(CBoundaryLines BoundaryLine, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);
            await Task.Run(() =>
            {
                BoundaryLine.BoundaryMinMax(ct);

                fd.UpdateFieldBoundaryGUIAreas();
            });
        }

        public async Task Task_TriangulateBoundary(CBoundaryLines Boundary, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            Boundary.Polygon.Indexer = await Task.Run(() =>
            {
                Boundary.Polygon.ResetIndexer = true;
                return Boundary.Polygon.Points.TriangulatePolygon(ct);
            });
        }

        public async Task Task_BuildGeoFenceLine(CBoundaryLines Boundary, double Distance, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            var tuple = await Task.Run(() =>
            {
                List<Vec2> OffsetPoints = new List<Vec2>();
                if (Distance == 0)
                    OffsetPoints = Boundary.Polygon.Points.ToList();
                else
                {
                    OffsetPoints = Boundary.Polygon.Points.OffsetPolyline(Distance, ct, true);
                    List<List<Vec2>> ss = OffsetPoints.FixPolyline(Distance, ct, true, in Boundary.Polygon.Points, true);
                    if (ss.Count > 0)
                        OffsetPoints = ss[0];
                }

                OffsetPoints.CalculateCalcListPolygon(ct, out List<Vec2> CalcList);
                return new Tuple<List<Vec2>, List<Vec2>>(OffsetPoints, CalcList);
            });
            Boundary.geoFenceLine = tuple.Item1;
            Boundary.GeoCalcList = tuple.Item2;
        }

        public async Task Task_BuildTurnLine(CBoundaryLines Boundary, double Distance, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            var tuple = await Task.Run(() =>
            {
                List<Vec2> OffsetPoints = new List<Vec2>();
                if (Distance == 0)
                    OffsetPoints = Boundary.Polygon.Points.ToList();
                else
                {
                    OffsetPoints = Boundary.Polygon.Points.OffsetPolyline(Distance, ct, true);

                    List<List<Vec2>> ss = OffsetPoints.FixPolyline(Distance, ct, true, in Boundary.Polygon.Points, true);
                    if (ss.Count > 0)
                        OffsetPoints = ss[0];
                }

                OffsetPoints.CalculateCalcListPolygon(ct, out List<Vec2> CalcList);
                return new Tuple<List<Vec2>, List<Vec2>>(OffsetPoints, CalcList);
            });
            Boundary.turnLine = tuple.Item1;
            Boundary.CalcList = tuple.Item2;
        }

        public async Task Task_TriangulateHeadland(CBoundaryLines BoundaryLine, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            await Task.Run(() =>
            {
                for (int j = 0; j < BoundaryLine.HeadLand.Count; j++)
                {
                    BoundaryLine.HeadLand[j].Indexer = BoundaryLine.HeadLand[j].Points.TriangulatePolygon(ct);
                    BoundaryLine.HeadLand[j].ResetIndexer = true;
                }
            });
        }

        public async Task Task_Delete(CBoundaryLines BoundaryLine, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            bnd.Boundaries.Remove(BoundaryLine);

            await Task.Run(() =>
            {
                fd.UpdateFieldBoundaryGUIAreas();
            });
        }

        public async Task Task_JobClose(CBoundaryLines BoundaryLine, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            if (InvokeRequired)
                BeginInvoke((MethodInvoker)(() => JobClose()));
            else
                JobClose();

            fd.areaOuterBoundary = 0;
            fd.areaBoundaryOuterLessInner = 0;

            maxFieldX = 0; minFieldX = 0; maxFieldY = 0; minFieldY = 0; maxFieldDistance = 1500;
            maxCrossFieldLength = 1000;
            maxFieldDistance = 100;
        }

        public async Task Task_JobNew(string fileAndDirectory, List<Task> tasks)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            if (InvokeRequired)
                BeginInvoke((MethodInvoker)(() => JobNew()));
            else
                JobNew();

            await Task.Run(() =>
            {
                FileOpenField2(fileAndDirectory);
            });
        }

        public async Task Task_Save(int t, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);
            
            await Task.Run(() =>
            {
                if (t == 1 || t == 6) FileSaveBoundary();
                if (t == 2 || t == 6) FileSaveHeadland();
                if (t == 0 || t == 3 || t == 7) FileSaveSections();
                if (t == 0 || t == 5) FileSaveFieldKML();
            });
        }

        public async Task Task_FixHeadLand(CBoundaryLines BoundaryLine, HeadLandTaskName HeadLandAction, double Offset, int Idx, int Boundary, int start, int end, List<Task> tasks, CancellationToken ct)
        {
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            await Task.Run(() =>
            {
                if (BoundaryLine.Template.Count > 0)
                {
                    if (HeadLandAction == HeadLandTaskName.Save)
                    {
                        for (int j = 0; j < BoundaryLine.HeadLand.Count; j++)
                        {
                        //    if (BoundaryLine.HeadLand[j].BufferPoints != int.MinValue)
                        //        GL.DeleteBuffer(BoundaryLine.HeadLand[j].BufferPoints);
                        //    if (BoundaryLine.HeadLand[j].BufferIndex != int.MinValue)
                        //        GL.DeleteBuffer(BoundaryLine.HeadLand[j].BufferIndex);
                        }

                        BoundaryLine.HeadLand.Clear();
                        if (Idx == 0)
                        {
                            BoundaryLine.HeadLand.AddRange(BoundaryLine.Template);
                        }

                        StartTasks(BoundaryLine, 0, TaskName.HeadLand);
                        StartTasks(null, 2, TaskName.Save);
                    }
                    else
                    {
                        double offset = Offset * (Boundary == 0 ? 1 : -1);
                        Vec2 Point;

                        int Start2 = start;
                        int End2 = end;

                        int Index = Idx;

                        if (start == -1 || end == -1)
                        {
                            Index = -1;
                        }
                        else
                        {
                            if (BoundaryLine.Template.Count > Idx)
                            {
                                if (End2 > BoundaryLine.Template[Idx].Points.Count) End2 = 0;
                                if (((BoundaryLine.Template[Idx].Points.Count - End2 + Start2) % BoundaryLine.Template[Idx].Points.Count) < ((BoundaryLine.Template[Idx].Points.Count - Start2 + End2) % BoundaryLine.Template[Idx].Points.Count))
                                {
                                    int index = Start2; Start2 = End2; End2 = index;
                                }
                            }
                        }

                        int test = BoundaryLine.Template.Count();

                        for (int i = 0; i < test; i++)
                        {
                            bool Loop = Start2 > End2;

                            if (HeadLandAction == HeadLandTaskName.Offset)
                            {
                                if (BoundaryLine.Template[i].Points.Count > 2)
                                {
                                    if (Index == -1 || i == Index)
                                    {
                                        List<Vec2> OffsetPoints = BoundaryLine.Template[i].Points.OffsetPolyline(offset, ct, true, Start2, End2);
                                        List<List<Vec2>> Output = OffsetPoints.FixPolyline(offset, ct, true, in BoundaryLine.Template[i].Points, true);
                                        for (int j = 0; j < Output.Count; j++)
                                        {
                                            BoundaryLine.Template.Add(new Polygon() { Points = Output[j] });
                                        }
                                    }
                                    else
                                    {
                                        BoundaryLine.Template.Add(BoundaryLine.Template[i]);
                                    }
                                }
                            }
                            else
                            {
                                List<Vec2> Template2 = new List<Vec2>();
                                for (int j = 0; j < BoundaryLine.Template[i].Points.Count; j++)
                                {
                                    Point = BoundaryLine.Template[i].Points[j];
                                    if (Index == -1 || (Index == i && ((Loop && (j <= End2 || j >= Start2)) || (!Loop && j > Start2 && j < End2))))
                                    {
                                        if (HeadLandAction == HeadLandTaskName.Up)
                                            Point.Northing++;
                                        else if (HeadLandAction == HeadLandTaskName.Down)
                                            Point.Northing--;
                                        else if (HeadLandAction == HeadLandTaskName.Right)
                                            Point.Easting++;
                                        else if (HeadLandAction == HeadLandTaskName.Left)
                                            Point.Easting--;
                                    }
                                    Template2.Add(Point);
                                }
                                BoundaryLine.Template.Add(new Polygon() { Points = Template2 });
                            }
                        }

                        for (int j = 0; j < test; j++)
                        {
                            //if (BoundaryLine.HeadLand[j].BufferPoints != int.MinValue)
                            //    GL.DeleteBuffer(BoundaryLine.HeadLand[j].BufferPoints);
                            //if (BoundaryLine.HeadLand[j].BufferIndex != int.MinValue)
                            //    GL.DeleteBuffer(BoundaryLine.HeadLand[j].BufferIndex);
                        }
                        BoundaryLine.Template.RemoveRange(0, test);
                    }
                }
            });
        }
    }
}
