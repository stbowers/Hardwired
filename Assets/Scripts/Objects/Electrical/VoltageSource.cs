#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Implementation notes:
    /// - pin A is the "negative" terminal, and pin B is the "positive" terminal
    ///   - So i.e. `V(B) - V(A) = V`
    /// - By convention, the solved current flow is from pin B (positive) to pin A (negative).
    ///   - Conceptually this represents the current _produced by_ this voltage source, rather than current flowing _through_ the voltage source.
    ///   - At first glance, this can appear to be backwards from what one might expect (for example, the current produced by a voltage source in a simple circuit is negative)
    /// </summary>
    public class VoltageSource : ElectricalComponent
    {
        /// <summary>
        /// The DC voltage, or RMS AC voltage.
        /// </summary>
        public double Voltage;

        /// <summary>
        /// The AC frequency of the voltage source, or 0 for a DC voltage source.
        /// </summary>
        public double Frequency;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Current;

        private MNASolver.Unknown? _i;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Frequency: {Frequency.ToStringPrefix("Hz", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
        }

        public override void Initialize(Circuit circuit)
        {
            base.Initialize(circuit);

            if (Circuit == null) { return; }

            Circuit.Solver.AddVoltageSource(_vA, _vB, out _i);
        }

        public override void UpdateState()
        {
            base.UpdateState();

            if (Circuit == null) { return; }

            Circuit.Solver.SetVoltage(_i, Voltage);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            Current = Circuit.Solver.GetValueOrDefault(_i);
        }
    }
}
