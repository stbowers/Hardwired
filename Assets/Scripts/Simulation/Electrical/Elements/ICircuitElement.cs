#nullable enable

using System;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Common interface for circuit elements, which represent the basic building blocks of a circuit, such as a resistor, voltage source, or capacitor.
    /// 
    /// Elements can be composed of multiple other "child" elements to represent more complex behaviors.
    /// </summary>
    public interface ICircuitElement : IDisposable
    {
        /// <summary>
        /// The circuit this element belongs to.
        /// </summary>
        public Circuit Circuit { get; }

        /// <summary>
        /// Called on each element in the circuit before solving; allows each element to modify its "stamp" in the MNA matricies, or make any other changes
        /// that might affect the solution for this tick.
        /// </summary>
        public void UpdateState();

        /// <summary>
        /// Called on each element in the circuit after solving; allows each element to update output properties based on the solution for this tick, or apply
        /// any time-varying behaviors (such as integrating some value over time, etc).
        /// </summary>
        public void ApplyState();

        public string DebugInfo() => "";
    }
}