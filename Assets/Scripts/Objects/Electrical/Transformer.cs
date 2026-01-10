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
    public class Transformer : ElectricalComponent
    {
        public int PinC = -1;

        public int PinD = -1;

        protected MNASolver.Unknown? _vC;

        protected MNASolver.Unknown? _vD;

        protected MNASolver.Unknown? _i1;

        protected MNASolver.Unknown? _i2;

        public double N;

        /// <summary>
        /// The solved voltage across the primary coil
        /// </summary>
        [HideInInspector]
        public Complex PrimaryVoltage;

        /// <summary>
        /// The solved voltage across the secondary coil
        /// </summary>
        [HideInInspector]
        public Complex SecondaryVoltage;

        /// <summary>
        /// The solved current across the primary coil
        /// </summary>
        [HideInInspector]
        public Complex PrimaryCurrent;

        /// <summary>
        /// The solved current across the secondary coil
        /// </summary>
        [HideInInspector]
        public Complex SecondaryCurrent;

        public override bool UsesConnection(int connection)
            => PinC == connection || PinD == connection || base.UsesConnection(connection);

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            var primaryPower = (PrimaryVoltage * PrimaryCurrent.Conjugate()).Real;
            var secondaryPower = (SecondaryVoltage * SecondaryCurrent.Conjugate()).Real;

            stringBuilder.AppendLine($"-- Transformer --");
            stringBuilder.AppendLine($"N: {N}");
            stringBuilder.AppendLine($"Primary Coil Voltage: {PrimaryVoltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Voltage: {SecondaryVoltage.ToStringPrefix(Circuit?.Frequency, "V", "yellow")}");
            stringBuilder.AppendLine($"Primary Coil Current: {PrimaryCurrent.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Current: {SecondaryCurrent.ToStringPrefix(Circuit?.Frequency, "A", "yellow")}");
            stringBuilder.AppendLine($"Primary Coil Power: {primaryPower.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Power: {secondaryPower.ToStringPrefix("W", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            // Initialize pins
            _vC = Circuit?.GetNode(this, PinC);
            _vD = Circuit?.GetNode(this, PinD);
        }

        public override void Initialize()
        {
            base.Initialize();

            if (Circuit == null) { return; }

            // Only works in AC
            if (Circuit.Frequency != 0f)
            {
                var l1 = 0.1;
                var l2 = l1 * N * N;
                var k = 0.999;
                var m = k * l1 * N;

                var w = 2f * Math.PI * Circuit.Frequency;
                var wL1 = w * l1;
                var wL2 = w * l2;
                var wM = w * m;

                Circuit.Solver.AddTransformer(_vA, _vB, _vC, _vD, wL1, wL2, wM, out _i1, out _i2);
            }
        }

        public override void Deinitialize()
        {
            base.Deinitialize();

            Circuit?.Solver.RemoveUnknown(_i1);
            Circuit?.Solver.RemoveUnknown(_i2);

            // TODO: Add MNASolver.RemoveTransformer() method so we don't have to force the entire circuit to re-initialize
            Circuit?.Invalidate();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            // Do nothing for DC
            if (Circuit.Frequency == 0f) { return; }

            var vA = Circuit.Solver.GetValueOrDefault(_vA);
            var vB = Circuit.Solver.GetValueOrDefault(_vB);
            var vC = Circuit.Solver.GetValueOrDefault(_vC);
            var vD = Circuit.Solver.GetValueOrDefault(_vD);

            PrimaryVoltage = vA - vB;
            SecondaryVoltage = vC - vD;

            PrimaryCurrent = Circuit.Solver.GetValueOrDefault(_i1);
            SecondaryCurrent = Circuit.Solver.GetValueOrDefault(_i2);
        }
    }
}
