#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Inductor : ElectricalComponent
    {
        /// <summary>
        /// Inductance value in Henrys
        /// </summary>
        public double Inductance;

        /// <summary>
        /// The instantaneous voltage differential across the inductor, at the current power tick
        /// </summary>
        [HideInInspector]
        public Complex Voltage;

        /// <summary>
        /// The current being permitted through this inductor
        /// </summary>
        [HideInInspector]
        public Complex Current;

        /// <summary>
        /// Reactance value in ohms (depends on AC circuit frequency)
        /// </summary>
        [HideInInspector]
        public double Reactance;

        /// <summary>
        /// The current energy held in the inductor, in Joules
        /// </summary>
        [HideInInspector]
        public double Energy;

        private MNASolver.Unknown? _i;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"-- Inductor --");
            stringBuilder.AppendLine($"Inductance: {Inductance.ToStringPrefix("F", "yellow")}");
            stringBuilder.AppendLine($"Reactance: {Reactance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Voltage: {Voltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Energy: {Energy.ToStringPrefix("J", "yellow")}");
        }

        protected override void InitializeInternal()
        {
            base.InitializeInternal();

            if (Circuit == null) { return; }

            // If circuit has AC current, add impedence based on the frequency
            if (Circuit.Frequency != 0)
            {
                var w = 2f * Math.PI * Circuit.Frequency;
                Reactance = w * Inductance;

                Circuit.Solver.AddReactance(_vA, _vB, Reactance);
            }
            // If circuit has DC current, use backwards Euler to solve differential equation
            else
            {
                // // TODO: Add some better documentation about what this is doing...
                // // By adding an extra term to the A matrix and z vector, we're essentially solving the differential equation step-by-step with an approximation similar to
                // // i(t) = i(t-1) + dv.
                // // A similar setup is used for Capacitor as well, and has better comments
                Circuit.Solver.AddVoltageSource(_vA, _vB, ref _i);

                // var dt = 0.5;
                // var x = Inductance / dt;

                // // TODO: We don't have "direct" access to the part of the A matrix we need to modify for inductors...
                // // It would probably be good to refactor this code at some point to make it more clear what we're adding
                // var j = Circuit.Solver.Nodes + _v;
                // solver.AddAdmittance(j, null, x);
            }
            // TODO: Add MNASolver.RemoveTransformer() method so we don't have to force the entire circuit to re-initialize
            Circuit?.Invalidate();
        }

        protected override void DeinitializeInternal()
        {
            base.DeinitializeInternal();

            if (Circuit == null) { return; }

            if (Circuit.Frequency != 0)
            {
                var w = 2f * Math.PI * Circuit.Frequency;
                Reactance = w * Inductance;

                Circuit.Solver.AddReactance(_vA, _vB, -Reactance);
            }
            else
            {
                Circuit.Solver.RemoveUnknown(_i);
                _i = null;
                // TODO: Add MNASolver.RemoveVoltageSource() method so we don't have to force the entire circuit to re-initialize
                Circuit.Invalidate();
            }
        }

        public override void UpdateState()
        {
            base.UpdateState();

            if (Circuit == null) { return; }

            if (Circuit.Frequency == 0f)
            {
                // var dt = 0.5;
                // var x = Inductance * Current / dt;

                // solver.SetVoltage(_v, x);
            }
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            Voltage = vA - vB;

            if (Circuit.Frequency == 0f)
            {
                Current = Circuit.Solver.GetValueOrDefault(_i);

                Energy = (0.5f * Inductance * Current * Current).Magnitude;
            }
            else
            {
                Current = Voltage / new Complex(0, Reactance);
            }
        }
    }
}
