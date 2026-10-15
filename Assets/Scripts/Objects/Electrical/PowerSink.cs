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
using Hardwired.Patches;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEditor;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Component to be attached to devices in order to draw power from the circuit.
    /// </summary>
    public class PowerSink : ElectricalComponent, ISerializationCallbackReceiver, IComponentSaveData
    {
        private Device? _device;
        private ConstantPowerSource? _powerSource;
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

        /// <summary>
        /// The power (in Watts) that this device will attempt to draw from the circuit.
        /// Determined from Device.GetUsedPower().
        /// </summary>
        public double PowerTarget { get; private set; }

        /// <summary>
        /// The minimum power (in Watts) the device needs to receive from the circuit this tick in order to be powered.
        /// Determined from PowerTarget and ActivePowerProfile.
        /// </summary>
        public double PowerThreshold { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            BuildDescriptionTooltip(stringBuilder);
            BuildPowerProfileTooltip(stringBuilder, true);
            BuildDebugInfoTooltip(stringBuilder);
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            if (InputCircuit == circuit)
            {
                if (_powerSource != null)
                {
                    RemoveFrom(_powerSource.Circuit);
                }

                var nodeA = PowerInput?.GetNode(circuit, WireType.Line1);
                var nodeB = PowerInput?.GetNode(circuit, WireType.Neutral);

                _powerSource = new(circuit, nodeA, nodeB);
            }
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );
            
            if (circuit != InputCircuit) { return; }

            // Check input conditions of circuit, to validate the current power profile
            IsInputValid =
                InputCircuit != null
                && (InputCircuit.Frequency == ActivePowerProfile.Frequency);

            if (IsInputValid)
            {
                // Set power target to the amount of power requested by the device (divided by efficiency of current profile)
                // Note - in most cases, ActivePowerProfile.Efficiency < 1, therfore `PowerTarget` will be *greater* than the actual
                // power used by the device. When the power is actually drawn from the circuit, it will be multiplied by the efficiency
                // factor again before being sent to _device.ReceivePower() so therefore, under normal circumstances, the power received
                // should equal the power requested.
                PowerTarget = _device?.GetUsedPower(InputCableNetwork) ?? 0f;
                PowerTarget = PowerTarget / ActivePowerProfile.Efficiency;
                PowerTarget = Math.Min(ActivePowerProfile.MaximumPower, PowerTarget);
                PowerThreshold = ActivePowerProfile.MinimumPowerRatio * PowerTarget;
            }
            else
            {
                // Input circuit is not valid, don't draw any power.
                PowerTarget = 0;
                PowerThreshold = 0;
            }

            if (_powerSource != null)
            {
                // Update power "source"
                _powerSource.SourcePower = PowerTarget;
                _powerSource.MaximumVoltage = ActivePowerProfile.VoltageMaximum;
                _powerSource.MinimumVoltage = ActivePowerProfile.VoltageNominalLow;
                _powerSource.NominalVoltage = ActivePowerProfile.VoltageNominalLow;
                _powerSource.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (circuit != InputCircuit) { return; }

            _powerSource?.ApplyState();

            PowerFactor = _powerSource?.PowerFactor ?? 0;
            VoltageDelta = _powerSource?.VoltageDelta ?? 0;
            CurrentDraw = _powerSource?.Current ?? 0;
            PowerDraw = _powerSource?.Power.Real ?? 0;

            // Calculate how much power will actually go to the device (after active profile's efficiency loss)
            var powerDelivered = PowerDraw * ActivePowerProfile.Efficiency;
            _device?.ReceivePower(InputCableNetwork, (float)powerDelivered);

            // Set the device's power status based on if it received enough power this tick
            _device?.SetPowerFromThread(InputCableNetwork, powerDelivered > PowerThreshold).Forget();
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            if (circuit == _powerSource?.Circuit)
            {
                PowerTarget = 0;
                PowerThreshold = 0;
                PowerFactor = 0;
                VoltageDelta = 0;
                CurrentDraw = 0;
                PowerDraw = 0;

                _powerSource?.Dispose();
                _powerSource = null;
            }
        }

        private void BuildDescriptionTooltip(StringBuilder stringBuilder)
        {
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
        }

        private void BuildDebugInfoTooltip(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(InputCircuit?.Frequency, "V", "yellow")} | Current Draw: {CurrentDraw.ToStringPrefix(InputCircuit?.Frequency, "A", "yellow")}");
        }

        /// <summary>
        /// Shows the active input power profile, or the list of available profiles if a screwdriver is being held.
        /// 
        /// If interactable == true, will also change the active power profile if a screwdriver is being held and
        /// the primary action button (left click) is pressed.
        /// 
        /// Note - it's probably not *ideal* to process interaction in the tooltip builder; in theory it'd be better
        /// to do this in something like InteractWith(); however InteractWith() is not called unless the user is
        /// specifically looking at an interactable hitbox, which would be impractical to add to every device...
        /// </summary>
        /// <param name="stringBuilder"></param>
        /// <param name="interactable"></param>
        internal void BuildPowerProfileTooltip(StringBuilder stringBuilder, bool interactable)
        {
            bool hasScrewdriver = InventoryManager.ActiveHandSlot.Get()?.PrefabName == "ItemScrewdriver";
            bool primaryButtonDown = KeyManager.GetButtonDown(KeyMap.PrimaryAction);

            // Set a cooldown on changing power profiles...
            // This prevents one input causing multiple changes if the tooltip is rendered more than once per frame.
            bool canChangePowerProfile = (DateTime.Now - _lastPowerProfileChanged).TotalSeconds > 0.2;

            // On click with screwdriver, cycle active profile index
            if (interactable
                && PowerProfiles.Count > 1
                && canChangePowerProfile
                && hasScrewdriver
                && primaryButtonDown)
            {
                var activeProfileIndex = PowerProfiles.IndexOf(ActivePowerProfile);
                activeProfileIndex = (activeProfileIndex + 1) % PowerProfiles.Count;
                ActivePowerProfile = PowerProfiles[activeProfileIndex];

                // Hardwired.LogDebug($"Changing active profile to {activeProfileIndex} (nProfiles: {PowerProfiles.Count})");

                _lastPowerProfileChanged = DateTime.Now;
            }

            stringBuilder.AppendLine($"[[ Power Input ]]");

            if (PowerProfiles.Count > 1)
            {
                stringBuilder.AppendLine($"Use screwdriver + left click to change".AsColor("yellow"));
            }

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

        #region Save Data
        private static readonly string CUSTOM_SAVE_DATA_PREFIX = "Hardwired.Objects.Electrical.PowerSink";
        private static readonly string ACTIVE_PROFILE_INDEX_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:ActivePowerProfileIndex";
        private static readonly string BUFFER_CHARGE_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:BufferCharge";
        private static readonly string MAX_BUFFER_CHARGE_STATE_NAME = $"{CUSTOM_SAVE_DATA_PREFIX}:MaxBufferCharge";

        public void DeserializeSave(ThingSaveData saveData)
        {
            if (saveData.TryGetCustomData(ACTIVE_PROFILE_INDEX_STATE_NAME, out int activePowerProfileIndex)
                && activePowerProfileIndex >= 0
                && activePowerProfileIndex < PowerProfiles.Count)
            {
                ActivePowerProfile = PowerProfiles[activePowerProfileIndex];
            }
        }

        public void SerializeSave(ThingSaveData saveData)
        {
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
