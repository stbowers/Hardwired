#nullable enable

using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class CurrentSource : ElectricalComponent
    {
        public double Current;

        /// <summary>
        /// The momentary voltage across this current source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public double? DeltaVoltage;

        /// <summary>
        /// The AC frequency of the current source, or 0 for a DC current source.
        /// </summary>
        public double Frequency;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {DeltaVoltage?.ToStringPrefix("V", "yellow") ?? "N/A"}");
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            solver.SetCurrent(n, m, Current);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            var vN = solver.GetVoltage(n);
            var vM = solver.GetVoltage(m);

            DeltaVoltage = (vN - vM).Real;
        }
    }
}
