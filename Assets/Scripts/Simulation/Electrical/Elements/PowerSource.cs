#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class PowerSource : ICircuitElement, IDipoleCircuitElement
    {
        private NortonEquivalent _nortonEquivalent;

        public Circuit Circuit { get; }

        public RefCounted<MNASolver.Unknown>? NodeA => _nortonEquivalent.NodeA;

        public RefCounted<MNASolver.Unknown>? NodeB => _nortonEquivalent.NodeB;

        public Complex Current => _nortonEquivalent.Current;

        public PowerProfile Profile { get; set; } = PowerProfile.Default;

        public double PowerAvailable { get; set; }

        public PowerSource(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB)
        {
            Circuit = circuit;
            _nortonEquivalent = new(circuit, nodeA, nodeB);
        }

        public void Dispose()
        {
            _nortonEquivalent.Dispose();
        }

        public void UpdateState()
        {
            _nortonEquivalent.Resistance = Profile.VoltageNominal * Profile.VoltageNominal / Profile.PowerNominal;
            _nortonEquivalent.CurrentShort = PowerAvailable / Profile.VoltageNominal;
            _nortonEquivalent.UpdateState();
        }

        public void ApplyState()
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
