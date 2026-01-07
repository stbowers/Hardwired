#nullable enable

using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Tests.Utility;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Objects.Electrical
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

            var gameObject = new GameObject();
            var iSource = gameObject.AddComponent<CurrentSource>();
            var resistor = gameObject.AddComponent<Resistor>();

            iSource.Current = i;
            iSource.PinA = -1;
            iSource.PinB = 0;

            resistor.Resistance = r;
            resistor.PinA = 0;
            resistor.PinB = -1;

            circuit.AddComponent(iSource);
            circuit.AddComponent(resistor);

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

            var gameObject = new GameObject();
            var iSource = gameObject.AddComponent<CurrentSource>();
            var iSink = gameObject.AddComponent<CurrentSource>();

            iSource.Current = i;
            iSource.PinA = -1;
            iSource.PinB = 0;
            iSource.InternalResistance = r;

            iSink.PinA = 0;
            iSink.PinB = -1;

            circuit.AddComponent(iSource);
            circuit.AddComponent(iSink);

            // No load
            iSink.Current = 0;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(vMax, iSource.Voltage);
            ComplexAssert.AreEqual(0f, iSource.PowerDraw);

            // 1/2 * V_max voltage drop (max power)
            iSink.Current = i / 2f;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(vMax / 2.0f, iSource.Voltage);
            ComplexAssert.AreEqual(pMax, iSource.PowerDraw);

            // 0.51 * V_max - should be less than max power
            iSink.Current = 0.49f * i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0.51f * vMax, iSource.Voltage);
            Assert.Less(iSource.PowerDraw, pMax);

            // 0.49 * V_max - should be less than max power
            iSink.Current = 0.51f * i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0.49f * vMax, iSource.Voltage);
            Assert.Less(iSource.PowerDraw, pMax);

            // Max current, 0 V, no power
            iSink.Current = i;
            circuit.ProcessTick();
            ComplexAssert.AreEqual(0f, iSource.Voltage);
            ComplexAssert.AreEqual(0f, iSource.PowerDraw);
        }

        [Test]
        public void CurrentFlowsFromAToB(
            [Values(-10.0, 0.0, 10.0)]
            double i
            )
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var iSource = gameObject.AddComponent<CurrentSource>();
            var resistor = gameObject.AddComponent<Resistor>();

            iSource.Current = i;
            iSource.PinA = -1;
            iSource.PinB = 0;

            resistor.Resistance = 100;
            resistor.PinA = 0;
            resistor.PinB = -1;

            circuit.AddComponent(iSource);
            circuit.AddComponent(resistor);

            circuit.ProcessTick();

            ComplexAssert.AreEqual(i, resistor.Current);
        }
    }
}

