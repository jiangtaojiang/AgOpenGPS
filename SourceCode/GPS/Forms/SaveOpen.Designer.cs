using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace AgOpenGPS
{
    public class CAutoLoadField
    {
        public Polygon Polygon = new Polygon();
        public double Northingmin, Northingmax, Eastingmin, Eastingmax;
        public double LatStart, LonStart, Area;
        public string Dir = "";
    }

    public partial class FormGPS
    {
        //list of the list of patch data individual triangles for field sections
        public List<List<Vec3>> PatchSaveList = new List<List<Vec3>>();
        public List<List<Vec3>> PatchDrawList = new List<List<Vec3>>();
        public List<CAutoLoadField> Fields = new List<CAutoLoadField>();

        public void FileSaveGuidanceLines()
        {
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";
            string directoryName = Path.GetDirectoryName(dirField).ToString(CultureInfo.InvariantCulture);

            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string filename = directoryName + "\\GuidanceLines.txt";

            int cnt = Guidance.Lines.Count;

            if (cnt > 0)
            {
                using (StreamWriter writer = new StreamWriter(filename, false))
                {
                    try
                    {
                        writer.WriteLine("$CurveLines");
                        for (int i = 0; i < cnt; i++)
                        {
                            //write out the Name
                            writer.WriteLine(Guidance.Lines[i].Name);

                            //write out the aveheading
                            writer.WriteLine(Guidance.Lines[i].Mode.ToString());
                            writer.WriteLine(Guidance.Lines[i].Heading.ToString(CultureInfo.InvariantCulture));

                            //write out the points of ref line
                            int cnt2 = Guidance.Lines[i].Segments.Count;

                            writer.WriteLine(Guidance.Lines[i].Segments.Count.ToString(CultureInfo.InvariantCulture));

                            for (int j = 0; j < Guidance.Lines[i].Segments.Count; j++)
                                writer.WriteLine(Math.Round(Guidance.Lines[i].Segments[j].Easting, 3).ToString(CultureInfo.InvariantCulture) + "," +
                                                    Math.Round(Guidance.Lines[i].Segments[j].Northing, 3).ToString(CultureInfo.InvariantCulture));

                        }
                    }
                    catch (Exception er)
                    {
                        WriteErrorLog("Saving Curve Line" + er.ToString());

                        return;
                    }
                }
            }
            else if (File.Exists(filename))
                File.Delete(filename);
        }

        public void FileLoadGuidanceLines()
        {
            Guidance.Lines.Clear();

            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";
            string directoryName = Path.GetDirectoryName(dirField);

            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string filename = directoryName + "\\GuidanceLines.txt";

            if (File.Exists(filename))
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    try
                    {
                        string line;

                        //read header $CurveLine
                        line = reader.ReadLine();

                        while (!reader.EndOfStream)
                        {
                            Guidance.Lines.Add(new CGuidanceLine());
                            int idx = Guidance.Lines.Count - 1;

                            //read header $CurveLine
                            Guidance.Lines[idx].Name = reader.ReadLine();

                            // get the average heading
                            line = reader.ReadLine();

                            if (Enum.TryParse(line, out Gmode outputEnum) && Enum.IsDefined(typeof(Gmode), outputEnum))
                            {
                                Guidance.Lines[idx].Mode = outputEnum;
                            }
                            line = reader.ReadLine();
                            Guidance.Lines[idx].Heading = double.Parse(line, CultureInfo.InvariantCulture);

                            line = reader.ReadLine();
                            int numPoints = int.Parse(line);

                            if (numPoints > 0)
                            {
                                for (int i = 0; i < numPoints; i++)
                                {
                                    line = reader.ReadLine();
                                    string[] words = line.Split(',');
                                    Vec2 vecPt = new Vec2(double.Parse(words[0], CultureInfo.InvariantCulture),
                                        double.Parse(words[1], CultureInfo.InvariantCulture));
                                    Guidance.Lines[idx].Segments.Add(vecPt);
                                }
                            }
                            else
                            {
                                if (Guidance.Lines.Count > 0)
                                {
                                    Guidance.Lines.RemoveAt(idx);
                                    idx--;
                                }
                            }
                        }
                    }
                    catch (Exception er)
                    {
                        TimedMessageBox(2000, String.Get("gsCurveLineFileIsCorrupt"), String.Get("gsButFieldIsLoaded"));
                        WriteErrorLog("Load Curve Line" + er.ToString());
                    }
                }
            }
        }

        //function that save vehicle and sections
        public void FileSaveVehicle(string FileName)
        {
            string dir = Path.GetDirectoryName(vehiclesDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            vehicleFileName = Path.GetFileNameWithoutExtension(FileName) + " - ";
            Properties.Vehicle.Default.setVehicle_vehicleName = vehicleFileName;
            Properties.Vehicle.Default.Save();

            using (StreamWriter writer = new StreamWriter(FileName))
            {
                writer.WriteLine("Version," + Application.ProductVersion.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("AntennaHeight," + Properties.Vehicle.Default.setVehicle_antennaHeight.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("AntennaPivot," + Properties.Vehicle.Default.setVehicle_antennaPivot.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("AntennaOffset," + Properties.Vehicle.Default.setVehicle_antennaOffset.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsPivotBehindAntenna," + Properties.Vehicle.Default.setVehicle_isPivotBehindAntenna.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsSteerAxleAhead," + Properties.Vehicle.Default.setVehicle_isSteerAxleAhead.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("Wheelbase," + Properties.Vehicle.Default.setVehicle_wheelbase.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("MinTurningRadius," + Properties.Vehicle.Default.setVehicle_minTurningRadius.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("MinFixStep," + Properties.Vehicle.Default.FixStepDist.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("VehicleType," + Properties.Vehicle.Default.VehicleType.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("DualHeadingCorrection," + Properties.Vehicle.Default.DualHeadingCorrection.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("DualAntennaDistance," + Properties.Vehicle.Default.DualAntennaDistance.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("GeoFenceOffset," + Properties.Vehicle.Default.GeoFenceOffset.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("UTurnSkipWidth," + Properties.Vehicle.Default.set_youSkipWidth.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("YouTurnDistance," + Properties.Vehicle.Default.set_youTurnDistance.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("youTriggerDistance," + Properties.Vehicle.Default.UturnTriggerDistance.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("YouTurnUseDubins," + Properties.Vehicle.Default.Youturn_Type.ToString(CultureInfo.InvariantCulture));

                //AutoSteer
                writer.WriteLine("pidP," + Properties.Vehicle.Default.setAS_Kp.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("LowSteerPWM," + Properties.Vehicle.Default.setAS_lowSteerPWM.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("pidD," + Properties.Vehicle.Default.setAS_Kd.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("pidO," + Properties.Vehicle.Default.setAS_Ko.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("SteerAngleOffset," + Properties.Vehicle.Default.setAS_steerAngleOffset.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("minPWM," + Properties.Vehicle.Default.setAS_minSteerPWM.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("HighSteerPWM," + Properties.Vehicle.Default.setAS_highSteerPWM.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("CountsPerDegree," + Properties.Vehicle.Default.setAS_countsPerDegree.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("MaxSteerAngle," + Properties.Vehicle.Default.setVehicle_maxSteerAngle.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("MaxAngularVelocity," + Properties.Vehicle.Default.setVehicle_maxAngularVelocity.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("SnapDistance," + Properties.Vehicle.Default.SnapOffsetDistance.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("isStanleyUsed," + Properties.Vehicle.Default.setVehicle_isStanleyUsed.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("StanleyGain," + Properties.Vehicle.Default.setVehicle_stanleyGain.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("StanleyHeadingError," + Properties.Vehicle.Default.setVehicle_stanleyHeadingErrorGain.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("GoalPointLookAhead," +
                    Properties.Vehicle.Default.setVehicle_goalPointLookAhead.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("GoalPointLookAheadUTurnMult," +
                    Properties.Vehicle.Default.setVehicle_goalPointLookAheadUturnMult.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("GoalPointLookAheadMinumum," +
                    Properties.Vehicle.Default.setVehicle_lookAheadMinimum.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("GoalPointLookAheadDistanceFromLine," +
                    Properties.Vehicle.Default.setVehicle_lookAheadDistanceFromLine.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("HydLiftLookAhead," + Properties.Vehicle.Default.setVehicle_hydraulicLiftLookAhead.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                //IMU
                writer.WriteLine("HeadingFromSource," + Properties.Vehicle.Default.HeadingFromSource);
                writer.WriteLine("GPSWhichSentence," + Properties.Vehicle.Default.FixFromSentence.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("HeadingFromBrick," + Properties.Vehicle.Default.HeadingCorrectionFromBrick);
                writer.WriteLine("RollFromAutoSteer," + Properties.Vehicle.Default.RollFromAutoSteer);
                writer.WriteLine("HeadingFromAutoSteer," + Properties.Vehicle.Default.HeadingCorrectionFromAutoSteer);
                writer.WriteLine("IMURollZero," + Properties.Vehicle.Default.RollZeroX16.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("IMUFusion," + Properties.Vehicle.Default.FusionWeight.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                //Arduino steer Config
                writer.WriteLine("ArduinoInclinometer," + Properties.Vehicle.Default.setArdSteer_inclinometer.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoMaxPulseCounts," + Properties.Vehicle.Default.setArdSteer_maxPulseCounts.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoMaxSpeed," + Properties.Vehicle.Default.setArdSteer_maxSpeed.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoMinSpeed," + Properties.Vehicle.Default.setArdSteer_minSpeed.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoSetting0," + Properties.Vehicle.Default.setArdSteer_setting0.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoSetting1," + Properties.Vehicle.Default.setArdSteer_setting1.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArduinoAckermanFix," + Properties.Vehicle.Default.setArdSteer_ackermanFix.ToString(CultureInfo.InvariantCulture));

                //Arduino Machine Config
                writer.WriteLine("ArdMachineRaiseTime," + Properties.Vehicle.Default.setArdMac_hydRaiseTime.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArdMachineLowerTime," + Properties.Vehicle.Default.setArdMac_hydLowerTime.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ArdMachineEnableHydraulics," + Properties.Vehicle.Default.setArdMac_isHydEnabled.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("ArdSteerSetting2," + Properties.Vehicle.Default.setArdSteer_setting2);
                writer.WriteLine("ArdMacSetting0," + Properties.Vehicle.Default.setArdMac_setting0);

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                //uturn sequences
                writer.WriteLine("SequenceFunctionEnter;" + Properties.Vehicle.Default.seq_FunctionEnter);
                writer.WriteLine("SequenceFunctionExit;" + Properties.Vehicle.Default.seq_FunctionExit);
                writer.WriteLine("SequenceActionEnter;" + Properties.Vehicle.Default.seq_ActionEnter);
                writer.WriteLine("SequenceActionExit;" + Properties.Vehicle.Default.seq_ActionExit);
                writer.WriteLine("SequenceDistanceEnter;" + Properties.Vehicle.Default.seq_DistanceEnter);
                writer.WriteLine("SequenceDistanceExit;" + Properties.Vehicle.Default.seq_DistanceExit);

                writer.WriteLine("FunctionList;" + Properties.Vehicle.Default.seq_FunctionList);
                writer.WriteLine("ActionList;" + Properties.Vehicle.Default.seq_ActionList);

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
            }

            //little show to say saved and where
            TimedMessageBox(3000, String.Get("gsSavedInFolder"), vehiclesDirectory);
        }

        //function to open a previously saved field
        public bool FileOpenVehicle(string filename)
        {
            //make sure the file if fully valid and vehicle matches sections
            using (StreamReader reader = new StreamReader(filename))
            {
                try
                {
                    string line;
                    Properties.Vehicle.Default.setVehicle_vehicleName = filename;
                    string[] words;
                    line = reader.ReadLine(); words = line.Split(',');

                    //file version
                    string[] fullVers = words[1].Split('.');
                    string vers = fullVers[0] + fullVers[1];
                    int fileVersion = int.Parse(vers, CultureInfo.InvariantCulture);

                    //assembly version
                    string assemblyVersion = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);
                    fullVers = assemblyVersion.Split('.');
                    int appVersion = int.Parse(fullVers[0] + fullVers[1], CultureInfo.InvariantCulture);

                    if (fileVersion < appVersion)
                    {
                        TimedMessageBox(5000, String.Get("gsVehicleFileIsWrongVersion"), String.Get("gsMustBeVersion") + Application.ProductVersion.ToString(CultureInfo.InvariantCulture) + " or higher");
                        return false;
                    }
                    else
                    {

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_antennaHeight = vehicle.antennaHeight = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_antennaPivot = vehicle.antennaPivot = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_antennaOffset = vehicle.antennaOffset = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_isPivotBehindAntenna = vehicle.isPivotBehindAntenna = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_isSteerAxleAhead = vehicle.isSteerAxleAhead = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_wheelbase = vehicle.wheelbase = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_minTurningRadius = vehicle.minTurningRadius = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.FixStepDist = FixStepDist = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.VehicleType = vehicle.VehicleType = int.Parse(words[1], CultureInfo.InvariantCulture);


                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.DualHeadingCorrection = DualHeadingCorrection = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.DualAntennaDistance = DualAntennaDistance = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.GeoFenceOffset = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.set_youSkipWidth = Guidance.rowSkipsWidth = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.set_youTurnDistance = Guidance.youTurnStartOffset = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.UturnTriggerDistance = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.Youturn_Type = Guidance.YouTurnType = byte.Parse(words[1]);



                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_Kp = mc.Config_AutoSteer[mc.ssKp] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_lowSteerPWM = mc.Config_AutoSteer[mc.ssLowPWM] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_Kd = mc.Config_AutoSteer[mc.ssKd] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_Ko = mc.Config_AutoSteer[mc.ssKo] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_steerAngleOffset = mc.Config_AutoSteer[mc.ssSteerOffset] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_minSteerPWM = mc.Config_AutoSteer[mc.ssMinPWM] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_highSteerPWM = mc.Config_AutoSteer[mc.ssHighPWM] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setAS_countsPerDegree = mc.Config_AutoSteer[mc.ssCountsPerDegree] = byte.Parse(words[1], CultureInfo.InvariantCulture);



                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_maxSteerAngle = vehicle.maxSteerAngle = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_maxAngularVelocity = vehicle.maxAngularVelocity = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.SnapOffsetDistance = int.Parse(words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_isStanleyUsed = isStanleyUsed = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_stanleyGain = vehicle.stanleyGain = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_stanleyHeadingErrorGain = vehicle.stanleyHeadingErrorGain = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');

                        Properties.Vehicle.Default.setVehicle_goalPointLookAhead = vehicle.goalPointLookAheadSeconds = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_goalPointLookAheadUturnMult = vehicle.goalPointLookAheadUturnMult = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_lookAheadMinimum = vehicle.goalPointLookAheadMinimumDistance = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_lookAheadDistanceFromLine = vehicle.goalPointDistanceMultiplier = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setVehicle_hydraulicLiftLookAhead = vehicle.hydLiftLookAheadTime = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();


                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.HeadingFromSource = HeadingFromSource = (words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.FixFromSentence = pn.FixFromSentence = (words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.HeadingCorrectionFromBrick = ahrs.isHeadingCorrectionFromBrick = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.RollFromAutoSteer = ahrs.isRollFromAutoSteer = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.HeadingCorrectionFromAutoSteer = ahrs.isHeadingCorrectionFromAutoSteer = bool.Parse(words[1]);


                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.RollZeroX16 = ahrs.rollZeroX16 = int.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.FusionWeight = ahrs.fusionWeight = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine(); words = line.Split(',');
                        byte inc;
                        Properties.Vehicle.Default.setArdSteer_inclinometer = inc = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        byte inc2;
                        Properties.Vehicle.Default.setArdSteer_maxPulseCounts = inc2 = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        mc.Config_ardSteer[mc.arIncMaxPulse] = (byte)(inc << 6 + inc2);


                        mc.Config_ardSteer[0] = 127; //PGN - 32750
                        mc.Config_ardSteer[1] = 238;
                        Properties.Vehicle.Default.setArdSteer_maxSpeed = mc.Config_ardSteer[mc.arMaxSpd] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdSteer_minSpeed = mc.Config_ardSteer[mc.arMinSpd] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdSteer_setting0 = mc.Config_ardSteer[mc.arSet0] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdSteer_setting1 = mc.Config_ardSteer[mc.arSet1] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdSteer_ackermanFix = mc.Config_ardSteer[mc.arAckermanFix] = byte.Parse(words[1], CultureInfo.InvariantCulture);

                        //Arduino Machine Config
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdMac_hydRaiseTime = mc.Config_ardMachine[mc.amRaiseTime] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdMac_hydLowerTime = mc.Config_ardMachine[mc.amLowerTime] = byte.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.setArdMac_isHydEnabled = mc.Config_ardMachine[mc.amEnableHyd] = byte.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        //if (words[0] == "Empty") Properties.Vehicle.Default.setVehicle_lookAheadDistanceFromLine = 0;
                        Properties.Vehicle.Default.setArdSteer_setting2 = mc.Config_ardSteer[mc.arSet2] = byte.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        //if (words[0] == "Empty") Properties.Vehicle.Default.setVehicle_lookAheadDistanceFromLine = 0;
                        Properties.Vehicle.Default.setArdMac_setting0 = mc.Config_ardMachine[mc.amSet0] = byte.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine();
                        line = reader.ReadLine();





                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_FunctionEnter = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seq.seqEnter[i].function);
                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_FunctionExit = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seq.seqExit[i].function);

                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_ActionEnter = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seq.seqEnter[i].action);
                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_ActionExit = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seq.seqExit[i].action);


                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_DistanceEnter = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                            double.TryParse(words[i], NumberStyles.Float, CultureInfo.InvariantCulture, out seq.seqEnter[i].distance);
                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_DistanceExit = words[1];
                        words = words[1].Split(',');
                        for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                            double.TryParse(words[i], NumberStyles.Float, CultureInfo.InvariantCulture, out seq.seqExit[i].distance);

                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_FunctionList = words[1];
                        line = reader.ReadLine(); words = line.Split(';');
                        Properties.Vehicle.Default.seq_ActionList = words[1];

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        //fill in the current variables with restored data
                        vehicleFileName = Path.GetFileNameWithoutExtension(filename) + " - ";
                        Properties.Vehicle.Default.setVehicle_vehicleName = vehicleFileName;

                        Properties.Vehicle.Default.Save();
                    }
                    return true;
                }
                catch (Exception e) //FormatException e || IndexOutOfRangeException e2)
                {
                    WriteErrorLog("Open Vehicle" + e.ToString());

                    MessageBox.Show(String.Get("gsProgramWillResetToRecoverPleaseRestart"), String.Get("gsVehicleFileIsCorrupt"), MessageBoxButtons.OK, MessageBoxIcon.Stop);

                    //vehicle is corrupt, reload with all default information
                    Properties.Vehicle.Default.Reset();
                    Properties.Vehicle.Default.Save();

                    LoadTools();
                    return false;
                }
            }
        }//end of open file

        //function that save vehicle and section settings
        public void FileSaveTool(string FileName)
        {
            CheckToolSettings();

            string dir = Path.GetDirectoryName(toolsDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            toolFileName = Path.GetFileNameWithoutExtension(FileName) + " - ";
            Properties.Vehicle.Default.setVehicle_toolName = toolFileName;
            Properties.Vehicle.Default.Save();

            using (StreamWriter writer = new StreamWriter(FileName))
            {
                writer.WriteLine("Version," + Application.ProductVersion.ToString(CultureInfo.InvariantCulture));


                writer.WriteLine("ToolCount," + Properties.Vehicle.Default.ToolSettings.Count.ToString());
                for (int i = 0; i < Properties.Vehicle.Default.ToolSettings.Count; i++)
                {
                    writer.WriteLine("LookAheadOn," + Properties.Vehicle.Default.ToolSettings[i].LookAheadOn.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("LookAheadOff," + Properties.Vehicle.Default.ToolSettings[i].LookAheadOff.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("TurnOffDelay," + Properties.Vehicle.Default.ToolSettings[i].TurnOffDelay.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("MappingOnDelay," + Properties.Vehicle.Default.ToolSettings[i].MappingOnDelay.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("MappingOffDelay," + Properties.Vehicle.Default.ToolSettings[i].MappingOffDelay.ToString(CultureInfo.InvariantCulture));

                    writer.WriteLine("HitchLength," + Properties.Vehicle.Default.ToolSettings[i].HitchLength.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("TankWheelLength," + Properties.Vehicle.Default.ToolSettings[i].TankWheelLength.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("TankHitchLength," + Properties.Vehicle.Default.ToolSettings[i].TankHitchLength.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("ToolWheelLength," + Properties.Vehicle.Default.ToolSettings[i].ToolWheelLength.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("ToolHitchLength," + Properties.Vehicle.Default.ToolSettings[i].ToolHitchLength.ToString(CultureInfo.InvariantCulture));

                    writer.WriteLine("IsToolBehindPivot," + Properties.Vehicle.Default.ToolSettings[i].BehindPivot.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("IsToolTrailing," + Properties.Vehicle.Default.ToolSettings[i].Trailing.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("IsToolTBT," + Properties.Vehicle.Default.ToolSettings[i].TBT.ToString(CultureInfo.InvariantCulture));

                    writer.WriteLine("ToolMinUnappliedPixels," + Properties.Vehicle.Default.ToolSettings[i].MinApplied.ToString(CultureInfo.InvariantCulture));

                    writer.WriteLine("Sections," + Properties.Vehicle.Default.ToolSettings[i].Sections.Count.ToString());
                    for (int j = 0; j < Properties.Vehicle.Default.ToolSettings[i].Sections.Count; j++)
                    {
                        writer.WriteLine("Section" + (j + 1).ToString() + "," + Properties.Vehicle.Default.ToolSettings[i].Sections[j][0].ToString() + "," + Properties.Vehicle.Default.ToolSettings[i].Sections[j][1].ToString());
                    }
                }

                writer.WriteLine("GuidanceWidth," + Properties.Vehicle.Default.GuidanceWidth.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("GuidanceOverlap," + Properties.Vehicle.Default.GuidanceOverlap.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("GuidanceOffset," + Properties.Vehicle.Default.GuidanceOffset.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("WorkSwitch," + Properties.Vehicle.Default.setF_IsWorkSwitchEnabled.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("ActiveLow," + Properties.Vehicle.Default.setF_IsWorkSwitchActiveLow.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("SwitchManual," + Properties.Vehicle.Default.setF_IsWorkSwitchManual.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("AutoSteerRemote," + Properties.Vehicle.Default.setAS_isAutoSteerAutoOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
            }

            //little show to say saved and where
            TimedMessageBox(3000, String.Get("gsSavedInFolder"), toolsDirectory);
        }

        //function to open a previously saved field
        public bool FileOpenTool(string fileName)
        {
            //make sure the file if fully valid and vehicle matches sections
            using (StreamReader reader = new StreamReader(fileName))
            {
                try
                {
                    string line;
                    Properties.Vehicle.Default.setVehicle_toolName = fileName;
                    string[] words;
                    line = reader.ReadLine(); words = line.Split(',');

                    string vers = words[1].Replace('.', '0');
                    int fileVersion = int.Parse(vers, CultureInfo.InvariantCulture);

                    string assemblyVersion = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);
                    assemblyVersion = assemblyVersion.Replace('.', '0');
                    int appVersion = int.Parse(assemblyVersion, CultureInfo.InvariantCulture);

                    appVersion /= 100;
                    fileVersion /= 100;

                    if (fileVersion < appVersion)
                    {
                        TimedMessageBox(5000, String.Get("gsFileError"), String.Get("gsMustBeVersion") + Application.ProductVersion.ToString(CultureInfo.InvariantCulture) + " or higher");
                        return false;
                    }

                    else
                    {
                        List<ToolSettings> test = new List<ToolSettings>();

                        line = reader.ReadLine(); words = line.Split(',');
                        int count = int.Parse(words[1], CultureInfo.InvariantCulture);
                        for (int i = 0; i < count; i++)
                        {
                            test.Add(new ToolSettings());

                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].LookAheadOn = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].LookAheadOff = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].TurnOffDelay = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].MappingOnDelay = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].MappingOffDelay = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].HitchLength = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].TankWheelLength = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].TankHitchLength = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].ToolWheelLength = double.Parse(words[1], CultureInfo.InvariantCulture);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].ToolHitchLength = double.Parse(words[1], CultureInfo.InvariantCulture);

                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].BehindPivot = bool.Parse(words[1]);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].Trailing = bool.Parse(words[1]);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].TBT = bool.Parse(words[1]);
                            line = reader.ReadLine(); words = line.Split(',');
                            test[i].MinApplied = int.Parse(words[1], CultureInfo.InvariantCulture);

                            line = reader.ReadLine(); words = line.Split(',');
                            int count2 = int.Parse(words[1], CultureInfo.InvariantCulture);
                            for (int j = 0; j < count2; j++)
                            {
                                line = reader.ReadLine(); words = line.Split(',');
                                test[i].Sections.Add(new double[] { double.Parse(words[1], CultureInfo.InvariantCulture), double.Parse(words[2], CultureInfo.InvariantCulture)});
                            }
                        }
                        Properties.Vehicle.Default.ToolSettings = test;

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.GuidanceWidth = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.GuidanceOverlap = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Vehicle.Default.GuidanceOffset = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine(); words = line.Split(',');
                        mc.isWorkSwitchEnabled = Properties.Vehicle.Default.setF_IsWorkSwitchEnabled = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        mc.isWorkSwitchActiveLow = Properties.Vehicle.Default.setF_IsWorkSwitchActiveLow = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        mc.isWorkSwitchManual = Properties.Vehicle.Default.setF_IsWorkSwitchManual = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        mc.RemoteAutoSteer = Properties.Vehicle.Default.setAS_isAutoSteerAutoOn = bool.Parse(words[1]);
                        line = reader.ReadLine();



                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        //fill in the current variables with restored data
                        toolFileName = Path.GetFileNameWithoutExtension(fileName) + " - ";
                        Properties.Vehicle.Default.setVehicle_toolName = toolFileName;

                        Properties.Vehicle.Default.Save();

                        LoadTools();

                        return true;
                    }
                }
                catch (Exception e)
                {
                    WriteErrorLog("pen Tool" + e.ToString());

                    TimedMessageBox(3000, String.Get("gsFileError"), String.Get("gsVehicleFileIsCorrupt"));

                    return false;
                }
            }    //cancelled out of open file
        }//end of open file

        //function that save vehicle and section settings
        public void FileSaveEnvironment(string FileName)
        {
            string dir = Path.GetDirectoryName(envDirectory);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            envFileName = Path.GetFileNameWithoutExtension(FileName);
            Properties.Vehicle.Default.setVehicle_envName = envFileName;
            Properties.Vehicle.Default.Save();

            using (StreamWriter writer = new StreamWriter(FileName))
            {
                writer.WriteLine("Version," + Application.ProductVersion.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("Culture," + Properties.Settings.Default.setF_culture.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("CamPitch," + Properties.Settings.Default.setDisplay_camPitch.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty,10");
                writer.WriteLine("LightBarCMPerPixel," + Properties.Settings.Default.setDisplay_lightbarCmPerPixel.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("LineWidth," + Properties.Settings.Default.setDisplay_lineWidth.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("IsCompassOn," + Properties.Settings.Default.setMenu_isCompassOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsGridOn," + Properties.Settings.Default.setMenu_isGridOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsLightBarOn," + Properties.Settings.Default.setMenu_isLightbarOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsLogNMEA," + Properties.Settings.Default.setMenu_isLogNMEA.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsMetric," + Properties.Settings.Default.setMenu_isMetric.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsOGLZoom," + Properties.Settings.Default.setMenu_isOGLZoomOn.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("IsPurePursuitLineOn," + Properties.Settings.Default.setMenu_isPureOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsGuideLinesOn," + Properties.Settings.Default.setMenu_isSideGuideLines.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsSimulatorOn," + Properties.Settings.Default.setMenu_isSimulatorOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsSkyOn," + Properties.Settings.Default.setMenu_isSkyOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsSpeedoOn," + Properties.Settings.Default.setMenu_isSpeedoOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsUTurnAlwaysOn," + Properties.Settings.Default.setMenu_isUTurnAlwaysOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsAutoDayNightModeOn," + Properties.Settings.Default.setDisplay_isAutoDayNight.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("StartFullScreen," + Properties.Settings.Default.setDisplay_isStartFullScreen.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsRTKOn," + Properties.Settings.Default.setGPS_isRTK.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("NMEA_Hz," + Properties.Settings.Default.setPort_NMEAHz.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");

                writer.WriteLine("IsNtripCasterIP," + Properties.Settings.Default.setNTRIP_casterIP.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripCasterPort," + Properties.Settings.Default.setNTRIP_casterPort.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripCasterURL," + Properties.Settings.Default.setNTRIP_casterURL.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripGGAManual," + Properties.Settings.Default.setNTRIP_isGGAManual.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripOn," + Properties.Settings.Default.setNTRIP_isOn.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripTCP," + Properties.Settings.Default.setNTRIP_isTCP.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripManualLat," + Properties.Settings.Default.setNTRIP_manualLat.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripManualLon," + Properties.Settings.Default.setNTRIP_manualLon.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripMount," + Properties.Settings.Default.setNTRIP_mount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripGGAInterval," + Properties.Settings.Default.setNTRIP_sendGGAInterval.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripSendToUDPPort," + Properties.Settings.Default.setNTRIP_sendToUDPPort.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripUserName," + Properties.Settings.Default.setNTRIP_userName.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsNtripUserPassword," + Properties.Settings.Default.setNTRIP_userPassword.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("IsUDPOn," + Properties.Settings.Default.setUDP_isOn.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("GPSSimLatitude," + Properties.Settings.Default.setGPS_SimLatitude.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("GPSSimLongitude" + "," + Properties.Settings.Default.setGPS_SimLongitude.ToString(CultureInfo.InvariantCulture));


                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");


                writer.WriteLine("FieldColorDay," + Properties.Settings.Default.setDisplay_colorFieldDay.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("SectionColorDay," + Properties.Settings.Default.setDisplay_colorSectionsDay.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("FieldColorNight," + Properties.Settings.Default.setDisplay_colorFieldNight.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("SectionColorNight," + Properties.Settings.Default.setDisplay_colorSectionsNight.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("DayColor," + Properties.Settings.Default.setDisplay_colorDayMode.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("NightColor," + Properties.Settings.Default.setDisplay_colorNightMode.ToArgb().ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsSimple," + Properties.Settings.Default.setDisplay_isSimple.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("IsDayMode," + Properties.Settings.Default.setDisplay_isDayMode.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("CustomColors," + Properties.Settings.Default.setDisplay_customColors.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
                writer.WriteLine("Empty," + "10");
            }

            //little show to say saved and where
            TimedMessageBox(3000, String.Get("gsSavedInFolder"), envDirectory);
        }

        //function to open a previously saved field
        public void FileOpenEnvironment(string fileName)
        {
            //make sure the file if fully valid and vehicle matches sections
            using (StreamReader reader = new StreamReader(fileName))
            {
                try
                {
                    string line;
                    Properties.Vehicle.Default.setVehicle_envName = fileName;
                    string[] words;
                    line = reader.ReadLine(); words = line.Split(',');


                    string vers = words[1].Replace('.', '0');
                    int fileVersion = int.Parse(vers, CultureInfo.InvariantCulture);

                    string assemblyVersion = Application.ProductVersion.ToString(CultureInfo.InvariantCulture);
                    assemblyVersion = assemblyVersion.Replace('.', '0');
                    int appVersion = int.Parse(assemblyVersion, CultureInfo.InvariantCulture);

                    appVersion /= 100;
                    fileVersion /= 100;

                    if (fileVersion < appVersion)
                    {
                        TimedMessageBox(5000, String.Get("gsFileError"), String.Get("gsMustBeVersion") + Application.ProductVersion.ToString(CultureInfo.InvariantCulture) + " or higher");
                        return;
                    }
                    else
                    {
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setF_culture = (words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_camPitch = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_lightbarCmPerPixel = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_lineWidth = int.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isCompassOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isGridOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isLightbarOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isLogNMEA = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isMetric = bool.Parse(words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isOGLZoomOn = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isPureOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isSideGuideLines = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isSimulatorOn = bool.Parse(words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isSkyOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isSpeedoOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setMenu_isUTurnAlwaysOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_isAutoDayNight = bool.Parse(words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_isStartFullScreen = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setGPS_isRTK = bool.Parse(words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        //if (words[0] == "Empty") Properties.Settings.Default.setPort_NMEAHz = 5;
                        Properties.Settings.Default.setPort_NMEAHz = int.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_casterIP = words[1];
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_casterPort = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_casterURL = words[1];

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_isGGAManual = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_isOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_isTCP = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_manualLat = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_manualLon = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_mount = (words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_sendGGAInterval = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_sendToUDPPort = int.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_userName = (words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setNTRIP_userPassword = (words[1]);

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setUDP_isOn = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setGPS_SimLatitude = double.Parse(words[1], CultureInfo.InvariantCulture);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setGPS_SimLongitude = double.Parse(words[1], CultureInfo.InvariantCulture);

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorFieldDay = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorSectionsDay = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorFieldNight = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorSectionsNight = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorDayMode = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_colorNightMode = Color.FromArgb(int.Parse(words[1], CultureInfo.InvariantCulture));

                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_isSimple = bool.Parse(words[1]);
                        line = reader.ReadLine(); words = line.Split(',');
                        Properties.Settings.Default.setDisplay_isDayMode = bool.Parse(words[1]);

                        line = reader.ReadLine();
                        Properties.Settings.Default.setDisplay_customColors = line.Substring(13);

                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        //fill in the current variables with restored data
                        envFileName = Path.GetFileNameWithoutExtension(fileName);
                        Properties.Vehicle.Default.setVehicle_envName = envFileName;

                        Properties.Settings.Default.Save();
                        Properties.Vehicle.Default.Save();

                        LoadSettings();
                    }
                }
                catch (Exception e) //FormatException e || IndexOutOfRangeException e2)
                {
                    WriteErrorLog("Open Vehicle" + e.ToString());

                    //vehicle is corrupt, reload with all default information
                    Properties.Settings.Default.Reset();
                    Properties.Settings.Default.Save();

                    TimedMessageBox(3000, String.Get("gsFileError"), String.Get("gsVehicleFileIsCorrupt"));
                }
            }
        }//end of open file

        //function to open a previously saved field, resume, open exisiting, open named field
        public void FileOpenField()
        {
            isJobStarted = true;

            List<Task> tasks = new List<Task>();
            for (int j = 0; j < TaskList.Count; j++)
            {
                if (TaskList[j].Task.IsCompleted)
                {
                    TaskList.RemoveAt(j);
                    j--;
                }
                else if (TaskList[j].TaskName == TaskName.OpenJob || TaskList[j].TaskName == TaskName.CloseJob || TaskList[j].TaskName == TaskName.Save || TaskList[j].TaskName == TaskName.FixBnd || TaskList[j].TaskName == TaskName.FixHead || TaskList[j].TaskName == TaskName.Delete)
                {
                    tasks.Add(TaskList[j].Task);
                }
                else
                {
                    TaskList[j].Token.Cancel();
                }
            }

            Task NewTask = Task_JobNew(currentFieldDirectory, tasks);
            TaskList.Add(new TaskClass(NewTask, null, TaskName.OpenJob, new CancellationTokenSource()));
            tasks.Add(NewTask);
        }

        public void FileOpenField2(string fileAndDirectory)
        {
            try
            {
                currentFieldDirectory = fileAndDirectory;

                //start to read the file
                string line;
                string[] words;

                //Boundaries
                //Either exit or update running save
                fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Boundary.txt";
                if (!File.Exists(fileAndDirectory))
                    fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Boundary.Tmp";

                if (File.Exists(fileAndDirectory))
                {
                    using (StreamReader reader = new StreamReader(fileAndDirectory))
                    {
                        try
                        {
                            line = reader.ReadLine();//"$Version,5.0.10"
                            if (line != null)
                            {
                                words = line.Split(',');
                                if (words.Length > 1 && words[1] == "5.0.10")
                                {

                                    line = reader.ReadLine(); words = line.Split(',');
                                    SetPlaneToLocal(double.Parse(words[1], CultureInfo.InvariantCulture), double.Parse(words[2], CultureInfo.InvariantCulture));

                                    line = reader.ReadLine(); words = line.Split(',');
                                    int BndCount = int.Parse(words[1]);//Boundaries
                                    for (int i = 0; i < BndCount; i++)
                                    {
                                        CBoundaryLines newbnd = new CBoundaryLines();

                                        line = reader.ReadLine(); words = line.Split(',');
                                        newbnd.isDriveThru = bool.Parse(words[1]);
                                        newbnd.isDriveAround = bool.Parse(words[2]);

                                        line = reader.ReadLine(); words = line.Split(',');

                                        line = reader.ReadLine(); words = line.Split(',');
                                        newbnd.Northingmax = double.Parse(words[1], CultureInfo.InvariantCulture);
                                        newbnd.Northingmin = double.Parse(words[2], CultureInfo.InvariantCulture);
                                        line = reader.ReadLine(); words = line.Split(',');
                                        newbnd.Eastingmax = double.Parse(words[1], CultureInfo.InvariantCulture);
                                        newbnd.Eastingmin = double.Parse(words[2], CultureInfo.InvariantCulture);

                                        line = reader.ReadLine(); words = line.Split(',');
                                        newbnd.Area = double.Parse(words[1], CultureInfo.InvariantCulture);

                                        line = reader.ReadLine(); words = line.Split(',');
                                        int numPoints = int.Parse(words[1]);
                                        for (int k = 0; k < numPoints; k++)
                                        {
                                            line = reader.ReadLine(); words = line.Split(',');
                                            double Easting = double.Parse(words[0], CultureInfo.InvariantCulture);
                                            double Northing = double.Parse(words[1], CultureInfo.InvariantCulture);
                                            newbnd.Polygon.Points.Add(new Vec2(Easting, Northing));
                                        }

                                        line = reader.ReadLine(); words = line.Split(',');
                                        int IdxPoints = int.Parse(words[1]);
                                        for (int k = 0; k < IdxPoints; k++)
                                        {
                                            line = reader.ReadLine();
                                            newbnd.Polygon.Indexer.Add(int.Parse(line, CultureInfo.InvariantCulture));
                                        }

                                        line = reader.ReadLine(); words = line.Split(',');

                                        if (numPoints > 0)
                                        {
                                            bnd.Boundaries.Add(newbnd);
                                            StartTasks(newbnd, bnd.Boundaries.Count - 1, TaskName.Boundary);
                                        }
                                        if (reader.EndOfStream) break;
                                    }
                                }
                                else
                                {
                                    TimedMessageBox(2000, String.Get("gsBoundaryLineFilesAreCorrupt"), "Wrong Field Version");
                                    StartTasks(null, 0, TaskName.CloseJob);
                                    return;
                                }
                            }
                            else
                            {
                                TimedMessageBox(2000, String.Get("gsBoundaryLineFilesAreCorrupt"), "Wrong Field Version");
                                StartTasks(null, 0, TaskName.CloseJob);
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            TimedMessageBox(2000, String.Get("gsBoundaryLineFilesAreCorrupt"), String.Get("gsChooseADifferentField"));
                            WriteErrorLog("Load Boundary Line" + e.ToString());
                            StartTasks(null, 0, TaskName.CloseJob);
                            return;
                        }
                    }
                }
                else
                {
                    TimedMessageBox(2000, String.Get("gsFieldFileIsCorrupt"), String.Get("gsChooseADifferentField"));

                    StartTasks(null, 0, TaskName.CloseJob);
                    return;
                }
                //CurveLines
                FileLoadGuidanceLines();

                //section patches
                fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Sections.txt";
                if (File.Exists(fileAndDirectory))
                {
                    using (StreamReader reader = new StreamReader(fileAndDirectory))
                    {
                        try
                        {
                            fd.workedAreaTotal = 0;
                            fd.distanceUser = 0;
                            Vec3 vecFix = new Vec3();

                            //read header
                            while (!reader.EndOfStream)
                            {
                                line = reader.ReadLine();
                                int verts = int.Parse(line);

                                PatchDrawList.Add(new List<Vec3>());
                                int idx = PatchDrawList.Count - 1;
                                for (int v = 0; v < verts; v++)
                                {
                                    line = reader.ReadLine(); words = line.Split(',');
                                    vecFix.Easting = double.Parse(words[0], CultureInfo.InvariantCulture);
                                    vecFix.Northing = double.Parse(words[1], CultureInfo.InvariantCulture);
                                    vecFix.Heading = double.Parse(words[2], CultureInfo.InvariantCulture);
                                    PatchDrawList[idx].Add(vecFix);
                                }
                                verts -= 2;
                                if (verts >= 2)
                                {
                                    for (int j = 1; j < verts; j++)
                                    {
                                        double temp = PatchDrawList[idx][j].Easting * (PatchDrawList[idx][j + 1].Northing - PatchDrawList[idx][j + 2].Northing) +
                                               PatchDrawList[idx][j + 1].Easting * (PatchDrawList[idx][j + 2].Northing - PatchDrawList[idx][j].Northing) +
                                               PatchDrawList[idx][j + 2].Easting * (PatchDrawList[idx][j].Northing - PatchDrawList[idx][j + 1].Northing);

                                        if (!double.IsNaN(temp))
                                            fd.workedAreaTotal += Math.Abs((temp * 0.5));
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            WriteErrorLog("Section file" + e.ToString());
                            TimedMessageBox(2000, String.Get("gsSectionFileIsCorrupt"), String.Get("gsButFieldIsLoaded"));
                        }
                    }
                }

                // Flags -------------------------------------------------------------------------------------------------

                //Either exit or update running save
                fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Flags.txt";
                if (File.Exists(fileAndDirectory))
                {
                    flagPts.Clear();
                    using (StreamReader reader = new StreamReader(fileAndDirectory))
                    {
                        try
                        {
                            //read header
                            line = reader.ReadLine();

                            //number of flags
                            line = reader.ReadLine();
                            int points = int.Parse(line);

                            if (points > 0)
                            {
                                double lat;
                                double longi;
                                double east;
                                double nort;
                                double head;
                                int color, ID;
                                string notes;

                                for (int v = 0; v < points; v++)
                                {
                                    line = reader.ReadLine(); words = line.Split(',');
                                    lat = double.Parse(words[0], CultureInfo.InvariantCulture);
                                    longi = double.Parse(words[1], CultureInfo.InvariantCulture);
                                    east = double.Parse(words[2], CultureInfo.InvariantCulture);
                                    nort = double.Parse(words[3], CultureInfo.InvariantCulture);

                                    if (words.Length == 8)
                                    {
                                        head = double.Parse(words[4], CultureInfo.InvariantCulture);
                                        color = int.Parse(words[5]);
                                        ID = int.Parse(words[6]);
                                        notes = words[7].Trim();
                                    }
                                    else
                                    {
                                        head = 0;
                                        color = int.Parse(words[4]);
                                        ID = int.Parse(words[5]);
                                        notes = "";
                                    }

                                    CFlag flagPt = new CFlag(lat, longi, east, nort, head, color, ID, notes);
                                    flagPts.Add(flagPt);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            TimedMessageBox(2000, String.Get("gsFlagFileIsCorrupt"), String.Get("gsButFieldIsLoaded"));
                            WriteErrorLog("FieldOpen, Loading Flags, Corrupt Flag File" + e.ToString());
                        }
                    }
                }


                // Headland  -------------------------------------------------------------------------------------------------
                fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Headland.txt";

                if (!File.Exists(fileAndDirectory))
                    fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\Headland.Tmp";

                if (File.Exists(fileAndDirectory))
                {
                    using (StreamReader reader = new StreamReader(fileAndDirectory))
                    {
                        try
                        {
                            //read header
                            line = reader.ReadLine();//writer.WriteLine("$Headland");
                            line = reader.ReadLine();
                            int headArr = int.Parse(line);

                            for (int i = 0; i < headArr && i < bnd.Boundaries.Count; i++)//hd.headArr.Count
                            {
                                line = reader.ReadLine();
                                int numHeadLine = int.Parse(line);
                                for (int j = 0; j < numHeadLine; j++)//hd.headArr[i].HeadLand.Count
                                {
                                    bnd.Boundaries[i].HeadLand.Add(new Polygon());
                                    line = reader.ReadLine();
                                    int numHeadLinecount = int.Parse(line);//hd.headArr[i].HeadLine[j].Count

                                    for (int k = 0; k < numHeadLinecount; k++)
                                    {
                                        line = reader.ReadLine(); words = line.Split(',');
                                        Vec2 vecPt = new Vec2(
                                            double.Parse(words[0], CultureInfo.InvariantCulture),
                                            double.Parse(words[1], CultureInfo.InvariantCulture));
                                        bnd.Boundaries[i].HeadLand[j].Points.Add(vecPt);
                                    }
                                }

                                StartTasks(bnd.Boundaries[i], i, TaskName.HeadLand);
                            }
                        }
                        catch (Exception e)
                        {
                            TimedMessageBox(2000, "Headland File is Corrupt", "But Field is Loaded");
                            WriteErrorLog("Load Headland Loop" + e.ToString());
                        }
                    }
                }

                //Recorded Path
                fileAndDirectory = fieldsDirectory + currentFieldDirectory + "\\RecPath.txt";
                if (File.Exists(fileAndDirectory))
                {
                    using (StreamReader reader = new StreamReader(fileAndDirectory))
                    {
                        try
                        {
                            //read header
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            int numPoints = int.Parse(line);
                            recPath.recList.Clear();

                            while (!reader.EndOfStream)
                            {
                                for (int v = 0; v < numPoints; v++)
                                {
                                    line = reader.ReadLine(); words = line.Split(',');
                                    CRecPathPt point = new CRecPathPt(
                                        double.Parse(words[0], CultureInfo.InvariantCulture),
                                        double.Parse(words[1], CultureInfo.InvariantCulture),
                                        double.Parse(words[2], CultureInfo.InvariantCulture),
                                        double.Parse(words[3], CultureInfo.InvariantCulture),
                                        bool.Parse(words[4]));

                                    //add the point
                                    recPath.recList.Add(point);
                                }
                            }
                        }

                        catch (Exception e)
                        {
                            TimedMessageBox(2000, String.Get("gsRecordedPathFileIsCorrupt"), String.Get("gsButFieldIsLoaded"));
                            WriteErrorLog("Load Recorded Path" + e.ToString());
                        }
                    }
                }
            }
            catch (Exception)
            {
            
            
            }
        }//end of open file

        public void FileCreateElevation()
        {
            //Saturday, February 11, 2017  -->  7:26:52 AM
            //$FieldDir
            //Bob_Feb11
            //$Offsets
            //533172,5927719,12 - offset easting, northing, zone
            //$StartFix
            //latitude, longitude

            string myFileName, dirField;

            //get the directory and make sure it exists, create if not
            dirField = fieldsDirectory + currentFieldDirectory + "\\";
            string directoryName = Path.GetDirectoryName(dirField);

            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            myFileName = "Elevation.txt";

            using (StreamWriter writer = new StreamWriter(dirField + myFileName))
            {
                //Write out the date
                writer.WriteLine(DateTime.Now.ToString("yyyy-MMMM-dd hh:mm:ss tt", CultureInfo.InvariantCulture));

                writer.WriteLine("$FieldDir");
                writer.WriteLine(currentFieldDirectory.ToString(CultureInfo.InvariantCulture));

                writer.WriteLine("StartFix");
                writer.WriteLine(Latitude.ToString(CultureInfo.InvariantCulture) + "," + Longitude.ToString(CultureInfo.InvariantCulture));
            }
        }

        //save field Patches
        public void FileSaveSections()
        {
            //make sure there is something to save
            if (PatchSaveList.Count() > 0)
            {
                //Append the current list to the field file
                using (StreamWriter writer = new StreamWriter((fieldsDirectory + currentFieldDirectory + "\\Sections.txt"), true))
                {
                    //for each patch, write out the list of triangles to the file
                    foreach (var triList in PatchSaveList)
                    {
                        int count2 = triList.Count();
                        writer.WriteLine(count2.ToString(CultureInfo.InvariantCulture));

                        for (int i = 0; i < count2; i++)
                            writer.WriteLine((Math.Round(triList[i].Easting, 3)).ToString(CultureInfo.InvariantCulture) +
                                "," + (Math.Round(triList[i].Northing, 3)).ToString(CultureInfo.InvariantCulture) +
                                 "," + (Math.Round(triList[i].Heading, 3)).ToString(CultureInfo.InvariantCulture));
                    }
                }

                //clear out that patchSaveList and begin adding new ones for next save
                PatchSaveList.Clear();
            }
        }

        public void FileCreateSections()
        {
            //$Sections
            //10 - points in this patch
            //10.1728031317344,0.723157039771303 -easting, northing

            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            //write out the file
            using (StreamWriter writer = new StreamWriter(dirField + "Sections.txt"))
            {
            }
        }

        //Create Flag file
        public void FileCreateFlags()
        {
            //$Sections
            //10 - points in this patch
            //10.1728031317344,0.723157039771303 -easting, northing

            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string myFileName = "Flags.txt";

            //write out the file
            using (StreamWriter writer = new StreamWriter(dirField + myFileName))
            {
                //write paths # of sections
                //writer.WriteLine("$Sectionsv4");
            }
        }

        //save the boundary
        public void FileSaveBoundary()
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            //write out the file
            using (StreamWriter writer = new StreamWriter(dirField + "Boundary.Tmp"))
            {
                writer.WriteLine("$Version,5.0.10");
                writer.WriteLine("StartPos," + LatStart.ToString(CultureInfo.InvariantCulture) + "," + LonStart.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Boundaries," + bnd.Boundaries.Count.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < bnd.Boundaries.Count; i++)
                {
                    writer.WriteLine("Settings," + bnd.Boundaries[i].isDriveThru + "," + bnd.Boundaries[i].isDriveAround);
                    writer.WriteLine("FutureFeature, 10");
                    writer.WriteLine("NorthMax_Min," + bnd.Boundaries[i].Northingmax.ToString(CultureInfo.InvariantCulture) + "," + bnd.Boundaries[i].Eastingmin.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("EastMax_Min," + bnd.Boundaries[i].Eastingmax.ToString(CultureInfo.InvariantCulture) + "," + bnd.Boundaries[i].Eastingmin.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("Area," + bnd.Boundaries[i].Area.ToString(CultureInfo.InvariantCulture));

                    writer.WriteLine("Points," + bnd.Boundaries[i].Polygon.Points.Count.ToString(CultureInfo.InvariantCulture));

                    for (int k = 0; k < bnd.Boundaries[i].Polygon.Points.Count; k++)
                    {
                        writer.WriteLine(Math.Round(bnd.Boundaries[i].Polygon.Points[k].Easting, 3).ToString(CultureInfo.InvariantCulture) + "," +
                                            Math.Round(bnd.Boundaries[i].Polygon.Points[k].Northing, 3).ToString(CultureInfo.InvariantCulture));
                    }

                    writer.WriteLine("IdxPoints," + bnd.Boundaries[i].Polygon.Indexer.Count.ToString(CultureInfo.InvariantCulture));
                    for (int k = 0; k < bnd.Boundaries[i].Polygon.Indexer.Count; k++)
                    {
                        writer.WriteLine(bnd.Boundaries[i].Polygon.Indexer[k].ToString(CultureInfo.InvariantCulture));
                    }

                    writer.WriteLine("FutureFeature, 10");
                }
            }
            if (File.Exists(dirField + "Boundary.Txt"))
                File.Delete(dirField + "Boundary.Txt");
            File.Move(dirField + "Boundary.Tmp", dirField + "Boundary.Txt");
        }

        //save the headland
        public void FileSaveHeadland()
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";
            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            //write out the file
            using (StreamWriter writer = new StreamWriter(dirField + "Headland.Tmp"))
            {
                writer.WriteLine("$Headland");

                writer.WriteLine(bnd.Boundaries.Count.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < bnd.Boundaries.Count; i++)
                {
                    writer.WriteLine(bnd.Boundaries[i].HeadLand.Count.ToString(CultureInfo.InvariantCulture));
                    for (int j = 0; j < bnd.Boundaries[i].HeadLand.Count; j++)
                    {
                        writer.WriteLine(bnd.Boundaries[i].HeadLand[j].Points.Count.ToString(CultureInfo.InvariantCulture));
                        for (int k = 0; k < bnd.Boundaries[i].HeadLand[j].Points.Count; k++)
                        {
                            writer.WriteLine(Math.Round(bnd.Boundaries[i].HeadLand[j].Points[k].Easting, 3).ToString(CultureInfo.InvariantCulture) + "," +
                                             Math.Round(bnd.Boundaries[i].HeadLand[j].Points[k].Northing, 3).ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }
            if (File.Exists(dirField + "Headland.Txt"))
                File.Delete(dirField + "Headland.Txt");
            File.Move(dirField + "Headland.Tmp", dirField + "Headland.Txt");
        }

        public void FileCreateRecPath()
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string myFileName = "RecPath.txt";

            //write out the file
            using (StreamWriter writer = new StreamWriter(dirField + myFileName))
            {
                //write paths # of sections
                writer.WriteLine("$RecPath");
                writer.WriteLine("0");
            }
        }

        //save the recorded path
        public void FileSaveRecPath()
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            if (!File.Exists(dirField + "RecPath.txt")) FileCreateRecPath();

            //write out the file
            using (StreamWriter writer = new StreamWriter((dirField + "RecPath.Tmp")))
            {
                writer.WriteLine("$RecPath");
                writer.WriteLine(recPath.recList.Count.ToString(CultureInfo.InvariantCulture));
                if (recPath.recList.Count > 0)
                {
                    for (int j = 0; j < recPath.recList.Count; j++)
                        writer.WriteLine(
                            Math.Round(recPath.recList[j].Easting, 3).ToString(CultureInfo.InvariantCulture) + "," +
                            Math.Round(recPath.recList[j].Northing, 3).ToString(CultureInfo.InvariantCulture) + "," +
                            Math.Round(recPath.recList[j].Heading, 3).ToString(CultureInfo.InvariantCulture) + "," +
                            Math.Round(recPath.recList[j].Speed, 1).ToString(CultureInfo.InvariantCulture) + "," +
                            (recPath.recList[j].AutoBtnState).ToString());

                    //Clear list
                    //recPath.recList.Clear();
                }
            }
            if (File.Exists(dirField + "RecPath.Txt"))
                File.Delete(dirField + "RecPath.Txt");
            File.Move(dirField + "RecPath.Tmp", dirField + "RecPath.Txt");
        }

        //save all the flag markers
        public void FileSaveFlags()
        {
            //Saturday, February 11, 2017  -->  7:26:52 AM
            //$FlagsDir
            //Bob_Feb11
            //$Offsets
            //533172,5927719,12 - offset easting, northing, zone

            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            int FlagCount = flagPts.Count;
            if (FlagCount > 0)
            {
                //use Streamwriter to create and overwrite existing flag file
                using (StreamWriter writer = new StreamWriter(dirField + "Flags.Tmp"))
                {
                    try
                    {
                        writer.WriteLine("$Flags");
                        writer.WriteLine(FlagCount);

                        for (int i = 0; i < FlagCount; i++)
                        {
                            writer.WriteLine(
                                flagPts[i].Latitude.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].Longitude.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].Easting.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].Northing.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].Heading.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].color.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].ID.ToString(CultureInfo.InvariantCulture) + "," +
                                flagPts[i].notes);
                        }
                    }

                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + "\n Cannot write to file.");
                        WriteErrorLog("Saving Flags" + e.ToString());
                        return;
                    }
                }
                if (File.Exists(dirField + "Flags.txt"))
                    File.Delete(dirField + "Flags.txt");
                File.Move(dirField + "Flags.Tmp", dirField + "Flags.txt");
            }
            else
            {
                if (File.Exists(dirField + "Flags.txt"))
                    File.Delete(dirField + "Flags.txt");
                if (File.Exists(dirField + "Flags.Tmp"))
                    File.Delete(dirField + "Flags.Tmp");
            }
        }

        //save nmea sentences
        public void FileSaveNMEA()
        {
            using (StreamWriter writer = new StreamWriter((fieldsDirectory + "\\NMEA_log.txt"), true))
            {
                writer.Write(pn.logNMEASentence.ToString());
            }
            pn.logNMEASentence.Clear();
        }

        //save nmea sentences
        public void FileSaveElevation()
        {
            if (!File.Exists(fieldsDirectory + currentFieldDirectory + "\\Elevation.txt"))
                FileCreateElevation();

            using (StreamWriter writer = new StreamWriter((fieldsDirectory + currentFieldDirectory + "\\Elevation.txt"), true))
            {
                writer.Write(sbFix.ToString());
            }
            sbFix.Clear();
        }


        //generate KML file from flag
        public void FileSaveSingleFlagKML(int flagNumber)
        {

            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string myFileName;
            myFileName = "Flag.kml";

            using (StreamWriter writer = new StreamWriter(dirField + myFileName))
            {
                //match new fix to current position

                writer.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>     ");
                writer.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""> ");

                int count2 = flagPts.Count;

                writer.WriteLine(@"<Document>");

                writer.WriteLine(@"  <Placemark>                                  ");
                writer.WriteLine(@"<Style> <IconStyle>");
                if (flagPts[flagNumber - 1].color == 0)  //red - xbgr
                    writer.WriteLine(@"<color>ff4400ff</color>");
                if (flagPts[flagNumber - 1].color == 1)  //grn - xbgr
                    writer.WriteLine(@"<color>ff44ff00</color>");
                if (flagPts[flagNumber - 1].color == 2)  //yel - xbgr
                    writer.WriteLine(@"<color>ff44ffff</color>");
                writer.WriteLine(@"</IconStyle> </Style>");
                writer.WriteLine(@" <name> " + flagNumber.ToString(CultureInfo.InvariantCulture) + @"</name>");
                writer.WriteLine(@"<Point><coordinates> " +
                                flagPts[flagNumber - 1].Longitude.ToString(CultureInfo.InvariantCulture) + "," + flagPts[flagNumber - 1].Latitude.ToString(CultureInfo.InvariantCulture) + ",0" +
                                @"</coordinates> </Point> ");
                writer.WriteLine(@"  </Placemark>                                 ");
                writer.WriteLine(@"</Document>");
                writer.WriteLine(@"</kml>                                         ");

            }
        }

        //generate KML file from flag
        public void FileMakeKMLFromCurrentPosition(double lat, double lon)
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            using (StreamWriter writer = new StreamWriter(dirField + "CurrentPosition.kml"))
            {

                writer.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>     ");
                writer.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""> ");

                int count2 = flagPts.Count;

                writer.WriteLine(@"<Document>");

                writer.WriteLine(@"  <Placemark>                                  ");
                writer.WriteLine(@"<Style> <IconStyle>");
                writer.WriteLine(@"<color>ff4400ff</color>");
                writer.WriteLine(@"</IconStyle> </Style>");
                writer.WriteLine(@" <name> Your Current Position </name>");
                writer.WriteLine(@"<Point><coordinates> " +
                                lon.ToString(CultureInfo.InvariantCulture) + "," + lat.ToString(CultureInfo.InvariantCulture) + ",0" +
                                @"</coordinates> </Point> ");
                writer.WriteLine(@"  </Placemark>                                 ");
                writer.WriteLine(@"</Document>");
                writer.WriteLine(@"</kml>                                         ");

            }
        }

        //generate KML file from flags
        public void FileSaveFieldKML()
        {
            //get the directory and make sure it exists, create if not
            string dirField = fieldsDirectory + currentFieldDirectory + "\\";

            string directoryName = Path.GetDirectoryName(dirField);
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            string myFileName;
            myFileName = "Field.Tmp";

            XmlTextWriter kml = new XmlTextWriter(dirField + myFileName, Encoding.UTF8);

            kml.Formatting = Formatting.Indented;
            kml.Indentation = 3;

            kml.WriteStartDocument();
            kml.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");
            kml.WriteStartElement("Document");
            
            string linePts = "";

            //guidance lines Curve
            kml.WriteStartElement("Folder");
            kml.WriteElementString("name", "Guidance_Lines");
            kml.WriteElementString("visibility", "0");

            for (int i = 0; i < Guidance.Lines.Count; i++)
            {
                linePts = "";
                kml.WriteStartElement("Placemark");
                kml.WriteElementString("visibility", "0");

                kml.WriteElementString("name", Guidance.Lines[i].Name);
                kml.WriteStartElement("Style");

                kml.WriteStartElement("LineStyle");
                kml.WriteElementString("color", "ff6699ff");
                kml.WriteElementString("width", "2");
                kml.WriteEndElement(); // <LineStyle>
                kml.WriteEndElement(); //Style

                kml.WriteStartElement("LineString");
                kml.WriteElementString("tessellate", "1");
                kml.WriteStartElement("coordinates");

                for (int j = 0; j < Guidance.Lines[i].Segments.Count; j++)
                {
                    linePts += ConvertLocalToWGS84(Guidance.Lines[i].Segments[j].Northing, Guidance.Lines[i].Segments[j].Easting);
                }
                kml.WriteRaw(linePts);

                kml.WriteEndElement(); // <coordinates>
                kml.WriteEndElement(); // <LineString>

                kml.WriteEndElement(); // <Placemark>
            }
            kml.WriteEndElement(); // <Folder>   

            //flags  *************************************************************************
            kml.WriteStartElement("Folder");
            kml.WriteElementString("name", "Flags");

            for (int i = 0; i < flagPts.Count; i++)
            {
                kml.WriteStartElement("Placemark");
                kml.WriteElementString("name", "Flag_" + i.ToString());

                kml.WriteStartElement("Style");
                kml.WriteStartElement("IconStyle");

                if (flagPts[i].color == 0)  //red - xbgr
                    kml.WriteElementString("color", "ff4400ff");
                if (flagPts[i].color == 1)  //grn - xbgr
                    kml.WriteElementString("color", "ff44ff00");
                if (flagPts[i].color == 2)  //yel - xbgr
                    kml.WriteElementString("color", "ff44ffff");

                kml.WriteEndElement(); //IconStyle
                kml.WriteEndElement(); //Style

                kml.WriteElementString("name", (i + 1).ToString());
                kml.WriteStartElement("Point");
                kml.WriteElementString("coordinates", flagPts[i].Longitude.ToString(CultureInfo.InvariantCulture) +
                    "," + flagPts[i].Latitude.ToString(CultureInfo.InvariantCulture) + ",0");
                kml.WriteEndElement(); //Point
                kml.WriteEndElement(); // <Placemark>
            }
            kml.WriteEndElement(); // <Folder>   
                                   //End of Flags

            //Boundary  ----------------------------------------------------------------------
            kml.WriteStartElement("Folder");
            kml.WriteElementString("name", "Boundaries");

            for (int i = 0; i < bnd.Boundaries.Count; i++)
            {
                kml.WriteStartElement("Placemark");
                if (i == 0) kml.WriteElementString("name", currentFieldDirectory);

                //lineStyle
                kml.WriteStartElement("Style");
                kml.WriteStartElement("LineStyle");
                if (i == 0) kml.WriteElementString("color", "ffdd00dd");
                else kml.WriteElementString("color", "ff4d3ffd");
                kml.WriteElementString("width", "4");
                kml.WriteEndElement(); // <LineStyle>

                kml.WriteStartElement("PolyStyle");
                if (i == 0) kml.WriteElementString("color", "407f3f55");
                else kml.WriteElementString("color", "703f38f1");
                kml.WriteEndElement(); // <PloyStyle>
                kml.WriteEndElement(); //Style

                kml.WriteStartElement("Polygon");
                kml.WriteElementString("tessellate", "1");
                kml.WriteStartElement("outerBoundaryIs");
                kml.WriteStartElement("LinearRing");

                //coords
                kml.WriteStartElement("coordinates");
                string bndPts = "";
                if (bnd.Boundaries[i].Polygon.Points.Count > 3)
                    bndPts = GetBoundaryPointsLatLon(i);
                kml.WriteRaw(bndPts);
                kml.WriteEndElement(); // <coordinates>
                kml.WriteEndElement(); // <Linear>
                kml.WriteEndElement(); // <OuterBoundary>
                kml.WriteEndElement(); // <Polygon>
                kml.WriteEndElement(); // <Placemark>
            }

            kml.WriteEndElement(); // <Folder>  
            //End of Boundary

            //Sections  ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
            kml.WriteStartElement("Folder");
            kml.WriteElementString("name", "Sections");

            string secPts = "";
            int cntr = 0;

            //for every new chunk of patch
            foreach (var triList in PatchDrawList)
            {
                if (triList.Count > 0)
                {
                    kml.WriteStartElement("Placemark");
                    kml.WriteElementString("name", "Sections_" + cntr.ToString());
                    cntr++;

                    string collor = "F0" + ((byte)(triList[0].Heading)).ToString("X2") +
                        ((byte)(triList[0].Northing)).ToString("X2") + ((byte)(triList[0].Easting)).ToString("X2");

                    //lineStyle
                    kml.WriteStartElement("Style");

                    kml.WriteStartElement("LineStyle");
                    kml.WriteElementString("color", collor);
                    //kml.WriteElementString("width", "6");
                    kml.WriteEndElement(); // <LineStyle>

                    kml.WriteStartElement("PolyStyle");
                    kml.WriteElementString("color", collor);
                    kml.WriteEndElement(); // <PloyStyle>
                    kml.WriteEndElement(); //Style

                    kml.WriteStartElement("Polygon");
                    kml.WriteElementString("tessellate", "1");
                    kml.WriteStartElement("outerBoundaryIs");
                    kml.WriteStartElement("LinearRing");

                    //coords
                    kml.WriteStartElement("coordinates");
                    secPts = "";
                    for (int k = 1; k < triList.Count; k += 2)
                    {
                        secPts += ConvertLocalToWGS84(triList[k].Northing, triList[k].Easting);
                    }
                    for (int k = triList.Count - 1; k > 1; k -= 2)
                    {
                        secPts += ConvertLocalToWGS84(triList[k].Northing, triList[k].Easting);
                    }
                    secPts += ConvertLocalToWGS84(triList[1].Northing, triList[1].Easting);
                    kml.WriteRaw(secPts);
                    kml.WriteEndElement(); // <coordinates>

                    kml.WriteEndElement(); // <LinearRing>
                    kml.WriteEndElement(); // <outerBoundaryIs>
                    kml.WriteEndElement(); // <Polygon>

                    kml.WriteEndElement(); // <Placemark>
                }
            }


            kml.WriteEndElement(); // <Folder>
            //End of sections

            //end of document
            kml.WriteEndElement(); // <Document>
            kml.WriteEndElement(); // <kml>

            //The end
            kml.WriteEndDocument();

            kml.Flush();

            //Write the XML to file and close the kml
            kml.Close();

            if (File.Exists(dirField + "Field.kml"))
                File.Delete(dirField + "Field.kml");
            File.Move(dirField + "Field.Tmp", dirField + "Field.kml");
        }

        public string GetBoundaryPointsLatLon(int bndNum)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bnd.Boundaries[bndNum].Polygon.Points.Count; i++)
            {
                sb.Append(ConvertLocalToWGS84(bnd.Boundaries[bndNum].Polygon.Points[i].Northing, bnd.Boundaries[bndNum].Polygon.Points[i].Easting));
            }
            return sb.ToString();
        }
    }
}