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
    }
}