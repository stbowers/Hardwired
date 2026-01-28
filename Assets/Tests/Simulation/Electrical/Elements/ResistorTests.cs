#nullable enable

using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Tests.Utility;
using Hardwired.Utility;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Simulation.Electrical.Elements
{
    public class ResistorTests
    {
        [Test]
        public void VoltageIsProportionalToCurrent(
            [Values(0.1, 1.0, 100.0)]
            double r,
            [Values(5.0, 12.0, 100.0, 1000.0)]
            double i
            )
        {
            // V = I * R
            var vExpected = i * r;

            var circuit = new Circuit();

            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());

            var iSource = new CurrentSource(circuit, null, nodeA) { Current = i };
            var resistor = new Resistor(circuit, nodeA, null) { Resistance = r };

            circuit.ProcessTick();

            ComplexAssert.AreEqual(vExpected, (resistor as IDipoleCircuitElement).VoltageDelta);
        }

        [Test]
        public void CurrentIsProportionalToVoltage(
            [Values(0.1, 1.0, 100.0)]
            double r,
            [Values(5.0, 12.0, 100.0, 1000.0)]
            double v
            )
        {
            // I = V / R
            var iExpected = v / r;

            var circuit = new Circuit();

            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());

            var vSource = new VoltageSource(circuit, null, nodeA) { VoltageDelta = v };
            var resistor = new Resistor(circuit, nodeA, null) { Resistance = r };

            circuit.ProcessTick();

            ComplexAssert.AreEqual(iExpected, resistor.Current);
        }
    }
}

