using System;

namespace AgOpenGPS
{
    public class CFieldData
    {
        //constructor
        public CFieldData(FormGPS _f)
        {
            mf = _f;
            workedAreaTotal = 0;
            workedAreaTotalUser = 0;
            userSquareMetersAlarm = 0;
        }
        private readonly FormGPS mf;

        //all the section area added up;
        public double workedAreaTotal;

        //just a cumulative tally based on distance and eq width.
        public double workedAreaTotalUser;

        //accumulated user distance
        public double distanceUser;

        public double barPercent = 0;

        public double overlapPercent = 0;

        //Outside area minus inner boundaries areas (m)
        public double areaBoundaryOuterLessInner;

        //used for overlap calcs - total done minus overlap
        public double actualAreaCovered;

        //Inner area of outer boundary(m)
        public double areaOuterBoundary;

        //not really used - but if needed
        public double userSquareMetersAlarm;

        //Area inside Boundary less inside boundary areas
        public string AreaBoundaryLessInnersHectares
        { 
            get
            {
                if ((areaBoundaryOuterLessInner) < 404048) return (areaBoundaryOuterLessInner * Glm.m2ha).ToString("N2");
                else return (areaBoundaryOuterLessInner * Glm.m2ha).ToString("N1");
            }
        }

        public string AreaBoundaryLessInnersAcres
        {
            get
            {
                if ((areaBoundaryOuterLessInner) < 404048) return (areaBoundaryOuterLessInner * Glm.m2ac).ToString("N2");
                else return (areaBoundaryOuterLessInner * Glm.m2ac).ToString("N1");
            }
        }

        //USer tally string
        public string WorkedUserHectares { get { return (workedAreaTotalUser * Glm.m2ha).ToString("N2"); } }

        //user tally string
        public string WorkedUserAcres { get { return (workedAreaTotalUser * Glm.m2ac).ToString("N2"); } }

        //String of Area worked
        public string WorkedAcres
        {
            get
            {
                if (workedAreaTotal < 404048) return (workedAreaTotal * 0.000247105).ToString("N2");
                else return (workedAreaTotal * 0.000247105).ToString("N1");
            }
        }

        public string WorkedHectares
        {
            get
            {
                if (workedAreaTotal < 99000) return (workedAreaTotal * 0.0001).ToString("N2");
                else return (workedAreaTotal * 0.0001).ToString("N1");
            }
        }

        //User Distance strings
        public string DistanceUserMeters { get { return Convert.ToString((UInt16)(distanceUser)) + " m"; } }

        public string DistanceUserFeet { get { return Convert.ToString((UInt16)(distanceUser * Glm.m2ft)) + " ft"; } }

        //remaining area to be worked
        public string WorkedAreaRemainHectares { get { return ((areaBoundaryOuterLessInner - workedAreaTotal) * Glm.m2ha).ToString("N2"); } }

        public string WorkedAreaRemainAcres { get { return ((areaBoundaryOuterLessInner - workedAreaTotal) * Glm.m2ac).ToString("N2"); } }

        //overlap strings

        public string WorkedAreaRemainPercentage
        {
            get
            {
                if (areaBoundaryOuterLessInner > 10)
                {
                    barPercent = ((areaBoundaryOuterLessInner - workedAreaTotal) * 100 / areaBoundaryOuterLessInner);
                    return barPercent.ToString("N1") + "%";
                }
                else
                {
                    barPercent = 0;
                    return "0.00%";
                }
            }
        }

        public string TimeTillFinished
        {
            get
            {
                if (mf.pn.speed > 2 && mf.Guidance.GuidanceWidth > 0)
                {
                    var timeSpan = TimeSpan.FromHours((areaBoundaryOuterLessInner - workedAreaTotal) * Glm.m2ha
                        / (mf.Guidance.GuidanceWidth * mf.pn.speed * 0.1));

                    if (timeSpan.Days > 0) return timeSpan.TotalDays.ToString("N0") + " \n Days";
                    else return timeSpan.Hours.ToString("00") + ":" + timeSpan.Minutes.ToString("00");
                }
                else return "\u221E Hrs";
            }
        }

        public string WorkRateHectares { get { return (mf.Guidance.GuidanceWidth * mf.pn.speed * 0.1).ToString("N1"); } }
        public string WorkRateAcres { get { return (mf.Guidance.GuidanceWidth * mf.pn.speed * 0.2471).ToString("N1"); } }


        public void UpdateFieldBoundaryGUIAreas()
        {
            if (mf.bnd.Boundaries.Count > 0)
            {
                areaOuterBoundary = mf.bnd.Boundaries[0].Area;
                areaBoundaryOuterLessInner = areaOuterBoundary;

                for (int i = 1; i < mf.bnd.Boundaries.Count; i++)
                {
                    areaBoundaryOuterLessInner -= mf.bnd.Boundaries[i].Area;
                }
            }
            else
            {
                areaOuterBoundary = 0;
                areaBoundaryOuterLessInner = 0;            
            }
            mf.CalculateMinMax();
        }
    }
}