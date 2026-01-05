#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Resistor : ElectricalComponent
    {
        /// <summary>
        /// Resistance value in ohms
        /// </summary>
        public double Resistance;

        /// <summary>
        /// The momentary voltage across this resistor calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The momentary current across this resistor calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            double p = (Voltage * Current.Conjugate()).Real;

            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {Voltage.Magnitude.ToStringPrefix("V", "yellow") ?? "N/A"}");
            stringBuilder.AppendLine($"Current: {Current.Magnitude.ToStringPrefix("A", "yellow") ?? "N/A"}");
            stringBuilder.AppendLine($"Power: {p.ToStringPrefix("W", "yellow") ?? "N/A"}");
        }


        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);
            solver.AddResistance(n, m, Resistance);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            Complex vA = solver.GetVoltage(n);
            Complex vB = solver.GetVoltage(m);

            Voltage = vB - vA;
            Current = Voltage / Resistance;
        }

    }
}
