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

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            double p = (Voltage * Current.Conjugate()).Real;

            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Power: {p.ToStringPrefix("W", "yellow")}");
        }


        public override void Initialize(Circuit circuit)
        {
            base.Initialize(circuit);

            if (Circuit == null) { return; }

            Circuit.Solver.AddResistance(_vA, _vB, Resistance);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            Voltage = vA - vB;

            Current = Voltage / Resistance;
        }

    }
}
