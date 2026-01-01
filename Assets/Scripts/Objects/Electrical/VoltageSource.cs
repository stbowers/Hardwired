#nullable enable

using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
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
        /// The DC voltage, or maximum AC voltage.
        /// </summary>
        public double Voltage;

        /// <summary>
        /// The AC frequency of the voltage source, or null for a DC voltage source.
        /// </summary>
        public double? Frequency;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            // Note - by convention current is from pin B to A; but this is backwards from what I expect when viewing, so we'll negate it for display...
            stringBuilder.AppendLine($"Current: {(-Current)?.ToStringPrefix("A", "yellow") ?? "N/A"}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            var v = GetVoltageSourceIndex(this);
            if (v == null) { return; }

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            solver.InitializeVoltageSource(n, m, v.Value);
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            var v = GetVoltageSourceIndex(this);
            if (v == null) { return; }

            solver.SetVoltage(v.Value, Voltage);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            var v = GetVoltageSourceIndex(this);
            if (v == null) { return; }

            Current = solver.GetCurrent(v.Value).Real;
        }
    }
}
