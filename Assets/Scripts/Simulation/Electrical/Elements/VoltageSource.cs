#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class VoltageSource : DipoleCircuitElementBase, ICircuitElement, IFrequencySource
    {
        private MNASolver.Unknown _i;
        private Complex? _appliedVoltage;

        public double? Frequency { get; set; }

        public override Complex Current => Circuit.Solver.GetValue(_i) ?? Complex.Zero;

        public Complex SourceVoltage { get; set; }

        public VoltageSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            Circuit.Solver.AddVoltageSource(NodeB?.Value, NodeA?.Value, ref _i);
        }

        public override void Dispose()
        {
            NodeA?.Dispose();
            NodeB?.Dispose();
            _i?.Dispose();
        }

        public override void UpdateState()
        {
            if (SourceVoltage == _appliedVoltage) { return; }

            Circuit.Solver.SetVoltage(_i, SourceVoltage);
            _appliedVoltage = Current;
        }

        public override void ApplyState()
        {
        }
    }
}
