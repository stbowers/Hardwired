#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Utility;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class PowerSink : ElectricalComponent
    {
        /// <summary>
        /// The target power in Watts.
        /// Each tick this component will adjust its current draw based on I = P / V
        /// </summary>
        public double PowerTarget;

        /// <summary>
        /// The maximum voltage this device can accept.
        /// If supplied voltage is above this value, the device will not draw any current.
        /// </summary>
        public double MaxVoltage = 600f;

        /// <summary>
        /// The minimum voltage this device can accept and still draw full power.
        /// If supplied voltage is below this value, the device will draw less power following `P = (V / BrownoutVoltage) * PowerTarget`, leading to machines slowing down or failing.
        /// </summary>
        public double BrownoutVoltage = 100f;

        /// <summary>
        /// The minimum voltage this device can accept and still draw any power.
        /// If supplied voltage is below this value, the device will not draw any current.
        /// </summary>
        public double MinVoltage = 50f;

        /// <summary>
        /// The actual power being delivered to the device
        /// </summary>
        [HideInInspector]
        public double Power;

        /// <summary>
        /// The momentary voltage across this current source calculated by the circuit solver.
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        [HideInInspector]
        public Complex Current;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Power Target: {PowerTarget.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Delivered: {Power.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.Magnitude.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.Magnitude.ToStringPrefix("A", "yellow")}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            solver.AddResistance(n, m, 100000);
        }

        public override void UpdateSolverInputs(MNASolver solver)
        {
            base.UpdateSolverInputs(solver);

            // Determine how much power we should "request"
            double v = Math.Max(0.01, Voltage.Magnitude);
            double requestedPower = PowerTarget;

            if (v < MinVoltage)
            {
                requestedPower = 0f;
            }
            else if (v > MaxVoltage)
            {
                requestedPower = 0f;
            }
            else if (v < BrownoutVoltage)
            {
                double r = (v - MinVoltage) / (BrownoutVoltage - MinVoltage);
                requestedPower = PowerTarget * r;
            }

            // Calculate new current value based on requested power and current voltage; ensure current is in-phase with voltage
            // Note - we clamp i to no more than 5A more than the previous value, to limit the rate at which current can suddenly "jump", to avoid shocking the network.
            double i = Math.Clamp(requestedPower / v, 0, Current.Magnitude + 5f);
            Complex vUnit = Voltage / v;
            Current = i * vUnit;

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            solver.SetCurrent(n, m, Current);
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            var n = GetNodeIndex(PinA);
            var m = GetNodeIndex(PinB);

            var vN = solver.GetVoltage(n);
            var vM = solver.GetVoltage(m);

            Voltage = vN - vM;
            Power = (Voltage * Current.Conjugate()).Real;
        }
    }
}
