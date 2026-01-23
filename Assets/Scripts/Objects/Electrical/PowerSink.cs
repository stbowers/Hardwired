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
    public class PowerSink : ElectricalComponent, INonlinearComponent
    {
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
        /// The internal impedance of the device - in "brownout" conditions (i.e. V_min < V < V_nom) this will limit the current/power draw.
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
        /// The total current flowing through the device
        /// </summary>
        [HideInInspector]
        public Complex Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Power Sink --");
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

            // Set the max size of the energy buffer such that it can "absorb" a full second of power loss
            EnergyBufferMax = 500;
        }

        public override void Deinitialize()
        {
            base.Deinitialize();
        }

        public void UpdateDifferentialState()
        {
            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;
            Voltage = vA - vB;
            var v2 = Voltage * Voltage;

            Complex didva, didvb;

            // Calculate the load impedance for the current power target
            LoadImpedance = VoltageNominal * VoltageNominal / PowerTarget;

            // Add inductance
            var w = 2f * Math.PI * (Circuit?.Frequency ?? 0);
            LoadImpedance += new Complex(0, w * Inductance);

            // If voltage is out of range, draw no power
            if (Voltage.Magnitude < VoltageMin || Voltage.Magnitude > VoltageMax)
            {
                Current = 0f;
                didva = 0f;
                didvb = 0f;
            }
            // If voltage is below nominal, load is purely resistive (linear current)
            else if (Voltage.Magnitude < VoltageNominal)
            {
                Current = Voltage / LoadImpedance;
                didva = 1 / LoadImpedance;
                didvb = -1 / LoadImpedance;
            }
            // If voltage is above nominal, load draws constant power (non-linear current)
            else
            {
                Current = PowerTarget / Voltage;
                didva = -PowerTarget / v2;
                didvb = -PowerTarget / v2;
            }

            Circuit?.Solver.AddNonlinearCurrent(_vA, _vB, Current, didva, didvb);
        }

        public override void ApplyState()
        {
            base.ApplyState();

            // Get node voltages
            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;
            Voltage = vA - vB;

            // Calculate real power delivered to the device
            var s = Voltage * Current.Conjugate();
            Power = s.Real;
            PowerFactor = s.Real / s.Magnitude;

            // Update energy buffer and energy output
            double dT = Circuit?.TimeDelta ?? 0.5;
            double dE = (Power - PowerTarget) * dT;
            dE = Math.Clamp(dE, -EnergyBuffer, EnergyBufferMax - EnergyBuffer);
            EnergyBuffer += dE;
            EnergyInput = (Power * dT) - dE;
        }
    }
}
