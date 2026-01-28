#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.Simulation.Electrical.Elements
{
    public class Switch : ICircuitElement, IDipoleCircuitElement
    {
        public Circuit Circuit { get; }

        public Resistor Resistor { get; }

        public bool Closed { get; set; }

        public RefCounted<MNASolver.Unknown>? NodeA => Resistor.NodeA;

        public RefCounted<MNASolver.Unknown>? NodeB => Resistor.NodeB;

        public Complex Current => Resistor.Current;

        public Switch(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB)
        {
            Circuit = circuit;

            Resistor = new(circuit, nodeA, nodeB);
        }

        public void Dispose()
        {
            Resistor.Dispose();
        }

        public void UpdateState()
        {
            Resistor.Resistance = Closed ? Resistor.R_SHORT : Resistor.R_OPEN;
            Resistor.UpdateState();
        }

        public void ApplyState()
        {
            Resistor.ApplyState();
        }
    }
}
