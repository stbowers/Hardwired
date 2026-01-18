#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class PowerSink : ElectricalComponent
    {
        /// <summary>
        /// The maximum designed power consumption of this device.
        /// Used to calculate the internal resistance value.
        /// </summary>
        public double MaxPower;

        /// <summary>
        /// The target power in Watts.
        /// Each tick this component will adjust its current draw based on I = P / V in order to maintain this power target.
        /// </summary>
        public double PowerTarget;

        /// <summary>
        /// The minimum operational voltage a device can accept. If the input voltage is below this, the device will enter an undervoltage protection
        /// state and stop drawing power.
        /// </summary>
        public double VoltageMin = 50;

        /// <summary>
        /// The maximum operational voltage a device can accept. If the input voltage is above this, the device will enter an overvoltage protection
        /// state and stop drawing power.
        /// </summary>
        public double VoltageMax = 200f;

        /// <summary>
        /// The nominal operational voltage for a device. If voltage is at this value or above (up to V_max), the device will draw the target power.
        /// If voltage is below this value (down to V_min), the device enters a "brownout" state where it draws less power in proportion to voltage.
        /// </summary>
        public double VoltageNominal = 100f;

        /// <summary>
        /// The internal impedance of the device - this is always added to the circuit.
        /// In "brownout" conditions (V_min < V < V_nom), this is the only contribution to current, leading to less power being delivered than was requested.
        /// In "nominal" conditions (V_nom < V < V_max), if the resistor were the only element it would deliver _too much_ power to the device, so the current
        /// through the resistor is limited to just what is needed for the current power target.
        /// </summary>
        [HideInInspector]
        public Complex LoadImpedance;

        /// <summary>
        /// The inductance in this load (or 0 for a purely resistive load)
        /// </summary>
        [HideInInspector]
        public double Inductance;

        /// <summary>
        /// The current charge in the internal energy buffer.
        /// 
        /// The energy buffer essentially acts as a capacitor (in theory, not in actual math), padding out sudden changes in current.
        /// Each tick when power is updated, the energy buffer will "absorb" the difference between the actual power delivered by the circuit and the requested power.
        /// If the requested power is higher than the actual power delivered, the energy buffer will be drained.
        /// If the requested power is lower than the actual power delivered, the energy buffer will be filled.
        /// 
        /// The device will try to maintain ~80% charge in the energy buffer by slightly increasing or decreasing the actual power "requested" per tick.
        /// 
        /// This prevents "phantom power" (i.e. the device drew more power than was actually available) as well as power loss due to the power source sending more power
        /// to the device than it needed.
        /// </summary>
        [HideInInspector]
        public double EnergyBuffer;

        /// <summary>
        /// The maximum charge in the internal energy buffer.
        /// If the energy buffer is filled past this point, any additional energy is lost to the void.
        /// </summary>
        [HideInInspector]
        public double EnergyBufferMax;

        /// <summary>
        /// The calculated energy consumed from the circuit by this power sink for this tick.
        /// (note that devices generally provide/consume power in Watts, i.e. via Device.UsePower(), so this value needs to be divided by dt to get the power used this tick)
        /// </summary>
        [HideInInspector]
        public double EnergyInput;

        /// <summary>
        /// The real power being delivered to the device.
        /// </summary>
        [HideInInspector]
        public double Power;

        /// <summary>
        /// The ratio of real power to apparent power
        /// </summary>
        [HideInInspector]
        public double PowerFactor;

        /// <summary>
        /// The momentary voltage across this current source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The current being applied to the circuit from the current source (to counteract over-current in the resistor)
        /// </summary>
        [HideInInspector]
        public Complex SourceCurrent;

        /// <summary>
        /// The current flowing through the resistor
        /// </summary>
        [HideInInspector]
        public Complex ResistorCurrent;

        /// <summary>
        /// The total current flowing through the device
        /// </summary>
        [HideInInspector]
        public Complex Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Power Sink --");
            stringBuilder.AppendLine($"Nominal Power: {MaxPower.ToStringPrefix("W", "yellow")} (@ {VoltageNominal.ToStringPrefix("V", "yellow")})");
            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Delivered: {Power.ToStringPrefix("W", "yellow")} | PF: {PowerFactor:F3}");
            stringBuilder.AppendLine($"Impedance: {LoadImpedance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Energy Buffer: {EnergyBuffer.ToStringPrefix("J", "yellow")} / {EnergyBufferMax.ToStringPrefix("J", "yellow")}");
        }

        public override void Initialize()
        {
            base.Initialize();

            if (Circuit == null) { return; }

            // Set the max size of the energy buffer such that it can "absorb" a full second of power loss
            EnergyBufferMax = 1 * MaxPower;

            // Calculate the load impedance - the resistor should be sized
            // such that P_resistor(V_nom) = MaxPower = V_nom^2 / Impedance
            LoadImpedance = VoltageNominal * VoltageNominal / MaxPower;

            // Add inductance
            var w = 2f * Math.PI * Circuit.Frequency;
            LoadImpedance += new Complex(0, w * Inductance);

            // Add the impedance to the circuit
            Circuit.Solver.AddImpedance(_vA, _vB, LoadImpedance);
        }

        public override void Deinitialize()
        {
            base.Deinitialize();

            Circuit?.Solver.AddImpedance(_vA, _vB, -LoadImpedance);
        }

        public override void UpdateState()
        {
            base.UpdateState();

            if (Circuit == null) { return; }

            var previousSourceCurrent = SourceCurrent;

            // If voltage is 0, don't add any current into the circut (avoids dividing by zero in math below)
            if (Voltage.Magnitude < 0.001)
            {
                SourceCurrent = 0f;
            }
            // If voltage is less than the min voltage, draw no power
            else if (Voltage.Magnitude < VoltageMin)
            {
                SourceCurrent = -ResistorCurrent;
            }
            // If voltage is more than the max voltage, draw no power
            else if (Voltage.Magnitude > VoltageMax)
            {
                SourceCurrent = -ResistorCurrent;
            }
            else
            {
                // Adjust the "requested" power by however much would be required to fill (or drain) the energy buffer to 80% capacity over the next tick
                var eRequired = (0.8 * EnergyBufferMax) - EnergyBuffer;
                var bufferPowerRequired = eRequired / Circuit.TimeDelta;
                var powerRequested = PowerTarget + bufferPowerRequired;

                // Calculate error in how much current we actually want given the input voltage and how much current is flowing through the resistor
                var iRequired = (powerRequested / Voltage).Conjugate();
                SourceCurrent = iRequired - ResistorCurrent.Conjugate();

                // Limit how fast current can change (slew)
                var dI = SourceCurrent - previousSourceCurrent;
                var r = Math.Max(0.1f, -dI.Magnitude / 2f  + 2f);
                SourceCurrent *= r;
            }

            // Only apply correction current if it counteracts the voltage (i.e. only subtract from the current, never add to make up for there not being enough power)
            if ((SourceCurrent * Voltage.Conjugate()).Real < 0)
            {
                SourceCurrent = 0;
            }

            // Add current to counteract the resistor to the circuit
            Circuit.Solver.AddCurrent(_vA, _vB, SourceCurrent);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            // Get node voltages
            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);

            // Get voltage across the device, and calculate current going through the resistor
            Voltage = vA - vB;
            ResistorCurrent = Voltage / LoadImpedance;

            // Determine the total current flowing through the device (current source is in parallel to resistor)
            Current = SourceCurrent + ResistorCurrent;

            // Calculate real power delivered to the device
            var s = Voltage * Current.Conjugate();
            Power = s.Real;
            PowerFactor = s.Real / s.Magnitude;

            // Update energy buffer and energy output
            double dE = (Power - PowerTarget) * Circuit.TimeDelta;
            dE = Math.Clamp(dE, -EnergyBuffer, EnergyBufferMax - EnergyBuffer);
            EnergyBuffer += dE;
            EnergyInput = (Power * Circuit.TimeDelta) - dE;
        }
    }
}
