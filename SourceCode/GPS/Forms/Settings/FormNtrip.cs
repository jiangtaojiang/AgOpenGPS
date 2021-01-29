using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class FormNtrip : Form
    {
        //class variables
        private readonly FormGPS mf;
        private int udpport = 0, casterport = 0, ggainterval = 0;
        private double latitude, longitude;

        public FormNtrip(Form callingForm)
        {
            Owner = mf = callingForm as FormGPS;
            InitializeComponent();

            this.groupBox2.Text = String.Get("gsNetworking");
            this.cboxIsNTRIPOn.Text = String.Get("gsNTRIPOn");
            this.label6.Text = String.Get("gsPort");
            this.label4.Text = String.Get("gsEnterBroadcasterURLOrIP");
            this.label7.Text = String.Get("gsToUDPPort");

            this.label3.Text = String.Get("gsUsername");
            this.label12.Text = String.Get("gsPassword");
            this.label13.Text = String.Get("gsMount");
            this.label15.Text = String.Get("gsGGAIntervalSecs");
            this.btnGetIP.Text = String.Get("gsConfirmIP");

            this.label9.Text = String.Get("gsCurrentGPSFix");
            this.label17.Text = String.Get("gsSendToManualFix");
            this.btnSetManualPosition.Text = String.Get("gsSendToManualFix");
            this.label18.Text = String.Get("gsSetToZeroForSerial");
            this.btnGetSourceTable.Text = String.Get("gsGetSourceTable");

            this.label1.Text = String.Get("gsRestartRequired");
            this.label19.Text = String.Get("gsZeroEqualsOff");

            this.Text = String.Get("gsNTRIPClientSettings");

        }

        private void FormNtrip_Load(object sender, EventArgs e)
        {
            string hostName = Dns.GetHostName(); // Retrieve the Name of HOST
            tboxHostName.Text = hostName;

            //IPAddress[] ipaddress = Dns.GetHostAddresses(hostName);
            tboxThisIP.Text = GetIP4Address();

            tboxEnterURL.Text = Properties.Settings.Default.setNTRIP_casterURL;

            tboxCasterIP.Text = Properties.Settings.Default.setNTRIP_casterIP;



            cboxIsNTRIPOn.Checked = Properties.Settings.Default.setNTRIP_isOn;

            tboxUserName.Text = Properties.Settings.Default.setNTRIP_userName;
            tboxUserPassword.Text = Properties.Settings.Default.setNTRIP_userPassword;
            tboxMount.Text = Properties.Settings.Default.setNTRIP_mount;


            TboxGGAInterval.Text = (ggainterval = Properties.Settings.Default.setNTRIP_sendGGAInterval).ToString();
            TboxUDPPort.Text = (udpport = Properties.Settings.Default.setNTRIP_sendToUDPPort).ToString();
            TboxCasterPort.Text = (casterport = Properties.Settings.Default.setNTRIP_casterPort).ToString();


            TboxLatitude.Text = (latitude = Properties.Settings.Default.setNTRIP_manualLat).ToString("N7");
            TboxLongitude.Text = (longitude = Properties.Settings.Default.setNTRIP_manualLon).ToString("N7");

            tboxCurrentLat.Text = (mf.Latitude + 0).ToString("N7");
            tboxCurrentLon.Text = (mf.Longitude + 0).ToString("N7");

            checkBoxusetcp.Checked = Properties.Settings.Default.setNTRIP_isTCP;

            if (Properties.Settings.Default.setNTRIP_isGGAManual) cboxGGAManual.Text = "Use Manual Fix";
            else cboxGGAManual.Text = "Use GPS Fix";

            if (Properties.Settings.Default.setNTRIP_isHTTP10) cboxHTTP.Text = "1.0";
            else cboxHTTP.Text = "1.1";

        }

        //get the ipv4 address only
        public static string GetIP4Address()
        {
            string IP4Address = string.Empty;

            foreach (IPAddress IPA in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (IPA.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP4Address = IPA.ToString();
                    break;
                }
            }

            return IP4Address;
        }

        private void BtnGetIP_Click(object sender, EventArgs e)
        {
            string actualIP = tboxEnterURL.Text.Trim();
            try
            {
                IPAddress[] addresslist = Dns.GetHostAddresses(actualIP);
                tboxCasterIP.Text = "";
                tboxCasterIP.Text = addresslist[0].ToString().Trim();
            }
            catch (Exception)
            {
                mf.TimedMessageBox(1500, "No IP Located", "Can't Find: " + actualIP);
            }
        }

        public bool CheckIPValid(string strIP)
        {
            //  Split string by ".", check that array length is 3
            string[] arrOctets = strIP.Split('.');

            //at least 4 groups in the IP
            if (arrOctets.Length != 4) return false;

            //  Check each substring checking that the int value is less than 255 and that is char[] length is !> 2
            const short MAXVALUE = 255;
            int temp; // Parse returns Int32
            foreach (string strOctet in arrOctets)
            {
                //check if at least 3 digits but not more OR 0 length
                if (strOctet.Length > 3 || strOctet.Length == 0) return false;

                //make sure all digits
                if (!int.TryParse(strOctet, out int temp2)) return false;

                //make sure not more then 255
                temp = int.Parse(strOctet);
                if (temp > MAXVALUE || temp < 0) return false;
            }
            return true;
        }

        private void TboxCasterIP_Validating(object sender, CancelEventArgs e)
        {
            if (!CheckIPValid(tboxCasterIP.Text))
            {
                tboxCasterIP.Text = "127.0.0.1";
                tboxCasterIP.Focus();
                mf.TimedMessageBox(2000, "Invalid IP Address", "Set to Default Local 127.0.0.1");
            }
        }

        private void BtnSerialOK_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.setNTRIP_casterIP = tboxCasterIP.Text;
            Properties.Settings.Default.setNTRIP_casterPort = casterport;

            Properties.Settings.Default.setNTRIP_isOn = cboxIsNTRIPOn.Checked;
            Properties.Settings.Default.setNTRIP_userName = tboxUserName.Text;
            Properties.Settings.Default.setNTRIP_userPassword = tboxUserPassword.Text;
            Properties.Settings.Default.setNTRIP_mount = tboxMount.Text;

            Properties.Settings.Default.setNTRIP_sendGGAInterval = ggainterval;
            Properties.Settings.Default.setNTRIP_sendToUDPPort = udpport;
            Properties.Settings.Default.setNTRIP_manualLat = latitude;
            Properties.Settings.Default.setNTRIP_manualLon = longitude;

            Properties.Settings.Default.setNTRIP_casterURL = tboxEnterURL.Text;
            Properties.Settings.Default.setNTRIP_isGGAManual = cboxGGAManual.Text == "Use Manual Fix";
            Properties.Settings.Default.setNTRIP_isHTTP10 = cboxHTTP.Text == "1.0";
            Properties.Settings.Default.setNTRIP_isTCP = checkBoxusetcp.Checked;

            Properties.Settings.Default.Save();

            mf.UpdateNtripButton();
            Close();
        }

        private void BtnSetManualPosition_Click(object sender, EventArgs e)
        {
            TboxLatitude.Text = (latitude = Math.Round(mf.Latitude,7)).ToString("N7");
            TboxLongitude.Text = (longitude = Math.Round(mf.Longitude,7)).ToString("N7");
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            tboxCurrentLat.Text = (mf.Latitude + 0).ToString("N7");
            tboxCurrentLon.Text = (mf.Longitude + 0).ToString("N7");
        }

        public List<string> DataList { get; set; } = new List<string>();

        private void BtnGetSourceTable_Click(object sender, EventArgs e)
        {
            IPAddress casterIP = IPAddress.Parse(tboxCasterIP.Text.Trim()); //Select correct Address

            Socket sckt;
            DataList.Clear();

            try
            {
                sckt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = true
                };
                sckt.Connect(new IPEndPoint(casterIP, casterport));

                string msg = "GET / HTTP/1.0\r\n" + "User-Agent: NTRIP iter.dk\r\n" +
                                    "Accept: */*\r\nConnection: close\r\n" + "\r\n";

                //Send request
                byte[] data = System.Text.Encoding.ASCII.GetBytes(msg);
                sckt.Send(data);
                int bytes = 0;
                byte[] bytesReceived = new byte[1024];
                string page = string.Empty;
                Thread.Sleep(200);

                do
                {
                    bytes = sckt.Receive(bytesReceived, bytesReceived.Length, SocketFlags.None);
                    page += Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                }
                while (bytes > 0);

                if (page.Length > 0)
                {
                    string[] words = page.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < words.Length; i++)
                    {
                        string [] words2 = words[i].Split(';');

                        if (words2[0] == "STR")
                        {
                            DataList.Add(words2[1].Trim().ToString() + "," + words2[9].ToString() + "," + words2[10].ToString()
                          + "," + words2[3].Trim().ToString() + "," + words2[6].Trim().ToString()
                                );
                        }
                    }
                }
            }

            catch (SocketException)
            {
                mf.TimedMessageBox(2000, "Socket Exception", "Invalid IP:Port");
                return;
            }

            catch (Exception)
            {
                mf.TimedMessageBox(2000, "Exception", "Get Source Table Error");
                return;
            }

            if (DataList.Count > 0)
            {
                string syte = "http://monitor.use-snip.com/?hostUrl=" + tboxCasterIP.Text + "&port=" + casterport.ToString();
                var form = new FormSource(this, DataList, mf.Latitude, mf.Longitude, syte);
                form.ShowDialog(this);
            }
            else
            {
                mf.TimedMessageBox(2000, "Error", "No Source Data");
            }
        }

        private void TboxEnterURL_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void TboxMount_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void TboxUserName_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void TboxUserPassword_Click(object sender, EventArgs e)
        {
            if (mf.isKeyboardOn)
            {
                mf.KeyboardToText((TextBox)sender, this);
                btnSerialCancel.Focus();
            }
        }

        private void BtnPassUsername_Click(object sender, EventArgs e)
        {
            if (tboxUserName.PasswordChar == '*') tboxUserName.PasswordChar = '\0';
            else tboxUserName.PasswordChar = '*';
            tboxUserName.Invalidate();
        }

        private void BtnPassPassword_Click(object sender, EventArgs e)
        {
            if (tboxUserPassword.PasswordChar == '*') tboxUserPassword.PasswordChar = '\0';
            else tboxUserPassword.PasswordChar = '*';
            tboxUserPassword.Invalidate();
        }

        private void BtnSerialCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void TboxUDPPort_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 65535, udpport, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxUDPPort.Text = (udpport = (int)form.ReturnValue).ToString();
                }
            }
            btnSerialCancel.Focus();
        }

        private void TboxLatitude_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-90, 90, latitude, this, 7, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLatitude.Text = (latitude = form.ReturnValue).ToString("N7");
                }
            }
            btnSerialCancel.Focus();
        }

        private void TboxLongitude_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(-180, 180, longitude, this, 7, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxLatitude.Text = (longitude = form.ReturnValue).ToString("N7");
                }
            }
            btnSerialCancel.Focus();
        }

        private void TboxGGAInterval_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 600, ggainterval, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxGGAInterval.Text = (ggainterval = (int)form.ReturnValue).ToString();
                }
            }
            btnSerialCancel.Focus();
        }

        private void TboxCasterPort_Enter(object sender, EventArgs e)
        {
            using (var form = new FormNumeric(0, 65535, casterport, this, 0, false))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    TboxCasterPort.Text = (casterport = (int)form.ReturnValue).ToString();
                }
            }
            btnSerialCancel.Focus();

        }
    }
}