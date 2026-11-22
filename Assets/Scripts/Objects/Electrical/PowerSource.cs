#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Component to be attached to devices in order to supply power to the circuit (generators, power converters, etc).
    /// </summary>
    public class PowerSource : ElectricalComponent
    {
        /// <summary>
        /// Note - we use a `NortonEquivalent` as the actual circuit component here, instead of a `ConstantPowerSource`, because
        /// `ConstantPowerSource` represents a non-linear power source/draw (i.e. constant power draw from devices).
        /// 
        /// In our case however, we don't want to actually supply a constant amount of power - we'll only supply as much power as is
        /// "requested".
        /// So instead of a constant power source, we basically want to model our generator/battery/whatever as a non-ideal voltage
        /// or current source, which is what a "norton equivalent" is.
        /// </summary>
        private NortonEquivalent[]? _nortonEquivalents;


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
            stringBuilder.AppendLine($"Phases: {_nortonEquivalents?.Length} (target: {PowerProfile.Phases})");
        }

        private void EnsureInitialized(Circuit circuit)
        {
            if (PowerProfile.Frequency == 0 || PowerProfile.Phases == 1)
            {
                EnsureInitializedSinglePhase(circuit);
            }
            else
            {
                EnsureInitializedThreePhase(circuit);
            }

        }

        private void EnsureInitializedSinglePhase(Circuit circuit)
        {
            var nodeA = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Line1);
            var nodeB = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Neutral);

            if (_nortonEquivalents?.Length == 1 && _nortonEquivalents[0]?.Circuit == circuit && _nortonEquivalents[0].NodeA == nodeA && _nortonEquivalents[0].NodeB == nodeB)
            {
                return;
            }

            if (_nortonEquivalents != null)
            {
                RemoveFrom(_nortonEquivalents[0].Circuit);
            }

            _nortonEquivalents = new NortonEquivalent[] {
                new(circuit, nodeA, nodeB)
            };
        }

        private void EnsureInitializedThreePhase(Circuit circuit)
        {
            var nodeA = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Line1);
            var nodeB = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Line2);
            var nodeC = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Line3);
            var commonNode = PowerOutput?.GetNode(circuit, Networks.CableSpan.ConductorType.Neutral);

            if (_nortonEquivalents?.Length == 3
                && _nortonEquivalents[0]?.Circuit == circuit
                && _nortonEquivalents[0].NodeA == nodeA && _nortonEquivalents[0].NodeB == commonNode
                && _nortonEquivalents[1].NodeA == nodeB && _nortonEquivalents[1].NodeB == commonNode
                && _nortonEquivalents[2].NodeA == nodeC && _nortonEquivalents[2].NodeB == commonNode)
            {
                return;
            }

            if (_nortonEquivalents != null)
            {
                RemoveFrom(_nortonEquivalents[0].Circuit);
            }

            _nortonEquivalents = new NortonEquivalent[] {
                new(circuit, nodeA, commonNode),
                new(circuit, nodeB, commonNode),
                new(circuit, nodeC, commonNode)
            };
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (circuit != OutputCircuit) { return; }

            EnsureInitialized(circuit);

            if (_nortonEquivalents == null) { return; }

            // Get generated power
            PowerGenerated = Device?.GetGeneratedPower(OutputCableNetwork) ?? 0;
            var powerPerPhase = PowerGenerated / _nortonEquivalents.Length;

            for (int phase = 0; phase < _nortonEquivalents.Length; phase++)
            {
                var nortonEquivalent = _nortonEquivalents[phase];
                nortonEquivalent.Frequency = PowerProfile.Frequency;


                // Set up current source if output is enabled (device is on) and non-zero power is being generated
                if (OutputEnabled && powerPerPhase > 0.1)
                {
                    nortonEquivalent.Resistance = PowerProfile.VoltageNominalHigh * PowerProfile.VoltageNominalHigh / (4 * powerPerPhase);

                    // set each phase voltage to be equally spaced
                    double phaseAngle = phase * (2 * Math.PI / _nortonEquivalents.Length);
                    nortonEquivalent.VoltageOpen = Complex.FromPolarCoordinates(PowerProfile.VoltageNominalHigh, phaseAngle);
                }
                // Otherwise, if device is off or not generating power, set voltage to 0 and resistance to "open"
                else
                {
                    nortonEquivalent.Resistance = Resistor.R_OPEN;
                    nortonEquivalent.VoltageOpen = 0;
                }

                nortonEquivalent.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            if (OutputCircuit != circuit) { return; }

            PowerDraw = 0;
            PowerFactor = 0;
            VoltageDelta = 0;
            CurrentDraw = 0;

            if (_nortonEquivalents == null) { return; }

            foreach (var ne in _nortonEquivalents)
            {
                ne.ApplyState();
                PowerDraw += ne.Power.Real;
                PowerFactor += ne.PowerFactor;
                VoltageDelta += ne.VoltageDelta;
                CurrentDraw += ne.Current;
            }

            // Average power factor & voltage
            PowerFactor /= _nortonEquivalents.Length;
            VoltageDelta /= _nortonEquivalents.Length;

            // Update internal charge
            Device?.UsePower(OutputCableNetwork, (float)PowerDraw);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);


            if (_nortonEquivalents?.Any(n => circuit == n.Circuit) == true)
            {
                PowerGenerated = 0;
                PowerFactor = 0;
                VoltageDelta = 0;
                CurrentDraw = 0;
                PowerDraw = 0;

                foreach (var ne in _nortonEquivalents)
                {
                    ne.Dispose();
                }
                _nortonEquivalents = null;
            }
        }
    }
}
