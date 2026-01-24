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

        /// <summary>
        /// The amount of power (W) dissipated by this resistor due to resistive heating.
        /// </summary>
        [HideInInspector]
        public double PowerDissipated;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;
            var vcc = (vA + vB) / 2;

            stringBuilder.AppendLine($"-- Resistor --");
            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Vcc: {vcc.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {Voltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power dissipated: {PowerDissipated.ToStringPrefix("W", "yellow")}");
        }


        public override void Initialize()
        {
            base.Initialize();

            if (Circuit == null) { return; }

            Circuit.Solver.AddResistance(_vA, _vB, Resistance);
        }

        public override void Deinitialize()
        {
            Circuit?.Solver.AddResistance(_vA, _vB, -Resistance);
            base.Deinitialize();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            Voltage = vA - vB;

            Current = Voltage / Resistance;

            PowerDissipated = (Voltage * Current.Conjugate()).Real;
        }

    }
}
