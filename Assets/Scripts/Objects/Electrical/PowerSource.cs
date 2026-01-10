#nullable enable

using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Power sources are modeled as a non-ideal current source with a specific resistance.
    /// </summary>
    public class PowerSource : CurrentSource
    {
        /// <summary>
        /// The nominal designed power output of the power source (used to size the internal resistance along with VoltageNominal)
        /// </summary>
        public double NominalPower;

        /// <summary>
        /// The actual power being supplied by this power source this tick (note that this is the total power _available_, regardless of if it gets used or not)
        /// </summary>
        public double PowerSetting;

        /// <summary>
        /// The nominal design voltage -- this is the voltage at which the power source will output max power.
        /// The maximum voltage (i.e. with no load) will be 2x this.
        /// </summary>
        public double VoltageNominal;

        /// <summary>
        /// The calculated energy output into the circuit by this power source for this tick
        /// (note that devices generally provide/consume power in Watts, i.e. via Device.UsePower(), so this value needs to be divided by dt to get the power output for the tick)
        /// </summary>
        public double EnergyOutput;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Power Source --");
            stringBuilder.AppendLine($"Nominal Power Output: {NominalPower.ToStringPrefix("W", "yellow")} (@ {VoltageNominal.ToStringPrefix("V", "yellow")})");
            stringBuilder.AppendLine($"Power setting: {PowerSetting.ToStringPrefix("W", "yellow")}");
        }

        public override void Initialize()
        {
            // P = I * V :. 1/2 * I = P_max / V_nom (1/2 current will go through resitor, other half through the circuit)
            // R = V / I :. R = V_nom / (2 * P_max / V_nom) = V_nom^2 / 2 * P_max
            InternalResistance = VoltageNominal * VoltageNominal / (2 * NominalPower);

            base.Initialize();
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

            EnergyOutput = PowerDraw * Circuit.TimeDelta;
        }
    }
}
