using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class FormABCurve : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf;
        private double upDnHeading = 0, BoundaryOffset = 0;

        public FormABCurve(Form _mf)
        {
            Owner = mf = _mf as FormGPS;
            InitializeComponent();

            btnPausePlay.Text = String.Get("gsPause");

            Text = String.Get("gsGuidance");
            TboxHeading.Text = "0";
        }

        private void FormABCurve_Load(object sender, EventArgs e)
        {
            lvLines.Clear();
            ListViewItem itm;

            mf.Guidance.isOkToAddPoints = false;
            mf.Guidance.isEditing = false;
            foreach (var item in mf.Guidance.Lines)
            {
                itm = new ListViewItem(item.Name);
                lvLines.Items.Add(itm);
            }

            if (lvLines.Items.Count > 0) lvLines.Items[lvLines.Items.Count - 1].EnsureVisible();

            Size = new Size(426, 409);
            SelectPanel.Visible = true;
            DrivePanel.Visible = false;
            NamePanel.Visible = false;
        }

        private void BtnAPoint_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Curve)
                {
                    mf.Guidance.isOkToAddPoints = true;
                    BtnAPoint.Enabled = false;
                    mf.Guidance.isEditing = true;
                }
                else
                {
                    TboxHeading.Text = (upDnHeading = Math.Round(Glm.ToDegrees(mf.fixHeading), 6)).ToString();
                    Vec3 pivot = mf.pivotAxlePos;

                    if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Easting != 0 && mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Northing != 0)
                    {
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Atan2(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Easting - pivot.Easting, mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1].Northing - pivot.Northing);
                        if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading < 0) mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading += Glm.twoPI;

                        TboxHeading.Text = Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 6).ToString();
                        NameBox.Text = "AB " + (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                            + "\u00B0" +
                            mf.FindDirection(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.AB;
                    }
                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0] = new Vec2(pivot.Easting, pivot.Northing);

                    TboxHeading.Enabled = true;
                }
                BtnBPoint.Enabled = true;
                btnPausePlay.Enabled = true;
            }
        }

        private void BtnBPoint_Click(object sender, EventArgs e)
        {

            mf.Guidance.isOkToAddPoints = false;
            mf.Guidance.isEditing = false;
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                int cnt = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Count;

                if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Spiral || mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Circle)
                {
                    if (cnt > 0)
                    {
                        double easting = 0;
                        double northing = 0;

                        for (int i = 0; i < cnt; i++)
                        {
                            easting += mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[i].Easting;
                            northing += mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[i].Northing;
                        }
                        easting /= cnt;
                        northing /= cnt;

                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Clear();
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Add(new Vec2(easting, northing));

                    }
                    else
                    {
                        Vec3 pivot = mf.pivotAxlePos;
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Add(new Vec2(pivot.Easting, pivot.Northing));
                    }
                    mf.YouTurnButtons(true);
                    //mf.FileSaveCurveLine();

                    BtnNext.Enabled = true;
                    NameBox.Text = (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Spiral ? "spiral " : "circle ") + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                }
                else if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.AB || mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Heading)
                {
                    Vec3 pivot = mf.pivotAxlePos;

                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Atan2(pivot.Easting - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Easting , pivot.Northing - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Northing );
                    if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading < 0) mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading += Glm.twoPI;

                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1] = new Vec2(pivot.Easting, pivot.Northing);

                    TboxHeading.Text = Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 6).ToString();

                    NameBox.Text = "AB " + (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                        + "\u00B0" +
                        mf.FindDirection(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.AB;
                    BtnNext.Enabled = true;
                }
                else if (cnt > 5)
                {
                    BtnBPoint.Enabled = false;
                    //build the tail extensions
                    mf.Guidance.AddFirstLastPoints();

                    //calculate average heading of line
                    double x = 0, y = 0, N, E;
                    double Distance = 0;
                    int i = 0;
                    for (int j = 1; j < mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Count; i = j++)
                    {
                        x += (N = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[j].Northing - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[i].Northing);
                        y += (E = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[j].Easting - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[i].Easting);
                        Distance += Math.Sqrt(Math.Pow(N, 2) + Math.Pow(E, 2));
                    }
                    x /= Distance;
                    y /= Distance;

                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Atan2(y, x);
                    if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading < 0) mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading += Glm.twoPI;

                    BtnNext.Enabled = true;
                    NameBox.Text = "~~ " + (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                        + "\u00B0" + mf.FindDirection(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading)
                        + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                }
            }
        }


        private void BtnTurnOff_Click(object sender, EventArgs e)
        {
            mf.Guidance.ResetABLine = true;
            mf.Guidance.isOkToAddPoints = false;
            mf.Guidance.isEditing = false;
            mf.Guidance.tryoutcurve = -1;
            mf.Guidance.BtnGuidanceOn = false;
            mf.Guidance.ExtraGuidanceLines.Clear();
            mf.btnGuidance.Image = Properties.Resources.CurveOff;


            mf.snapLeftBigStrip.Enabled = mf.snapRightBigStrip.Enabled = mf.snapToCurrent.Enabled = false;

            mf.YouTurnButtons(false);

            if (mf.isAutoSteerBtnOn)
            {
                mf.isAutoSteerBtnOn = false;
                mf.btnAutoSteer.Image = Properties.Resources.AutoSteerOff;
                if (mf.Guidance.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
            }
            mf.btnCycleLines.Text = String.Get("gsOff");
            Close();
        }

        private void BtnListDelete_Click(object sender, EventArgs e)
        {
            if (lvLines.SelectedItems.Count > 0)
            {
                int num = lvLines.SelectedIndices[0];
                lvLines.SelectedItems[0].Remove();


                mf.Guidance.CurrentEditLine--;
                if (mf.Guidance.CurrentLine == num)
                {
                    mf.Guidance.ResetABLine = true;
                    mf.Guidance.CurrentLine = -1;
                    mf.Guidance.ExtraGuidanceLines.Clear();
                }
                else if (mf.Guidance.CurrentLine > num)
                    mf.Guidance.CurrentLine--;
                mf.Guidance.Lines.RemoveAt(num);
                Properties.Settings.Default.LastGuidanceLine = mf.Guidance.CurrentLine;
                Properties.Settings.Default.Save();

                if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
                else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() +
                        " of " + mf.Guidance.Lines.Count.ToString();
                mf.FileSaveGuidanceLines();

            }
        }

        private void BtnListUse_Click(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;

            if (count > 0)
            {
                int idx = lvLines.SelectedIndices[0];

                mf.Guidance.ResetABLine = true;
                mf.Guidance.CurrentLine = idx;
                mf.Guidance.ExtraGuidanceLines.Clear();
                mf.Guidance.tryoutcurve = -1;
                Properties.Settings.Default.LastGuidanceLine = idx;
                Properties.Settings.Default.Save();
                mf.YouTurnButtons(true);

                if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
                else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() +
                        " of " + mf.Guidance.Lines.Count.ToString();

                Close();
            }
        }

        private void LvLines_SelectedIndexChanged(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;
            if (count > 0)
            {
                mf.Guidance.tryoutcurve = lvLines.SelectedIndices[0];
                BtnListDelete.Enabled = true;
                BtnListUse.Enabled = true;
            }
            else
            {
                mf.Guidance.tryoutcurve = -1;
                BtnListDelete.Enabled = false;
                BtnListUse.Enabled = false;
            }
        }

        private void BtnPausePlay_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.isOkToAddPoints = !mf.Guidance.isOkToAddPoints)
            {
                btnPausePlay.Image = Properties.Resources.boundaryPause;
                btnPausePlay.Text = String.Get("gsPause");
            }
            else
            {
                btnPausePlay.Image = Properties.Resources.BoundaryRecord;
                btnPausePlay.Text = String.Get("gsRecord");
            }
        }

        private void TboxHeading_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 360, upDnHeading, this, 6, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxHeading.Text = (upDnHeading = Math.Round(form.ReturnValue, 6)).ToString();

                    if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
                    {
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Round(Glm.ToRadians(upDnHeading), 6);
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.Heading;
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1] = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0];

                        NameBox.Text = "AB " + (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                            + "\u00B0" +
                            mf.FindDirection(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                    }
                    BtnNext.Enabled = true;
                }
            }
            btnCancel2.Focus();
        }

        private void BtnAB_Click(object sender, EventArgs e)
        {
            mf.Guidance.tryoutcurve = -1;
            DrivePanel.Left = 0;

            Size = new Size(256, 409);

            SelectPanel.Visible = false;
            DrivePanel.Visible = true;
            NamePanel.Visible = false;

            BtnAPoint.Visible = true;
            BtnAPoint.Enabled = true;
            BtnBPoint.Visible = true;
            BtnBPoint.Enabled = false;
            btnPausePlay.Visible = false;
            TboxHeading.Enabled = false;
            TboxHeading.Visible = true;
            TboxHeading.Text = "";
            TboxOffset.Visible = false;
            BtnNext.Enabled = false;

            mf.Guidance.Lines.Add(new CGuidanceLine());
            mf.Guidance.CurrentEditLine = mf.Guidance.Lines.Count - 1;
            mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.AB;
            mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Add(new Vec2());
            mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.Add(new Vec2());
        }

        private void BtnCurve_Click(object sender, EventArgs e)
        {
            mf.Guidance.tryoutcurve = -1;
            DrivePanel.Left = 0;
            Size = new Size(256, 409);

            SelectPanel.Visible = false;
            DrivePanel.Visible = true;
            NamePanel.Visible = false;

            BtnAPoint.Visible = true;
            BtnAPoint.Enabled = true;
            BtnBPoint.Visible = true;
            BtnBPoint.Enabled = false;

            btnPausePlay.Visible = true;
            btnPausePlay.Enabled = false;

            TboxOffset.Visible = false;
            TboxHeading.Visible = false;
            BtnNext.Enabled = false;

            mf.Guidance.Lines.Add(new CGuidanceLine());
            mf.Guidance.CurrentEditLine = mf.Guidance.Lines.Count - 1;
            mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.Curve;
        }

        private void BtnCancelMain_Click_1(object sender, EventArgs e)
        {
            Close();
        }

        private void BtnCancelMain_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                if (mf.Guidance.CurrentLine == mf.Guidance.CurrentEditLine)
                {
                    mf.Guidance.ResetABLine = true;
                    mf.Guidance.CurrentLine = -1;
                    mf.Guidance.ExtraGuidanceLines.Clear();
                }
                else if (mf.Guidance.CurrentLine > mf.Guidance.CurrentEditLine)
                    mf.Guidance.CurrentLine--;
                mf.Guidance.Lines.RemoveAt(mf.Guidance.CurrentEditLine);

                Properties.Settings.Default.LastGuidanceLine = mf.Guidance.CurrentLine;
                Properties.Settings.Default.Save();
            }

            if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
            else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() +
                    " of " + mf.Guidance.Lines.Count.ToString();
            mf.Guidance.tryoutcurve = -1;
            mf.Guidance.CurrentEditLine = -1;
            mf.Guidance.isOkToAddPoints = false;
            mf.Guidance.isEditing = false;

            Size = new Size(426, 409);
            SelectPanel.Visible = true;
            DrivePanel.Visible = false;
            NamePanel.Visible = false;
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            NamePanel.Left = 0;
            Size = new Size(256, 409);
            SelectPanel.Visible = false;
            DrivePanel.Visible = false;
            NamePanel.Visible = true;
        }

        private void TextBox1_Enter(object sender, EventArgs e)
        {
            NameBox.Text = "";
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                BtnAdd.Focus();
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (NameBox.Text.Length > 0)
            {
                if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Boundary)
                {
                    BoundaryOffset += mf.Guidance.WidthMinusOverlap / 2;

                    List<Vec2> OffsetList = mf.bnd.Boundaries[0].Polygon.Points.OffsetPolyline(BoundaryOffset, CancellationToken.None, true);
                    List<List<Vec2>> Output = OffsetList.FixPolyline(BoundaryOffset, CancellationToken.None, true, in mf.bnd.Boundaries[0].Polygon.Points, true);
                    if (Output.Count > 0)
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments.AddRange(Output[0]);
                }

                if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
                {
                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Name = NameBox.Text.Trim();
                }

                mf.Guidance.CurrentLine = mf.Guidance.CurrentEditLine;

                mf.Guidance.ResetABLine = true;
                mf.Guidance.ExtraGuidanceLines.Clear();
                Properties.Settings.Default.LastGuidanceLine = mf.Guidance.CurrentLine;
                Properties.Settings.Default.Save();
                mf.Guidance.CurrentEditLine = -1;
                mf.Guidance.tryoutcurve = -1;

                mf.FileSaveGuidanceLines();

                if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
                else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() +
                        " of " + mf.Guidance.Lines.Count.ToString();
                Close();
            }
            else if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Spiral || mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Circle)
                    NameBox.Text = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Spiral ? "spiral " : "circle ";
                else if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Boundary)
                    NameBox.Text = "{} " + (BoundaryOffset * mf.Mtr2Unit).ToString(mf.GuiFix) + " Offset ";
                else
                {
                    if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode == Gmode.Curve)
                        NameBox.Text = "~~ ";
                    else
                        NameBox.Text = "AB ";

                    NameBox.Text += (Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 1)).ToString(CultureInfo.InvariantCulture)
                    + "\u00B0" + mf.FindDirection(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading) + " ";
                }
                NameBox.Text += DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        private void BtnBoundary_Click(object sender, EventArgs e)
        {
            DrivePanel.Left = 0;
            Size = new Size(256, 409);
            SelectPanel.Visible = false;
            DrivePanel.Visible = true;
            NamePanel.Visible = false;

            TboxOffset.Text = (BoundaryOffset = 0).ToString(mf.GuiFix);

            NameBox.Text = "{} " + (BoundaryOffset * mf.Mtr2Unit).ToString(mf.GuiFix) + " Offset " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

            mf.Guidance.tryoutcurve = -1;

            BtnBPoint.Visible = false;
            BtnAPoint.Visible = false;
            btnPausePlay.Visible = false;

            TboxHeading.Visible = false;
            TboxOffset.Visible = true;
            BtnNext.Enabled = true;

            mf.Guidance.Lines.Add(new CGuidanceLine());
            mf.Guidance.CurrentEditLine = mf.Guidance.Lines.Count - 1;
            mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.Boundary;
        }

        private void TboxOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 50, BoundaryOffset, this, 2, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxOffset.Text = ((BoundaryOffset = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);

                    NameBox.Text = "{} " + (BoundaryOffset * mf.Mtr2Unit).ToString(mf.GuiFix) + " Offset " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
                }
            }
            btnCancel2.Focus();
        }
    }
}