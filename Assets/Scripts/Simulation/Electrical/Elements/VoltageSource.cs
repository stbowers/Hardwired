#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class VoltageSource : CircuitElementBase, ICircuitElement, IDipoleCircuitElement, IFrequencySource
    {
        private MNASolver.Unknown _i;
        private Complex? _appliedVoltage;

        public RefCounted<MNASolver.Unknown>? NodeA { get; }

        public RefCounted<MNASolver.Unknown>? NodeB { get; }

        public double? Frequency { get; set; }

        public Complex VoltageDelta { get; set; }

        public Complex Current => Circuit.Solver.GetValue(_i) ?? Complex.Zero;

        public VoltageSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit)
        {
            NodeA = nodeA?.Clone();
            NodeB = nodeB?.Clone();

            Circuit.Solver.AddVoltageSource(NodeA?.Value, NodeB?.Value, ref _i);
        }

        public override void Dispose()
        {
            NodeA?.Dispose();
            NodeB?.Dispose();
            _i?.Dispose();
        }

        public override void UpdateState()
        {
            if (VoltageDelta == _appliedVoltage) { return; }

            Circuit.Solver.SetVoltage(_i, VoltageDelta);
            _appliedVoltage = Current;
        }

        public override void ApplyState()
        {
        }
    }
}
