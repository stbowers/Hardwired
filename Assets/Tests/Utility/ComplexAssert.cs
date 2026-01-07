#nullable enable

using System.Numerics;
using NUnit.Framework;

namespace Hardwired.Tests.Utility
{
    public static class ComplexAssert
    {
        public static void AreEqual(Complex expected, Complex actual, double delta = 0.001)
        {
            var difference = expected - actual;

            if (difference.Magnitude > delta)
            {
                throw new AssertionException($"Values are not equal. Expected: {expected}, actual: {actual}");
            }
        }
    }
}

