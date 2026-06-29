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
    public class PowerSource : ElectricalComponent, IComponentSaveData
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

        public double BufferCharge { get; private set; }

        /// <summary>
        /// The maximum amount of energy that can be stored in the internal buffer.
        /// 
        /// Note - the energy in the buffer essentially dictates the maximum power draw of the source (at V = 1/2 * V_max).
        /// The default value of 30.0 kWt is based on a maximum power draw of 5 kW, with the following calculations:
        /// 
        /// - Target voltage droop: 40% (default device power profile is 150-250 V)
        ///   - 40% droop instead of 50% (1/2 * V_max) = 1.25x buffer needed
        /// - Buffer needed for max power draw P_max: Q = P_max * 4
        ///   - Buffer needed for max power draw P_max with 1.25x droop: Q = 1.25 * P_max * 4
        /// - Need an additional P_max Wt in buffer, since power used is subtracted from buffer at end of tick
        /// - Q_total = (1.25 * P_max * 4) + P_max = 30 kWt
        /// 
        /// Even with this default size, power sources can still end up drooping quite a lot when the load approaches 5 kW, until buffers fill up and can recover...
        /// It may be worth increasing the default buffer size to 50 kWt or more, which will make power sources "stiffer" and less prone to droop.
        /// Even with a larger buffer, if the power draw is higher than power supplied, the internal buffer will eventually drain and the source will droop, but it will take longer to reach that point.
        /// Ideally the buffer should be as small as we can get away with, to reduce the amount of energy that can be stored in the system, and increase responsiveness to load changes (so the player can diagnose issues).
        /// 
        /// Also note that this default buffer size is used for all power sources, even though most generators will generate less than 5 kW (and some power converters may need to supply more than 5 kW in the future).
        /// At some point the default buffer size could be calculated per power source (i.e. with a "MaxPowerDraw" property).
        /// </summary>
        public double MaxBufferCharge { get; set; } = 30000;

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
            stringBuilder.AppendLine($"Internal Buffer: {BufferCharge.ToStringPrefix("Wt", "yellow")} / {MaxBufferCharge.ToStringPrefix("Wt", "yellow")}");
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

            // Note - the following line was commented out because it was causing really weird behavior with
            // the APC... Basically the APC returns the full charge of the attached battery for GetGeneratedPower(),
            // which is way to large for this internal buffer...
            // There might be a cleaner way to handle this, but for now I've just set ChargeMaximum to 4.5 kWt by default,
            // which should be large enough for most generators...
            // ChargeMaximum = Math.Max(4.5 * PowerGenerated, ChargeMaximum);

            // Update internal charge
            var powerUsed = Math.Clamp(PowerGenerated, 0, MaxBufferCharge - BufferCharge);
            BufferCharge = Math.Clamp(BufferCharge + powerUsed, 0f, MaxBufferCharge);

            Device?.UsePower(OutputCableNetwork, (float)powerUsed);

            if (_nortonEquivalent != null)
            {
                _nortonEquivalent.Frequency = PowerProfile.Frequency;

                // Determine charge to use for actual calculations
                // - No output if output is disabled (device is off)
                // - Set minimum charge to avoid dividing by zero
                var charge = Math.Max(1e-5, OutputEnabled ? BufferCharge : 0.0);

                _nortonEquivalent.Resistance = PowerProfile.VoltageNominalHigh * PowerProfile.VoltageNominalHigh / charge;
                _nortonEquivalent.CurrentShort = charge / PowerProfile.VoltageNominalHigh;

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
            BufferCharge = Math.Clamp(BufferCharge + PowerDraw, 0f, MaxBufferCharge);
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

        #region Save Data
        private static readonly string CUSTOM_SAVE_DATA_PREFIX = "Hardwired.Objects.Electrical.PowerSource";
        private static readonly string BUFFER_CHARGE_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:BufferCharge";
        private static readonly string MAX_BUFFER_CHARGE_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:MaxBufferCharge";

        public void DeserializeSave(ThingSaveData saveData)
        {
            if (saveData.TryGetCustomData(BUFFER_CHARGE_STATE_NAME, out float bufferCharge))
            {
                BufferCharge = bufferCharge;
            }

            // if (saveData.TryGetCustomData(MAX_BUFFER_CHARGE_STATE_NAME, out float maxBufferCharge))
            // {
            //     MaxBufferCharge = maxBufferCharge;
            // }
        }

        public void SerializeSave(ThingSaveData saveData)
        {
            saveData.AddCustomData(BUFFER_CHARGE_STATE_NAME, (float)BufferCharge);
            // saveData.AddCustomData(MAX_BUFFER_CHARGE_STATE_NAME, (float)MaxBufferCharge);
        }
        #endregion
    }
}
