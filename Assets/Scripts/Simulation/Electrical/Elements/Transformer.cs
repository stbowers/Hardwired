#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class Transformer : ICircuitElement
    {
        public Circuit Circuit { get; }

        public Transformer(Circuit circuit)
        {
            Circuit = circuit;
        }

        public void Dispose()
        {
        }

        public void UpdateState()
        {
            // // Only works in AC
            // if (Circuit.Frequency != 0f)
            // {
            //     var l1 = 10;
            //     var l2 = l1 * N * N;
            //     var k = 0.999;
            //     var m = k * l1 * N;

            //     var w = 2f * Math.PI * Circuit.Frequency;
            //     var wL1 = w * l1;
            //     var wL2 = w * l2;
            //     var wM = w * m;

            //     Circuit.Solver.AddTransformer(_vA, _vB, _vC, _vD, wL1, wL2, wM, ref _i1, ref _i2);
            // }
        }

        public void ApplyState()
        {
            // if (Circuit == null)
            // {
            //     PrimaryVoltage = 0f;
            //     SecondaryVoltage = 0f;
            //     PrimaryCurrent = 0f;
            //     SecondaryCurrent = 0f;

            //     return;
            // }

            // var vA = Circuit.Solver.GetValueOrDefault(_vA);
            // var vB = Circuit.Solver.GetValueOrDefault(_vB);
            // var vC = Circuit.Solver.GetValueOrDefault(_vC);
            // var vD = Circuit.Solver.GetValueOrDefault(_vD);

            // PrimaryVoltage = vA - vB;
            // SecondaryVoltage = vC - vD;

            // PrimaryCurrent = Circuit.Solver.GetValueOrDefault(_i1);
            // SecondaryCurrent = Circuit.Solver.GetValueOrDefault(_i2);
        }
    }
}
