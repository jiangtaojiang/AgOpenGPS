//Please, if you use this, share the improvements

using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    /// <summary>
    /// Represents a three dimensional vector.
    /// </summary>
    /// 
    public struct Vec3
    {
        public double Easting, Northing, Heading;

        public Vec3(double easting, double northing, double heading)
        {
            Northing = northing;
            Easting = easting;
            Heading = heading;
        }

        public static Vec3 operator *(Vec3 self, double s)
        {
            return new Vec3(self.Easting * s, self.Northing * s, self.Heading * s);
        }

        public static Vec3 operator /(Vec3 self, double s)
        {
            return new Vec3(self.Easting * s, self.Northing * s, self.Heading * s);
        }

        public static Vec3 operator +(Vec3 lhs, Vec3 rhs)
        {
            return new Vec3(lhs.Easting + rhs.Easting, lhs.Northing + rhs.Northing, lhs.Heading + rhs.Heading);
        }

        public static Vec3 operator -(Vec3 lhs, Vec3 rhs)
        {
            return new Vec3(lhs.Easting - rhs.Easting, lhs.Northing - rhs.Northing, lhs.Heading - rhs.Heading);
        }
    }

    //

    /// <summary>
    /// easting, northing, heading, boundary#
    /// </summary>
    public struct Vec4
    {
        public double Easting;
        public double Northing;
        public double Heading;
        public double Time;
        public int Index;

        public Vec4(double easting, double northing, double heading, double time, int index)
        {
            Northing = northing;
            Easting = easting;
            Heading = heading;
            Time = time;
            Index = index;
        }
    }

    public class ToolSettings
    {
        public double LookAheadOn = 1, LookAheadOff = 0.8, TurnOffDelay = 0, MappingOnDelay = 0.95, MappingOffDelay = 0.85;
        public double ToolWheelLength = 6, TankWheelLength = 1.5, ToolHitchLength = 0.5, TankHitchLength = 0.5, HitchLength = 0.5, ToolOffset = 0, SlowSpeedCutoff = 0;
        public bool BehindPivot = true, Trailing = false, TBT = false;
        public int MinApplied = 0;
        public List<double[]> Sections = new List<double[]> {};
        public ToolSettings()
        {}

        public ToolSettings(ToolSettings settings)
        {
            LookAheadOn = settings.LookAheadOn;
            LookAheadOff = settings.LookAheadOff;
            TurnOffDelay = settings.TurnOffDelay;
            MappingOnDelay = settings.MappingOnDelay;
            MappingOffDelay = settings.MappingOffDelay;
            ToolWheelLength = settings.ToolWheelLength;
            TankWheelLength = settings.TankWheelLength;
            ToolHitchLength = settings.ToolHitchLength;
            TankHitchLength = settings.TankHitchLength;
            HitchLength = settings.HitchLength;
            ToolOffset = settings.ToolOffset;
            SlowSpeedCutoff = settings.SlowSpeedCutoff;
            BehindPivot = settings.BehindPivot;
            Trailing = settings.Trailing;
            TBT = settings.TBT;
            MinApplied = settings.MinApplied;
            Sections = settings.Sections;
        }
    }

    public struct Vec2
    {
        public double Easting;
        public double Northing;

        public Vec2(double easting, double northing)
        {
            Easting = easting;
            Northing = northing;
        }

        public Vec2(Vec2 point)
        {
            Easting = point.Easting;
            Northing = point.Northing;
        }

        public Vec2 Normalize()
        {
            double length = GetLength();
            if (Math.Abs(length) < double.Epsilon)
            {
                throw new DivideByZeroException("Trying to normalize a vector with length of zero.");
            }

            return new Vec2(Easting /= length, Northing /= length);
        }

        public double GetLength()
        {
            return Math.Sqrt((Easting * Easting) + (Northing * Northing));
        }

        public double GetLengthSquared()
        {
            return (Easting * Easting) + (Northing * Northing);
        }

        public static Vec2 operator *(Vec2 self, double s)
        {
            return new Vec2(self.Easting * s, self.Northing * s);
        }
        public static Vec2 operator /(Vec2 self, double s)
        {
            return new Vec2(self.Easting / s, self.Northing / s);
        }

        public static Vec2 operator +(Vec2 lhs, Vec2 rhs)
        {
            return new Vec2(lhs.Easting + rhs.Easting, lhs.Northing + rhs.Northing);
        }
        public static Vec2 operator -(Vec2 lhs, Vec2 rhs)
        {
            return new Vec2(lhs.Easting - rhs.Easting, lhs.Northing - rhs.Northing);
        }
        public static Vec2 operator -(Vec3 lhs, Vec2 rhs)
        {
            return new Vec2(lhs.Easting - rhs.Easting, lhs.Northing - rhs.Northing);
        }
    }
}