#nullable enable

using System;
using System.Numerics;
using MathNet.Numerics;

namespace Hardwired.Utility.Extensions
{
    public static class MathExtensions
    {
        public static readonly double SQRT2 = Math.Sqrt(2);

        public static double RootMeanSquare(this Complex value)
            => value.Magnitude / SQRT2;
    }
}