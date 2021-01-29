using System;
using System.Globalization;

namespace AgOpenGPS
{
    public class CSequence
    {
        private readonly FormGPS mf;

        //an array of events to take place
        public SeqEvent[] seqEnter;

        public SeqEvent[] seqExit;

        public string pos1 = "Manual Button";
        public string pos2 = "Auto Button";
        public string pos3 = "";
        public string pos4 = "";
        public string pos5 = "";
        public string pos6 = "";
        public string pos7 = "";
        public string pos8 = "";

        public bool isSequenceTriggered, isEntering;

        public struct SeqEvent
        {
            public int function; //event name
            public int action; //where in the turn procedure
            public bool isTrig;
            public double distance; //trigger distance to turn on

            public SeqEvent(int function, int action, bool isTrig, double distance)
            {
                this.function = function;
                this.action = action;
                this.isTrig = isTrig;
                this.distance = distance;
            }
        }

        //constructor
        public CSequence(FormGPS _f)
        {
            mf = _f;

            //Fill in the strings for comboboxes - editable
            string line = Properties.Vehicle.Default.seq_FunctionList;
            string[] words = line.Split(',');

            pos3 = words[0];
            pos4 = words[1];
            pos5 = words[2];
            pos6 = words[3];
            pos7 = words[4];
            pos8 = words[5];

            string sentence;

            seqEnter = new SeqEvent[FormGPS.MAXFUNCTIONS];
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
            {
                seqEnter[i].function = 0;
                seqEnter[i].action = 0;
                seqEnter[i].isTrig = true;
                seqEnter[i].distance = 0;
            }

            sentence = Properties.Vehicle.Default.seq_FunctionEnter;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seqEnter[i].function);

            sentence = Properties.Vehicle.Default.seq_ActionEnter;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seqEnter[i].action);

            sentence = Properties.Vehicle.Default.seq_DistanceEnter;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                double.TryParse(words[i], NumberStyles.Float, CultureInfo.InvariantCulture, out seqEnter[i].distance);

            seqExit = new SeqEvent[FormGPS.MAXFUNCTIONS];
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
            {
                seqExit[i].function = 0;
                seqExit[i].action = 0;
                seqExit[i].isTrig = true;
                seqExit[i].distance = 0;
            }

            sentence = Properties.Vehicle.Default.seq_FunctionExit;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seqExit[i].function);

            sentence = Properties.Vehicle.Default.seq_ActionExit;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++) int.TryParse(words[i], out seqExit[i].action);

            sentence = Properties.Vehicle.Default.seq_DistanceExit;
            words = sentence.Split(',');
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                double.TryParse(words[i], NumberStyles.Float, CultureInfo.InvariantCulture, out seqExit[i].distance);
        }

        public void DisableSeqEvent(int index, bool isEnter)
        {
            if (isEnter)
            {
                seqEnter[index].function = 0;
                seqEnter[index].action = 0;
                seqEnter[index].isTrig = true;
                seqEnter[index].distance = 0;
            }
            else
            {
                seqExit[index].function = 0;
                seqExit[index].action = 0;
                seqExit[index].isTrig = true;
                seqExit[index].distance = 0;
            }
        }

        //reset trig flag to false on all array elements with a function
        public void ResetSequenceEventTriggers()
        {
            for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
            {
                if (mf.seq.seqEnter[i].function != 0) mf.seq.seqEnter[i].isTrig = false;
                if (mf.seq.seqExit[i].function != 0) mf.seq.seqExit[i].isTrig = false;
            }
        }

        //determine when if and how functions are triggered
        public void DoSequenceEvent(bool Force)
        {
            if (isSequenceTriggered)
            {
                int c = 0;
                for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                {
                    //checked for any not triggered yet (false) - if there is, not done yet
                    if (!mf.seq.seqEnter[i].isTrig) c++;
                    if (!mf.seq.seqExit[i].isTrig) c++;
                }

                if (c == 0)
                {
                    //sequences all done so reset everything and leave
                    isSequenceTriggered = false;
                    ResetSequenceEventTriggers();
                    mf.distanceToolToTurnLine = -2222;
                    return;
                }

                bool tt = mf.Guidance.ytLength - mf.Guidance.onA > 0.5 * mf.Guidance.ytLength;

                for (int i = 0; i < FormGPS.MAXFUNCTIONS; i++)
                {
                    //have we gone past the distance and still haven't done it
                    if (tt && mf.Guidance.onA >= mf.seq.seqEnter[i].distance && !mf.seq.seqEnter[i].isTrig)
                    {
                        //it shall only run once
                        mf.seq.seqEnter[i].isTrig = true;
                        //send the function and action to perform
                        mf.DoYouTurnSequenceEvent(mf.seq.seqEnter[i].function, mf.seq.seqEnter[i].action);
                        mf.UpdateSendDataText("Uturn: " + Convert.ToString(mf.mc.Send_Uturn[5], 2).PadLeft(8, '0'));
                    }
                    else if (!tt && !mf.seq.seqExit[i].isTrig && (Force || Math.Round(mf.Guidance.ytLength - mf.Guidance.onA, 1) <= mf.seq.seqExit[i].distance))
                    {
                        //it shall only run once
                        mf.seq.seqExit[i].isTrig = true;

                        //send the function and action to perform
                        mf.DoYouTurnSequenceEvent(mf.seq.seqExit[i].function, mf.seq.seqExit[i].action);
                        mf.UpdateSendDataText("Uturn: " + Convert.ToString(mf.mc.Send_Uturn[5], 2).PadLeft(8, '0'));
                    }
                }
            }
        }
    }
}