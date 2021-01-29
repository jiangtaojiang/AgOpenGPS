using System;
using System.Globalization;
using System.Text;

namespace AgOpenGPS
{
    public class CNMEA
    {
        public bool EnableHeadRoll;
        private string rawBuffer = "";
        private string[] words;
        private string nextNMEASentence = "";
        public string FixFromSentence;

        //UTM coordinates
        //public double northing, easting;
        public Vec2 fix = new Vec2(0, 0);

        //used to offset the antenna position to compensate for drift
        public Vec2 fixOffset = new Vec2(0, 0);

        //other GIS Info
        public double altitude, speed;

        public double HeadingForced = 9999, hdop, ageDiff;

        //imu
        public double nRoll, nYaw, nAngularVelocity;

        public bool isValidIMU;
        public int FixQuality;
        public int satellitesTracked;
        public string status = "q";
        public DateTime utcDateTime;
        public char hemisphere = 'N';

        public StringBuilder logNMEASentence = new StringBuilder();
        private readonly FormGPS mf;

        public CNMEA(FormGPS f)
        {
            //constructor, grab the main form reference
            mf = f;
            FixFromSentence = Properties.Vehicle.Default.FixFromSentence;
        }

        //ParseNMEA
        private double rollK, Pc, G, Xp, Zp, XeRoll;
        private double P = 1.0;
        private readonly double varRoll = 0.1; // variance, smaller, more faster filtering
        private readonly double varProcess = 0.0003;
        // Returns a valid NMEA sentence from the pile from portData
        public string Parse()
        {
            string sentence;
            do
            {
                //double check for valid sentence
                // Find start of next sentence
                int start = rawBuffer.IndexOf("$", StringComparison.Ordinal);
                if (start == -1) return null;
                rawBuffer = rawBuffer.Substring(start);

                // Find end of sentence
                int end = rawBuffer.IndexOf("\n", StringComparison.Ordinal);
                if (end == -1) return null;

                //the NMEA sentence to be parsed
                sentence = rawBuffer.Substring(0, end + 1);

                //remove the processed sentence from the rawBuffer
                rawBuffer = rawBuffer.Substring(end + 1);
            }

            //if sentence has valid checksum, its all good
            while (!ValidateChecksum(sentence));

            //do we want to log? Grab before pieces are missing
            //if (mf.isLogNMEA )
            //{
            //    logNMEASentence.Append(sentence);
            //    nmeaCntr = 0;
            //}

            // Remove trailing checksum and \r\n and return
            sentence = sentence.Substring(0, sentence.IndexOf("*", StringComparison.Ordinal));

            return sentence;
        }

        public void ParseNMEA(string Buffer)
        {
            rawBuffer += Buffer;

            if (rawBuffer == null) return;

            //find end of a sentence
            int cr = rawBuffer.IndexOf("\n", StringComparison.Ordinal);
            if (cr == -1) return; // No end found, wait for more data

            // Find start of next sentence
            int dollar = rawBuffer.IndexOf("$", StringComparison.Ordinal);
            if (dollar == -1) return;

            //if the $ isn't first, get rid of the tail of corrupt sentence
            if (dollar >= cr) rawBuffer = rawBuffer.Substring(dollar);

            cr = rawBuffer.IndexOf("\n", StringComparison.Ordinal);
            if (cr == -1) return; // No end found, wait for more data
            dollar = rawBuffer.IndexOf("$", StringComparison.Ordinal);
            if (dollar == -1) return;

            //if the $ isn't first, get rid of the tail of corrupt sentence
            if (dollar >= cr) rawBuffer = rawBuffer.Substring(dollar);

            cr = rawBuffer.IndexOf("\n", StringComparison.Ordinal);
            dollar = rawBuffer.IndexOf("$", StringComparison.Ordinal);
            if (cr == -1 || dollar == -1) return;

            //mf.recvSentenceSettings = rawBuffer;

            //now we have a complete sentence or more somewhere in the portData
            while (true)
            {
                //extract the next NMEA single sentence
                nextNMEASentence = Parse();
                if (nextNMEASentence == null) return;

                mf.recvSentenceSettings[3] = mf.recvSentenceSettings[2];
                mf.recvSentenceSettings[2] = mf.recvSentenceSettings[1];
                mf.recvSentenceSettings[1] = mf.recvSentenceSettings[0];
                mf.recvSentenceSettings[0] = nextNMEASentence;

                //parse them accordingly
                words = nextNMEASentence.Split(',');
                if (words.Length < 3) return;

                if (words[0] == "$GPGGA" || words[0] == "$GNGGA") ParseGGA();
                if (words[0] == "$GPVTG" || words[0] == "$GNVTG") ParseVTG();
                if (words[0] == "$GPRMC" || words[0] == "$GNRMC") ParseRMC();
                if (words[0] == "$GPHDT" || words[0] == "$GNHDT") ParseHDT();
                if (words[0] == "$PAOGI") ParseOGI();
                if (words[0] == "$PTNL") ParseAVR();
                if (words[0] == "$GNTRA") ParseTRA();
                if (words[0] == "$PSTI" && words[1] == "032") ParseSTI032(); //there is also an $PSTI,030,... wich contains different data!

            }// while still data
        }

        private void ParseAVR()
        {
            #region AVR Message
            // $PTNL,AVR,145331.50,+35.9990,Yaw,-7.8209,Tilt,-0.4305,Roll,444.232,3,1.2,17 * 03

            //0 Message ID $PTNL,AVR
            //1 UTC of vector fix
            //2 Yaw angle, in degrees
            //3 Yaw
            //4 Tilt angle, in degrees
            //5 Tilt
            //6 Roll angle, in degrees
            //7 Roll
            //8 Range, in meters
            //9 GPS quality indicator:
            // 0: Fix not available or invalid
            // 1: Autonomous GPS fix
            // 2: Differential carrier phase solution RTK(Float)
            // 3: Differential carrier phase solution RTK(Fix)
            // 4: Differential code-based solution, DGPS
            //10 PDOP
            //11 Number of satellites used in solution
            //12 The checksum data, always begins with *
            #endregion AVR Message
            if (!string.IsNullOrEmpty(words[1]))
            {
                if (words[8] == "Roll")
                    double.TryParse(words[7], NumberStyles.Float, CultureInfo.InvariantCulture, out nRoll);
                else
                    double.TryParse(words[5], NumberStyles.Float, CultureInfo.InvariantCulture, out nRoll);

                //input to the kalman filter
                if (mf.ahrs.isRollFromAVR)
                {
                    //added by Andreas Ortner
                    rollK = nRoll;

                    //Kalman filter
                    Pc = P + varProcess;
                    G = Pc / (Pc + varRoll);
                    P = (1 - G) * Pc;
                    Xp = XeRoll;
                    Zp = Xp;
                    XeRoll = (G * (rollK - Zp)) + Xp;

                    mf.ahrs.rollX16 = (int)(XeRoll * 16);
                }
            }
        }

        private void ParseGGA()
        {
            #region GGA Message
            //$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M ,  ,*47

            //GGA          Global Positioning System Fix Data
            //123519       Fix taken at 12:35:19 UTC
            //4807.038,N   Latitude 48 deg 07.038' N
            //01131.000,E  Longitude 11 deg 31.000' E
            //1            Fix quality: 0 = invalid
            //                          1 = GPS fix (SPS)
            //                          2 = DGPS fix
            //                          3 = PPS fix
            //                          4 = Real Time Kinematic
            //                          5 = Float RTK
            //                          6 = estimated (dead reckoning) (2.3 feature)
            //                          7 = Manual input mode
            //                          8 = Simulation mode
            //08           Number of satellites being tracked
            //0.9          Horizontal dilution of position
            //545.4,M      Altitude, Meters, above mean sea level
            //46.9,M       Height of geoid (mean sea level) above WGS84 ellipsoid
            //(empty field) time in seconds since last DGPS update
            //(empty field) DGPS station ID number
            //*47          the checksum data, always begins with *
            #endregion GGA Message

            if (!string.IsNullOrEmpty(words[2]) && !string.IsNullOrEmpty(words[3])
                && !string.IsNullOrEmpty(words[4]) && !string.IsNullOrEmpty(words[5]))
            {
                //FixQuality
                int.TryParse(words[6], NumberStyles.Float, CultureInfo.InvariantCulture, out FixQuality);

                //satellites tracked
                int.TryParse(words[7], NumberStyles.Float, CultureInfo.InvariantCulture, out satellitesTracked);

                //hdop
                double.TryParse(words[8], NumberStyles.Float, CultureInfo.InvariantCulture, out hdop);

                //altitude
                double.TryParse(words[9], NumberStyles.Float, CultureInfo.InvariantCulture, out altitude);

                //age of differential
                double.TryParse(words[11], NumberStyles.Float, CultureInfo.InvariantCulture, out ageDiff);

                if (FixFromSentence == "GGA")
                {
                    //get latitude and convert to decimal degrees
                    int decim = words[2].IndexOf(".", StringComparison.Ordinal);
                    decim -= 2;
                    double.TryParse(words[2].Substring(0, decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double Latitude);
                    double.TryParse(words[2].Substring(decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double temp);
                    temp *= 0.01666666666667;
                    Latitude += temp;
                    if (words[3] == "S")
                    {
                        Latitude *= -1;
                        hemisphere = 'S';
                    }
                    else { hemisphere = 'N'; }
                    mf.Latitude = Latitude;
                    //get longitude and convert to decimal degrees
                    decim = words[4].IndexOf(".", StringComparison.Ordinal);
                    decim -= 2;
                    double.TryParse(words[4].Substring(0, decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double Longitude);
                    double.TryParse(words[4].Substring(decim), NumberStyles.Float, CultureInfo.InvariantCulture, out temp);
                    Longitude += temp * 0.0166666666667;

                    { if (words[5] == "W") Longitude *= -1; }
                    mf.Longitude = Longitude;

                    mf.UpdateFixPosition();
                }
            }
        }

        private void ParseVTG()
        {
            #region VTG Message
            //$GPVTG,054.7,T,034.4,M,005.5,N,010.2,K*48

            //VTG          Track made good and ground speed
            //054.7,T      True track made good (degrees)
            //034.4,M      Magnetic track made good
            //005.5,N      Ground speed, knots
            //010.2,K      Ground speed, Kilometers per hour
            //*48          Checksum
            #endregion VTG Message

            //is the sentence GGA
            if (!string.IsNullOrEmpty(words[1]) && !string.IsNullOrEmpty(words[5]))
            {
                //kph for speed - knots read
                double.TryParse(words[5], NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
                speed = Math.Round(speed * 1.852, 3);

                if (mf.vehicle.isReverse && speed > 0) speed *= -1;
                AverageTheSpeed();

                //True heading
                double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out HeadingForced);
            }
            else
            {
                speed = 0;
            }
        }

        private void ParseOGI()
        {
            #region PAOGI Message
            /*
            $PAOGI
            (1) 123519 Fix taken at 1219 UTC

            Roll corrected position
            (2,3) 4807.038,N Latitude 48 deg 07.038' N
            (4,5) 01131.000,E Longitude 11 deg 31.000' E

            (6) 1 Fix quality: 
                0 = invalid
                1 = GPS fix(SPS)
                2 = DGPS fix
                3 = PPS fix
                4 = Real Time Kinematic
                5 = Float RTK
                6 = estimated(dead reckoning)(2.3 feature)
                7 = Manual input mode
                8 = Simulation mode
            (7) Number of satellites being tracked
            (8) 0.9 Horizontal dilution of position
            (9) 545.4 Altitude (ALWAYS in Meters, above mean sea level)
            (10) 1.2 time in seconds since last DGPS update

            (11) 022.4 Speed over the ground in knots - can be positive or negative

            FROM AHRS:
            (12) Heading in degrees
            (13) Roll angle in degrees(positive roll = right leaning - right down, left up)
            (14) Pitch angle in degrees(Positive pitch = nose up)
            (15) Yaw Rate in Degrees / second

            * CHKSUM
            */
            #endregion PAOGI Message

            if (!string.IsNullOrEmpty(words[2]) && !string.IsNullOrEmpty(words[3])
                && !string.IsNullOrEmpty(words[4]) && !string.IsNullOrEmpty(words[5]))
            {
                //FixQuality
                int.TryParse(words[6], NumberStyles.Float, CultureInfo.InvariantCulture, out FixQuality);

                //satellites tracked
                int.TryParse(words[7], NumberStyles.Float, CultureInfo.InvariantCulture, out satellitesTracked);

                //hdop
                double.TryParse(words[8], NumberStyles.Float, CultureInfo.InvariantCulture, out hdop);

                //altitude
                double.TryParse(words[9], NumberStyles.Float, CultureInfo.InvariantCulture, out altitude);

                //age of differential
                double.TryParse(words[10], NumberStyles.Float, CultureInfo.InvariantCulture, out ageDiff);

                //kph for speed - knots read
                double.TryParse(words[11], NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
                speed = Math.Round(speed * 1.852, 3);
                if (mf.vehicle.isReverse && speed > 0) speed *= -1;
                AverageTheSpeed();

                //Dual antenna derived heading
                double.TryParse(words[12], NumberStyles.Float, CultureInfo.InvariantCulture, out HeadingForced);

                //roll
                double.TryParse(words[13], NumberStyles.Float, CultureInfo.InvariantCulture, out nRoll);

                //used only for sidehill correction - position is compensated in Lat/Lon of Dual module
                if (mf.ahrs.isRollFromOGI)
                {
                    rollK = nRoll; //input to the kalman filter
                    Pc = P + varProcess;
                    G = Pc / (Pc + varRoll);
                    P = (1 - G) * Pc;
                    Xp = XeRoll;
                    Zp = Xp;
                    XeRoll = (G * (rollK - Zp)) + Xp;//result

                    mf.ahrs.rollX16 = (int)(XeRoll * 16);
                }

                //pitch
                //double.TryParse(words[14], NumberStyles.Float, CultureInfo.InvariantCulture, out nPitch);

                //Angular velocity
                double.TryParse(words[15], NumberStyles.Float, CultureInfo.InvariantCulture, out nAngularVelocity);

                if (FixFromSentence == "OGI")
                {
                    //get latitude and convert to decimal degrees
                    double.TryParse(words[2].Substring(0, 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double Latitude);
                    double.TryParse(words[2].Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out double temp);
                    temp *= 0.01666666666666666666666666666667;
                    Latitude += temp;
                    if (words[3] == "S")
                    {
                        Latitude *= -1;
                        hemisphere = 'S';
                    }
                    else { hemisphere = 'N'; }

                    mf.Latitude = Latitude;
                    //get longitude and convert to decimal degrees
                    double.TryParse(words[4].Substring(0, 3), NumberStyles.Float, CultureInfo.InvariantCulture, out double Longitude);
                    double.TryParse(words[4].Substring(3), NumberStyles.Float, CultureInfo.InvariantCulture, out temp);
                    Longitude += temp * 0.01666666666666666666666666666667;

                    { if (words[5] == "W") Longitude *= -1; }
                    mf.Longitude = Longitude;

                    mf.UpdateFixPosition();
                }
            }
        }

        private void ParseHDT()
        {
            //$GNHDT,123.456,T * 00

            //(0)   Message ID $GNHDT
            //(1)   Heading in degrees
            //(2)   T: Indicates heading relative to True North
            //(3)   The checksum data, always begins with *

            if (!string.IsNullOrEmpty(words[1]))
            {
                //True heading
                double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out HeadingForced);
            }
        }

        private void ParseSTI032() //heading and roll from SkyTraQ receiver
        {
            #region STI0 Message
            //$PSTI,032,033010.000,111219,A,R,‐4.968,‐10.817,‐1.849,12.046,204.67,,,,,*39

            //(1) 032 Baseline Data indicator
            //(2) UTC time hhmmss.sss
            //(3) UTC date ddmmyy
            //(4) Status:
            //    V = Void
            //    A = Active
            //(5) Mode Indicator:
            //    F = Float RTK
            //    R = fixed RTK
            //(6) East-projection of baseline, meters
            //(7) North-projection of baseline, meters
            //(8) Up-projection of baseline, meters
            //(9) Baseline length, meters
            //(10) Baseline course: angle between baseline vector and north direction, degrees
            //(11) - (15) Reserved
            //(16) * Checksum
            #endregion STI0 Message

            if (!string.IsNullOrEmpty(words[10]))
            {
                //baselineCourse: angle between baseline vector (from kinematic base to rover) and north direction, degrees
                double.TryParse(words[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double baselineCourse);
                HeadingForced = (baselineCourse < 270) ? (baselineCourse + 90.0) : (baselineCourse - 270.0); //Rover Antenna on the left, kinematic base on the right!!!
            }

            if (!string.IsNullOrEmpty(words[8]) && !string.IsNullOrEmpty(words[9]))
            {
                double.TryParse(words[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double upProjection); //difference in hight of both antennas (rover - kinematic base)
                double.TryParse(words[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double baselineLength); //distance between kinematic base and rover
                nRoll = Glm.ToDegrees(Math.Atan(upProjection / baselineLength)); //roll to the right is positiv (rover left, kinematic base right!)

                if (mf.ahrs.isRollFromAVR)
                //input to the kalman filter
                {
                    rollK = nRoll;

                    //Kalman filter
                    Pc = P + varProcess;
                    G = Pc / (Pc + varRoll);
                    P = (1 - G) * Pc;
                    Xp = XeRoll;
                    Zp = Xp;
                    XeRoll = (G * (rollK - Zp)) + Xp;

                    mf.ahrs.rollX16 = (int)(XeRoll * 16);
                }
            }

        }

        private void ParseTRA()  //tra contains hdt and roll for the ub482 receiver
        {
            if (!string.IsNullOrEmpty(words[1]))
            {

                double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out HeadingForced);
                //  Console.WriteLine(HeadingForced);
                double.TryParse(words[3], NumberStyles.Float, CultureInfo.InvariantCulture, out nRoll);
                // Console.WriteLine(nRoll);

                int.TryParse(words[5], NumberStyles.Float, CultureInfo.InvariantCulture, out int trasolution);
                if (trasolution != 4) nRoll = 0;

                if (mf.ahrs.isRollFromAVR)
                    mf.ahrs.rollX16 = (int)(nRoll * 16);
            }
        }

        private void ParseRMC()
        {
            #region RMC Message
            //$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A

            //RMC          Recommended Minimum sentence C
            //123519       Fix taken at 12:35:19 UTC
            //A            Status A=active or V=Void.
            //4807.038,N   Latitude 48 deg 07.038' N
            //01131.000,E  Longitude 11 deg 31.000' E
            //022.4        Speed over the ground in knots
            //084.4        Track angle in degrees True
            //230394       Date - 23rd of March 1994
            //003.1,W      Magnetic Variation
            //*6A          * Checksum
            #endregion RMC Message

            if (!string.IsNullOrEmpty(words[3]) && !string.IsNullOrEmpty(words[4])
                && !string.IsNullOrEmpty(words[5]) && !string.IsNullOrEmpty(words[6]))
            {
                //Convert from knots to kph for speed
                double.TryParse(words[7], NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
                speed = Math.Round(speed * 1.852, 3);
                if (mf.vehicle.isReverse && speed > 0) speed *= -1;
                //average the speed
                AverageTheSpeed();

                //True heading
                double.TryParse(words[8], NumberStyles.Float, CultureInfo.InvariantCulture, out HeadingForced);

                //Status
                if (string.IsNullOrEmpty(words[2]))
                {
                    status = "z";
                }
                else
                {
                    try { status = words[2]; }
                    catch (Exception e)
                    {
                        mf.WriteErrorLog("Parse RMC" + e);
                    }
                }

                if (FixFromSentence == "RMC")
                {
                    //get latitude and convert to decimal degrees
                    double.TryParse(words[3].Substring(0, 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double Latitude);
                    double.TryParse(words[3].Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out double temp);
                    Latitude += temp * 0.01666666666666666666666666666667;

                    if (words[4] == "S")
                    {
                        Latitude *= -1;
                        hemisphere = 'S';
                    }
                    else { hemisphere = 'N'; }
                    mf.Latitude = Latitude;

                    //get longitude and convert to decimal degrees
                    double.TryParse(words[5].Substring(0, 3), NumberStyles.Float, CultureInfo.InvariantCulture, out double Longitude);
                    double.TryParse(words[5].Substring(3), NumberStyles.Float, CultureInfo.InvariantCulture, out temp);
                    Longitude += temp * 0.01666666666666666666666666666667;

                    if (words[6] == "W") Longitude *= -1;
                    mf.Longitude = Longitude;
                    
                    mf.UpdateFixPosition();
                }
            }
        }

        //checks the checksum against the string
        public void AverageTheSpeed()
        {
            //average the speed
            mf.avgSpeed = (mf.avgSpeed * 0.65) + (speed * 0.35);
        }

        public bool ValidateChecksum(string Sentence)
        {
            int sum = 0;
            try
            {
                char[] sentenceChars = Sentence.ToCharArray();
                // All character xor:ed results in the trailing hex checksum
                // The checksum calc starts after '$' and ends before '*'
                int inx;
                for (inx = 1; ; inx++)
                {
                    if (inx >= sentenceChars.Length) // No checksum found
                        return false;
                    var tmp = sentenceChars[inx];
                    // Indicates end of data and start of checksum
                    if (tmp == '*') break;
                    sum ^= tmp;    // Build checksum
                }
                // Calculated checksum converted to a 2 digit hex string
                string sumStr = string.Format("{0:X2}", sum);

                // Compare to checksum in sentence
                return sumStr.Equals(Sentence.Substring(inx + 1, 2));
            }
            catch (Exception e)
            {
                mf.WriteErrorLog("Validate Checksum" + e);
                return false;
            }
        }
    }
}