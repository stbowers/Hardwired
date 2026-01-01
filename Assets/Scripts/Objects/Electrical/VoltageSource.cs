#nullable enable

using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
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
            stringBuilder.AppendLine($"Current: {Current?.ToStringPrefix("A", "yellow") ?? "N/A"}");
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

            Current = solver.GetCurrent(v.Value).Magnitude;
        }
    }
}
