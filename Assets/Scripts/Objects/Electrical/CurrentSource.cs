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
    public class CurrentSource : ElectricalComponent
    {
        /// <summary>
        /// The DC output current, or RMS AC current
        /// </summary>
        public double Current;

        /// <summary>
        /// The AC frequency of the current source, or 0 for a DC current source.
        /// </summary>
        public double Frequency;

        /// <summary>
        /// The internal resistance across the pins of this current source (i.e. in parallel)
        /// </summary>
        public double InternalResistance;

        /// <summary>
        /// The momentary voltage across this current source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The momentary "current draw" from the rest of the circuit, which is the full current minus the current flowing through the internal resistor.
        /// If the internal resistor is 0 (ideal current source) this will always be the full current; otherwise, this represents the actual current
        /// being used by the circuit.
        /// </summary>
        [HideInInspector]
        public Complex CurrentDraw;

        /// <summary>
        /// The momentary "power draw" from the rest of the circuit, based on CurrentDraw and Voltage.
        /// </summary>
        [HideInInspector]
        public double PowerDraw;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Frequency: {Frequency.ToStringPrefix("Hz", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current Draw: {CurrentDraw.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")}");
        }

        public override void Initialize(Circuit circuit)
        {
            base.Initialize(circuit);

            if (Circuit == null) { return; }

            if (InternalResistance != 0f)
            {
                Circuit.Solver.AddResistance(_vA, _vB, InternalResistance);
            }
        }

        public override void UpdateState()
        {
            base.UpdateState();

            if (Circuit == null) { return; }

            Circuit.Solver.AddCurrent(_vA, _vB, Current);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            Voltage = vB - vA;

            CurrentDraw = Current;

            if (InternalResistance != 0)
            {
                CurrentDraw -= Voltage / InternalResistance;
            }

            PowerDraw = (Voltage * CurrentDraw.Conjugate()).Real;
        }
    }
}
