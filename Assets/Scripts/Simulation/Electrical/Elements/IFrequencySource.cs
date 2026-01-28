#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public interface IFrequencySource
    {
        public double? Frequency { get; }
    }
}