using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormEditAB : Form
    {
        private readonly FormGPS mf;
        private double snapAdj;
        private readonly System.Windows.Forms.Timer Timer = new System.Windows.Forms.Timer();
        private byte TimerMode = 0;

        public FormEditAB(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;

            InitializeComponent();
            Timer.Tick += new EventHandler(TimerRepeat_Tick);

            this.Text = String.Get("gsEditABLine");
            label2.Text = String.Get("gsABHeading");
            label5.Text = String.Get("gsOffset");

            Size = new Size(426, 554);

            snapAdj = Math.Round(mf.Guidance.WidthMinusOverlap / 2, 2);

            TboxSnapAdj.Text = (snapAdj * mf.Mtr2Unit).ToString(mf.GuiFix);

            mf.Guidance.SmoothCount = 20;
            lblSmooth.Text = "**";

            BtnCancel.Focus();

            mf.Guidance.CurrentEditLine = mf.Guidance.CurrentLine;

            if (mf.Guidance.Lines[mf.Guidance.CurrentLine].Mode == Gmode.AB || mf.Guidance.Lines[mf.Guidance.CurrentLine].Mode == Gmode.Heading)
            {
                tboxHeading.Visible = true;
                btnBPoint.Visible = true;
                label2.Visible = true;
                if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1) tboxHeading.Text = Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading).ToString("0.00##");
            }
            else
            {
                btnBPoint.Visible = false;
                tboxHeading.Visible = false;
                label2.Visible = false;
            }
        }

        private void BntOk_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.isSmoothWindowOpen)
            {
                mf.Guidance.isSmoothWindowOpen = false;
                mf.Guidance.SaveSmoothList();
                mf.Guidance.smooList.Clear();
            }

            mf.Guidance.CurrentEditLine = -1;
            mf.FileSaveGuidanceLines();
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            int last = mf.Guidance.CurrentLine;
            mf.FileLoadGuidanceLines();
            mf.Guidance.ResetABLine = true;
            mf.Guidance.CurrentLine = last;
            mf.Guidance.ExtraGuidanceLines.Clear();
            Properties.Settings.Default.LastGuidanceLine = last;
            Properties.Settings.Default.Save();
            mf.Guidance.CurrentEditLine = -1;

            if (mf.Guidance.CurrentLine == -1) mf.btnCycleLines.Text = String.Get("gsOff");
            else mf.btnCycleLines.Text = (mf.Guidance.CurrentLine + 1).ToString() +
                    " of " + mf.Guidance.Lines.Count.ToString();

            mf.Guidance.isSmoothWindowOpen = false;
            mf.Guidance.smooList.Clear();

            Close();
        }

        private void BtnSwapAB_Click(object sender, EventArgs e)
        {
            mf.Guidance.SwapHeading(mf.Guidance.CurrentEditLine);
            tboxHeading.Text = Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading).ToString("0.00##");
        }

        private void BtnBPoint_Click(object sender, EventArgs e)
        {
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                Vec3 pivot = mf.pivotAxlePos;

                mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.AB;
                mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Atan2(pivot.Easting - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Easting, pivot.Northing - mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0].Northing );
                if (mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading < 0) mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading += Glm.twoPI;

                mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1] = new Vec2(pivot.Easting, pivot.Northing);
                tboxHeading.Text = Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading).ToString("0.00##");
            }
        }

        private void TboxHeading_Enter(object sender, EventArgs e)
        {
            if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
            {
                using (var form = new FormNumeric(0, 360, Math.Round(Glm.ToDegrees(mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading), 4), this, 6, false))
                {
                    var result = form.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        tboxHeading.Text = form.ReturnValue.ToString("0.00##");
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading = Math.Round(Glm.ToRadians(form.ReturnValue),6);

                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[1] = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments[0];
                        mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode = Gmode.Heading;
                    }
                }
            }
            BtnCancel.Focus();
        }

        private void TboxSnapAdj_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.1, 50, snapAdj, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxSnapAdj.Text = ((snapAdj = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            BtnCancel.Focus();
        }

        private void BtnContourPriority_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentEditLine, mf.Guidance.isSameWay ? mf.Guidance.distanceFromRefLine : -mf.Guidance.distanceFromRefLine);
        }

        private void BtnRightHalfWidth_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentEditLine, mf.Guidance.isSameWay ? snapAdj : -snapAdj);
        }

        private void BtnLeftHalfWidth_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentEditLine, mf.Guidance.isSameWay ? -snapAdj: snapAdj);
        }

        private void BtnAdjRight_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentEditLine, mf.Guidance.isSameWay ? 0.1 : -0.1);
        }

        private void BtnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentEditLine, mf.Guidance.isSameWay ? -0.1 : 0.1);
        }

        private void BtnDown_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 0;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 1;
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

            //BtnSaveAs.Enabled = true;
            mf.Guidance.isSmoothWindowOpen = true;
            if (TimerMode == 0)
            {
                if (--mf.Guidance.SmoothCount < 2) mf.Guidance.SmoothCount = 2;
                else mf.Guidance.SmoothAB(mf.Guidance.SmoothCount * 2);
            }
            else if (TimerMode == 1)
            {
                if (++mf.Guidance.SmoothCount > 100) mf.Guidance.SmoothCount = 100;
                else mf.Guidance.SmoothAB(mf.Guidance.SmoothCount * 2);
            }
            lblSmooth.Text = mf.Guidance.SmoothCount.ToString();
        }

        private void BtnSaveAs_Click(object sender, EventArgs e)
        {
                Size = new Size(256, 409);
                NamePanel.Location = new Point(0, 0);
                NamePanel.Visible = true;
                EditPanel.Visible = false;
                NameBox.Text = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Name;
        }

        private void NameBox_Enter(object sender, EventArgs e)
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

                if (!mf.Guidance.isSmoothWindowOpen)
                {
                    mf.Guidance.smooList = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Segments;
                }
                mf.FileLoadGuidanceLines();

                mf.Guidance.Lines.Add(new CGuidanceLine());
                int idx = mf.Guidance.Lines.Count - 1;
                //create a name
                mf.Guidance.Lines[idx].Name = NameBox.Text;
                mf.Guidance.Lines[idx].Mode = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Mode;
                mf.Guidance.Lines[idx].Heading = mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Heading;

                mf.Guidance.Lines[idx].Segments.AddRange(mf.Guidance.smooList);

                mf.Guidance.isSmoothWindowOpen = false;

                mf.Guidance.smooList.Clear();

                if (mf.Guidance.CurrentEditLine < mf.Guidance.Lines.Count && mf.Guidance.CurrentEditLine > -1)
                {
                    mf.Guidance.Lines[mf.Guidance.CurrentEditLine].Name = NameBox.Text.Trim();
                }

                mf.Guidance.CurrentLine = idx;

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
                    NameBox.Text = "{} ";
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
    }
}
