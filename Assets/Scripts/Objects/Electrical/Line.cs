#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Represents a segment of cable.
    /// In the circuit, this is just simulated as a simple resistor - this componenet however adds additional logic to track the temperature of the cable
    /// as power is dissipated through it via resistive heating, so we can melt cables when too much current goes through them.
    /// </summary>
    public class Line : Resistor
    {
        /// <summary>
        /// The specific heat capacity of this cable, in J/K (i.e. how much energy needs to be added to the cable in order to raise it's temperature by 1 K)
        /// </summary>
        public double SpecificHeat;

        /// <summary>
        /// The current temperature (K) of this segment of cable.
        /// Will slowly rise due to resitive heating
        /// </summary>
        [HideInInspector]
        public double Temperature;

        /// <summary>
        /// How much power can this cable dissipate into the void, based on it's temperature (W/K).
        /// 
        /// Each tick, if PowerDissipated is greater than this, the temperature will increase.
        /// If PowerDissipated is lower, the temperature will decrease (to ~20 C)
        /// 
        /// This is a temporary system that approximates the power dissipation of a cable, but eventually I'd like to
        /// replace this with a more accurate calculation (in particular, cables should heat up the room they're in, and
        /// the temperature around them will determine how effectively they can convect heat)
        /// </summary>
        [HideInInspector]
        public double DissipationCapacity;


        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            double tCelsius = Temperature - 273.15;

            stringBuilder.AppendLine($"Temperature: {tCelsius.ToStringPrefix("°C", "yellow")}");
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            // Calculate temperature change due to resistive heating
            // double dE = PowerDissipated * Circuit.TimeDelta;
            double pRadiated = Math.Max(DissipationCapacity * (Temperature - 293f), 0f);
            double dE = (PowerDissipated - pRadiated) * Circuit.TimeDelta;
            double dT = dE / SpecificHeat;

            // Cap max change in temperature per tick
            // NOTE: this is a workaround for a bug caused by the current implementation of the power sink... If a circuit is suddenly disconnected
            // from any other power sources, and a power sink's current source becomes the only source of voltage in the circuit, a wire may have
            // the full voltage of the power sink applied, so Voltage would be much higher than usual (i.e. ~100-200 V, when it's usually < 1 V, since
            // it's the voltage drop across the resistor), causing current/power to be much higher than it should be.
            // Eventually I'd like to replace the power sink with a non-linear solution that can exactly determine the voltage/current/power draw in
            // one tick, after which this shouldn't be an issue.
            dT = Math.Min(dT, 10f);

            // Update temperature (with min temp ~20 C, to avoid DissipationCapacity bringing the temp down to absolute zero)
            Temperature += dT;
        }

    }
}
