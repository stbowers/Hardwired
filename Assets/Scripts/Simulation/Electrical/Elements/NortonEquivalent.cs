#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// Represents a "norton equivalent" circuit, which consists of a current source in parallel with a resistor.
    /// 
    /// Norton's theorem states that a complex circuit of voltage sources and resistances can be simplified into a single current source with parallel resistor.
    /// Norton's theorem is the dual to Thévenin's theorem, but we generally prefer to use a Norton equivalent element instead of a Thévenin equivalent, because
    /// in our architecture a Thévenin circuit (which is a voltage source with series resistor) would introduce 2 extra unknowns into the MNA matrix (1 for the
    /// voltage source, and 1 for the internal node between the voltage source and resistor) compared to the equivalent Norton circuit.
    /// </summary>
    public class NortonEquivalent : DipoleCircuitElementBase, ICircuitElement
    {
        private CurrentSource _currentSource;
        private Resistor _resistor;

        public CurrentSource CurrentSource => _currentSource;

        public Resistor Resistor => _resistor;

        /// <summary>
        /// Gets or sets the "open voltage" - i.e. the voltage that would be measured between A and B when "open circuited" (no connection/load).
        /// This is in practice the voltage of the equivalent Thévenin voltage source.
        /// 
        /// Only one of `VoltageOpen` or `CurrentShort` needs to be set at any given time; the other will reflect the new value.
        /// </summary>
        public Complex VoltageOpen
        {
            get => CurrentShort * _resistor.Resistance;
            set => CurrentShort = value / _resistor.Resistance;
        }

        /// <summary>
        /// Gets or sets the "short current" - i.e. the current that would be measured between A and B when "short circuited" (direct conneciton/no resistance)
        /// 
        /// Only one of `VoltageOpen` or `CurrentShort` needs to be set at any given time; the other will reflect the new value.
        /// </summary>
        public Complex CurrentShort
        {
            get => _currentSource.SourceCurrent;
            set => _currentSource.SourceCurrent = value;
        }

        /// <summary>
        /// The internal resistance in parallel with the current source
        /// </summary>
        public double Resistance
        {
            get => _resistor.Resistance;
            set => _resistor.Resistance = value;
        }

        public double? Frequency
        {
            get => _currentSource.Frequency;
            set => _currentSource.Frequency = value;
        }

        /// <summary>
        /// Gets the current output by the Norton equivalent circuit (i.e. the current flowing from node A to node B "outside" of the norton circuit, _not_ the current produced by the current source)
        /// </summary>
        public override Complex Current => -CurrentShort - _resistor.Current;

        public NortonEquivalent(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _currentSource = new(circuit, nodeA, nodeB);
            _resistor = new(circuit, nodeB, nodeA);
        }

        public override void Dispose()
        {
            base.Dispose();

            _currentSource.Dispose();
            _resistor.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            _currentSource.UpdateState();
            _resistor.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            _currentSource.ApplyState();
            _resistor.ApplyState();
        }
    }
}
