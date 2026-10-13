#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Patches;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class PowerSource : ElectricalComponent
    {
        private NortonEquivalent? _nortonEquivalent;

        [SerializeReference]
        public PowerProfile PowerProfile = new(PowerProfile.DefaultGenerator);

        /// <summary>
        /// Gets whether or not this power source is outputting power to the circuit.
        /// 
        /// If the attached device has an on/off state (i.e. power switch), that state will be used.
        /// Otherwise, the output is always enabled.
        /// </summary>
        public bool OutputEnabled => Device != null && (!Device.HasOnOffState || Device.OnOff);

        public double PowerGenerated { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public override Connection? PowerOutput => base.PowerOutput ?? base.PowerInput;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Supplies electrical power to the circuit.");
                stringBuilder.AppendLine($"Modeled as a non-ideal voltage source with a maximum output voltage and a series power-limiting resistor.");
                stringBuilder.AppendLine($"Includes an internal energy buffer that is charged each tick by generated power and discharged by the load.");
                stringBuilder.AppendLine($"As current increases, the series resistance causes the output voltage to droop.");
                stringBuilder.AppendLine($"Maximum power draw occurs at 1/2 · V_max and is limited by the available energy in the buffer.");
                stringBuilder.AppendLine($"The internal buffer smooths grid transients and is computationally cheaper to simulate than a fully non-linear power source/sink.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            stringBuilder.AppendLine($"Output enabled: {OutputEnabled}");
            stringBuilder.AppendLine($"Power Generated: {PowerGenerated.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | ΔV_max: {PowerProfile.VoltageNominalHigh.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Internal resistance: {_nortonEquivalent?.Resistance.ToStringPrefix("Ω", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            if (OutputCircuit == circuit)
            {
                if (_nortonEquivalent != null)
                {
                    RemoveFrom(_nortonEquivalent.Circuit);
                }

                var nodeA = GetNode(circuit, PowerOutput, WireType.Line1);

                _nortonEquivalent = new(circuit, nodeA, null);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (circuit != OutputCircuit) { return; }

            // Get generated power
            PowerGenerated = Device?.GetGeneratedPower(OutputCableNetwork) ?? 0;

            if (_nortonEquivalent != null)
            {
                _nortonEquivalent.Frequency = PowerProfile.Frequency;

                // Determine power to use for actual calculations
                // - No output if output is disabled (device is off)
                // - Set minimum charge to avoid dividing by zero
                var powerAvailable = Math.Max(1e-5, OutputEnabled ? PowerGenerated : 0.0);

                _nortonEquivalent.Resistance = PowerProfile.VoltageNominalHigh * PowerProfile.VoltageNominalHigh / (4 * powerAvailable);
                _nortonEquivalent.VoltageOpen = PowerProfile.VoltageNominalHigh;

                _nortonEquivalent.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (OutputCircuit != circuit) {return;}

            _nortonEquivalent?.ApplyState();

            PowerDraw = _nortonEquivalent?.Power.Real ?? 0;
            PowerFactor = _nortonEquivalent?.PowerFactor ?? 0;
            VoltageDelta = _nortonEquivalent?.VoltageDelta ?? 0;
            CurrentDraw = _nortonEquivalent?.Current ?? 0;

            // Update internal charge
            Device?.UsePower(OutputCableNetwork, (float)PowerDraw);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _nortonEquivalent?.Circuit)
            {
                _nortonEquivalent?.Dispose();
                _nortonEquivalent = null;
            }
        }
    }
}
