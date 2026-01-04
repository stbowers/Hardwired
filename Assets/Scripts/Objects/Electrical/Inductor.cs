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

        private int _v;

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

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            // If circuit has AC current, add impedence based on the frequency
            if (solver.Frequency != 0)
            {
                var w = 2f * Math.PI * solver.Frequency;
                Reactance = w * Inductance;

                solver.AddReactance(n, m, Reactance);
            }
            else
            {
                // TODO: Add some better documentation about what this is doing...
                // By adding an extra term to the A matrix and z vector, we're essentially solving the differential equation step-by-step with an approximation similar to
                // i(t) = i(t-1) + dv.
                // A similar setup is used for Capacitor as well, and has better comments
                _v = solver.AddVoltageSource(n, m);

                var dt = 0.5;
                var x = Inductance / dt;

                // TODO: We don't have "direct" access to the part of the A matrix we need to modify for inductors...
                // It would probably be good to refactor this code at some point to make it more clear what we're adding
                var j = solver.Nodes + _v;
                solver.AddAdmittance(j, null, x);
            }
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            if (solver.Frequency == 0f)
            {
                var dt = 0.5;
                var x = Inductance * Current / dt;

                solver.SetVoltage(_v, x);
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
                Current = solver.GetCurrent(_v);

                Energy = (0.5f * Inductance * Current * Current).Magnitude;
            }
            else
            {
                Current = Voltage / new Complex(0, Reactance);
            }
        }
    }
}
