#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Resistor : ElectricalComponent
    {
        /// <summary>
        /// A global minimum admittance to add between every node and ground - this prevents floating "islands" with no solution.
        /// 
        /// For debugging, this should be set to 0, as it is useful to find and diagnose situations that lead to these floating nodes.
        /// For release, this should be set to a small value such as 1e-10 to avoid significantly impacting the circuit, but still preventing singular values that prevent solving.
        /// </summary>
        private const double G_MIN = 0; // 1e-10;

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
        /// The max voltage from either pin of the resistor to ground
        /// </summary>
        [HideInInspector]
        public Complex VoltageGround;

        /// <summary>
        /// The momentary current across this resistor calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Current;

        /// <summary>
        /// The amount of power (W) dissipated by this resistor due to resistive heating.
        /// </summary>
        [HideInInspector]
        public double PowerDissipated;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Resistor --");
            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Vcc: {VoltageGround.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {Voltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power dissipated: {PowerDissipated.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"G_MIN: {G_MIN.ToStringPrefix("Ω", "yellow")}");
        }


        protected override void InitializeInternal()
        {
            base.InitializeInternal();

            Circuit?.Solver.AddAdmittance(_vA, null, G_MIN);
            Circuit?.Solver.AddAdmittance(_vB, null, G_MIN);
            Circuit?.Solver.AddResistance(_vA, _vB, Resistance);
        }

        protected override void DeinitializeInternal()
        {
            base.DeinitializeInternal();

            Circuit?.Solver.AddAdmittance(_vA, null, -G_MIN);
            Circuit?.Solver.AddAdmittance(_vB, null, -G_MIN);
            Circuit?.Solver.AddResistance(_vA, _vB, -Resistance);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            Voltage = vA - vB;
            VoltageGround = (vA.Magnitude > vB.Magnitude) ? vA : vB;

            Current = Voltage / Resistance;

            PowerDissipated = (Voltage * Current.Conjugate()).Real;
        }

    }
}
