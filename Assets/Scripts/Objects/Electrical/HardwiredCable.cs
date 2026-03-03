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
        private Circuit? _circuit;

        public double Resistance;

        /// <summary>
        /// The specific heat capacity of this cable, in J/K (i.e. how much energy needs to be added to the cable in order to raise it's temperature by 1 K)
        /// </summary>
        public double SpecificHeat;

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
        public double DissipationCapacity;

        /// <summary>
        /// The maximum voltage rating for this cable - represents the ability for the cable's insulation to prevent arcing between the wires.
        /// 
        /// If the voltage between any node and ground exceeds this value, the cable will have a random chance of burning up.
        /// </summary>
        public double MaximumVoltageRating;

        /// <summary>
        /// The maximum rated current this cable can sustain before it burns up.
        /// </summary>
        public double MaximumCurrentRating { get; set; }

        /// <summary>
        /// The current temperature (K) of this segment of cable.
        /// Will slowly rise due to resitive heating
        /// </summary>
        public double Temperature { get; private set; } = 273.15;

        public Complex VoltageDeltaAverage { get; private set; }

        public Complex VoltageAverage { get; private set; }

        public double Current { get; private set; }

        public double PowerDissapated { get; private set;}

        public override Circuit? InputCircuit => _circuit;

        public override Circuit? OutputCircuit => null;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            // If this is the only component, add a description (otherwise, only show debug values so we don't take up too much space...)
            if (GetComponents<ElectricalComponent>().Length == 1)
            {
                stringBuilder.AppendLine($"Transfers electrical power between devices.");
                stringBuilder.AppendLine($"Each cable is modeled as a resistor, causing a voltage drop that increases with current flow.");
                stringBuilder.AppendLine($"Power dissipated as heat is given by P = I^2 · R.");
                stringBuilder.AppendLine($"Resistive heating raises the cable's temperature; if it exceeds 100 °C, the cable will burn up.");
                stringBuilder.AppendLine($"Each cable has a maximum rated current, limited by heating from resistive losses.");
                stringBuilder.AppendLine($"Each cable also has a maximum rated voltage to ground, representing insulation strength.");
                stringBuilder.AppendLine($"If the voltage to ground exceeds the rated value, the cable may fail due to an arc fault.");
                stringBuilder.AppendLine($"ΔV(avg) is the average voltage drop between the cable's nodes.");
                stringBuilder.AppendLine($"Vg(avg) is the average voltage to ground.");

                stringBuilder.AppendLine($"\n---\n");
            }

            double tCelsius = Temperature - 273.15;

            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")} | I_max: {MaximumCurrentRating.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Vg(avg): {VoltageAverage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | Vg_max: {MaximumVoltageRating.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"ΔV(avg): {VoltageDeltaAverage.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Temperature: {tCelsius.ToStringPrefix("°C", "yellow")} | Dissapated: {PowerDissapated.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"dbg: {_resistors.FirstOrDefault()?.NodeA?.Value.Index} | {_resistors.FirstOrDefault()?.NodeB?.Value.Index}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (_circuit != null && _circuit != circuit)
            {
                RemoveFrom(_circuit);
            }

            _circuit = circuit;

            if (Cable == null)
            {
                return;
            }

            for (int i = 0; i < Cable.OpenEnds.Count; i++)
            {
                for (int j = i + 1; j < Cable.OpenEnds.Count; j++)
                {
                    var nodeA = GetNode(circuit, Cable.OpenEnds[i], WireType.Line1);
                    var nodeB = GetNode(circuit, Cable.OpenEnds[j], WireType.Line1);
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

            foreach (var resistor in _resistors)
            {
                resistor.ApplyState();
            }

            PowerDissapated = 0;
            Current = 0;

            VoltageDeltaAverage = 0;
            VoltageAverage = 0;

            foreach (var resistor in _resistors)
            {
                VoltageDeltaAverage += resistor?.VoltageDelta ?? 0;
                VoltageAverage += resistor?.VoltageA ?? 0;
                VoltageAverage += resistor?.VoltageB ?? 0;

                PowerDissapated += resistor?.Power.Real ?? 0;
                Current += resistor?.Current.Magnitude ?? 0;
            }

            VoltageDeltaAverage /= _resistors.Count;
            VoltageAverage /= _resistors.Count * 2;

            // Calculate temperature change due to resistive heating
            // double dE = PowerDissipated * Circuit.TimeDelta;
            double pRadiated = DissipationCapacity * (Temperature - 293.15f);
            double dE = (PowerDissapated - pRadiated) * circuit.TimeDelta;
            double dT = dE / SpecificHeat;

            // Cap max change in temperature per tick
            // NOTE: this is a workaround for a bug caused by the current implementation of the power sink... If a circuit is suddenly disconnected
            // from any other power sources, and a power sink's current source becomes the only source of voltage in the circuit, a wire may have
            // the full voltage of the power sink applied, so Voltage would be much higher than usual (i.e. ~100-200 V, when it's usually < 1 V, since
            // it's the voltage drop across the resistor), causing current/power to be much higher than it should be.
            // Eventually I'd like to replace the power sink with a non-linear solution that can exactly determine the voltage/current/power draw in
            // one tick, after which this shouldn't be an issue.
            dT = Math.Min(dT, 10f);

            // Update temperature
            Temperature += dT;

            // Randomly break cables that are over temp, with a higher chance the hotter they are
            // 100 C ~ 0%
            // 150 C ~ 100%
            double breakChance = (Temperature - 373.15) / 50f;
            breakChance = Math.Clamp(breakChance, 0f, 1f);

            bool shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
            if (shouldBreak)
            {
                Cable?.Break();
                Hardwired.LogDebug($"Burning cable (too hot/too much current!) -- i: {Current} | T: {Temperature}");
            }

            // Randomly break cables that are over voltage, with a higher chance the further away from their design voltage they are
            // 100% of MaximumVoltageRating ~ 0%
            // 150% of MaximumVoltageRating ~ 100%
            var voltageCapacity = VoltageAverage.Magnitude / MaximumVoltageRating;
            breakChance = (voltageCapacity - 1.0) / 0.5f;
            breakChance = Math.Clamp(breakChance, 0f, 1f);

            shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
            if (shouldBreak)
            {
                Cable?.Break();
                Hardwired.LogDebug($"Burning cable (too much voltage!) -- V: {VoltageAverage} / {MaximumVoltageRating}");
            }

            // Check fuses
            var fuses = Cable?.AttachedDevices.OfType<CableFuse>() ?? Enumerable.Empty<CableFuse>();
            foreach (var fuse in fuses)
            {
                var cable = fuse.SmallCell.Cable;

                var vNominal = 1000f;
                var iLimit = fuse.PowerBreak / vNominal;

                if (Current > iLimit)
                {
                    fuse.Break();
                    Hardwired.LogDebug($"Breaking fuse -- i: {Current} | iLimit: {iLimit}, PowerBreak: {fuse.PowerBreak}");
                }
            }
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            foreach (var resistor in _resistors)
            {
                resistor.Dispose();
            }

            _resistors.Clear();
        }

    }
}
