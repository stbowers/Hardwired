#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEditor;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class PowerSink : ElectricalComponent, ISerializationCallbackReceiver
    {
        private Device? _device;
        private EnergyBuffer? _energyBuffer;
        private DateTime _lastPowerProfileChanged = DateTime.Now;

        /// <summary>
        /// Gets the list of "power profiles" that this power sink can use.
        /// 
        /// Each profile describes the required input conditions for the power (voltage, frequency, etc), as well
        /// as the effects of using that profile (inductance/capacitance to add to the circuit, power draw efficiency, etc).
        /// </summary>
        [NonSerialized]
        public List<PowerProfile> PowerProfiles = new();

        /// <summary>
        /// Gets the currently active "power profile"
        /// </summary>
        [SerializeReference]
        public PowerProfile ActivePowerProfile = new(PowerProfile.DefaultGrid);

        /// <summary>
        /// True if the input circuit matches the constraints of the active power profile (and therefore the device can
        /// draw power), or false otherwise.
        /// </summary>
        public bool IsInputValid { get; private set; }

        public double PowerTarget { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public double BufferCharge { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Consumes electrical power from the circuit.");
                stringBuilder.AppendLine($"Modeled as a constant impedance (resistive or resistive + reactive) in series with a controllable voltage source, and an internal energy buffer.");
                stringBuilder.AppendLine($"As the energy buffer charges, the internal voltage source increases, pushing back on the circuit and reducing current draw on the next tick.");
                stringBuilder.AppendLine($"The system settles into equilibrium when input power equals the device's power consumption.");
                stringBuilder.AppendLine($"Each device has a nominal, minimum, and maximum design voltage; if voltage is above maximum or below minimum, it will not draw power.");
                stringBuilder.AppendLine($"If input voltage is above nominal (but below maximum), excess power charges the buffer until equilibrium is reached.");
                stringBuilder.AppendLine($"If input voltage is below nominal (but above minimum), the device enters a brownout state where available power is limited.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Buffer charge: {BufferCharge.ToStringPrefix("Wt", "yellow")} / {(_energyBuffer?.ChargeMaximum ?? 0).ToStringPrefix("Wt", "yellow")}");

            bool hasScrewdriver = InventoryManager.ActiveHandSlot.Get()?.PrefabName == "ItemScrewdriver";
            bool primaryButtonDown = KeyManager.GetButtonDown(KeyMap.PrimaryAction);

            // Set a cooldown on changing power profiles...
            // This prevents one input causing multiple changes if the tooltip is rendered more than once per frame.
            bool canChangePowerProfile = (DateTime.Now - _lastPowerProfileChanged).TotalSeconds > 0.2;

            // On click with screwdriver, cycle active profile index
            if (PowerProfiles.Count > 1
                && canChangePowerProfile
                && hasScrewdriver
                && primaryButtonDown)
            {
                var activeProfileIndex = PowerProfiles.IndexOf(ActivePowerProfile);
                activeProfileIndex = (activeProfileIndex + 1) % PowerProfiles.Count;
                ActivePowerProfile = PowerProfiles[activeProfileIndex];

                Hardwired.LogDebug($"Changing active profile to {activeProfileIndex} (nProfiles: {PowerProfiles.Count})");

                _lastPowerProfileChanged = DateTime.Now;
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"[[ Power Input ]]");
            stringBuilder.AppendLine($"Use screwdriver + left click to change".AsColor("yellow"));

            // If player is holding a screwdriver, show a list of all available power profiles
            if (hasScrewdriver)
            {
                for (int i = 0; i < PowerProfiles.Count; i++)
                {
                    var powerProfile = PowerProfiles[i];
                    var isActive = powerProfile == ActivePowerProfile;

                    stringBuilder.AppendLine($"[{(isActive ? "*".AsColor("green") : " ")}] {powerProfile}");
                }
            }
            // Otherwise, only show currently active profile
            else
            {
                stringBuilder.AppendLine($"{ActivePowerProfile}");
            }

            // Show power input warning
            if (!IsInputValid)
            {
                stringBuilder.AppendLine($"WARNING! Attached power input does not match selected power input profile. No power will be drawn.".AsColor("red"));
            }
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            if (InputCircuit == circuit)
            {
                if (_energyBuffer != null)
                {
                    RemoveFrom(_energyBuffer.Circuit);
                }

                var nodeA = GetNode(circuit, PowerInput, WireType.Line1);

                _energyBuffer = new(circuit, nodeA, null);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );
            
            if (circuit != InputCircuit) { return; }

            // Check input conditions of circuit, to validate the current power profile
            IsInputValid = 
                InputCircuit != null
                && (InputCircuit.Frequency == ActivePowerProfile.Frequency)
                && (_energyBuffer?.VoltageDelta.Magnitude >= ActivePowerProfile.VoltageMinimum)
                && (_energyBuffer?.VoltageDelta.Magnitude <= ActivePowerProfile.VoltageMaximum);

            if (IsInputValid)
            {
                // Set power target to the amount of power requested by the device (divided by efficiency of current profile)
                PowerTarget = _device?.GetUsedPower(InputCableNetwork) ?? 0f;
                PowerTarget = PowerTarget / ActivePowerProfile.Efficiency;
                PowerTarget = Math.Min(ActivePowerProfile.MaximumPower, PowerTarget);
            }
            else
            {
                // Input circuit is not valid, don't draw any power.
                PowerTarget = 0;
            }

            if (_energyBuffer != null)
            {
                // Set a very low minimum power target to use for the actual calculations, to avoid dividing by zero.
                // This low of a power target will cause the resistance to be very high, which will effectively act
                // as if the sink were disconnected from the circuit if PowerTarget ~= 0.
                var powerTarget = Math.Max(1e-5, PowerTarget);

                // Set resistance such that the energy buffer would draw the full power target at min voltage
                _energyBuffer.Resistance = ActivePowerProfile.VoltageNominalLow * ActivePowerProfile.VoltageNominalLow / powerTarget;

                // Set max voltage
                _energyBuffer.VoltageMaximum = ActivePowerProfile.VoltageMaximum;

                // The maximum power delivered to the energy buffer (at V = VoltageMax, Charge = 0) should be
                // (VoltageMaximum / VoltageMinimum)^2 * PowerTarget.
                // Ensure the buffer is at least large enough to hold one tick at that power rate.
                var rVmaxVnom = ActivePowerProfile.VoltageMaximum / ActivePowerProfile.VoltageNominalLow;
                var pMax = rVmaxVnom * rVmaxVnom * powerTarget;
                _energyBuffer.ChargeMaximum = Math.Max(pMax, _energyBuffer.ChargeMaximum);

                // Set other (static) properties and update buffer state
                _energyBuffer.VoltageCurve = EnergyBuffer.VoltageCurveFunction.Linear;
                _energyBuffer.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (circuit != InputCircuit) { return; }

            _energyBuffer?.ApplyState();

            BufferCharge = _energyBuffer?.Charge ?? 0;
            PowerFactor = _energyBuffer?.PowerFactor ?? 0;
            VoltageDelta = _energyBuffer?.VoltageDelta ?? 0;
            CurrentDraw = _energyBuffer?.Current ?? 0;

            // Calculate how much power to actually remove from the buffer (based on PowerTarget)
            PowerDraw = Math.Min(BufferCharge, PowerTarget);

            if (_energyBuffer != null)
            {
                _energyBuffer.Charge -= PowerDraw;
            }

            // Calculate how much power will actually go to the device (after active profile's efficiency loss),
            // and the minimum power draw required in order to enable the device
            var powerDelivered = PowerDraw * ActivePowerProfile.Efficiency;

            _device?.ReceivePower(InputCableNetwork, (float)powerDelivered);
            _device?.SetPowerFromThread(InputCableNetwork, PowerDraw > 0).Forget();
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _energyBuffer?.Circuit)
            {
                _energyBuffer?.Dispose();
                _energyBuffer = null;
            }
        }

        #region Save Data
        private static readonly string CUSTOM_SAVE_DATA_PREFIX = "Hardwired.Objects.Electrical.PowerSink";
        private static readonly string ACTIVE_PROFILE_INDEX_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:ActivePowerProfileIndex";

        public override void DeserializeSave(ThingSaveData saveData)
        {
            base.DeserializeSave(saveData);

            if (saveData.TryGetCustomData(ACTIVE_PROFILE_INDEX_STATE_NAME, out int activePowerProfileIndex)
                && activePowerProfileIndex >= 0
                && activePowerProfileIndex < PowerProfiles.Count)
            {
                ActivePowerProfile = PowerProfiles[activePowerProfileIndex];
            }
        }

        public override void SerializeSave(ThingSaveData saveData)
        {
            base.SerializeSave(saveData);

            int activePowerProfileIndex = PowerProfiles.IndexOf(ActivePowerProfile);
            saveData.AddCustomData(ACTIVE_PROFILE_INDEX_STATE_NAME, activePowerProfileIndex);
        }
        #endregion

        #region Unity Custom Serialization
        /// <summary>
        /// Custom serialized property to back the `PowerProfiles` list using unity's ISerializationCallbackReceiver interface.
        /// 
        /// Note - this is done because for some reason `PowerProfiles` is not correctly serialized by Unity. In fact, _no_ types from the
        /// Hardwired assembly can be serialized inline, it appears everything must be serialized as a reference. However, [SerializeReference]
        /// does not work directly on `PowerProfiles` either, so we serialize the power profiles by reference as a list of objects, which
        /// _does_ appear to work.
        /// 
        /// I _think_ this might be because this is a BepInEx mod which is loaded at runtime, and so Unity's serialization infrastructure
        /// doesn't "know about" our custom classes. While it can serialize our types individually, it can't serialize them as a graph (i.e.
        /// it doesn't know what to do with any field types that use Hardwired types). The [SerializeReference] attribute _does_ appear to work,
        /// probably because internally Unity serializes the references separately from the rest of the graph. However, [SerializeReference] doesn't
        /// seem to work on strongly typed lists...
        /// 
        /// While I don't *love* this workaround, I didn't want to spend too much time trying to figure out exactly what was happening, so it's
        /// possible there's some way to get Unity to recognize Hardwired types for serialization in a cleaner way...
        /// </summary>
        [SerializeField, SerializeReference, HideInInspector]
        private List<object> _serializedPowerProfileReferences = new();

        public void OnBeforeSerialize()
        {
            if (PowerProfiles.Count == 0)
            {
                PowerProfiles.Add(ActivePowerProfile);
            }

            _serializedPowerProfileReferences.Clear();
            _serializedPowerProfileReferences.AddRange(PowerProfiles);
        }

        public void OnAfterDeserialize()
        {
            PowerProfiles.Clear();
            PowerProfiles.AddRange(_serializedPowerProfileReferences.OfType<PowerProfile>());

            if (PowerProfiles.Count > 0)
            {
                ActivePowerProfile = PowerProfiles[0];
            }
        }
        #endregion
    }
}
