#nullable enable


using System;
using System.Numerics;
using Assets.Scripts.Util;

namespace Hardwired.Utility.Extensions
{
    public static class ComplexExtensions
    {
        public static string ToStringPrefix(this Complex value, string unit, string color, bool adaptive = true)
        {
            double magnitude = value.Magnitude;
            double phaseDegrees = 180f * value.Phase / Math.PI;

            return $"{magnitude.ToStringPrefix(unit, color, adaptive)} @ {phaseDegrees.ToStringPrefix("°", color, false)}";
        }

        /// <summary>
        /// Formats an AC phasor value such as voltage or current.
        /// 
        /// For AC (f > 0):
        /// {Magnitude} {Unit} @ {Phase} ° ({Frequency} Hz)
        /// 
        /// For DC (f == 0 || f == null):
        /// {Magnitude} {Unit}
        /// </summary>
        /// <param name="value"></param>
        /// <param name="f"></param>
        /// <param name="unit"></param>
        /// <param name="color"></param>
        /// <param name="adaptive"></param>
        /// <returns></returns>
        public static string ToStringPrefix(this Complex value, double? f, string unit, string color, bool adaptive = true)
        {
            double magnitude = value.Magnitude;
            double phaseDegrees = 180f * value.Phase / Math.PI;

            // AC
            if (f > 0)
            {
                return $"{ToStringPrefix(value, unit, color, adaptive)} ({f.Value.ToStringPrefix("Hz", color, adaptive)})";
            }
            // DC
            else
            {
                return magnitude.ToStringPrefix(unit, color, adaptive);
            }
        }
    }
}