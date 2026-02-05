#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Base class for elements with two pins/nodes.
    /// Note that by convention, the voltage delta between pins is calculated as `V(A) - V(B)`; in other words pin A is the "positive" pin, and pin B is the "negative" pin.
    /// </summary>
    public abstract class DipoleCircuitElementBase : CircuitElementBase, ICircuitElement
    {
        /// <summary>
        /// The node for pin "A". By convention this is usually the positive pin
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeA { get; }

        /// <summary>
        /// The node for pin "B". By convention this is usually the negative pin
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeB { get; }

        /// <summary>
        /// The current flowing from node A to B
        /// 
        /// Note - voltage sources and current sources have a negative current in this convention, since the current flows from node B to A in those cases.
        /// This also means for voltage and current sources, Power will also be negative (indicating power flowing _out_ of the source).
        /// </summary>
        public abstract Complex Current { get; }

        /// <summary>
        /// The voltage at node "A", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageA => Circuit.Solver.GetValueOrDefault(NodeA?.Value);
        
        /// <summary>
        /// The voltage at node "B", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageB => Circuit.Solver.GetValueOrDefault(NodeB?.Value);

        /// <summary>
        /// The voltage delta between nodes A and B
        /// </summary>
        public virtual Complex VoltageDelta => VoltageA - VoltageB;

        /// <summary>
        /// The apparent (complex) power being "consumed" by this component.
        /// 
        /// The real part indicates the "real power" consumed (or produced, if negative) by this element.
        /// The imaginary part indicates the "reactive power", which only exists in AC circuits and indicates how much power is flowing back and forth without actually being used due to voltage
        /// and current being out of phase.
        /// </summary>
        public virtual Complex Power => VoltageDelta * Current.Conjugate();

        /// <summary>
        /// The ratio of real power to apparent power (i.e. `Power.Real / Power.Magnitude`)
        /// </summary>
        public virtual double PowerFactor
        {
            get
            {
                var s = Power;
                return s.Real / s.Magnitude;
            }
        }

        public DipoleCircuitElementBase(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit)
        {
            NodeA = nodeA?.Clone();
            NodeB = nodeB?.Clone();
        }

        string ICircuitElement.DebugInfo() => $"A: {NodeA?.Value.Index ?? -1}, B: {NodeB?.Value.Index ?? -1}";
    }
}
