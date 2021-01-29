using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormFilePicker : Form
    {
        private readonly FormGPS mf;

        private int order;

        public List<string> FileList { get; set; } = new List<string>();

        public FormFilePicker(Form callingForm)
        {
            //get copy of the calling main form
            Owner = mf = callingForm as FormGPS;

            InitializeComponent();
            btnByDistance.Text = String.Get("gsSort");
            btnOpenExistingLv.Text = String.Get("gsUseSelected");
        }

        private void FormFilePicker_Load(object sender, EventArgs e)
        {
            order = 0;
            FileList.Clear();

            for (int i = 0; i < mf.Fields.Count; i++)
            {
                double Distance = Math.Pow(mf.Fields[i].LatStart - mf.Latitude, 2) + Math.Pow(mf.Fields[i].LonStart - mf.Longitude, 2);
                Distance = Math.Sqrt(Distance);
                Distance *= 100;

                FileList.Add(mf.Fields[i].Dir);
                FileList.Add(Distance.ToString("####0.##").PadLeft(10));
                if (mf.Fields[i].Area == 0) FileList.Add("No Bndry");
                else FileList.Add(mf.Fields[i].Area.ToString("###0.##").PadLeft(10));
            }

            for (int i = 0; i < FileList.Count - 2; i += 3)
            {
                string[] fieldNames = { FileList[i], FileList[i + 1], FileList[i + 2] };
                lvLines.Items.Add(new ListViewItem(fieldNames));
            }

            if (lvLines.Items.Count > 0)
            {
                this.chName.Text = "Field Name";
                this.chName.Width = 680;

                this.chDistance.Text = "Distance";
                this.chDistance.Width = 140;

                this.chArea.Text = "Area";
                this.chArea.Width = 140;
            }
        }

        private void BtnByDistance_Click(object sender, EventArgs e)
        {
            ListViewItem itm;

            lvLines.Items.Clear();
            order += 1;
            if (order == 3) order = 0;

            for (int i = 0; i < FileList.Count-2; i += 3)
            {
                if (order == 0)
                {
                    string[] fieldNames = { FileList[i], FileList[i + 1], FileList[i + 2] };
                    itm = new ListViewItem(fieldNames);
                }
                else if (order == 1)
                {
                    string[] fieldNames = { FileList[i + 1], FileList[i], FileList[i + 2] };
                    itm = new ListViewItem(fieldNames);
                }
                else
                {
                    string[] fieldNames = { FileList[i + 2], FileList[i], FileList[i + 1] };
                    itm = new ListViewItem(fieldNames);
                }

                lvLines.Items.Add(itm);
            }

            if (lvLines.Items.Count > 0)
            {
                if (order == 0)
                {
                    this.chName.Text = "Field Name";
                    this.chName.Width = 680;

                    this.chDistance.Text = "Distance";
                    this.chDistance.Width = 140;

                    this.chArea.Text = "Area";
                    this.chArea.Width = 140;
                }
                else if (order == 1)
                {
                    this.chName.Text = "Distance";
                    this.chName.Width = 140;

                    this.chDistance.Text = "Field Name";
                    this.chDistance.Width = 680;

                    this.chArea.Text = "Area";
                    this.chArea.Width = 140;
                }

                else
                {
                    this.chName.Text = "Area";
                    this.chName.Width = 140;

                    this.chDistance.Text = "Field Name";
                    this.chDistance.Width = 680;

                    this.chArea.Text = "Distance";
                    this.chArea.Width = 140;
                }


            }
        }

        private void BtnOpenExistingLv_Click(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;
            if (count > 0)
            {
                if (lvLines.SelectedItems[0].SubItems[0].Text == "Error" ||
                    lvLines.SelectedItems[0].SubItems[1].Text == "Error" ||
                    lvLines.SelectedItems[0].SubItems[2].Text == "Error")
                {
                    MessageBox.Show("This Field is Damaged, Please Delete \r\n ALREADY TOLD YOU THAT :)", String.Get("gsFileError"),
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    mf.currentFieldDirectory = lvLines.SelectedItems[0].SubItems[(order == 0) ? 0 : 1].Text;
                    DialogResult = DialogResult.Yes;
                    Close();
                }
            }
        }
    }
}