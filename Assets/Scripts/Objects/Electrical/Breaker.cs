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

        public override void Initialize()
        {
            base.Initialize();

            // Add large resistance to ground, to ensure there are no "floating" nodes
            Circuit?.Solver.AddResistance(_vA, null, 1e10);
            Circuit?.Solver.AddResistance(_vB, null, 1e10);

            if (Closed)
            {
                // If breaker is closed, add a small resistance between nodes A and B to allow current to flow
                Circuit?.Solver.AddResistance(_vA, _vB, 1e-10);
            }

            _internalState = Closed;
        }

        public override void Deinitialize()
        {
            base.Deinitialize();

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
