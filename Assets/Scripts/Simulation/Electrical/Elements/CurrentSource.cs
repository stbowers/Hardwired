#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class CurrentSource : DipoleCircuitElementBase, ICircuitElement, IFrequencySource
    {
        private Complex? _appliedCurrent;

        /// <summary>
        /// The current being produced by this source, which flows from node "B" (negative) to node "A" (positive)
        /// 
        /// Note that by convention `Current = -SourceCurrent`, since `Current` flows from node A to B, but `SourceCurrent` flows from node B to A.
        /// </summary>
        public Complex SourceCurrent { get; set; }

        public override Complex Current => -SourceCurrent;

        public double? Frequency { get; set; }

        public CurrentSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
        }

        public override void Dispose()
        {
            RemoveState();
        }

        public override void UpdateState()
        {
            if (SourceCurrent == _appliedCurrent) { return; }

            RemoveState();

            Circuit.Solver.AddCurrent(NodeB?.Value, NodeA?.Value, SourceCurrent);
            _appliedCurrent = SourceCurrent;
        }

        public override void ApplyState()
        {
        }

        private void RemoveState()
        {
            if (_appliedCurrent != null)
            {
                Circuit.Solver.AddCurrent(NodeB?.Value, NodeA?.Value, -_appliedCurrent.Value);
                _appliedCurrent = null;
            }
        }
    }
}
