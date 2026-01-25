#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using Objects.Pipes;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Breaker : ElectricalComponent
    {

        private Assets.Scripts.Objects.Pipes.Device? _device;
        private bool _internalState;

        /// <summary>
        /// The maximum voltage from either terminal to ground (i.e. max of vA and vB)
        /// </summary>
        public Complex VoltageGround { get; private set; }

        /// <summary>
        /// The voltage drop across the terminals (i.e. vA - vB).
        /// 
        /// Will be close to zero when closed, and dependent on the terminal voltages when open.
        /// </summary>
        public Complex VoltageDrop { get; private set; }

        public Complex Current { get; private set; }

        public bool Closed;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Breaker --");
            stringBuilder.AppendLine($"Closed: {Closed}");
            stringBuilder.AppendLine($"Vcc: {VoltageGround.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"ΔV: {VoltageDrop.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")}");
        }

        protected override void InitializeInternal()
        {
            base.InitializeInternal();

            TryGetComponent<Assets.Scripts.Objects.Pipes.Device>(out _device);

            // Add large resistance to ground, to ensure there are no "floating" nodes
            Circuit?.Solver.AddResistance(_vA, null, R_GND);
            Circuit?.Solver.AddResistance(_vB, null, R_GND);

            if (Closed)
            {
                // If breaker is closed, add a small resistance between nodes A and B to allow current to flow
                Circuit?.Solver.AddResistance(_vA, _vB, R_CLOSED);
            }

            _internalState = Closed;
        }

        protected override void DeinitializeInternal()
        {
            base.DeinitializeInternal();

            // Remove ground resistances
            Circuit?.Solver.AddResistance(_vA, null, -R_GND);
            Circuit?.Solver.AddResistance(_vB, null, -R_GND);

            // If previous state was closed, remove small resistance betwen A and B
            if (_internalState)
            {
                Circuit?.Solver.AddResistance(_vA, _vB, -R_CLOSED);
            }
        }

        public override void UpdateState()
        {
            // Update closed state from device
            if (_device != null)
            {
                Closed = _device.OnOff;
            }

            // If state has changed, de-init and re-init with new topology
            if (_internalState != Closed)
            {
                Deinitialize();
                Initialize();
            }

            base.UpdateState();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            var vA = Circuit?.Solver.GetValue(_vA) ?? Complex.Zero;
            var vB = Circuit?.Solver.GetValue(_vB) ?? Complex.Zero;

            VoltageDrop = vA - vB;
            VoltageGround = vA.Magnitude > vB.Magnitude ? vA : vB;

            if (Closed)
            {
                Current = VoltageDrop / R_CLOSED;
            }
            else
            {
                Current = Complex.Zero;
            }
        }
    }
}
