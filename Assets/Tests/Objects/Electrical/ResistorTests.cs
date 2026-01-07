#nullable enable

using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Tests.Utility;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Objects.Electrical
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

            ComplexAssert.AreEqual(vExpected, resistor.Voltage);
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

            var gameObject = new GameObject();
            var vSource = gameObject.AddComponent<VoltageSource>();
            var resistor = gameObject.AddComponent<Resistor>();

            vSource.Voltage = v;
            vSource.PinA = -1;
            vSource.PinB = 0;

            resistor.Resistance = r;
            resistor.PinA = 0;
            resistor.PinB = -1;

            circuit.AddComponent(vSource);
            circuit.AddComponent(resistor);

            circuit.ProcessTick();

            ComplexAssert.AreEqual(iExpected, resistor.Current);
        }
    }
}

