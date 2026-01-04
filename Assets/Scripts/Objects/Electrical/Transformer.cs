#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Hardwired.Utility;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Transformer : ElectricalComponent
    {
        public int PinC = -1;

        public int PinD = -1;

        public Connection? ConnectionC => GetConnection(PinA);

        public Connection? ConnectionD => GetConnection(PinB);

        public double N;

        [HideInInspector]
        public Complex VoltageA;

        [HideInInspector]
        public Complex VoltageB;

        [HideInInspector]
        public Complex CurrentA;

        [HideInInspector]
        public Complex CurrentB;

        private int _v;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"N: {N}");
            stringBuilder.AppendLine($"Voltage A: {VoltageA.Magnitude.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Voltage B: {VoltageB.Magnitude.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Current A: {CurrentA.Magnitude.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Current B: {CurrentB.Magnitude.ToStringPrefix("A", "yellow")}");
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
                _v = solver.AddTransformer(a, b, c, d, N);
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

            VoltageA = vA - vB;
            VoltageB = vC - vD;

            CurrentA = solver.GetCurrent(_v);
            CurrentB = solver.GetCurrent(_v + 1);
        }
    }
}
