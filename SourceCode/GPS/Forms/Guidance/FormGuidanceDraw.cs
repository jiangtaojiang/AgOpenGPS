using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

namespace AgOpenGPS
{
    public partial class FormGuidanceDraw : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf;

        private double fieldCenterX, fieldCenterY, maxFieldDistance, Offset;
        private bool isA = true, isSet = false, ResetHeadLine = false, isDrawSections = false, Changed = false;
        private readonly bool HeadLand;
        private int Start = -1, End = -1, Boundary = -1, TemplateIndex;
        private readonly System.Windows.Forms.Timer Timer = new System.Windows.Forms.Timer();
        private byte TimerMode = 0;


        public FormGuidanceDraw(Form callingForm, bool HeadDraw)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;

            InitializeComponent();
            Timer.Tick += new EventHandler(TimerRepeat_Tick);
            //lblPick.Text = gStr.gsSelectALine;
            label3.Text = String.Get("gsCreate");
            label4.Text = String.Get("gsSelect");

            Offset1.Text = Offset2.Text = String.Get("gsOffset");


            Boundary = 0;

            Offset = mf.Guidance.GuidanceWidth;

            Size = new Size(1006, 759);
            if (HeadLand = HeadDraw)
            {
                HeadLandBox.Visible = true;
                ABDrawBox.Visible = false;
                Text = String.Get("gsHeadlandForm");
                RebuildHeadLineTemplate(false);
                HeadLandBox.Location = new Point(710, 85);
            }
            else
            {
                Offset = Math.Round(Offset / 2, 2);
                HeadLandBox.Visible = false;
                ABDrawBox.Visible = true;
                Text = String.Get("gsClick2Pointsontheboundary");
                UpdateBoundary();
            }
            TboxOffset1.Text = TboxOffset2.Text = (Offset * mf.Mtr2Unit).ToString(mf.GuiFix);
        }

        private void FixGuidanceLabels()
        {
            label1.Text = (mf.Guidance.CurrentEditLine + 1).ToString() + " of " + mf.Guidance.Lines.Count.ToString();
            if (mf.Guidance.CurrentEditLine > -1 && mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count)
            {
                btnGuidanceDelete.Enabled = true;
                lblGuidanceName.Text = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Name;
            }
            else
            {
                btnGuidanceDelete.Enabled = false;
                lblGuidanceName.Text = "***";
            }
        }

        private void BtnSelectGuidance_Click(object sender, EventArgs e)
        {
            mf.Guidance.CurrentEditLine = (mf.Guidance.Lines.Count > 0) ? (mf.Guidance.CurrentEditLine + 1) % mf.Guidance.Lines.Count : -1;

            FixGuidanceLabels();
            oglSelf.Refresh();
        }

        private void BtnCancelTouch_Click(object sender, EventArgs e)
        {
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;

            isA = true;
            Start = End = -1;
            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();

            btnCancelTouch.Enabled = false;
            oglSelf.Refresh();
        }

        private void BtnDeleteCurve_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                mf.Guidance.Lines.RemoveAt(mf.Guidance.CurrentEditLine);
                if (mf.Guidance.CurrentEditLine < mf.Guidance.CurrentLine)
                    mf.Guidance.CurrentLine--;
                if (mf.Guidance.CurrentEditLine == mf.Guidance.CurrentLine)
                {
                    mf.Guidance.ResetABLine = true;
                    mf.Guidance.CurrentLine = -1;
                    mf.Guidance.ExtraGuidanceLines.Clear();
                }
                mf.Guidance.CurrentEditLine--;

                if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
                else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() + " of " + mf.Guidance.Lines.Count.ToString();
                Properties.Settings.Default.LastGuidanceLine = mf.Guidance.CurrentLine;
                Properties.Settings.Default.Save();
            }

            FixGuidanceLabels();
            oglSelf.Refresh();
        }

        private void BtnDrawSections_Click(object sender, EventArgs e)
        {
            isDrawSections = !isDrawSections;
            if (isDrawSections) btnDrawSections.Text = "On";
            else btnDrawSections.Text = "Off";
            oglSelf.Refresh();
        }

        private void UpdateBoundary()
        {
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                CalculateMinMax();
                oglSelf.Refresh();
            }

            FixGuidanceLabels();

            if (HeadLand)
            {
                if (isSet)
                {
                    btnMoveLeft.Enabled = true;
                    btnMoveRight.Enabled = true;
                    btnMoveUp.Enabled = true;
                    btnMoveDown.Enabled = true;
                    btnDoneManualMove.Enabled = true;
                    btnDeletePoints.Enabled = true;
                    btnStartUp.Enabled = true;
                    btnStartDown.Enabled = true;
                    btnEndDown.Enabled = true;
                    btnEndUp.Enabled = true;
                }
                else
                {
                    btnMoveLeft.Enabled = false;
                    btnMoveRight.Enabled = false;
                    btnMoveUp.Enabled = false;
                    btnMoveDown.Enabled = false;
                    btnDoneManualMove.Enabled = false;
                    btnDeletePoints.Enabled = false;
                    btnStartUp.Enabled = false;
                    btnStartDown.Enabled = false;
                    btnEndDown.Enabled = false;
                    btnEndUp.Enabled = false;
                }
            }
        }

        private void RebuildHeadLineTemplate(bool Reset)
        {
            if (mf.bnd.Boundaries.Count > Boundary && Boundary > -1)
            {
                mf.bnd.Boundaries[Boundary].Template.Clear();
                if (!Reset && mf.bnd.Boundaries[Boundary].HeadLand.Count > 0)
                {
                    mf.bnd.Boundaries[Boundary].Template.AddRange(mf.bnd.Boundaries[Boundary].HeadLand);
                    ResetHeadLine = false;
                }
                else
                {
                    mf.bnd.Boundaries[Boundary].Template.Add(new Polygon() {Points = new List<Vec2>(mf.bnd.Boundaries[Boundary].Polygon.Points)});
                    ResetHeadLine = true;
                }

                Changed = !Reset;

                UpdateBoundary();
            }
            Start = End = -1;
            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();
            isA = true;
            isSet = false;
            TemplateIndex = 0;
            oglSelf.Refresh();
        }

        private void BtnDeletePoints_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            Changed = true;
            if (TemplateIndex <= mf.bnd.Boundaries[Boundary].Template.Count && Start >= 0 && End >= 0)
            {
                int start2 = Start;
                int end2 = End;

                if (((mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - end2 + start2) % mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count) < ((mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - start2 + end2) % mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count)) { int index = start2; start2 = end2; end2 = index; }

                if (start2 > end2)
                {
                    mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.RemoveRange(start2, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - start2);
                    mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.RemoveRange(0, end2);
                }
                else
                {
                    mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.RemoveRange(start2, end2 - start2);
                }
                if (mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count < 3) mf.bnd.Boundaries[Boundary].Template.RemoveAt(TemplateIndex);
            }

            Start = End = -1;
            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();
            isA = true;
            isSet = false;
            UpdateBoundary();
        }

        private void BtnDoneManualMove_Click(object sender, EventArgs e)
        {
            Start = End = -1;
            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();
            isA = true;
            isSet = false;
            UpdateBoundary();
        }

        private void BtnTurnOffHeadland_Click(object sender, EventArgs e)
        {
            mf.bnd.Boundaries[Boundary].Template.Clear();
            Close();
        }

        private void BtnExit2_Click(object sender, EventArgs e)
        {
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                if (Changed || ResetHeadLine)
                {
                    StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Save, 0, ResetHeadLine ? 1 : 0, 0, 0, 0);
                }
                ResetHeadLine = false;
            }
            DialogResult = DialogResult.Yes;
            Close();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            ResetHeadLine = true;
            RebuildHeadLineTemplate(true);

            for (int j = 0; j < mf.TaskList.Count; j++)
            {
                if (mf.TaskList[j].Task.IsCompleted)
                {
                    mf.TaskList.RemoveAt(j);
                    j--;
                }
                else if (mf.TaskList[j].Idx == mf.bnd.Boundaries[Boundary])
                {
                    if (mf.TaskList[j].TaskName == TaskName.Delete)
                    {
                        return;
                    }
                    else if (mf.TaskList[j].TaskName == TaskName.FixHead || mf.TaskList[j].TaskName == TaskName.FixBnd)
                    {
                        mf.TaskList[j].Token.Cancel();
                    }
                    else if (mf.TaskList[j].TaskName == TaskName.HeadLand)
                    {
                        mf.TaskList[j].Token.Cancel();
                    }
                }
            }
        }

        private void BtnMoveUp_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Up, Offset, TemplateIndex, Boundary, Start, End);
                Changed = true;
            }
        }

        private void BtnMoveDown_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Down, Offset, TemplateIndex, Boundary, Start, End);
                Changed = true;
            }
        }

        private void BtnMoveLeft_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Left, Offset, TemplateIndex, Boundary, Start, End);
                Changed = true;
            }
        }

        private void BtnMoveRight_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Right, Offset, TemplateIndex, Boundary, Start, End);
                Changed = true;
            }
        }

        private void BtnMakeFixedHeadland_Click(object sender, EventArgs e)
        {
            ResetHeadLine = false;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Offset, Offset, TemplateIndex, Boundary, Start, End);
                BtnDoneManualMove_Click(null, null);
                Changed = true;
            }

        }

        private void StartTask_FixHead(CBoundaryLines BoundaryLine, HeadLandTaskName HeadLandAction, double Offset, int Idx, int Boundary, int start, int end)
        {
            List<Task> tasks = new List<Task>();
            CancellationTokenSource newtoken = new CancellationTokenSource();
            for (int j = 0; j < mf.TaskList.Count; j++)
            {
                if (mf.TaskList[j].Task.IsCompleted)
                {
                    mf.TaskList.RemoveAt(j);
                    j--;
                }
                //{ OpenJob, CloseJob, Save, Delete     FixBnd, FixHead,    HeadLand     Boundary,     TurnLine, GeoFence, Triangulate, MinMax }
                else if (mf.TaskList[j].TaskName == TaskName.OpenJob || mf.TaskList[j].TaskName == TaskName.CloseJob || mf.TaskList[j].TaskName == TaskName.Save)
                {
                    tasks.Add(mf.TaskList[j].Task);
                }
                else if (mf.TaskList[j].Idx == BoundaryLine)
                {
                    if (mf.TaskList[j].TaskName == TaskName.Delete)
                    {
                        return;
                    }
                    else if (mf.TaskList[j].TaskName == TaskName.FixHead || mf.TaskList[j].TaskName == TaskName.FixBnd)
                    {
                        tasks.Add(mf.TaskList[j].Task);
                    }
                    else if (mf.TaskList[j].TaskName == TaskName.HeadLand)
                    {
                        tasks.Add(mf.TaskList[j].Task);
                        mf.TaskList[j].Token.Cancel();
                    }
                }
            }

            Task NewTask = mf.Task_FixHeadLand(BoundaryLine, HeadLandAction, Offset, Idx, Boundary, start, end, tasks, newtoken.Token);
            mf.TaskList.Add(new TaskClass(NewTask, BoundaryLine, TaskName.FixHead, newtoken));

            Awaittask(NewTask);
        }

        public async void Awaittask(Task task)
        {
            await Task.WhenAll(task);
            UpdateBoundary();
        }

        private void BtnEndUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 0;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnEndDown_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 1;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnStartUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 2;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnStartDown_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 3;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void Btn_MouseUp(object sender, MouseEventArgs e)
        {
            Timer.Enabled = false;
        }

        private void TimerRepeat_Tick(object sender, EventArgs e)
        {
            if (Timer.Enabled)
            {
                if (Timer.Interval > 50) Timer.Interval -= 50;
            }
            else
                Timer.Interval = 500;

            Timer.Enabled = true;

            if (TimerMode == 0)
            {

                if (TemplateIndex <= mf.bnd.Boundaries[Boundary].Template.Count)
                {
                    if (End != -1)
                    {
                        End = (End + 1).Clamp(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count);
                    }
                    if (End == -1) lblEnd.Text = "--";
                    else lblEnd.Text = End.ToString();
                }

            }
            else if (TimerMode == 1)
            {
                if (TemplateIndex <= mf.bnd.Boundaries[Boundary].Template.Count)
                {
                    if (End != -1)
                    {
                        End = (End - 1).Clamp(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count);
                    }
                    if (End == -1) lblEnd.Text = "--";
                    else lblEnd.Text = End.ToString();
                }
            }
            else if (TimerMode == 2)
            {
                if (TemplateIndex <= mf.bnd.Boundaries[Boundary].Template.Count)
                {
                    if (Start != -1)
                    {
                        Start = (Start + 1).Clamp(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count);
                    }
                    if (Start == -1) lblStart.Text = "--";
                    else lblStart.Text = Start.ToString();
                }
            }
            else if (TimerMode == 3)
            {
                if (TemplateIndex <= mf.bnd.Boundaries[Boundary].Template.Count)
                {
                    if (Start != -1)
                    {
                        Start = (Start - 1).Clamp(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count);
                    }

                    if (Start == -1) lblStart.Text = "--";
                    else lblStart.Text = Start.ToString();
                }
            }
            UpdateBoundary();
        }

        private void TboxOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 100, Offset, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxOffset1.Text = TboxOffset2.Text = ((Offset = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            btnExit2.Focus();
            btnExit.Focus();
        }

        private void OglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            btnCancelTouch.Enabled = true;
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;
            if (isSet) return;
            if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
            {
                Point pt = oglSelf.PointToClient(Cursor.Position);

                //Convert to Origin in the center of window, 800 pixels
                Vec2 pint;

                pint.Easting = (pt.X - 350) * maxFieldDistance / 632.0 + fieldCenterX;
                pint.Northing = (700 - pt.Y - 350) * maxFieldDistance / 632.0 + fieldCenterY;

                double minDist = double.PositiveInfinity;

                int Closest = 0;

                if (HeadLand)
                {
                    if (mf.bnd.Boundaries[Boundary].Template.Count > 0)
                    {
                        if (isA)
                        {
                            for (int i = 0; i < mf.bnd.Boundaries[Boundary].Template.Count; i++)
                            {
                                for (int j = 0; j < mf.bnd.Boundaries[Boundary].Template[i].Points.Count; j++)
                                {
                                    double dist = ((pint.Easting - mf.bnd.Boundaries[Boundary].Template[i].Points[j].Easting) * (pint.Easting - mf.bnd.Boundaries[Boundary].Template[i].Points[j].Easting))
                                                    + ((pint.Northing - mf.bnd.Boundaries[Boundary].Template[i].Points[j].Northing) * (pint.Northing - mf.bnd.Boundaries[Boundary].Template[i].Points[j].Northing));
                                    if (dist < minDist)
                                    {
                                        TemplateIndex = i;
                                        minDist = dist;
                                        Closest = j;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int j = 0; j < mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count; j++)
                            {
                                double dist = ((pint.Easting - mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[j].Easting) * (pint.Easting - mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[j].Easting))
                                                + ((pint.Northing - mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[j].Northing) * (pint.Northing - mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[j].Northing));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    Closest = j;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int ptCount = mf.bnd.Boundaries[Boundary].Polygon.Points.Count;
                    if (ptCount > 1)
                    {
                        for (int t = 0; t < ptCount; t++)
                        {
                            double dist = ((pint.Easting - mf.bnd.Boundaries[Boundary].Polygon.Points[t].Easting) * (pint.Easting - mf.bnd.Boundaries[Boundary].Polygon.Points[t].Easting))
                                            + ((pint.Northing - mf.bnd.Boundaries[Boundary].Polygon.Points[t].Northing) * (pint.Northing - mf.bnd.Boundaries[Boundary].Polygon.Points[t].Northing));
                            if (dist < minDist)
                            {
                                minDist = dist;
                                Closest = t;
                            }
                        }
                    }
                }

                if (isA && (minDist != double.PositiveInfinity))
                {
                    Start = Closest;
                    End = -1;
                    isA = false;
                }
                else if (minDist != double.PositiveInfinity)
                {
                    End = Closest;
                    isA = true;
                    if (HeadLand) isSet = true;
                    btnMakeABLine.Enabled = true;
                    btnMakeCurve.Enabled = true;

                    UpdateBoundary();
                }
                else
                {
                    Start = End = -1;
                    isA = true;
                    isSet = false;
                }

                if (Start == -1) lblStart.Text = "--";
                else lblStart.Text = Start.ToString();
                if (End == -1) lblEnd.Text = "--";
                else lblEnd.Text = End.ToString();
            }
            oglSelf.Refresh();
        }

        private void Next_Click(object sender, EventArgs e)
        {
            if (mf.bnd.Boundaries.Count > 0)
            {
                if (HeadLand)
                {
                    if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
                    {
                        if (Changed || ResetHeadLine)
                        {
                            StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Save, 0, ResetHeadLine ? 1 : 0, 0, 0, 0);

                            ResetHeadLine = false;
                        }
                    }

                    Boundary++;
                    if (Boundary > mf.bnd.Boundaries.Count - 1) Boundary = 0;

                    RebuildHeadLineTemplate(false);
                }
                else
                {
                    btnMakeABLine.Enabled = btnMakeCurve.Enabled = btnCancelTouch.Enabled = false;

                    isA = true;
                    Start = End = -1;
                    if (Start == -1) lblStart.Text = "--";
                    else lblStart.Text = Start.ToString();
                    if (End == -1) lblEnd.Text = "--";
                    else lblEnd.Text = End.ToString();
                    Boundary++;
                    if (Boundary > mf.bnd.Boundaries.Count - 1) Boundary = 0;

                    UpdateBoundary();
                }
            }
        }

        private void Previous_Click(object sender, EventArgs e)
        {
            if (mf.bnd.Boundaries.Count > 0)
            {
                if (HeadLand)
                {
                    if (mf.bnd.Boundaries.Count > Boundary && Boundary >= 0)
                    {
                        if (Changed || ResetHeadLine)
                        {
                            StartTask_FixHead(mf.bnd.Boundaries[Boundary], HeadLandTaskName.Save, 0, ResetHeadLine ? 1 : 0, 0, 0, 0);
                            ResetHeadLine = false;
                        }
                    }

                    Boundary--;
                    if (Boundary < 0 || Boundary > mf.bnd.Boundaries.Count - 1) Boundary = mf.bnd.Boundaries.Count - 1;

                    RebuildHeadLineTemplate(false);
                }
                else
                {
                    btnMakeABLine.Enabled = btnMakeCurve.Enabled = btnCancelTouch.Enabled = false;
                    isA = true;
                    Start = End = -1;
                    if (Start == -1) lblStart.Text = "--";
                    else lblStart.Text = Start.ToString();
                    if (End == -1) lblEnd.Text = "--";
                    else lblEnd.Text = End.ToString();

                    Boundary--;
                    if (Boundary < 0 || Boundary > mf.bnd.Boundaries.Count - 1) Boundary = mf.bnd.Boundaries.Count - 1;
                    UpdateBoundary();
                }
            }
        }

        private void BtnMakeCurve_Click(object sender, EventArgs e)
        {
            btnCancelTouch.Enabled = false;

            Button b = (Button)sender;
            bool test = b.Name == "btnMakeCurve";

            int ptCount = mf.bnd.Boundaries[Boundary].Polygon.Points.Count;
            if (test)
            {
                if (((ptCount - End + Start) % ptCount) < ((ptCount - Start + End) % ptCount)) { int index = Start; Start = End; End = index; }
                if (((ptCount - Start + End) % ptCount) < 1) return;
            }
            else
            {
                Start = 0;
                End = mf.bnd.Boundaries[Boundary].Polygon.Points.Count-1;
                if (ptCount < 3) return;
            }

            mf.Guidance.Lines.Add(new CGuidanceLine());
            int idx = mf.Guidance.Lines.Count - 1;
            mf.Guidance.CurrentEditLine = idx;



            if (!test) mf.Guidance.Lines[idx].Mode = Gmode.Boundary;
            else mf.Guidance.Lines[idx].Mode = Gmode.Curve;


            List<Vec2> newList = new List<Vec2>();
            if (Start > End)
            {
                newList.AddRange(mf.bnd.Boundaries[Boundary].Polygon.Points.GetRange(Start, mf.bnd.Boundaries[Boundary].Polygon.Points.Count - Start));
                newList.AddRange(mf.bnd.Boundaries[Boundary].Polygon.Points.GetRange(0, End+1));
            }
            else
            {
                newList.AddRange(mf.bnd.Boundaries[Boundary].Polygon.Points.GetRange(Start, End - Start+1));
            }

            mf.Guidance.Lines[idx].Segments = newList.OffsetPolyline(Offset, CancellationToken.None, mf.Guidance.Lines[idx].Mode == Gmode.Boundary);

            //build the tail extensions
            if (mf.Guidance.Lines[idx].Mode == Gmode.Curve)
            {
                mf.Guidance.AddFirstLastPoints();
            }


            //calculate average heading of line
            double x = 0, y = 0, N, E;

            double Distance = 0;
            int i = 0;
            for (int j = 1; j < mf.Guidance.Lines[idx].Segments.Count; i = j++)
            {
                x += (N = mf.Guidance.Lines[idx].Segments[j].Northing - mf.Guidance.Lines[idx].Segments[i].Northing);
                y += (E = mf.Guidance.Lines[idx].Segments[j].Easting - mf.Guidance.Lines[idx].Segments[i].Easting);
                Distance += Math.Sqrt(Math.Pow(N, 2) + Math.Pow(E, 2));
            }
            x /= Distance;
            y /= Distance;
            mf.Guidance.Lines[idx].Heading = Math.Atan2(y, x);
            if (mf.Guidance.Lines[idx].Heading < 0) mf.Guidance.Lines[idx].Heading += Glm.twoPI;


            //create a name
            mf.Guidance.Lines[idx].Name = "~~ " +(Math.Round(Glm.ToDegrees(mf.Guidance.Lines[idx].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                 + "\u00B0" + mf.FindDirection(mf.Guidance.Lines[idx].Heading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);


            mf.FileSaveGuidanceLines();

            //update the arrays
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;
            Start = -1; End = -1;

            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();

            FixGuidanceLabels();
            oglSelf.Refresh();
        }

        private void BtnMakeABLine_Click(object sender, EventArgs e)
        {
            btnCancelTouch.Enabled = false;

            //calculate the AB Heading
            double abHead = Math.Atan2(mf.bnd.Boundaries[Boundary].Polygon.Points[End].Easting - mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Easting, mf.bnd.Boundaries[Boundary].Polygon.Points[End].Northing - mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Northing);
            if (abHead < 0) abHead += Glm.twoPI;

            mf.Guidance.Lines.Add(new CGuidanceLine());

            int idx = mf.Guidance.Lines.Count - 1;
            mf.Guidance.Lines[idx].Heading = abHead;

            mf.Guidance.Lines[idx].Segments.Add(new Vec2((Math.Cos(abHead) * Offset) + mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Easting, (Math.Sin(abHead) * -Offset) + mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Northing));
            mf.Guidance.Lines[idx].Segments.Add(new Vec2((Math.Cos(abHead) * Offset) + mf.bnd.Boundaries[Boundary].Polygon.Points[End].Easting, (Math.Sin(abHead) * -Offset) + mf.bnd.Boundaries[Boundary].Polygon.Points[End].Northing));

            mf.Guidance.Lines[idx].Mode = Gmode.AB;

            //create a name
            mf.Guidance.Lines[idx].Name = "AB " + (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[idx].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                 + "\u00B0" + mf.FindDirection(mf.Guidance.Lines[idx].Heading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            //clean up gui
            btnMakeABLine.Enabled = false;
            btnMakeCurve.Enabled = false;
            Start = -1; End = -1;
            if (Start == -1) lblStart.Text = "--";
            else lblStart.Text = Start.ToString();
            if (End == -1) lblEnd.Text = "--";
            else lblEnd.Text = End.ToString();

            FixGuidanceLabels();
            oglSelf.Refresh();
        }

        private void OglSelf_Paint(object sender, PaintEventArgs e)
        {
            oglSelf.MakeCurrent();

            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            GL.LoadIdentity();                  // Reset The View

            //back the camera up
            GL.Translate(0, 0, -maxFieldDistance);

            //translate to that spot in the world
            GL.Translate(-fieldCenterX, -fieldCenterY, 0);

            GL.Color3(1, 1, 1);

            GL.LineWidth(2);

            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                if (Boundary == i) GL.Color3(1.0f, 0.0f, 0.0f);
                else GL.Color3(0.95f, 0.5f, 0.250f);

                mf.bnd.Boundaries[i].Polygon.DrawPolygon(false);
            }

            if (isDrawSections)
            {
                mf.CalcFrustum();
                mf.DrawPatchList(8);
                mf.DrawSectionsPatchList(true);
            }

            //the vehicle
            GL.PointSize(8.0f);
            GL.Begin(PrimitiveType.Points);
            GL.Color3(0.95f, 0.90f, 0.0f);
            Vec3 pivot = mf.pivotAxlePos;
            GL.Vertex3(pivot.Easting, pivot.Northing, 0.0);
            GL.End();

            if (HeadLand)
            {
                if (mf.bnd.Boundaries[Boundary].Template.Count > 0)
                {
                    GL.LineWidth(1);
                    GL.Color3(0.20f, 0.96232f, 0.30f);
                    GL.PointSize(2);
                    for (int h = 0; h < mf.bnd.Boundaries[Boundary].Template.Count; h++)
                    {
                        GL.Begin(PrimitiveType.LineLoop);
                        for (int i = 0; i < mf.bnd.Boundaries[Boundary].Template[h].Points.Count; i++)
                        {
                            GL.Vertex3(mf.bnd.Boundaries[Boundary].Template[h].Points[i].Easting, mf.bnd.Boundaries[Boundary].Template[h].Points[i].Northing, 0);
                        }
                        GL.End();
                    }

                    GL.PointSize(6);
                    if (TemplateIndex < mf.bnd.Boundaries[Boundary].Template.Count)
                    {
                        GL.Begin(PrimitiveType.Points);
                        GL.Color3(0.990, 0.00, 0.250);
                        if (Start != -1 && Start < mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count) GL.Vertex3(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[Start].Easting, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[Start].Northing, 0);
                        GL.Color3(0.990, 0.960, 0.250);
                        if (End != -1 && End < mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count) GL.Vertex3(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[End].Easting, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[End].Northing, 0);
                        GL.End();

                        if (Start != -1 && End != -1)
                        {
                            GL.Color3(0.965, 0.250, 0.950);
                            GL.LineWidth(2.0f);
                            int ptCount = mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count;
                            if (ptCount < 1) return;

                            int start2 = Math.Min(Start, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - 1);
                            int end2 = Math.Min(End, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count);
                            if (((mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - end2 + start2) % mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count) < ((mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count - start2 + end2) % mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count)) { int index = start2; start2 = end2; end2 = index; }
                            bool Loop = start2 > end2;


                            GL.Begin(PrimitiveType.LineStrip);
                            for (int i = start2; i <= end2 || Loop; i++)
                            {
                                if (i > mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count)
                                {
                                    i = 0;
                                    Loop = false;
                                }
                                if (i < mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points.Count) GL.Vertex3(mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[i].Easting, mf.bnd.Boundaries[Boundary].Template[TemplateIndex].Points[i].Northing, 0);
                            }
                            GL.End();
                        }
                    }
                }
            }
            else
            {
                GL.PointSize(8);
                GL.Begin(PrimitiveType.Points);

                GL.Color3(0.95, 0.950, 0.0);
                if (Start != -1) GL.Vertex3(mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Easting, mf.bnd.Boundaries[Boundary].Polygon.Points[Start].Northing, 0);

                GL.Color3(0.950, 096.0, 0.0);
                if (End != -1) GL.Vertex3(mf.bnd.Boundaries[Boundary].Polygon.Points[End].Easting, mf.bnd.Boundaries[Boundary].Polygon.Points[End].Northing, 0);
                GL.End();

                //draw the actual built lines
                if (Start == -1 && End == -1)
                {
                    GL.LineWidth(2);
                    GL.Enable(EnableCap.LineStipple);
                    GL.LineStipple(1, 0x0707);
                    GL.Color3(0.0f, 1.0f, 0.0f);
                    for (int i = 0; i < mf.Guidance.Lines.Count; i++)
                    {
                        if (mf.Guidance.CurrentEditLine != i)
                        {

                            GL.Begin(PrimitiveType.LineStrip);
                            if (mf.Guidance.Lines[i].Mode == Gmode.AB || mf.Guidance.Lines[i].Mode == Gmode.Heading)
                            {
                                GL.Begin(PrimitiveType.Lines);

                                GL.Vertex2(mf.Guidance.Lines[i].Segments[0].Easting + Math.Sin(mf.Guidance.Lines[i].Heading) * mf.maxCrossFieldLength,
                                    mf.Guidance.Lines[i].Segments[0].Northing + Math.Cos(mf.Guidance.Lines[i].Heading) * mf.maxCrossFieldLength);
                                GL.Vertex2(mf.Guidance.Lines[i].Segments[1].Easting + Math.Sin(mf.Guidance.Lines[i].Heading) * -mf.maxCrossFieldLength,
                                    mf.Guidance.Lines[i].Segments[1].Northing + Math.Cos(mf.Guidance.Lines[i].Heading) * -mf.maxCrossFieldLength);
                                GL.End();
                            }
                            else
                            {
                                foreach (Vec2 item in mf.Guidance.Lines[i].Segments)
                                {
                                    GL.Vertex3(item.Easting, item.Northing, 0);
                                }
                            }
                            GL.End();
                        }
                    }
                    GL.Disable(EnableCap.LineStipple);
                    GL.LineWidth(4);
                    GL.Color3(1.0f, 0.0f, 0.0f);


                    if (mf.Guidance.Lines.Count > mf.Guidance.CurrentEditLine && mf.Guidance.CurrentEditLine > -1)
                    {

                        GL.Begin(PrimitiveType.LineStrip);
                        if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.AB || mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Heading)
                        {
                            GL.Begin(PrimitiveType.Lines);
                            GL.Vertex2(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Easting + Math.Sin(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) * mf.maxCrossFieldLength,
                                mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Northing + Math.Cos(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) * mf.maxCrossFieldLength);
                            GL.Vertex2(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Easting + Math.Sin(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) * -mf.maxCrossFieldLength,
                                mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Northing + Math.Cos(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) * -mf.maxCrossFieldLength);
                            GL.End();
                        }
                        else
                        {
                            foreach (Vec2 item in mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments)
                            {
                                GL.Vertex3(item.Easting, item.Northing, 0);
                            }
                        }
                        GL.End();
                    }
                }
            }

            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            mf.Guidance.CurrentEditLine = -1;
            mf.FileSaveGuidanceLines();
            Close();
        }

        private void OglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            //58 degrees view
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(1.01f, 1.0f, (float)(maxFieldDistance - 10), (float)(maxFieldDistance + 10));
            GL.LoadMatrix(ref mat);

            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void OglSelf_Load(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.ClearColor(0.23122f, 0.2318f, 0.2315f, 1.0f);
        }

        //determine mins maxs of patches and whole field.
        private void CalculateMinMax()
        {
            if (mf.bnd.Boundaries.Count > 0)
            {
                //the largest distancew across field
                double dist = Math.Abs(mf.bnd.Boundaries[0].Eastingmin - mf.bnd.Boundaries[0].Eastingmax);
                double dist2 = Math.Abs(mf.bnd.Boundaries[0].Northingmin - mf.bnd.Boundaries[0].Northingmax);

                if (dist > dist2) maxFieldDistance = dist;
                else maxFieldDistance = dist2;

                if (maxFieldDistance < 100) maxFieldDistance = 100;

                fieldCenterX = (mf.bnd.Boundaries[0].Eastingmax + mf.bnd.Boundaries[0].Eastingmin) / 2.0;
                fieldCenterY = (mf.bnd.Boundaries[0].Northingmax + mf.bnd.Boundaries[0].Northingmin) / 2.0;
            }
        }
    }
}