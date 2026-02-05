#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class NortonEquivalent : DipoleCircuitElementBase, ICircuitElement
    {
        private CurrentSource _currentSource;
        private Resistor _resistor;

        public Complex VoltageOpen
        {
            get => CurrentShort * _resistor.Resistance;
            set => CurrentShort = value / _resistor.Resistance;
        }

        public Complex CurrentShort
        {
            get => _currentSource.SourceCurrent;
            set => _currentSource.SourceCurrent = value;
        }

        public double Resistance
        {
            get => _resistor.Resistance;
            set => _resistor.Resistance = value;
        }

        /// <summary>
        /// Gets the current output by the Norton equivalent circuit
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
