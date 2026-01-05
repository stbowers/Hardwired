#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class CurrentSource : ElectricalComponent
    {
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

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            var p = (Voltage * CurrentDraw.Conjugate()).Real;

            stringBuilder.AppendLine($"-- INPUTS:");
            stringBuilder.AppendLine($"Current (RMS): {Current.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Frequency: {Frequency.ToStringPrefix("Hz", "yellow")}");
            stringBuilder.AppendLine($"-- OUTPUTS:");
            stringBuilder.AppendLine($"Voltage (RMS): {Voltage.Real.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current Draw (RMS): {CurrentDraw.Real.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {p.ToStringPrefix("W", "yellow")}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            if (InternalResistance != 0f)
            {
                solver.AddResistance(n, m, InternalResistance);
            }
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

            Voltage = vM - vN;

            var internalResistorCurrent = Complex.Zero;

            if (InternalResistance != 0)
            {
                internalResistorCurrent = Voltage / InternalResistance;
            }

            CurrentDraw = Current - internalResistorCurrent;
        }
    }
}
