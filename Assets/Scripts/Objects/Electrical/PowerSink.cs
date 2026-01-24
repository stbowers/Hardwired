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
        /// Used for "histeresis" so we don't switch modes within a single tick
        /// </summary>
        private bool? _voltageInRage;

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
        /// The real power being delivered to the device this tick.
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
        }

        public void UpdateDifferentialState()
        {
            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;
            Voltage = vA - vB;
            var v2 = Voltage * Voltage;

            // Initialize _voltageInRage if this is the first tick
            _voltageInRage ??= Voltage.Magnitude >= VoltageMin && Voltage.Magnitude <= VoltageMax;

            // Calculate the load impedance for the current power target
            LoadImpedance = VoltageNominal * VoltageNominal / PowerTarget;

            // Add inductance
            var w = 2f * Math.PI * (Circuit?.Frequency ?? 0);
            LoadImpedance += new Complex(0, w * Inductance);

            Complex didva, didvb;

            // If voltage is out of range, draw no power
            if (_voltageInRage != true)
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

            // Determine operating mode
            // Note - we set this here instead of in UpdateDifferentialState() so that there are no discontinuities in I(v) while solving a single tick
            _voltageInRage = Voltage.Magnitude >= VoltageMin && Voltage.Magnitude <= VoltageMax;
        }
    }
}
