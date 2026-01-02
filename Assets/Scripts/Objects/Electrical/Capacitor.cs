#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Implementation notes:
    /// a capacitor will act as a voltage source in the circuit, applying a voltage based on it's current charge.
    /// After the circuit is solved, the charge is updated based on how much current flowed through the capacitor.
    /// </summary>
    public class Capacitor : VoltageSource
    {
        /// <summary>
        /// Capacitance value in Farads
        /// </summary>
        public double Capacitance;

        /// <summary>
        /// Reactance value in ohms (depends on AC circuit frequency)
        /// </summary>
        [HideInInspector]
        public double Reactance;

        /// <summary>
        /// The current charge in the capacitor, in Coulombs
        /// </summary>
        [HideInInspector]
        public double Charge;

        /// <summary>
        /// The current energy held in the capacitor, in Joules
        /// </summary>
        [HideInInspector]
        public double Energy;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Capacitance: {Capacitance.ToStringPrefix("F", "yellow")}");
            stringBuilder.AppendLine($"Charge: {Charge.ToStringPrefix("C", "yellow") ?? "N/A"}");
            stringBuilder.AppendLine($"Energy: {Energy.ToStringPrefix("J", "yellow") ?? "N/A"}");

            if (Frequency != 0f)
            {
                stringBuilder.AppendLine($"Reactance: {Reactance.ToStringPrefix("Ω", "yellow") ?? "N/A"}");
            }
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            // Match frequency from solver
            Frequency = solver.Frequency;

            // If circuit has AC current, add impedence based on the frequency
            if (Frequency != 0f)
            {
                // Note - the complex impedence value for the capacitor is 1 / (j * w * C), where j is the imaginary unit (instead of 'i' to avoid confusion with current).
                // When treating the impedence as a real value we negate it since 1 / j = -j, so when later used as the imaginary component of a complex value it will be correct.
                var w = 2f * Math.PI * Frequency;
                Reactance = -1f / (w * Capacitance);

                int? n = GetNodeIndex(PinA);
                int? m = GetNodeIndex(PinB);

                solver.AddReactance(n, m, Reactance);
            }
            else
            {
                Reactance = 0f;
            }
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            // Don't update charge for AC circuit
            if (Frequency != 0f) { return; }

            // We must have solved for a valid current, otherwise we have nothing to do...
            if (Current == null){ return; }

            // Calculate charge - dQ = I * dT
            // Each power tick is .5 seconds - should this be a constant or calculated instead of hard coded?
            var deltaCharge = 0.5 * Current.Value.Real;
            Charge += deltaCharge;

            // Update voltage & energy from new charge
            Voltage = Charge / Capacitance;
            Energy = 0.5 * Charge * Charge / Capacitance;
        }

    }
}
