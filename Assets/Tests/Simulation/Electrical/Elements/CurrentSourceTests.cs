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
    public class CurrentSourceTests
    {
        [Test]
        public void IdealCurrentSourceProvidesConstantCurrent(
            [NUnit.Framework.Range(-10.0, 10.0, 1.0)]
            double i,
            [NUnit.Framework.Range(1.0, 100.0, 20.0)]
            double r
            )
        {
            var circuit = new Circuit();

            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());

            var iSource = new CurrentSource(circuit, nodeA, null) { SourceCurrent = i };
            var resistor = new Resistor(circuit, nodeA, null) { Resistance = r };

            circuit.ProcessTick();

            ComplexAssert.AreEqual(i, resistor.Current);
        }

        [Test]
        public void NonIdealCurrentSourcePowerCurve(
            [NUnit.Framework.Range(-9.0, 9.0, 2.0)]
            double i,
            [NUnit.Framework.Range(1.0, 100.0, 20.0)]
            double r
            )
        {
            // ---- Calculate expected outputs
            // Voltage at no load; all current going through resistor
            var vMax = i * r;
            // Peak power output
            var pMax = i * vMax / 4f;

            // ---- Set up circuit
            var circuit = new Circuit();

            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());

            var iSource = new NortonEquivalent(circuit, nodeA, null) { CurrentShort = i, Resistance = r };
            var iSink = new CurrentSource(circuit, nodeA, null);

            // No load
            iSink.SourceCurrent = 0;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(vMax, iSource.VoltageDelta);
            ComplexAssert.AreEqual(0f, iSource.Power.Real);

            // 1/2 * V_max voltage drop (max power)
            iSink.SourceCurrent = -i / 2f;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(vMax / 2.0f, iSource.VoltageDelta);
            ComplexAssert.AreEqual(-pMax, iSource.Power.Real);

            // 0.51 * V_max - should be less than max power
            iSink.SourceCurrent = -0.49f * i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0.51f * vMax, iSource.VoltageDelta);
            Assert.Less(-iSource.Power.Real, pMax);

            // 0.49 * V_max - should be less than max power
            iSink.SourceCurrent = -0.51f * i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0.49f * vMax, iSource.VoltageDelta);
            Assert.Less(-iSource.Power.Real, pMax);

            // Max current, 0 V, no power
            iSink.SourceCurrent = -i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0f, iSource.VoltageDelta);
            ComplexAssert.AreEqual(0f, iSource.Power.Real);
        }

        [Test]
        public void CurrentFlowsFromAToB(
            [Values(-10.0, 0.0, 10.0)]
            double i
            )
        {
            var circuit = new Circuit();

            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());

            var iSource = new CurrentSource(circuit, nodeA, null) { SourceCurrent = i };
            var resistor = new Resistor(circuit, nodeA, null) { Resistance = 100 };

            circuit.ProcessTick();

            ComplexAssert.AreEqual(i, resistor.Current);
        }
    }
}

