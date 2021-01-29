using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormTram : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf;
        private double snapAdj = 0;
        private readonly Timer Timer = new Timer();
        private byte TimerMode = 0;

        public FormTram(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;

            InitializeComponent();
            Timer.Tick += new EventHandler(TimerRepeat_Tick);

            this.Text = String.Get("gsTramLines");
            lblSmallSnapRight.Text = String.Get("gsWidth");
            label1.Text = String.Get("gsTrack");
            label3.Text = String.Get("gsPasses");

            CurrentLineIdx.Text = (mf.Guidance.CurrentTramLine + 1).ToString() + " of " + mf.Guidance.Lines.Count.ToString();
            if (mf.Guidance.CurrentTramLine > -1 && mf.Guidance.CurrentTramLine < mf.Guidance.Lines.Count)
                lblGuidanceName.Text = mf.Guidance.Lines[mf.Guidance.CurrentTramLine].Name;
            else
                lblGuidanceName.Text = "***";
        }

        private void FormTram_Load(object sender, EventArgs e)
        {
            snapAdj = Math.Round(mf.Guidance.WidthMinusOverlap / 2, 2);
            TboxSnapAdj.Text = (snapAdj * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;

            TboxWheelTrack.Text = (mf.Guidance.TramWheelTrack * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
            TboxWheelWidth.Text = (mf.Guidance.TramWheelWidth * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
            TboxOffset.Text = (mf.Guidance.TramOffset * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
            TboxTramWidth.Text = (mf.Guidance.TramWidth * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
            TboxPasses.Text = mf.Guidance.TramPasses.ToString();
            label2.Text = (0.1 * mf.Mtr2Unit).ToString(mf.GuiFix) + (mf.isMetric ? " (Mtr)" : " Inch?");
            //if off, turn it on because they obviously want a tram.
            if (mf.Guidance.TramDisplayMode == 0)
            {
                mf.Guidance.TramDisplayMode = 1;
                mf.Guidance.BuildTram();
            }

            switch (mf.Guidance.TramDisplayMode)
            {
                case 0:
                    btnMode.Image = Properties.Resources.TramOff;
                    break;
                case 1:
                    btnMode.Image = Properties.Resources.TramAll;
                    break;
                case 2:
                    btnMode.Image = Properties.Resources.TramLines;
                    break;
                case 3:
                    btnMode.Image = Properties.Resources.TramOuter;
                    break;

                default:
                    break;
            }
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            Timer.Stop();
            Timer.Dispose();
            mf.FileSaveGuidanceLines();
            Close();
        }

        private void BtnLeft_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentTramLine, -0.1);
        }

        private void BtnRight_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentTramLine, 0.1);
        }

        private void BtnAdjLeft_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentTramLine, -snapAdj);
        }

        private void BtnAdjRight_Click(object sender, EventArgs e)
        {
            mf.Guidance.MoveLine(mf.Guidance.CurrentTramLine, snapAdj);
        }

        private void BtnSwapAB_Click(object sender, EventArgs e)
        {
            mf.Guidance.SwapHeading(mf.Guidance.CurrentTramLine);
        }

        private void ChangeTramLine_Click(object sender, EventArgs e)
        {
            mf.Guidance.CurrentTramLine = (mf.Guidance.CurrentTramLine + 1 >= mf.Guidance.Lines.Count) ? -1 : mf.Guidance.CurrentTramLine + 1;

            CurrentLineIdx.Text = (mf.Guidance.CurrentTramLine + 1).ToString() + " of " + mf.Guidance.Lines.Count.ToString();
            if (mf.Guidance.CurrentTramLine > -1 && mf.Guidance.CurrentTramLine < mf.Guidance.Lines.Count)
                lblGuidanceName.Text = mf.Guidance.Lines[mf.Guidance.CurrentTramLine].Name;
            else
                lblGuidanceName.Text = "***";

            mf.Guidance.BuildTram();
        }

        private void BtnTriggerDistanceUp_MouseDown(object sender, MouseEventArgs e)
        {
            TimerMode = 0;
            Timer.Enabled = false;
            TimerRepeat_Tick(null, EventArgs.Empty);
        }

        private void BtnTriggerDistanceDn_MouseDown(object sender, MouseEventArgs e)
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

            if (TimerMode == 0)
            {
                mf.Guidance.TramPasses++;
            }
            else if (TimerMode == 1)
            {
                mf.Guidance.TramPasses--;
            }
            TboxPasses.Text = mf.Guidance.TramPasses.ToString();
            Properties.Settings.Default.setTram_passes = mf.Guidance.TramPasses;
            Properties.Settings.Default.Save();
            mf.Guidance.BuildTram();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            mf.Guidance.TramList.Clear();
            mf.Guidance.BoundaryTram.Clear();

            mf.Guidance.ResetBoundaryTram = true;
            mf.Guidance.TramDisplayMode = 0;
            Timer.Stop();
            Timer.Dispose();
            mf.FileLoadGuidanceLines();
            Close();
        }

        private void BtnMode_Click(object sender, EventArgs e)
        {
            mf.Guidance.TramDisplayMode++;
            if (mf.Guidance.TramDisplayMode > 3) mf.Guidance.TramDisplayMode = 0;
            
            switch (mf.Guidance.TramDisplayMode)
            {
                case 0:
                    btnMode.Image = Properties.Resources.TramOff;
                    break;
                case 1:
                    btnMode.Image = Properties.Resources.TramAll;
                    break;
                case 2:
                    btnMode.Image = Properties.Resources.TramLines;
                    break;
                case 3:
                    btnMode.Image = Properties.Resources.TramOuter;
                    break;

                default:
                    break;
            }
        }


        #region NummericUpDown

        private void TboxSnapAdj_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.01, 50, snapAdj, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxSnapAdj.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
                    Properties.Settings.Default.setTram_snapAdj = snapAdj = form.ReturnValue;
                    Properties.Settings.Default.Save();
                }
            }
            btnCancel.Focus();
        }

        private void TboxWheelWidth_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.05, 5, mf.Guidance.TramWheelWidth, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxWheelWidth.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
                    Properties.Settings.Default.Tram_wheelWidth = mf.Guidance.TramWheelWidth = form.ReturnValue;
                    Properties.Settings.Default.Save();
                    mf.Guidance.ResetBoundaryTram = true;
                    mf.Guidance.BuildTram();
                }
            }
            btnCancel.Focus();
        }

        private void TboxWheelTrack_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.1, 10, mf.Guidance.TramWheelTrack, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxWheelTrack.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
                    Properties.Settings.Default.setTram_wheelSpacing = mf.Guidance.TramWheelTrack = form.ReturnValue;
                    mf.Guidance.TramHalfWheelTrack = mf.Guidance.TramWheelTrack * 0.5;
                    Properties.Settings.Default.Save();
                    mf.Guidance.ResetBoundaryTram = true;
                    mf.Guidance.BuildTram();
                }
            }
            btnCancel.Focus();
        }

        private void TboxOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-100, 100, mf.Guidance.TramOffset, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxOffset.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;

                    Properties.Settings.Default.setTram_offset = mf.Guidance.TramOffset = form.ReturnValue;
                    Properties.Settings.Default.Save();
                    mf.Guidance.ResetBoundaryTram = true;
                    mf.Guidance.BuildTram();
                }
            }
            btnCancel.Focus();
        }

        private void TboxTramWidth_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0.2, 100, mf.Guidance.TramWidth, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxTramWidth.Text = (form.ReturnValue * mf.Mtr2Unit).ToString(mf.GuiFix) + mf.Units;
                    Properties.Settings.Default.setTram_eqWidth = mf.Guidance.TramWidth = form.ReturnValue;
                    Properties.Settings.Default.Save();
                    mf.Guidance.ResetBoundaryTram = true;
                    mf.Guidance.BuildTram();
                }
            }
            btnCancel.Focus();
        }

        private void TboxPasses_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-999, 999, mf.Guidance.TramPasses, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    mf.Guidance.TramPasses = (int)form.ReturnValue;
                    TboxPasses.Text = mf.Guidance.TramPasses.ToString();
                    Properties.Settings.Default.setTram_passes = mf.Guidance.TramPasses;
                    Properties.Settings.Default.Save();
                    mf.Guidance.BuildTram();
                }
            }
            btnCancel.Focus();
        }

        #endregion NummericUpDown

    }
}