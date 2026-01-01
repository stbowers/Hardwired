#nullable enable

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
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            // We must have solved for a valid current, otherwise we have nothing to do...
            if (Current == null){ return; }

            // Calculate charge - dQ = I * dT
            // Each power tick is .5 seconds - should this be a constant or calculated instead of hard coded?
            var deltaCharge = 0.5 * Current.Value;
            Charge += deltaCharge;

            // Update voltage & energy from new charge
            Voltage = Charge / Capacitance;
            Energy = 0.5 * Charge * Charge / Capacitance;
        }

    }
}
