using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormBoundary : Form
    {
        private readonly FormGPS mf;

        private bool scroll = false;
        private int Position = 0, contentHeight = 0, oldY = 0;
        private double viewableRatio = 0, thumbHeight = 0, ScrollCalc = 0;
        private readonly int SliderMaxHeight = 0, SliderStartY = 0, SliderStartX = 0;
        private readonly int rowheight = 0,formheight = 0;

        public FormBoundary(Form callingForm)
        {
            Owner = mf = callingForm as FormGPS;

            //winform initialization
            InitializeComponent();

            SliderMaxHeight = Slider_Scroll.Size.Height;
            SliderStartY = Slider_Scroll.Location.Y;
            SliderStartX = Slider_Scroll.Location.X;

            formheight = BoundaryPanel.Height;
            rowheight = (int)BoundaryPanel.RowStyles[0].Height;

            UpdateScroll(-1);
            Text = String.Get("gsStartDeleteABoundary");

            //Column Header
            Boundary.Text = String.Get("gsBounds");
            Thru.Text = String.Get("gsDriveThru");
            Area.Text = String.Get("gsArea");
            Around.Text = String.Get("gsAround");

            ShowPanel.Visible = true;
            ChoosePanel.Visible = false;
            DrivePanel.Visible = false;
            Size = new Size(566, 559);
            lblOffset.Text = String.Get("gsOffset");
            BoundaryPanel.RowCount = 0;
        }

        public void UpdateScroll(double pos)
        {
            if (viewableRatio < 1)
            {
                if (pos < 0)
                {
                    Slider_Scroll.Location = new Point(SliderStartX, (int)(SliderStartY + Position / ScrollCalc + 0.5));
                }
                else
                {
                    Slider_Scroll.Location = new Point(SliderStartX, (int)(SliderStartY + pos));
                    Position = (int)(pos * ScrollCalc + 0.5);
                }
            }
            else
            {
                Position = 0;
                Slider_Scroll.Location = new Point(SliderStartX, SliderStartY);
            }


            BoundaryPanel.Height = mf.bnd.Boundaries.Count * rowheight;

            BoundaryPanel.SetBounds(0, -Position, BoundaryPanel.Width, BoundaryPanel.Height);
        }

        public void UpdateChart()
        {
            contentHeight = rowheight * mf.bnd.Boundaries.Count;
            viewableRatio = panel1.Size.Height / (double)contentHeight;
            thumbHeight = (SliderMaxHeight * viewableRatio < 100) ? 100 : (SliderMaxHeight * viewableRatio);

            if (viewableRatio >= 1)
            {
                Position = 0;
                BoundaryPanel.Width = 530;
                Slider_Scroll.Size = new Size(50, SliderMaxHeight);
                Slider_Scroll.Visible = false;
                Up_Scroll.Visible = false;
                Down_Scroll.Visible = false;
            }
            else
            {
                BoundaryPanel.Width = 480;
                Slider_Scroll.Size = new Size(50, (int)(thumbHeight + 0.5));
                Slider_Scroll.Visible = true;
                Up_Scroll.Visible = true;
                Down_Scroll.Visible = true;
            }


            ScrollCalc = (contentHeight - panel1.Size.Height) / (SliderMaxHeight - thumbHeight);

            if (Position < 0) Position = 0;
            else if (Position > contentHeight - panel1.Size.Height) Position = contentHeight - panel1.Size.Height;


            int inner = 1;

            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                Control aa = BoundaryPanel.GetControlFromPosition(0, i);
                if (aa == null)
                {
                    BoundaryPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, rowheight));

                    BoundaryPanel.RowCount++;
                    Font backupfont = new Font("Tahoma", 18F, FontStyle.Bold);
                    var a = new Button
                    {
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        Font = backupfont,
                        Size = new Size(280, 50),
                        Name = string.Format("{0}", i),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    a.Click += B_Click;
                    a.FlatStyle = FlatStyle.Flat;
                    a.FlatAppearance.BorderColor = BackColor;
                    a.FlatAppearance.MouseOverBackColor = BackColor;
                    a.FlatAppearance.MouseDownBackColor = BackColor;
                    aa = a;

                    var b = new Button
                    {
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        Font = backupfont,
                        Size = new Size(110, 50),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Name = string.Format("{0}", i),
                    };
                    b.Click += B_Click;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = BackColor;
                    b.FlatAppearance.MouseOverBackColor = BackColor;
                    b.FlatAppearance.MouseDownBackColor = BackColor;

                    var c = new Button
                    {
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        Font = backupfont,
                        Size = new Size(80, 50),
                        Name = string.Format("{0}", i),
                        Anchor = AnchorStyles.None,
                        BackColor = Color.WhiteSmoke,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    c.Click += DriveThru_Click;

                    var d = new Button
                    {
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        Font = backupfont,
                        Size = new Size(80, 50),
                        Name = string.Format("{0}", i),
                        Anchor = AnchorStyles.None,
                        BackColor = Color.WhiteSmoke,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    d.Click += DriveAround_Click;



                    if (i == 0)
                    {
                        a.Text = String.Get("gsOuter") + " 1";
                        c.Enabled = false;
                        d.Enabled = false;

                        mf.bnd.Boundaries[i].isDriveThru = false;
                        mf.bnd.Boundaries[i].isDriveAround = false;
                        c.Text = String.Get("gsNo");
                        d.Text = String.Get("gsNo");
                    }
                    else
                    {
                        aa.Text = String.Get("gsInner") + " " + inner++;
                        c.Enabled = true;
                        d.Enabled = true;
                    }

                    if (mf.isMetric)
                        b.Text = Math.Round(mf.bnd.Boundaries[i].Area * 0.0001, 2) + " Ha";
                    else
                        b.Text = Math.Round(mf.bnd.Boundaries[i].Area * 0.000247105, 2) + " Ac";


                    c.Text = String.Get(mf.bnd.Boundaries[i].isDriveThru ? "gsYes" : "gsNo");
                    d.Text = String.Get(mf.bnd.Boundaries[i].isDriveAround ? "gsYes" : "gsNo");


                    BoundaryPanel.Controls.Add(a, 0, i);
                    BoundaryPanel.Controls.Add(b, 1, i);
                    BoundaryPanel.Controls.Add(c, 2, i);
                    BoundaryPanel.Controls.Add(d, 3, i);
                }
                else if (i > 0) inner++;

                Control bb = BoundaryPanel.GetControlFromPosition(1, i);
                Control cc = BoundaryPanel.GetControlFromPosition(2, i);
                Control dd = BoundaryPanel.GetControlFromPosition(3, i);

                int width = BoundaryPanel.GetColumnWidths()[0];
                int height = BoundaryPanel.GetRowHeights()[i];
                aa.Size = new Size(width, height);
                width = BoundaryPanel.GetColumnWidths()[1];
                bb.Size = new Size(width, height);
                width = BoundaryPanel.GetColumnWidths()[2];
                cc.Size = new Size(width, height);
                width = BoundaryPanel.GetColumnWidths()[3];
                dd.Size = new Size(width, height);
            }
        }

        private void FormBoundary_Load(object sender, EventArgs e)
        {
            BtnLeftRight.Image = mf.bnd.isDrawRightSide ? Properties.Resources.BoundaryRight : Properties.Resources.BoundaryLeft;
            BtnDelete.Enabled = false;

            //update the list view with real data
            TboxBndOffset.Text = ((mf.bnd.createBndOffset = mf.Guidance.GuidanceWidth * 0.5) * mf.Mtr2Unit).ToString(mf.GuiFix);

            UpdateChart();
            UpdateScroll(-1);
        }

        void DriveThru_Click(object sender, EventArgs e)
        {
            if (sender is Button b)
            {
                int pos = BoundaryPanel.GetRow(b);
                mf.bnd.Boundaries[pos].isDriveThru = !mf.bnd.Boundaries[pos].isDriveThru;
                b.Text = String.Get(mf.bnd.Boundaries[pos].isDriveThru ? "gsYes" : "gsNo");
            }
        }

        void DriveAround_Click(object sender, EventArgs e)
        {
            if (sender is Button b)
            {
                int pos = BoundaryPanel.GetRow(b);
                mf.bnd.Boundaries[pos].isDriveAround = !mf.bnd.Boundaries[pos].isDriveAround;
                b.Text = String.Get(mf.bnd.Boundaries[pos].isDriveAround ? "gsYes" : "gsNo");
            }
        }

        void B_Click(object sender, EventArgs e)
        {
            if (mf.bnd.boundarySelected > -1)
            {
                Control aa = BoundaryPanel.GetControlFromPosition(0, mf.bnd.boundarySelected);
                if (aa != null)
                    aa.ForeColor = default;
                Control bb = BoundaryPanel.GetControlFromPosition(1, mf.bnd.boundarySelected);
                if (bb != null)
                    bb.ForeColor = default;
            }

            if (sender is Button b)
            {
                mf.bnd.boundarySelected = BoundaryPanel.GetRow(b);

                Control cc = BoundaryPanel.GetControlFromPosition(0, mf.bnd.boundarySelected);
                if (cc != null)
                    cc.ForeColor = Color.Red;
                Control dd = BoundaryPanel.GetControlFromPosition(1, mf.bnd.boundarySelected);
                if (dd != null)
                    dd.ForeColor = Color.Red;

                if (mf.bnd.boundarySelected == 0)
                { 
                    if (mf.bnd.Boundaries.Count == 1) BtnDelete.Enabled = true;
                    else BtnDelete.Enabled = false;
                }
                else if (mf.bnd.Boundaries.Count > mf.bnd.boundarySelected) BtnDelete.Enabled = true;

                else BtnDelete.Enabled = false;
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            mf.bnd.boundarySelected = -1;
            mf.bnd.isOkToAddPoints = false;
            Close();
        }

        private void BtnLeftRight_Click(object sender, EventArgs e)
        {
            mf.bnd.isDrawRightSide = !mf.bnd.isDrawRightSide;
            BtnLeftRight.Image = mf.bnd.isDrawRightSide ? Properties.Resources.BoundaryRight : Properties.Resources.BoundaryLeft;
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            //DialogResult result3 = MessageBox.Show(String.Get("gsCompletelyDeleteBoundary"),
            //    String.Get("gsDeleteForSure"),
            //    MessageBoxButtons.YesNo,
            //    MessageBoxIcon.Question,
            //    MessageBoxDefaultButton.Button2);

            //if (result3 == DialogResult.Yes)
            {
                BtnDelete.Enabled = false;

                if (mf.bnd.Boundaries.Count > mf.bnd.boundarySelected)
                {



                    RemoveArbitraryRow(BoundaryPanel, mf.bnd.boundarySelected);

                    BoundaryPanel.Height = Math.Max(formheight, mf.bnd.Boundaries.Count * rowheight);
                    mf.StartTasks(mf.bnd.Boundaries[mf.bnd.boundarySelected], mf.bnd.boundarySelected, TaskName.Delete);
                    if (mf.bnd.Boundaries.Count == 1) mf.bnd.BtnHeadLand = false;

                    mf.StartTasks(null, 6, TaskName.Save);

                    mf.bnd.boundarySelected = -1;

                    UpdateScroll(-1);
                }
            }
        }

        public void RemoveArbitraryRow(TableLayoutPanel panel, int rowIndex)
        {
            if (rowIndex >= panel.RowCount)
            {
                return;
            }

            // delete all controls of row that we want to delete
            for (int i = 0; i < panel.ColumnCount; i++)
            {
                var control = panel.GetControlFromPosition(i, rowIndex);
                panel.Controls.Remove(control);
            }

            // move up row controls that comes after row we want to remove
            for (int i = rowIndex + 1; i < panel.RowCount; i++)
            {
                for (int j = 0; j < panel.ColumnCount; j++)
                {
                    var control = panel.GetControlFromPosition(j, i);
                    if (control != null)
                    {
                        panel.SetRow(control, i - 1);
                    }
                }
            }

            var removeStyle = panel.RowCount - 1;

            if (panel.RowStyles.Count > removeStyle)
                panel.RowStyles.RemoveAt(removeStyle);

            panel.RowCount--;





            contentHeight = rowheight * (mf.bnd.Boundaries.Count-1);
            viewableRatio = panel1.Size.Height / (double)contentHeight;
            thumbHeight = (SliderMaxHeight * viewableRatio < 100) ? 100 : (SliderMaxHeight * viewableRatio);


            if (viewableRatio >= 1)
            {
                BoundaryPanel.Width = 530;
                Position = 0;
                Slider_Scroll.Size = new Size(50, SliderMaxHeight);
                Slider_Scroll.Visible = false;
                Up_Scroll.Visible = false;
                Down_Scroll.Visible = false;
            }
            else
            {
                BoundaryPanel.Width = 480;
                Slider_Scroll.Size = new Size(50, (int)(thumbHeight + 0.5));
                Slider_Scroll.Visible = true;
                Up_Scroll.Visible = true;
                Down_Scroll.Visible = true;
            }


            ScrollCalc = (contentHeight - panel1.Size.Height) / (SliderMaxHeight - thumbHeight);

            if (Position < 0) Position = 0;
            else if (Position > contentHeight - panel1.Size.Height) Position = contentHeight - panel1.Size.Height;

            UpdateScroll(-1);
        }

        private void ResetAllBoundary()
        {
            Position = 0;

            for (int i = 0; i < mf.bnd.Boundaries.Count; i++)
            {
                mf.StartTasks(mf.bnd.Boundaries[i], i, TaskName.Delete);
            }

            mf.StartTasks(null, 6, TaskName.Save);

            BoundaryPanel.Controls.Clear();
            BoundaryPanel.RowStyles.Clear();
            BoundaryPanel.RowCount = 0;
            BoundaryPanel.Height = Math.Max(formheight, mf.bnd.Boundaries.Count * rowheight);
            UpdateScroll(0);

            BtnDelete.Enabled = false;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            mf.bnd.bndBeingMadePts.Clear();
            timer1.Enabled = false;
            ShowPanel.Visible = true;
            DrivePanel.Visible = false;
            ChoosePanel.Visible = false;
            Size = new Size(566, 559);
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            ShowPanel.Visible = false;
            ChoosePanel.Visible = true;
            DrivePanel.Visible = false;
            ChoosePanel.Location = new Point(0, 0);
            Size = new Size(296, 559);
        }

        private void BtnBack_Click(object sender, EventArgs e)
        {
            ShowPanel.Visible = true;
            ChoosePanel.Visible = false;
            DrivePanel.Visible = false;
            Size = new Size(566, 559);
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            if (mf.bnd.bndBeingMadePts.Count > 2)
            {
                CBoundaryLines newbnd = new CBoundaryLines();
                newbnd.Polygon.Points = mf.bnd.bndBeingMadePts.ToList();
                mf.bnd.Boundaries.Add(newbnd);
                BoundaryPanel.Height = Math.Max(formheight , mf.bnd.Boundaries.Count * rowheight);

                mf.StartTasks(newbnd, mf.bnd.Boundaries.Count - 1, TaskName.Boundary);
                mf.StartTasks(null, 1, TaskName.Save);
            }

            //stop it all for adding
            mf.bnd.isOkToAddPoints = false;
            mf.bnd.isBndBeingMade = false;


            mf.bnd.bndBeingMadePts.Clear();

            UpdateChart();
            UpdateScroll(-1);

            ShowPanel.Visible = true;
            ChoosePanel.Visible = false;
            DrivePanel.Visible = false;
            Size = new Size(566, 559);
        }

        private void BtnAddPoint_Click(object sender, EventArgs e)
        {
            mf.bnd.isOkToAddPoints = true;
            mf.AddBoundaryPoint();
            mf.bnd.isOkToAddPoints = false;
            lblPoints.Text = "Points: " + mf.bnd.bndBeingMadePts.Count.ToString();

            BtnStop.Focus();
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            int ptCount = mf.bnd.bndBeingMadePts.Count;
            double area = 0;

            if (ptCount > 0)
            {
                int j = ptCount - 1;  // The last vertex is the 'previous' one to the first

                for (int i = 0; i < ptCount; j = i++)
                {
                    area += (mf.bnd.bndBeingMadePts[j].Easting + mf.bnd.bndBeingMadePts[i].Easting) * (mf.bnd.bndBeingMadePts[j].Northing - mf.bnd.bndBeingMadePts[i].Northing);
                }
                area = Math.Abs(area / 2);
            }
            if (mf.isMetric)
            {

                lblArea.Text = String.Get("gsArea") + ": " + Math.Round(area * 0.0001, 2) + " Ha";
            }
            else
            {
                lblArea.Text = String.Get("gsArea") + ": " + Math.Round(area * 0.000247105, 2) + " Acre";
            }
            lblPoints.Text = "Points: " + mf.bnd.bndBeingMadePts.Count.ToString();

        }

        private void BtnDeleteLast_Click(object sender, EventArgs e)
        {
            int ptCount = mf.bnd.bndBeingMadePts.Count;
            if (ptCount > 0)
                mf.bnd.bndBeingMadePts.RemoveAt(ptCount - 1);
            lblPoints.Text = "Points: " + mf.bnd.bndBeingMadePts.Count.ToString();
            BtnStop.Focus();
        }

        private void BtnRestart_Click(object sender, EventArgs e)
        {
            DialogResult result3 = MessageBox.Show(String.Get("gsCompletelyDeleteBoundary"),
                                    String.Get("gsDeleteForSure"),
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question,
                                    MessageBoxDefaultButton.Button2);
            if (result3 == DialogResult.Yes)
            {
                mf.bnd.bndBeingMadePts.Clear();
                lblPoints.Text = "Points: 0";
            }
            BtnStop.Focus();
        }

        private void TboxBndOffset_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 50, mf.bnd.createBndOffset, this, mf.Decimals, true, mf.Unit2Mtr, mf.Mtr2Unit))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxBndOffset.Text = ((mf.bnd.createBndOffset = form.ReturnValue) * mf.Mtr2Unit).ToString(mf.GuiFix);
                }
            }
            BtnStop.Focus();
        }

        private void BtnPausePlay_Click(object sender, EventArgs e)
        {
            mf.bnd.isOkToAddPoints = !mf.bnd.isOkToAddPoints;
            BtnAddPoint.Enabled = !mf.bnd.isOkToAddPoints;
            BtnDeleteLast.Enabled = !mf.bnd.isOkToAddPoints;

            BtnPausePlay.Image = mf.bnd.isOkToAddPoints ? Properties.Resources.boundaryPause : Properties.Resources.BoundaryRecord;
            BtnPausePlay.Text = mf.bnd.isOkToAddPoints ? String.Get("gsPause") : String.Get("gsRecord");

            mf.Focus();
        }

        private void BtnOpenGoogleEarth_Click(object sender, EventArgs e)
        {
            //save new copy of kml with selected flag and view in GoogleEarth
          
            mf.FileMakeKMLFromCurrentPosition(mf.Latitude, mf.Longitude);
            System.Diagnostics.Process.Start(mf.fieldsDirectory + mf.currentFieldDirectory + "\\CurrentPosition.KML");
            Close();
        }

        private void BtnGo_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;

            BtnPausePlay.Image = mf.bnd.isOkToAddPoints ? Properties.Resources.boundaryPause : Properties.Resources.BoundaryRecord;

            mf.bnd.isBndBeingMade = true;

            if (mf.isMetric)
                lblArea.Text = String.Get("gsArea") + ": 0 Ha";
            else
                lblArea.Text = String.Get("gsArea") + ": 0 Acre";

            lblPoints.Text = "Points: 0";

            ShowPanel.Visible = false;
            ChoosePanel.Visible = false;
            DrivePanel.Visible = true;
            DrivePanel.Location = new Point(0, 0);
            Size = new Size(296, 559);
        }

        private void BtnDeleteAll_Click(object sender, EventArgs e)
        {
            DialogResult result3 = MessageBox.Show(String.Get("gsCompletelyDeleteBoundary"),
                String.Get("gsDeleteForSure"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result3 == DialogResult.Yes)
            {

                ResetAllBoundary();

                mf.bnd.boundarySelected = -1;

                BtnDelete.Enabled = false;

                mf.bnd.isOkToAddPoints = false;

                mf.bnd.BtnHeadLand = false;
            }
        }

        private void Up_Scroll_Click(object sender, EventArgs e)
        {
            Position -= rowheight;

            if (Position < 0) Position = 0;

            UpdateScroll(-1);
        }

        private void Down_Scroll_Click(object sender, EventArgs e)
        {
            Position += rowheight;
            if (Position > contentHeight - panel1.Size.Height) Position = contentHeight - panel1.Size.Height;
            UpdateScroll(-1);
        }

        void MouseWheel_Scroll(object sender, MouseEventArgs e)
        {
            Position -= e.Delta;

            if (Position < 0) Position = 0;
            else if (Position > contentHeight - panel1.Size.Height) Position = contentHeight - panel1.Size.Height;

            UpdateScroll(-1);
        }

        void Mouse_Down(object sender, MouseEventArgs e)
        {
            oldY = MousePosition.Y;
            scroll = true;
        }

        void Mouse_Up(object sender, MouseEventArgs e)
        {
            scroll = false;
        }

        void Mouse_Leave(object sender, EventArgs e)
        {
            scroll = false;
        }

        void Mouse_Move(object sender, MouseEventArgs e)
        {
            if (scroll == true && viewableRatio < 1)
            {
                if (oldY != MousePosition.Y)
                {
                    int diff = MousePosition.Y - oldY;

                    if (Slider_Scroll.Location.Y + diff > SliderStartY)
                    {
                        if ((Slider_Scroll.Location.Y + diff) < (SliderStartY + SliderMaxHeight - thumbHeight))
                        {
                            oldY += diff;
                            UpdateScroll(Slider_Scroll.Location.Y + diff - SliderStartY);
                        }
                        else
                        {
                            int tt = (int)((Slider_Scroll.Location.Y + diff) - (SliderStartY + SliderMaxHeight - thumbHeight));

                            oldY += diff - tt;


                            UpdateScroll(SliderMaxHeight - thumbHeight);
                        }
                    }
                    else
                    {
                        int tt = (int)((Slider_Scroll.Location.Y + diff) - SliderStartY);

                        oldY += diff - tt;

                        UpdateScroll(0);
                    }
                }
            }

        }

        private void BtnLoadBoundaryFromGE_Click(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                string fileAndDirectory;
                {
                    //create the dialog instance
                    using (OpenFileDialog ofd = new OpenFileDialog
                    {
                        //set the filter to text KML only
                        Filter = "KML files (*.KML)|*.KML",

                        //the initial directory, fields, for the open dialog
                        InitialDirectory = mf.fieldsDirectory + mf.currentFieldDirectory
                    })
                    {
                        //was a file selected
                        if (ofd.ShowDialog(this) == DialogResult.Cancel) return;
                        else fileAndDirectory = ofd.FileName;
                    }
                }

                //start to read the file
                string line;
                string coordinates = null;
                int startIndex;
                int i = mf.bnd.Boundaries.Count;

                using (System.IO.StreamReader reader = new System.IO.StreamReader(fileAndDirectory))
                {
                    try
                    {
                        while (!reader.EndOfStream)
                        {
                            line = reader.ReadLine();

                            startIndex = line.IndexOf("<coordinates>");

                            if (startIndex != -1)
                            {
                                while (true)
                                {
                                    int endIndex = line.IndexOf("</coordinates>");

                                    if (endIndex == -1)
                                    {
                                        //just add the line
                                        if (startIndex == -1) coordinates += line.Substring(0);
                                        else coordinates += line.Substring(startIndex + 13);
                                    }
                                    else
                                    {
                                        if (startIndex == -1) coordinates += line.Substring(0, endIndex);
                                        else coordinates += line.Substring(startIndex + 13, endIndex - (startIndex + 13));
                                        break;
                                    }
                                    line = reader.ReadLine();
                                    line = line.Trim();
                                    startIndex = -1;
                                }

                                line = coordinates;
                                char[] delimiterChars = { ' ', '\t', '\r', '\n' };
                                string[] numberSets = line.Split(delimiterChars);

                                //at least 3 points
                                if (numberSets.Length > 2)
                                {
                                    CBoundaryLines newbnd = new CBoundaryLines();
                                    double LatMax = 0, LatMin = 0, LonMax = 0, LonMin = 0;
                                    List<Vec2> test = new List<Vec2>();
                                    bool Start = true;
                                    foreach (var item in numberSets)
                                    {
                                        string[] fix = item.Split(',');
                                        if (fix.Length > 1)
                                        {
                                            double Lon = double.Parse(fix[0], CultureInfo.InvariantCulture);
                                            double Lat = double.Parse(fix[1], CultureInfo.InvariantCulture);

                                            if (i == 0)
                                            {
                                                if (Start)
                                                {
                                                    LatMax = LatMin = Lat;
                                                    LonMax = LonMin = Lon;
                                                    Start = false;
                                                }
                                                if (LatMax < Lat) LatMax = Lat;
                                                if (LatMin > Lat) LatMin = Lat;
                                                if (LonMax < Lon) LonMax = Lon;
                                                if (LonMin > Lon) LonMin = Lon;
                                                test.Add(new Vec2(Lon, Lat));
                                            }
                                            else
                                            {
                                                mf.ConvertWGS84ToLocal(Lat, Lon, out double Northing, out double Easting);
                                                newbnd.Polygon.Points.Add(new Vec2(Easting, Northing));
                                            }
                                        }
                                    }

                                    if (i == 0)
                                    {
                                        mf.SetPlaneToLocal((LatMax + LatMin) /2, (LonMax + LonMin) /2);

                                        for (int j = 0; j < test.Count; j++)
                                        {
                                            mf.ConvertWGS84ToLocal(test[j].Northing, test[j].Easting, out double Northing, out double Easting);
                                            newbnd.Polygon.Points.Add(new Vec2(Easting, Northing));
                                        }
                                    }

                                    mf.bnd.Boundaries.Add(newbnd);
                                    mf.StartTasks(newbnd, mf.bnd.Boundaries.Count - 1, TaskName.Boundary);
                                    
                                    coordinates = "";
                                    i++;
                                }
                                else
                                {
                                    mf.TimedMessageBox(2000, String.Get("gsErrorreadingKML"), String.Get("gsChooseBuildDifferentone"));
                                }
                                if (button.Name == "BtnLoadBoundaryFromGE")
                                {
                                    break;
                                }
                            }
                        }
                        mf.StartTasks(null, 1, TaskName.Save);

                        UpdateChart();
                        UpdateScroll(-1);
                    }
                    catch (Exception)
                    {
                    }
                }
                ShowPanel.Visible = true;
                ChoosePanel.Visible = false;
                DrivePanel.Visible = false;
                Size = new Size(566, 559);
            }
        }
    }
}
