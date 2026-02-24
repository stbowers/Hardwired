#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class PowerSource : DipoleCircuitElementBase, ICircuitElement
    {
        private NortonEquivalent _nortonEquivalent;

        public NortonEquivalent NortonEquivalent => _nortonEquivalent;

        public override Complex Current => _nortonEquivalent.Current;

        /// <summary>
        /// The nominal output voltage of the power source when there is no load.
        /// </summary>
        public double VoltageNominal { get; set; }

        /// <summary>
        /// The AC frequency this power source should generate, or 0 for DC, or null to follow the grid.
        /// </summary>
        public double? Frequency
        {
            get => _nortonEquivalent.Frequency;
            set => _nortonEquivalent.Frequency = value;
        }

        public double PowerAvailable { get; set; }

        public PowerSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _nortonEquivalent = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            base.Dispose();

            _nortonEquivalent.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            if (Math.Abs(PowerAvailable) < 0.1)
            {
                _nortonEquivalent.Resistance = Resistor.R_OPEN;
            }
            else
            {
                _nortonEquivalent.Resistance = VoltageNominal * VoltageNominal / PowerAvailable;
            }

            _nortonEquivalent.CurrentShort = PowerAvailable / VoltageNominal;

            _nortonEquivalent.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            _nortonEquivalent.ApplyState();
        }
    }
}
