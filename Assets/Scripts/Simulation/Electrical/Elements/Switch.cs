#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class Switch : DipoleCircuitElementBase, ICircuitElement
    {
        private Resistor _resistor;

        public bool Closed { get; set; }

        public override Complex Current => _resistor.Current;

        public Switch(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _resistor = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            base.Dispose();

            _resistor.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            _resistor.Resistance = Closed ? Resistor.R_SHORT : Resistor.R_OPEN;
            _resistor.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            _resistor.ApplyState();
        }
    }
}
