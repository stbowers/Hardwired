#nullable enable

using System.Numerics;
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
        /// The AC frequency of the voltage source, or 0 for a DC voltage source.
        /// </summary>
        public double Frequency;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex? Current;

        private int? _v;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Frequency: {Frequency.ToStringPrefix("Hz", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current?.Magnitude.ToStringPrefix("A", "yellow") ?? "N/A"}");

            if (Frequency != 0f)
            {
                // Note - by convention current flow for a voltage source is essentially the current "produced" by the source, not "flowing through"
                // the source... This means it's generally opposite from what we expect, so we negate it first before displaying.
                stringBuilder.AppendLine($"Current Phase: {(-Current)?.Phase.ToStringPrefix("rad", "yellow") ?? "N/A"}");
            }
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            _v = solver.AddVoltageSource(n, m);
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            if (_v == null) { return; }

            solver.SetVoltage(_v.Value, Voltage);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            if (_v == null) { return; }

            Current = solver.GetCurrent(_v.Value);
        }
    }
}
