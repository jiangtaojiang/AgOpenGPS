using System;
using System.IO;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormJob : Form
    {
        //class variables
        private readonly FormGPS mf;

        public FormJob(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;

            InitializeComponent();

            btnJobOpen.Text = String.Get("gsOpen");
            btnJobNew.Text = String.Get("gsNew");
            btnJobResume.Text = String.Get("gsResume");
            label1.Text = String.Get("gsLastFieldUsed");
            Text = String.Get("gsStartNewField");
        }

        private void FormJob_Load(object sender, EventArgs e)
        {
            string fileAndDirectory = mf.fieldsDirectory + Properties.Settings.Default.setF_CurrentDir + "\\Boundary.txt";

            if (string.IsNullOrEmpty(Properties.Settings.Default.setF_CurrentDir) && !File.Exists(fileAndDirectory))
            {
                textBox1.Text = "";
                btnJobResume.Enabled = false;
                mf.currentFieldDirectory = "";
                Properties.Settings.Default.setF_CurrentDir = "";
                Properties.Settings.Default.Save();
            }
            else
            {
                textBox1.Text = Properties.Settings.Default.setF_CurrentDir;
            }
        }

        private void BtnJobOpen_Click(object sender, EventArgs e)
        {
            using (var form = new FormFilePicker(mf))
            {
                var result = form.ShowDialog(mf);

                if (result == DialogResult.Yes)
                {
                    DialogResult = result;
                    Close();
                }
            }
        }

        private void BtnInField_Click(object sender, EventArgs e)
        {
            string infieldList = "";
            int numFields = 0;

            for (int i = 0; i < mf.Fields.Count; i++)
            {
                if (mf.Fields[i].Eastingmin <= mf.pn.fix.Easting && mf.Fields[i].Eastingmax >= mf.pn.fix.Easting && mf.Fields[i].Northingmin <= mf.pn.fix.Northing && mf.Fields[i].Northingmax >= mf.pn.fix.Northing)
                {
                    bool oddNodes = false;
                    int k = mf.Fields[i].Polygon.Points.Count - 1;
                    for (int j = 0; j < mf.Fields[i].Polygon.Points.Count; j++)
                    {
                        if ((mf.Fields[i].Polygon.Points[j].Northing < mf.pn.fix.Northing && mf.Fields[i].Polygon.Points[k].Northing >= mf.pn.fix.Northing
                        || mf.Fields[i].Polygon.Points[k].Northing < mf.pn.fix.Northing && mf.Fields[i].Polygon.Points[j].Northing >= mf.pn.fix.Northing)
                        && (mf.Fields[i].Polygon.Points[j].Easting <= mf.pn.fix.Easting || mf.Fields[i].Polygon.Points[k].Easting <= mf.pn.fix.Easting))
                        {
                            oddNodes ^= (mf.Fields[i].Polygon.Points[j].Easting + (mf.pn.fix.Northing - mf.Fields[i].Polygon.Points[j].Northing) /
                            (mf.Fields[i].Polygon.Points[k].Northing - mf.Fields[i].Polygon.Points[j].Northing) * (mf.Fields[i].Polygon.Points[k].Easting - mf.Fields[i].Polygon.Points[j].Easting) < mf.pn.fix.Easting);
                        }
                        k = j;
                    }

                    if (oddNodes)
                    {
                        numFields++;
                        if (string.IsNullOrEmpty(infieldList))
                            infieldList += Path.GetFileName(mf.Fields[i].Dir);
                        else
                            infieldList += "," + Path.GetFileName(mf.Fields[i].Dir);
                    }
                }
            }
            if (!string.IsNullOrEmpty(infieldList))
            {
                if (numFields > 1)
                {
                    using (var form = new FormDrivePicker(mf, this, infieldList))
                    {
                        var result = form.ShowDialog(this);
                        if (result == DialogResult.Yes)
                        {
                            DialogResult = DialogResult.Yes;
                            Close();
                        }
                    }
                }
                else // 1 field found
                {
                    mf.currentFieldDirectory = infieldList;
                    DialogResult = DialogResult.Yes;
                    Close();
                }
            }
            else //no fields found
            {
                mf.TimedMessageBox(2000, String.Get("gsNoFieldsFound"), String.Get("gsFieldNotOpen"));
            }
        }

        private void BtnJobResume_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.setF_CurrentDir))
            {
                mf.currentFieldDirectory = Properties.Settings.Default.setF_CurrentDir;
                DialogResult = DialogResult.Yes;
            }
        }
    }
}