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
    public class Capacitor : ElectricalComponent
    {
        /// <summary>
        /// Capacitance value in Farads
        /// </summary>
        public double Capacitance;

        /// <summary>
        /// The DC voltage, or maximum AC voltage.
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The AC frequency of the voltage source, or 0 for a DC voltage source.
        /// </summary>
        [HideInInspector]
        public double Frequency;

        /// <summary>
        /// The momentary current across this voltage source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex? Current;

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

            stringBuilder.AppendLine($"Voltage: {Voltage.Magnitude.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Frequency: {Frequency.ToStringPrefix("Hz", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current?.Magnitude.ToStringPrefix("A", "yellow") ?? "N/A"}");

            if (Frequency != 0f)
            {
                // Note - by convention current flow for a voltage source is essentially the current "produced" by the source, not "flowing through"
                // the source... This means it's generally opposite from what we expect, so we negate it first before displaying.
                stringBuilder.AppendLine($"Current Phase: {(-Current)?.Phase.ToStringPrefix("rad", "yellow") ?? "N/A"}");
            }

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

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            // Match frequency from solver
            Frequency = solver.Frequency;

            // If circuit is DC, set up differential equation to simulate transient behavior
            if (solver.Frequency == 0f)
            {
                Reactance = 0f;

                // TODO: Add some better documentation about what this is doing...
                // Basically we're adding another term, + (C * dv/dt), to the current equations for nodes n and m, representing the current flowing through the capacitor.
                // If we then add (C * V_t-1 / dt) (where V_t-1 is the voltage from the last tick) to both sides of the equation, the dv/dt term turns in to (C / dt) * (V_t-1 + dv).
                // If we then consider V = V_t-1 + dv (approximation based on tick speed), the term is now (C / dt) * V, and we can simply add it to the A matrix as (C / dt).
                //
                // TL;DR: by adding an extra term to the A matrix and z vector, we're essentially solving the differential equation step-by-step with an approximation similar to
                // v(t) = v(t-1) + dv
                var dt = 0.5;
                var a = Capacitance / dt;

                solver.AddAdmittance(n, m, a);

            }
            // Otherwise, if circuit is AC the capacitor has no transient behavior but instead just has a complex impedance value
            else
            {
                // Note - the complex impedence value for the capacitor is 1 / (j * w * C), where j is the imaginary unit (instead of 'i' to avoid confusion with current).
                // When treating the impedence as a real value we negate it since 1 / j = -j, so when later used as the imaginary component of a complex value it will be correct.
                var w = 2f * Math.PI * Frequency;
                Reactance = -1f / (w * Capacitance);

                solver.AddReactance(n, m, Reactance);
            }
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            // If circuit is DC, update the 
            if (Frequency == 0f)
            {
                int? n = GetNodeIndex(PinA);
                int? m = GetNodeIndex(PinB);

                // Add (C * V_t-1 / dt) to the right hand side of the node equations in order to take in to account transient behavior (see setup above)
                var dt = 0.5;
                var x = Capacitance * Voltage / dt;

                solver.SetCurrent(n, m, x);
            }
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            int? n = GetNodeIndex(PinA);
            int? m = GetNodeIndex(PinB);

            if (Frequency == 0f)
            {
                var vN = solver.GetVoltage(n);
                var vM = solver.GetVoltage(m);
                Voltage = vM - vN;

                // Calculate charge - dQ = I * dT
                // Each power tick is .5 seconds - should this be a constant or calculated instead of hard coded?
                // var deltaCharge = 0.5 * Current.Value.Real;
                // Charge += deltaCharge;

                // Update voltage & energy from new charge
                // Voltage = Charge / Capacitance;
                Charge = Voltage.Real * Capacitance;
                Energy = 0.5 * Charge * Charge / Capacitance;
            }
            else
            {
                var vN = solver.GetVoltage(n);
                var vM = solver.GetVoltage(m);
                Voltage = vM - vN;

                Current = Voltage / new Complex(0, Reactance);
            }
        }
    }
}
