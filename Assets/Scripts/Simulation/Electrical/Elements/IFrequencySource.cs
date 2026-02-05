#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Interface for any element that is a "frequency source" in the circuit.
    /// 
    /// Each frequency source can specify a desired AC frequency, or 0 for DC; or `null` to indicate that the element will "follow" the circuit's frequency if set by any other source.
    /// 
    /// A circuit can only operate at one frequency at a time - if there are multiple frequency sources with non-null frequencies that don't match, the circuit will fail to find a solution.
    /// </summary>
    public interface IFrequencySource
    {
        public double? Frequency { get; }
    }
}