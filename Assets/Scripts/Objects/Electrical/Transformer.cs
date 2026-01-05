#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Hardwired.Utility;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Transformer : ElectricalComponent
    {
        public int PinC = -1;

        public int PinD = -1;

        public Connection? ConnectionC => GetConnection(PinC);

        public Connection? ConnectionD => GetConnection(PinD);

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

        private int _v;

        protected override bool UsesConnection(int connection)
            => PinC == connection || PinD == connection || base.UsesConnection(connection);

        public override void ConnectCircuit()
        {
            base.ConnectCircuit();

            // Check for components connected to each pin
            var connectedC = GetConnectedComponent(ConnectionC);
            var connectedD = GetConnectedComponent(ConnectionD);

            // Merge connected circuits
            Circuit.Merge(Circuit, connectedC?.Circuit);
            Circuit.Merge(Circuit, connectedD?.Circuit);
        }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            var vP_rms = PrimaryVoltage.RootMeanSquare();
            var vS_rms = SecondaryVoltage.RootMeanSquare();
            var iP_rms = PrimaryCurrent.RootMeanSquare();
            var iS_rms = SecondaryCurrent.RootMeanSquare();
            var primaryPower = 0.5f * (PrimaryVoltage * PrimaryCurrent.Conjugate()).Real;
            var secondaryPower = 0.5f * (SecondaryVoltage * SecondaryCurrent.Conjugate()).Real;

            stringBuilder.AppendLine($"N: {N}");
            stringBuilder.AppendLine($"Primary Coil Voltage (RMS): {vP_rms.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Voltage (RMS): {vS_rms.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Primary Coil Current (RMS): {iP_rms.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Current (RMS): {iS_rms.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Primary Coil Power: {primaryPower.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Secondary Coil Power: {secondaryPower.ToStringPrefix("W", "yellow")}");
        }

        public override void InitializeSolver(MNASolver solver)
        {
            base.InitializeSolver(solver);

            int? a = GetNodeIndex(PinA);
            int? b = GetNodeIndex(PinB);
            int? c = GetNodeIndex(PinC);
            int? d = GetNodeIndex(PinD);


            if (solver.Frequency == 0f)
            {
                // Do nothing for DC
            }
            else
            {
                var l1 = 0.1;
                var l2 = l1 * N * N;
                var k = 0.999;
                var m = k * l1 * N;

                _v = solver.AddTransformer(a, b, c, d, l1, l2, m);
            }
        }

        public override void GetSolverOutputs(MNASolver solver)
        {
            base.GetSolverOutputs(solver);

            // Do nothing for DC
            if (solver.Frequency == 0f) { return; }

            int? a = GetNodeIndex(PinA);
            int? b = GetNodeIndex(PinB);
            int? c = GetNodeIndex(PinC);
            int? d = GetNodeIndex(PinD);

            var vA = solver.GetVoltage(a);
            var vB = solver.GetVoltage(b);
            var vC = solver.GetVoltage(c);
            var vD = solver.GetVoltage(d);

            PrimaryVoltage = vA - vB;
            SecondaryVoltage = vC - vD;

            PrimaryCurrent = solver.GetCurrent(_v);
            SecondaryCurrent = solver.GetCurrent(_v + 1);
        }
    }
}
