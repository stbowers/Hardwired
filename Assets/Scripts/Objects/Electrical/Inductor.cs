#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Inductor : ElectricalComponent
    {
        /// <summary>
        /// Inductance value in Henrys
        /// </summary>
        public double Inductance;

        /// <summary>
        /// The instantaneous voltage differential across the inductor, at the current power tick
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The current being permitted through this inductor
        /// </summary>
        [HideInInspector]
        public Complex Current;

        /// <summary>
        /// Reactance value in ohms (depends on AC circuit frequency)
        /// </summary>
        [HideInInspector]
        public double Reactance;

        /// <summary>
        /// The current energy held in the inductor, in Joules
        /// </summary>
        [HideInInspector]
        public double Energy;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Voltage: {Voltage.Real.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.Real.ToStringPrefix("A", "yellow") ?? "N/A"}");

            // Note - by convention current flow for a voltage source is essentially the current "produced" by the source, not "flowing through"
            // the source... This means it's generally opposite from what we expect, so we negate it first before displaying.
            stringBuilder.AppendLine($"Current Phase: {(-Current).Phase.ToStringPrefix("rad", "yellow") ?? "N/A"}");

            stringBuilder.AppendLine($"Inductance: {Inductance.ToStringPrefix("F", "yellow")}");
            stringBuilder.AppendLine($"Energy: {Energy.ToStringPrefix("J", "yellow") ?? "N/A"}");

            stringBuilder.AppendLine($"Reactance: {Reactance.ToStringPrefix("Ω", "yellow") ?? "N/A"}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            // If circuit has AC current, add impedence based on the frequency
            if (solver.Frequency != 0)
            {
                var w = 2f * Math.PI * solver.Frequency;
                Reactance = w * Inductance;

                int? n = GetNodeIndex(PinA);
                int? m = GetNodeIndex(PinB);

                solver.AddReactance(n, m, Reactance);
            }
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            if (solver.Frequency == 0f)
            {
                solver.SetCurrent(n, m, Current);
            }
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);
            var vN = solver.GetVoltage(n);
            var vM = solver.GetVoltage(m);
            Voltage = vN - vM;

            if (solver.Frequency == 0f)
            {
                var dt = 0.5;
                var dI = dt * Voltage / Inductance;

                Current += dI;

                Energy = (0.5f * Inductance * Current * Current).Magnitude;
            }
            else
            {
                Current = Voltage / new Complex(0, Reactance);
            }
        }
    }
}
