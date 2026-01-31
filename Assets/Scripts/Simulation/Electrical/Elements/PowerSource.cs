#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class PowerSource : DipoleCircuitElementBase, ICircuitElement
    {
        private NortonEquivalent _nortonEquivalent;

        public override Complex Current => _nortonEquivalent.Current;

        public PowerProfile Profile { get; set; } = PowerProfile.Default;

        public double PowerAvailable { get; set; }

        public PowerSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB) : base(circuit, nodeA, nodeB)
        {
            _nortonEquivalent = new(circuit, nodeA, nodeB);
        }

        public override void Dispose()
        {
            _nortonEquivalent.Dispose();
        }

        public override void UpdateState()
        {
            _nortonEquivalent.Resistance = Profile.VoltageNominal * Profile.VoltageNominal / Profile.PowerNominal;
            _nortonEquivalent.CurrentShort = PowerAvailable / Profile.VoltageNominal;
            _nortonEquivalent.UpdateState();
        }

        public override void ApplyState()
        {
            _nortonEquivalent.ApplyState();
        }

        public readonly struct PowerProfile
        {
            public static readonly PowerProfile Default = new() { Frequency = 60f, VoltageNominal = 300f, PowerNominal = 500f};

            /// <summary>
            /// The nominal power output of the power source.
            /// </summary>
            public double PowerNominal { get; init; }

            /// <summary>
            /// The nominal output voltage of the power source when there is no load.
            /// </summary>
            public double VoltageNominal { get; init; }

            /// <summary>
            /// The AC frequency this power source should generate, or 0 for DC, or null to follow the grid.
            /// </summary>
            public double? Frequency { get; init; }
        }
    }
}
