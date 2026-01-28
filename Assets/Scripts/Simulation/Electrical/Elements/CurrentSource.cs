#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class CurrentSource : CircuitElementBase, ICircuitElement, IFrequencySource
    {
        private Complex? _appliedCurrent;

        public RefCounted<MNASolver.Unknown>? NodeA { get; }

        public RefCounted<MNASolver.Unknown>? NodeB { get; }

        public Complex Current { get; set; }

        public double? Frequency { get; set; }

        public CurrentSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit)
        {
            NodeA = nodeA?.Clone();
            NodeB = nodeB?.Clone();
        }

        public override void Dispose()
        {
            RemoveState();
        }

        public override void UpdateState()
        {
            if (Current == _appliedCurrent) { return; }

            RemoveState();

            Circuit.Solver.AddCurrent(NodeA?.Value, NodeB?.Value, Current);
            _appliedCurrent = Current;
        }

        public override void ApplyState()
        {
        }

        private void RemoveState()
        {
            if (_appliedCurrent != null)
            {
                Circuit.Solver.AddCurrent(NodeA?.Value, NodeB?.Value, -_appliedCurrent.Value);
                _appliedCurrent = null;
            }
        }
    }
}
