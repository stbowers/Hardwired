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
        /// <summary>
        /// Resistance value to use to tie nodes to ground, to prevent floating nodes.
        /// Should be relatively large, to avoid leaking any significant current, but not too large to avoid causing an ill-conditioned (singular or near-singular) matrix, which can cause problems for the solver.
        /// </summary>
        private const double R_GND = 1e6;

        /// <summary>
        /// Resistance value to use between the two nodes of the breaker when closed.
        /// Should be relatively small, to avoid voltage drop across the breaker, but not too small as to introduce numerical errors into the solver.
        /// </summary>
        private const double R_CLOSED = 1e-4;

        private Assets.Scripts.Objects.Pipes.Device? _device;
        private bool _internalState;

        public Complex Voltage { get; private set; }

        public Complex Current { get; private set; }

        public bool Closed;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Breaker --");
            stringBuilder.AppendLine($"Closed: {Closed}");
            stringBuilder.AppendLine($"Vcc: {Voltage.ToStringPrefix("V", "yellow")}");
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
            Circuit?.Solver.AddResistance(_vA, null, -1e10);
            Circuit?.Solver.AddResistance(_vB, null, -1e10);

            // If previous state was closed, remove small resistance betwen A and B
            if (_internalState)
            {
                Circuit?.Solver.AddResistance(_vA, _vB, -1e-10);
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
            Voltage = vA.Magnitude > vB.Magnitude ? vA : vB;

            if (Closed)
            {
                var dV = vA - vB;
                Current = dV / 1e-10;
            }
            else
            {
                Current = Complex.Zero;
            }
        }
    }
}
