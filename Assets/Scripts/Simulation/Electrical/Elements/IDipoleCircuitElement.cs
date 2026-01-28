#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    public interface IDipoleCircuitElement : ICircuitElement
    {
        public RefCounted<MNASolver.Unknown>? NodeA { get; }

        public RefCounted<MNASolver.Unknown>? NodeB { get; }

        public Complex Current { get; }

        public Complex VoltageA => Circuit.Solver.GetValueOrDefault(NodeA?.Value);
        
        public Complex VoltageB => Circuit.Solver.GetValueOrDefault(NodeB?.Value);

        public Complex VoltageDelta => VoltageA - VoltageB;

        public Complex Power => VoltageDelta * Current.Conjugate();

        public double PowerFactor
        {
            get
            {
                var s = Power;
                return s.Real / s.Magnitude;
            }
        }

        string ICircuitElement.DebugInfo() => $"A: {NodeA?.Value.Index ?? -1}, B: {NodeB?.Value.Index ?? -1}";
    }
}
