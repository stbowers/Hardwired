#nullable enable

using Hardwired.Simulation.Electrical;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Power sources are modeled as a non-ideal current source with a specific resistance.
    /// </summary>
    public class PowerSource : CurrentSource
    {
        /// <summary>
        /// The maximum designed power output of the power source (used to size the internal resistance)
        /// </summary>
        public double MaxPower;

        /// <summary>
        /// The actual power being supplied by this power source this tick
        /// </summary>
        public double PowerSetting;

        /// <summary>
        /// The nominal design voltage -- this is the voltage at which the power source will output max power.
        /// The maximum voltage (i.e. with no load) will be 2x this.
        /// </summary>
        public double VoltageNominal;

        /// <summary>
        /// The calculated energy input into the circuit by this power source for this tick
        /// </summary>
        public double EnergyInput;

        public override void Initialize(Circuit circuit)
        {
            // P = I * V :. 1/2 * I = P_max / V_nom (1/2 current will go through resitor, other half through the circuit)
            // R = V / I :. R = V_nom / (2 * P_max / V_nom) = V_nom^2 / 2 * P_max
            InternalResistance = VoltageNominal * VoltageNominal / 2 * MaxPower;

            base.Initialize(circuit);
        }

        public override void UpdateState()
        {
            // I = 2 * P_max / V_nom
            Current = 2 * PowerSetting / VoltageNominal;

            base.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            EnergyInput = PowerDraw * Circuit.TimeDelta;
        }
    }
}
