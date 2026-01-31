#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class Resistor : DipoleCircuitElementBase, ICircuitElement
    {
        /// <summary>
        /// Resistance value to emulate an open circuit (i.e. no connection).
        /// Should be relatively large, to avoid leaking any significant current, but not too large to avoid causing an ill-conditioned (singular or near-singular) matrix, which can cause problems for the solver.
        /// </summary>
        public const double R_OPEN = 1e10;

        /// <summary>
        /// Resistance value to to emulate a short circuit (i.e. direct connection).
        /// Should be relatively small, to avoid voltage drop across the connection, but not too small as to introduce numerical errors into the solver.
        /// </summary>
        public const double R_SHORT = 1e-4;

        private double? _appliedResistance;

        public double Resistance { get; set; }

        public override Complex Current => VoltageDelta / Resistance;

        public Resistor(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            circuit.Solver.AddResistance(NodeA?.Value, null, R_OPEN);
            circuit.Solver.AddResistance(NodeB?.Value, null, R_OPEN);
        }

        public override void Dispose()
        {
            RemoveState();

            Circuit.Solver.AddResistance(NodeA?.Value, null, -R_OPEN);
            Circuit.Solver.AddResistance(NodeB?.Value, null, -R_OPEN);

            NodeA?.Dispose();
            NodeB?.Dispose();
        }

        public override void UpdateState()
        {
            if (Resistance == _appliedResistance) { return; }

            RemoveState();

            if (Resistance > 0f)
            {
                Circuit.Solver.AddResistance(NodeA?.Value, NodeB?.Value, Resistance);
                _appliedResistance = Resistance;
            }
        }

        public override void ApplyState()
        {
        }

        private void RemoveState()
        {
            if (_appliedResistance != null)
            {
                Circuit.Solver.AddResistance(NodeA?.Value, NodeB?.Value, -_appliedResistance.Value);
                _appliedResistance = null;
            }
        }
    }
}
