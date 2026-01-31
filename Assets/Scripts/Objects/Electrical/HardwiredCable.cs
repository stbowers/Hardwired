#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
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
    public class HardwiredCable : ElectricalComponent
    {
        private List<Resistor> _resistors = new();
        private Cable? _cable;

        public double Resistance;

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

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _cable ??= GetComponent<Cable>();
            _resistors.Clear();

            for (int i = 0; i < _cable.OpenEnds.Count; i++)
            {
                for (int j = i + 1; j < _cable.OpenEnds.Count; j++)
                {
                    var nodeA = GetNode(circuit, _cable.OpenEnds[i], WireType.Line1);
                    var nodeB = GetNode(circuit, _cable.OpenEnds[j], WireType.Line1);
                    Resistor resistor = new(circuit, nodeA, nodeB);

                    resistor.Resistance = Resistance;
                    
                    _resistors.Add(resistor);
                }
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            foreach (var resistor in _resistors)
            {
                resistor.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            double power = 0;
            double current = 0;

            foreach (var resistor in _resistors)
            {
                resistor.ApplyState();

                power += resistor?.Power.Real ?? 0;
                current = Math.Max(resistor?.Current.Magnitude ?? 0, current);
            }

            // Calculate temperature change due to resistive heating
            // double dE = PowerDissipated * Circuit.TimeDelta;
            double pRadiated = Math.Max(DissipationCapacity * (Temperature - 293.15f), 0f);
            double dE = (power - pRadiated) * 0.5f;
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

            // Randomly break cables that are over temp, with a higher chance the hotter they are
            // 100 C ~ 0%
            // 150 C ~ 100%
            double breakChance = (Temperature - 373.15) / 50f;
            breakChance = Math.Clamp(breakChance, 0f, 1f);

            bool shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
            if (shouldBreak)
            {
                _cable?.Break();
                Hardwired.LogDebug($"Burning cable -- i: {current} | T: {Temperature}");
            }

            // Check fuses
            var fuses = _cable?.AttachedDevices.OfType<CableFuse>() ?? Enumerable.Empty<CableFuse>();
            foreach (var fuse in fuses)
            {
                var cable = fuse.SmallCell.Cable;

                var vNominal = 1000f;
                var iLimit = fuse.PowerBreak / vNominal;

                if (current > iLimit)
                {
                    fuse.Break();
                    Hardwired.LogDebug($"Breaking fuse -- i: {current} | iLimit: {iLimit}, PowerBreak: {fuse.PowerBreak}");
                }
            }
        }

    }
}
