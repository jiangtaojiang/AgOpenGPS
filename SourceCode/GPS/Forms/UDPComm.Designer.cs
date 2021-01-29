using System;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        // Send and Recv socket
        private Socket sendSocket;
        private Socket recvSocket;
        public bool isUDPSendConnected;
        public int autoSteerUDPActivity, machineUDPActivity, switchUDPActivity;

        //IP address and port of Auto Steer server
        IPAddress epIP = IPAddress.Parse(Properties.Settings.Default.setIP_autoSteerIP);

        // Data stream
        private byte[] buffer = new byte[1024];

        // - App Sockets  -----------------------------------------------------
        private Socket sendTo_App_Socket;
        private Socket recvFrom_App_Socket;
        
        EndPoint recvFrom_App_EndPoint, SendTo_App_EndPoint, epNTRIP;

        // Data stream
        private byte[] appBuffer = new byte[1024];

        // Status delegate
        private delegate void UpdateStatusDelegate(string status);

        //start the UDP server
        public void StartUDPServer()
        {
            try
            {
                if (isUDPSendConnected) StopUDPServer();

                //epAutoSteer = new IPEndPoint(epIP, Properties.Settings.Default.setIP_autoSteerPort);

                // Initialise the IPEndPoint for the client
                //epSender = new IPEndPoint(IPAddress.Any, 0);


                // Initialise the socket
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                sendSocket.EnableBroadcast = true;
                recvSocket.EnableBroadcast = true;

                // Associate the socket with this IP address and port
                recvSocket.Bind(new IPEndPoint(IPAddress.Any, Properties.Settings.Default.setIP_thisPort));
                sendSocket.Bind(new IPEndPoint(IPAddress.Any, 9998));

                EndPoint client = new IPEndPoint(IPAddress.Any, 0);

                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref client, new AsyncCallback(ReceiveData), recvSocket);
                isUDPSendConnected = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Load Error: " + e.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StopUDPServer()
        {
            try
            {
                if (isUDPSendConnected)
                {
                    sendSocket.Shutdown(SocketShutdown.Both);
                    sendSocket.Close();
                    recvSocket.Shutdown(SocketShutdown.Both);
                    recvSocket.Close();
                }

                isUDPSendConnected = false;
            }
            catch (Exception e)
            {
                MessageBox.Show("Close Error: " + e.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateRecvMessage(int port, byte[] Data)
        {
            if (Data.Length < 4) return;
            //update progress bar for autosteer
            if (port != 5) pbarUDP++;

            if (Data[0] == 0xB5 && Data[1] == 0x62 && Data[2] == 0x01)//Daniel P
            {
                if (Data[3] == 0x07 && Data.Length > 99)//UBX-NAV-PVT
                {
                    int CK_A = 0;
                    int CK_B = 0;

                    for (int j = 2; j < 98; j += 1)// start with Class and end by Checksum
                    {
                        CK_A = (CK_A + Data[j]) & 0xFF;
                        CK_B = (CK_B + CK_A) & 0xFF;
                    }

                    if (Data[98] == CK_A && Data[99] == CK_B)
                    {
                        long itow = Data[6] | (Data[7] << 8) | (Data[8] << 16) | (Data[9] << 24);

                        if ((Data[27] & 0x81) == 0x81)
                        {
                            pn.FixQuality = 4;
                            pn.EnableHeadRoll = true;
                        }
                        else if ((Data[27] & 0x41) == 0x41)
                        {
                            pn.FixQuality = 5;
                            pn.EnableHeadRoll = true;
                        }
                        else
                        {
                            pn.FixQuality = 1;
                            pn.EnableHeadRoll = false;
                        }

                        pn.satellitesTracked = Data[29];

                        Longitude = (Data[30] | (Data[31] << 8) | (Data[32] << 16) | (Data[33] << 24)) * 0.0000001;//to deg
                        Latitude = (Data[34] | (Data[35] << 8) | (Data[36] << 16) | (Data[37] << 24)) * 0.0000001;//to deg

                        //Height above ellipsoid
                        pn.altitude = (Data[38] | (Data[39] << 8) | (Data[40] << 16) | (Data[41] << 24)) * 0.001;//to meters
                        // Height above mean sea level
                        pn.altitude = (Data[42] | (Data[43] << 8) | (Data[44] << 16) | (Data[45] << 24)) * 0.001;//to meters

                        pn.hdop = (Data[46] | (Data[47] << 8) | (Data[48] << 16) | (Data[49] << 24)) * 0.01;

                        if (Longitude != 0)
                        {
                            pn.speed = (Data[66] | (Data[67] << 8) | (Data[68] << 16) | (Data[69] << 24)) * 0.0036;//to km/h

                            if (vehicle.isReverse && pn.speed > 0) pn.speed *= -1;

                            //average the speed
                            pn.AverageTheSpeed();

                            recvSentenceSettings[2] = recvSentenceSettings[0];
                            recvSentenceSettings[0] = "$UBX-PVT, Longitude = " + Longitude.ToString("N8", CultureInfo.InvariantCulture) + ", Latitude = " + Latitude.ToString("N8", CultureInfo.InvariantCulture) + ", Altitude = " + pn.altitude.ToString("N3", CultureInfo.InvariantCulture) + ", itow = " + itow.ToString();

                            UpdateFixPosition();
                        }
                        else
                        {
                            pn.EnableHeadRoll = false;
                            pn.FixQuality = 0;
                            recvSentenceSettings[2] = recvSentenceSettings[0];
                            recvSentenceSettings[0] = "$UBX-PVT, Longitude = ???, Latitude = ???, Altitude = ???, itow = " + itow.ToString();
                        }
                    }
                }
                else if (Data[3] == 0x3C && Data.Length > 71)//Daniel P
                {
                    int CK_A = 0;
                    int CK_B = 0;
                    for (int j = 2; j < 70; j += 1)// start with Class and end by Checksum
                    {
                        CK_A = (CK_A + Data[j]) & 0xFF;
                        CK_B = (CK_B + CK_A) & 0xFF;
                    }

                    if (Data[70] == CK_A && Data[71] == CK_B)
                    {
                        long itow = Data[10] | (Data[11] << 8) | (Data[12] << 16) | (Data[13] << 24);
                        if (pn.EnableHeadRoll && ((Data[67] & 0x01) == 0x01) && (((Data[66] & 0x2D) == 0x2D) || ((Data[66] & 0x35) == 0x35)))
                        {
                            int relposlength = Data[26] | (Data[27] << 8) | (Data[28] << 16) | (Data[29] << 24);//in cm!

                            if ((DualAntennaDistance - 0.05)*100.0 < relposlength && relposlength < (DualAntennaDistance + 0.05) * 100.0)
                            {
                                double RelPosN = ((Data[14] | (Data[15] << 8) | (Data[16] << 16) | (Data[17] << 24)) + Data[38] * 0.01);
                                double RelPosE = ((Data[18] | (Data[19] << 8) | (Data[20] << 16) | (Data[21] << 24)) + Data[39] * 0.01);
                                double relPosD = ((Data[22] | (Data[23] << 8) | (Data[24] << 16) | (Data[25] << 24)) + Data[40] * 0.01);

                                ahrs.rollX16 = (int)(Math.Atan2(relPosD, Math.Sqrt(RelPosN * RelPosN + RelPosE * RelPosE)) * 916.732472209);
                            }

                            pn.HeadingForced = (Data[30] | (Data[31] << 8) | (Data[32] << 16) | (Data[33] << 24)) * 0.00001 + DualHeadingCorrection;

                            recvSentenceSettings[3] = recvSentenceSettings[1];
                            recvSentenceSettings[1] = "$UBX-RELPOSNED, Heading = " + pn.HeadingForced.ToString("N4", CultureInfo.InvariantCulture) + ", Roll = " + (ahrs.rollX16 / 16.0).ToString("N4", CultureInfo.InvariantCulture) + ", itow = " + itow.ToString();
                        }
                        else //Bad Quality
                        {
                            ahrs.rollX16 = 9999;
                            pn.HeadingForced = 9999;
                            recvSentenceSettings[3] = recvSentenceSettings[1];
                            recvSentenceSettings[1] = "$UBX-RELPOSNED, Heading = 9999, Roll = 9999, itow = " + itow.ToString();
                        }
                    }
                }
                return;
            }
            else if (Data[0] == 0x24)//if it starts with a $, its an nmea sentence
            {
                BeginInvoke((MethodInvoker)(() => pn.ParseNMEA(Encoding.ASCII.GetString(Data))));

                if (isLogNMEA)
                {
                    pn.logNMEASentence.Append(Encoding.ASCII.GetString(Data));
                }
                return;
            }
            else
            {
                if (Data[1] == 0xE6)
                {
                    //quick check
                    if (Data.Length != 10) return;
                    checksumRecd = Data[2];

                    if (Data[3] != inoVersionInt)
                    {
                        Form af = Application.OpenForms["FormSteer"];

                        if (af != null)
                        {
                            af.Focus();
                            af.Close();
                        }

                        af = Application.OpenForms["FormArduinoSettings"];

                        if (af != null)
                        {
                            af.Focus();
                            af.Close();
                        }

                        //spAutoSteer.Close();
                        MessageBox.Show("Arduino INO Is Wrong Version \r\n Upload AutoSteer_" + currentVersionStr + ".ino", String.Get("gsFileError"),
                                            MessageBoxButtons.OK, MessageBoxIcon.Question);
                        Close();
                    }
                    else if (checksumRecd != checksumSent)
                    {
                        MessageBox.Show(
                            "Sent: " + checksumSent + "\r\n Recieved: " + checksumRecd,
                                "Checksum Error",
                                        MessageBoxButtons.OK, MessageBoxIcon.Question);
                    }
                    return;
                }

                if (Data[0] == 0x7F && Data.Length > 2)
                {
                    if (Data.Length < Data[2]) return;

                    for (int i = 7; i > 0; i--) DataRecieved[i] = DataRecieved[i - 1];


                    if (Data[1] == 0xC0 && Data.Length > 5)
                    {
                        if (port != 5) autoSteerUDPActivity++;
                        int steer = (Int16)((Data[3] << 8) + Data[4]);
                        actualSteerAngleDisp = steer;

                        mc.pwmDisplay = Data[5];
                        DataRecieved[0] = "Actual Steer Angle: Steer Angle " + steer.ToString() + ", PWM " + Data[5].ToString();
                    }
                    else if (Data[1] == 0xC1 && Data.Length > 5)//Section Control
                    {
                        if (port != 5) machineUDPActivity++;

                        DataRecieved[8] = "Sections Control: Tool " + (Data[3] + 1).ToString() + ", Section " + (Data[4] + 1).ToString() + ", State " + ((Data[5] & 2) == 2 ? "On" : (Data[5] & 1) == 1 ? "Auto" : "Off");

                        for (int j = (Data[3] == 0xFF ? 0 : Data[3]); j < Tools.Count && (Data[3] == 0xFF ? true : j <= Data[3]); j++)
                        {
                            for (int k = (Data[4] == 0xFF ? 0 : Data[4]); k < Tools[j].Sections.Count && (Data[4] == 0xFF ? true : k <= Data[4]); k++)
                            {
                                if ((Data[5] & 2) == 2 && autoBtnState != 0)
                                {
                                    if (Tools[j].Sections[k].BtnSectionState != btnStates.On) Tools[j].Sections[k].BtnSectionState = btnStates.On;
                                }
                                else if (Data[5] == 1 && autoBtnState != 0)
                                {
                                    if (Tools[j].Sections[k].BtnSectionState != btnStates.Auto) Tools[j].Sections[k].BtnSectionState = btnStates.Auto;
                                }
                                else
                                {
                                    if (Tools[j].Sections[k].BtnSectionState != btnStates.Off) Tools[j].Sections[k].BtnSectionState = btnStates.Off;
                                }
                                Tools[j].SectionButtonColor(k);
                            }
                        }


                        /*
                        if (port != 5) machineUDPActivity++;

                        int Cnt = Data[3];//Sections * 2.0    /4 is bytes
                        int SectionIndex = 0;
                        int ToolIndex = 0;
                        int i = 0;
                        int bit = 0;

                        while (i+4 < Data[2])
                        {
                            if (ToolIndex < Tools.Count && SectionIndex < Tools[ToolIndex].numOfSections)
                            {
                                byte ff = Data[4 + i];
                                if ((ff & (1 << bit + 1)) == (1 << bit + 1) && autoBtnState != 0)
                                {
                                    if (Tools[ToolIndex].Sections[SectionIndex].BtnSectionState != btnStates.On)
                                    {
                                        Tools[ToolIndex].Sections[SectionIndex].BtnSectionState = btnStates.On;
                                        Tools[ToolIndex].SectionButtonColor(SectionIndex);
                                    }
                                }
                                else if ((ff & (1 << bit)) == (1 << bit) && autoBtnState != 0)
                                {
                                    if (Tools[ToolIndex].Sections[SectionIndex].BtnSectionState != btnStates.Auto)
                                    {
                                        Tools[ToolIndex].Sections[SectionIndex].BtnSectionState = btnStates.Auto;
                                        Tools[ToolIndex].SectionButtonColor(SectionIndex);
                                    }
                                }
                                else
                                {
                                    if (Tools[ToolIndex].Sections[SectionIndex].BtnSectionState != btnStates.Off)
                                    {
                                        Tools[ToolIndex].Sections[SectionIndex].BtnSectionState = btnStates.Off;
                                        Tools[ToolIndex].SectionButtonColor(SectionIndex);
                                    }
                                }
                                bit += 2;
                                SectionIndex++;

                                if (bit == 8)
                                {
                                    bit = 0;
                                    i++;
                                }
                            }
                            else
                            {
                                SectionIndex = 0;
                                ToolIndex++;
                                if (ToolIndex >= Tools.Count) break;
                            }
                        }
                        DataRecieved[0] = "Sections Control";
                        */
                    }
                    else if (Data[1] == 0xC2 && Data.Length > 6)
                    {
                        int Heading = ((Data[3] << 8) + Data[4]);
                        if (Heading != 9999)
                        {
                            ahrs.correctionHeadingX16 = Heading;
                        }

                        int Roll = ((Data[5] << 8) + Data[6]);
                        if (Roll != 9999)
                        {
                            ahrs.rollX16 = Roll;
                        }

                        DataRecieved[0] = "Heading / Roll: Heading " + Heading.ToString() + ", Roll " + Roll.ToString();
                    }
                    else if (Data[1] == 0xC4 && Data.Length > 3)
                    {
                        if (port != 5) switchUDPActivity++;
                        if ((Data[3] & 16) == 16)//SteerSensorCount
                        {
                            DataRecieved[0] = "Steer Sensor Trigger";

                            isAutoSteerBtnOn = false;
                            btnAutoSteer.Image = isAutoSteerBtnOn ? Properties.Resources.AutoSteerOn : Properties.Resources.AutoSteerOff;
                        }
                        else if (mc.RemoteAutoSteer)
                        {
                            DataRecieved[0] = "Remote Auto Steer: State " + ((Data[3] & 2) == 2 ? "On" : "Off");

                            isAutoSteerBtnOn = (isJobStarted && !recPath.isDrivingRecordedPath && Guidance.BtnGuidanceOn && (Data[3] & 2) == 2) ? true : false;
                            btnAutoSteer.Image = isAutoSteerBtnOn ? Properties.Resources.AutoSteerOn : Properties.Resources.AutoSteerOff;
                        }
                        else DataRecieved[0] = "Remote Auto Steer not turned on!";
                    }
                    else if (Data[1] == 0xC5 && Data.Length > 3)
                    {
                        if (port != 5) switchUDPActivity++;
                        if (isJobStarted && mc.isWorkSwitchEnabled)
                        {
                            DataRecieved[0] = "Remote Work Switch: State " + ((Data[3] & 3) == 3 ? "On" : (Data[3] & 1) == 1 ? "Auto" : "Off");
                            if ((Data[3] & 3) == 3)
                            {
                                if (autoBtnState != FormGPS.btnStates.On) autoBtnState = FormGPS.btnStates.On;
                            }
                            else if ((Data[3] & 1) == 1)
                            {
                                if (autoBtnState != FormGPS.btnStates.Auto) autoBtnState = FormGPS.btnStates.Auto;
                            }
                            else if (autoBtnState != FormGPS.btnStates.Off) autoBtnState = FormGPS.btnStates.Off;

                            btnSection_Update();
                        }
                        else DataRecieved[0] = "Remote Work Switch not turned on!";
                    }
                    else if (Data[1] == 0xC6 && Data.Length > 3)
                    {
                        DataRecieved[0] = "Checksum";
                        checksumRecd = Data[3];

                        if (checksumRecd != checksumSent)
                        {
                            MessageBox.Show("Sent: " + checksumSent + "\r\n Recieved: " + checksumRecd, "Checksum Error", MessageBoxButtons.OK, MessageBoxIcon.Question);
                        }

                        if (Data[4] != inoVersionInt)
                        {
                            Form af = Application.OpenForms["FormSteer"];

                            if (af != null)
                            {
                                af.Focus();
                                af.Close();
                            }

                            af = Application.OpenForms["FormArduinoSettings"];

                            if (af != null)
                            {
                                af.Focus();
                                af.Close();
                            }

                            //spAutoSteer.Close();
                            MessageBox.Show("Arduino INO Is Wrong Version \r\n Upload AutoSteer_UDP_" + currentVersionStr + ".ino", String.Get("gsFileError"),
                                                MessageBoxButtons.OK, MessageBoxIcon.Question);
                            Close();
                        }

                    }
                    else if (Data[1] == 0xC7 && Data.Length > 4)
                    {
                        DataRecieved[0] = "Lidar";
                        mc.lidarDistance = (Int16)((Data[3] << 8) + Data[4]);
                    }
                }
                /*
                
                        //mc.recvUDPSentence = DateTime.Now.ToString() + "," + mc.lidarDistance.ToString();
                string Text = "";
                for (int i = 0; i < Data.Length; i++)
                {
                    Text += ((int)Data[i]).ToString();
                    if (i + 1 < Data.Length) Text += ",";
                }
                mc.serialRecvMachineStr = Text;

                if (pbarMachine++ > 99) pbarMachine = 0;
                switchUDPActivity++;

                */

                //mc.serialRecvAutoSteerStr = Text;
                //if (port == 5) if (pbarSteer++ > 99) pbarSteer = 0;
                //    else autoSteerUDPActivity++;

                return;
            }
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            try
            {/*
                // Receive all data
                int msgLen = recvSocket.EndReceiveFrom(asyncResult, ref epSender);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);

                // Listen for more connections again...
                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epSender, new AsyncCallback(ReceiveData), epSender);

                int port = ((IPEndPoint)epSender).Port;
                // Update status through a delegate

                int Length = Math.Max((localMsg[4] + (localMsg[5] << 8)) + 8, 8);
                int CK_A = 0;
                int CK_B = 0;
                for (int j = 2; j < Length - 2; j++)// start with Class and end by Checksum
                {
                    CK_A = (CK_A + localMsg[j]) & 0xFF;
                    CK_B = (CK_B + CK_A) & 0xFF;
                }

                if (localMsg.Length >= Length && localMsg[Length - 2] == CK_A && localMsg[Length - 1] == CK_B)
                    BeginInvoke((MethodInvoker)(() => UpdateRecvMessage(port, localMsg)));
                */
            }
            catch (Exception)
            {
                //WriteErrorLog("UDP Recv data " + e.ToString());
                //MessageBox.Show("ReceiveData Error: " + e.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendData(IAsyncResult asyncResult)
        {
            try
            {
                sendSocket.EndSend(asyncResult);
            }
            catch (Exception) {}
        }

        // ------------------------------------------------------------------

        public void SendAppData(IAsyncResult asyncResult)
        {
            try
            {
                sendTo_App_Socket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SendData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateSendDataText(string Text)
        {
            for (int i = DataSend.Length - 1; i > 0; i--) DataSend[i] = DataSend[i - 1];
            DataSend[0] = Text;
        }

        public void SendData(byte[] Data, bool Checksum)
        {
            if (sendTo_App_Socket != null)
            {
                try
                {
                    int crc = 0;
                    for (int i = 2; i+1 < Data.Length; i++)
                    {
                        crc += Data[i];
                    }
                    Data[Data.Length - 1] = (byte)crc;

                    if (Data.Length != 0)
                        sendTo_App_Socket.BeginSendTo(Data, 0, Data.Length, SocketFlags.None, SendTo_App_EndPoint, new AsyncCallback(SendAppData), null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Send Error: " + ex.Message, "UDP Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (spAutoSteer.IsOpen)
            {
                try { spAutoSteer.Write(Data, 0, Data.Length); }
                catch (Exception e)
                {
                    WriteErrorLog("Out Steer Port " + e.ToString());
                    SerialPortAutoSteerClose();
                }
            }

            if (spMachine.IsOpen)
            {
                try { spMachine.Write(Data, 0, Data.Length); }
                catch (Exception e)
                {
                    WriteErrorLog("Out Machine Port " + e.ToString());
                    SerialPortMachineClose();
                }
            }

            //send out to udp network
            if (Properties.Settings.Default.setUDP_isOn)
            {
                if (isUDPSendConnected)
                {
                    try
                    {
                        if (Data.Length != 0)
                        {
                            //sendSocket.BeginSendTo(Data, 0, Data.Length, 0, epAutoSteer, new AsyncCallback(SendData), null);
                        }
                    }
                    catch (Exception) { }
                }
            }

            if (Checksum)
            {
                int tt = Data[2];
                checksumSent = 0;
                for (int i = 3; i < Data[2]; i++)
                {
                    checksumSent += Data[i];
                }
            }
        }

        public void StartLocalUDPServer()
        {
            try
            {
                sendTo_App_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendTo_App_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                sendTo_App_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                sendTo_App_Socket.Bind(new IPEndPoint(IPAddress.Loopback, 15550));
                SendTo_App_EndPoint = new IPEndPoint(IPAddress.Loopback, 17777);
                //15550 --> 17777

                recvFrom_App_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvFrom_App_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                recvFrom_App_Socket.Bind(new IPEndPoint(IPAddress.Loopback, 15555));
                recvFrom_App_EndPoint = new IPEndPoint(IPAddress.Loopback, 17770);
                recvFrom_App_Socket.BeginReceiveFrom(appBuffer, 0, appBuffer.Length, SocketFlags.None, ref recvFrom_App_EndPoint, new AsyncCallback(ReceiveAppData), recvFrom_App_Socket);
                //17770 --> 15555
            }
            catch (Exception ex)
            {
                MessageBox.Show("Load Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReceiveAppData(IAsyncResult asyncResult)
        {
            try
            {
                // Receive all data
                int msgLen = recvFrom_App_Socket.EndReceiveFrom(asyncResult, ref recvFrom_App_EndPoint);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(appBuffer, localMsg, msgLen);

                // Listen for more connections again...
                recvFrom_App_Socket.BeginReceiveFrom(appBuffer, 0, appBuffer.Length, SocketFlags.None, ref recvFrom_App_EndPoint, new AsyncCallback(ReceiveAppData), recvFrom_App_Socket);

                int port = ((IPEndPoint)recvFrom_App_EndPoint).Port;
                if (localMsg.Length > 5)
                {
                    int Length = Math.Max((localMsg[4]) + 6, 6);
                    if (localMsg.Length >= Length)
                    {
                        int CK_A = 0;
                        int CK_B = 0;
                        for (int j = 2; j < Length - 2; j++)
                        {
                            CK_A = (CK_A + localMsg[j]) & 0xFF;
                            CK_B = (CK_B + CK_A) & 0xFF;
                        }

                        if (localMsg[Length - 1] == CK_A && localMsg[Length - 1] == CK_B)
                            BeginInvoke((MethodInvoker)(() => UpdateRecvMessage(port, localMsg)));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ReceiveData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region keystrokes
        //keystrokes for easy and quick startup
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            
            if (keyData == Keys.L)//reset Sim
            {
                btnResetSim.PerformClick();
                return true;
            }
            else if (keyData == Keys.Up)//speed up
            {
                if (sim.stepDistance < 10) sim.stepDistance += 0.4;
                else sim.stepDistance += 0.55;
                if (sim.stepDistance > 27.7777777778) sim.stepDistance = 27.7777777778;
                hsbarStepDistance.Value = (int)(sim.stepDistance * 36);
                return true;
            }
            else if (keyData == Keys.Down)//slow down
            {
                if (sim.stepDistance < 10) sim.stepDistance -= 0.04;
                else sim.stepDistance -= 0.055;
                if (sim.stepDistance < 0) sim.stepDistance = 0;
                hsbarStepDistance.Value = (int)(sim.stepDistance * 36);
                return true;
            }
            else if (keyData == Keys.OemPeriod)//Stop
            {
                sim.stepDistance = 0;
                hsbarStepDistance.Value = 0;

                sim.reverse = false;
                btnReverseDirection.BackgroundImage = Properties.Resources.UpArrow64;
                return true;
            }
            else if (keyData == Keys.Right)//turn right
            {
                sim.steerAngle += 2;
                if (sim.steerAngle > 40) sim.steerAngle = 40;
                if (sim.steerAngle < -40) sim.steerAngle = -40;
                sim.steerAngleScrollBar = sim.steerAngle;
                btnResetSteerAngle.Text = sim.steerAngle.ToString();
                hsbarSteerAngle.Value = (int)(10 * sim.steerAngle) + 400;
                return true;
            }
            else if (keyData == Keys.Left)//turn left
            {
                sim.steerAngle -= 2;
                if (sim.steerAngle > 40) sim.steerAngle = 40;
                if (sim.steerAngle < -40) sim.steerAngle = -40;
                sim.steerAngleScrollBar = sim.steerAngle;
                btnResetSteerAngle.Text = sim.steerAngle.ToString();
                hsbarSteerAngle.Value = (int)(10 * sim.steerAngle) + 400;
                return true;
            }
            else if (keyData == Keys.OemQuestion)
            {
                sim.steerAngle = 0.0;
                sim.steerAngleScrollBar = sim.steerAngle;
                btnResetSteerAngle.Text = sim.steerAngle.ToString();
                hsbarSteerAngle.Value = (int)(10 * sim.steerAngle) + 400;
                return true;
            }
            else if (keyData == (Keys.F))
            {
                JobNewOpenResume();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.A)) //autosteer button on off
            {
                btnAutoSteer.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.S)) //open the steer chart
            {
                btnSnapCurrent.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.C)) //open the steer chart
            {
                steerChartStripMenu.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.V)) //open the vehicle Settings
            {
                toolstripVehicleConfig.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.U)) //open the UTurn Settings
            {
                toolstripYouTurnConfig.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.NumPad1)) //auto section on off
            {
                btnAutoSection.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.N)) //auto section on off
            {
                btnAutoSection.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.NumPad0)) //auto section on off
            {
                btnManualSection.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.M)) //auto section on off
            {
                btnManualSection.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.G)) // Flag click
            {
                btnFlag.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.P)) // Snap/Prioritu click
            {
                btnSnapCurrent.PerformClick();
                return true;    // indicate that you handled this keystroke
            }
            else if (keyData == (Keys.F11)) // Full Screen click
            {
                btnFullScreen.PerformClick();
                return true;    // indicate that you handled this keystroke
            }

            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }
        #endregion

        #region Gesture

        // Private variables used to maintain the state of gestures
        //private DrawingObject _dwo = new DrawingObject();
        private Point _ptFirst = new Point();

        private Point _ptSecond = new Point();
        private int _iArguments = 0;

        // One of the fields in GESTUREINFO structure is type of Int64 (8 bytes).
        // The relevant gesture information is stored in lower 4 bytes. This
        // bit mask is used to get 4 lower bytes from this argument.
        private const Int64 ULL_ARGUMENTS_BIT_MASK = 0x00000000FFFFFFFF;

        //-----------------------------------------------------------------------
        // Multitouch/Touch glue (from winuser.h file)
        // Since the managed layer between C# and WinAPI functions does not
        // exist at the moment for multi-touch related functions this part of
        // code is required to replicate definitions from winuser.h file.
        //-----------------------------------------------------------------------
        // Touch event window message constants [winuser.h]
        private const int WM_GESTURENOTIFY = 0x011A;

        private const int WM_GESTURE = 0x0119;

        private const int GC_ALLGESTURES = 0x00000001;

        // Gesture IDs
        private const int GID_BEGIN = 1;

        private const int GID_END = 2;
        private const int GID_ZOOM = 3;
        private const int GID_PAN = 4;
        private const int GID_ROTATE = 5;
        private const int GID_TWOFINGERTAP = 6;


        private const int GID_PRESSANDTAP = 7;

        // Gesture flags - GESTUREINFO.dwFlags
        private const int GF_BEGIN = 0x00000001;

        private const int GF_INERTIA = 0x00000002;
        private const int GF_END = 0x00000004;

        //
        // Gesture configuration structure
        //   - Used in SetGestureConfig and GetGestureConfig
        //   - Note that any setting not included in either GESTURECONFIG.dwWant
        //     or GESTURECONFIG.dwBlock will use the parent window's preferences
        //     or system defaults.
        //
        // Touch API defined structures [winuser.h]
        [StructLayout(LayoutKind.Sequential)]
        private struct GESTURECONFIG
        {
            public int dwID;    // gesture ID
            public int dwWant;  // settings related to gesture ID that are to be

            // turned on
            public int dwBlock; // settings related to gesture ID that are to be

            // turned off
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTS
        {
            public short x;
            public short y;
        }

        //
        // Gesture information structure
        //   - Pass the HGESTUREINFO received in the WM_GESTURE message lParam
        //     into the GetGestureInfo function to retrieve this information.
        //   - If cbExtraArgs is non-zero, pass the HGESTUREINFO received in
        //     the WM_GESTURE message lParam into the GetGestureExtraArgs
        //     function to retrieve extended argument information.
        //
        [StructLayout(LayoutKind.Sequential)]
        private struct GESTUREINFO
        {
            public int cbSize;           // size, in bytes, of this structure

            // (including variable length Args
            // field)
            public int dwFlags;          // see GF_* flags

            public int dwID;             // gesture ID, see GID_* defines
            public IntPtr hwndTarget;    // handle to window targeted by this

            // gesture
            [MarshalAs(UnmanagedType.Struct)]
            internal POINTS ptsLocation; // current location of this gesture

            public int dwInstanceID;     // internally used
            public int dwSequenceID;     // internally used
            public Int64 ullArguments;   // arguments for gestures whose

            // arguments fit in 8 BYTES
            public int cbExtraArgs;      // size, in bytes, of extra arguments,

            // if any, that accompany this gesture
        }

        // Currently touch/multitouch access is done through unmanaged code
        // We must p/invoke into user32 [winuser.h]
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetGestureConfig(IntPtr hWnd, int dwReserved, int cIDs, ref GESTURECONFIG pGestureConfig, int cbSize);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGestureInfo(IntPtr hGestureInfo, ref GESTUREINFO pGestureInfo);

        // size of GESTURECONFIG structure
        private int _gestureConfigSize;

        // size of GESTUREINFO structure
        private int _gestureInfoSize;

        [SecurityPermission(SecurityAction.Demand)]
        private void SetupStructSizes()
        {
            // Both GetGestureCommandInfo and GetTouchInputInfo need to be
            // passed the size of the structure they will be filling
            // we get the sizes upfront so they can be used later.
            _gestureConfigSize = Marshal.SizeOf(new GESTURECONFIG());
            _gestureInfoSize = Marshal.SizeOf(new GESTUREINFO());
        }

        //-------------------------------------------------------------
        // Since there is no managed layer at the moment that supports
        // event handlers for WM_GESTURENOTIFY and WM_GESTURE
        // messages we have to override WndProc function
        //
        // in
        //   m - Message object
        //-------------------------------------------------------------
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            bool handled = false;

            switch (m.Msg)
            {
                case WM_GESTURENOTIFY:
                    {
                        // This is the right place to define the list of gestures
                        // that this application will support. By populating
                        // GESTURECONFIG structure and calling SetGestureConfig
                        // function. We can choose gestures that we want to
                        // handle in our application. In this app we decide to
                        // handle all gestures.
                        GESTURECONFIG gc = new GESTURECONFIG
                        {
                            dwID = 0,                // gesture ID
                            dwWant = GC_ALLGESTURES, // settings related to gesture
                                                     // ID that are to be turned on
                            dwBlock = 0 // settings related to gesture ID that are
                        };
                        // to be

                        // We must p/invoke into user32 [winuser.h]
                        bool bResult = SetGestureConfig(
                            Handle, // window for which configuration is specified
                            0,      // reserved, must be 0
                            1,      // count of GESTURECONFIG structures
                            ref gc, // array of GESTURECONFIG structures, dwIDs
                                    // will be processed in the order specified
                                    // and repeated occurances will overwrite
                                    // previous ones
                            _gestureConfigSize // sizeof(GESTURECONFIG)
                        );

                        if (!bResult)
                        {
                            throw new Exception("Error in execution of SetGestureConfig");
                        }
                    }
                    handled = true;
                    break;

                case WM_GESTURE:
                    // The gesture processing code is implemented in
                    // the DecodeGesture method
                    handled = DecodeGesture(ref m);
                    break;

                default:
                    handled = false;
                    break;
            }

            // Filter message back up to parents.
            base.WndProc(ref m);

            if (handled)
            {
                // Acknowledge event if handled.
                try
                {
                    m.Result = new System.IntPtr(1);
                }
                catch (Exception)
                {
                }
            }
        }

        // Taken from GCI_ROTATE_ANGLE_FROM_ARGUMENT.
        // Converts from "binary radians" to traditional radians.
        static protected double ArgToRadians(Int64 arg)
        {
            return (arg / 65535.0 * 4.0 * 3.14159265) - (2.0 * 3.14159265);
        }

        // Handler of gestures
        //in:
        //  m - Message object
        private bool DecodeGesture(ref Message m)
        {
            GESTUREINFO gi;

            try
            {
                gi = new GESTUREINFO();
            }
            catch (Exception)
            {
                return false;
            }

            gi.cbSize = _gestureInfoSize;

            // Load the gesture information.
            // We must p/invoke into user32 [winuser.h]
            if (!GetGestureInfo(m.LParam, ref gi))
            {
                return false;
            }

            switch (gi.dwID)
            {
                case GID_BEGIN:
                case GID_END:
                    break;

                case GID_ZOOM:
                    switch (gi.dwFlags)
                    {
                        case GF_BEGIN:
                            _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                            _ptFirst.X = gi.ptsLocation.x;
                            _ptFirst.Y = gi.ptsLocation.y;
                            _ptFirst = PointToClient(_ptFirst);
                            break;

                        default:
                            // We read here the second point of the gesture. This
                            // is middle point between fingers in this new
                            // position.
                            _ptSecond.X = gi.ptsLocation.x;
                            _ptSecond.Y = gi.ptsLocation.y;
                            _ptSecond = PointToClient(_ptSecond);
                            {
                                // The zoom factor is the ratio of the new
                                // and the old distance. The new distance
                                // between two fingers is stored in
                                // gi.ullArguments (lower 4 bytes) and the old
                                // distance is stored in _iArguments.
                                double k = (double)(_iArguments)
                                            / (double)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                                //lblX.Text = k.ToString();
                                camera.zoomValue *= k;
                                if (camera.zoomValue < 6.0) camera.zoomValue = 6;
                                camera.camSetDistance = camera.zoomValue * camera.zoomValue * -1;
                                SetZoom(camera.camSetDistance);
                            }

                            // Now we have to store new information as a starting
                            // information for the next step in this gesture.
                            _ptFirst = _ptSecond;
                            _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                            break;
                    }
                    break;

                //case GID_PAN:
                //    switch (gi.dwFlags)
                //    {
                //        case GF_BEGIN:
                //            _ptFirst.X = gi.ptsLocation.x;
                //            _ptFirst.Y = gi.ptsLocation.y;
                //            _ptFirst = PointToClient(_ptFirst);
                //            break;

                //        default:
                //            // We read the second point of this gesture. It is a
                //            // middle point between fingers in this new position
                //            _ptSecond.X = gi.ptsLocation.x;
                //            _ptSecond.Y = gi.ptsLocation.y;
                //            _ptSecond = PointToClient(_ptSecond);

                //            // We apply move operation of the object
                //            _dwo.Move(_ptSecond.X - _ptFirst.X, _ptSecond.Y - _ptFirst.Y);

                //            Invalidate();

                //            // We have to copy second point into first one to
                //            // prepare for the next step of this gesture.
                //            _ptFirst = _ptSecond;
                //            break;
                //    }
                //    break;

                case GID_ROTATE:
                    switch (gi.dwFlags)
                    {
                        case GF_BEGIN:
                            _iArguments = 32768;
                            break;

                        default:
                            // Gesture handler returns cumulative rotation angle. However we
                            // have to pass the delta angle to our function responsible
                            // to process the rotation gesture.
                            double k = ((int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK) - _iArguments) * 0.01;
                            camera.camPitch -= k;
                            if (camera.camPitch < -74) camera.camPitch = -74;
                            if (camera.camPitch > 0) camera.camPitch = 0;
                            _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                            break;
                    }
                    break;

                    //case GID_TWOFINGERTAP:
                    //    // Toggle drawing of diagonals
                    //    _dwo.ToggleDrawDiagonals();
                    //    Invalidate();
                    //    break;

                    //case GID_PRESSANDTAP:
                    //    if (gi.dwFlags == GF_BEGIN)
                    //    {
                    //        // Shift drawing color
                    //        _dwo.ShiftColor();
                    //        Invalidate();
                    //    }
                    //    break;
            }

            return true;
        }

        #endregion Gesture

    }
}
