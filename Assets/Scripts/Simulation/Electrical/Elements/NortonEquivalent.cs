#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class NortonEquivalent : ICircuitElement, IDipoleCircuitElement
    {
        private CurrentSource _currentSource;
        private Resistor _resistor;

        public Circuit Circuit { get; }

        public RefCounted<MNASolver.Unknown>? NodeA { get; }

        public RefCounted<MNASolver.Unknown>? NodeB { get; }

        public Complex VoltageOpen
        {
            get => CurrentShort * _resistor.Resistance;
            set => CurrentShort = value / _resistor.Resistance;
        }

        public Complex CurrentShort
        {
            get => _currentSource.Current;
            set => _currentSource.Current = value;
        }

        public double Resistance
        {
            get => _resistor.Resistance;
            set => _resistor.Resistance = value;
        }

        /// <summary>
        /// Gets the current output by the Norton equivalent circuit
        /// </summary>
        public Complex Current => CurrentShort - _resistor.Current;

        public NortonEquivalent(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB)
        {
            Circuit = circuit;

            _currentSource = new(circuit, nodeA, nodeB);
            _resistor = new(circuit, nodeA, nodeB);
        }

        public void Dispose()
        {
            _currentSource.Dispose();
            _resistor.Dispose();
        }

        public void UpdateState()
        {
            _currentSource.UpdateState();
            _resistor.UpdateState();
        }

        public void ApplyState()
        {
            _currentSource.ApplyState();
            _resistor.ApplyState();
        }
    }
}
