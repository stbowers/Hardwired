#nullable enable

using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests
{
    public class CircuitTests
    {
        [Test]
        public void CanSolveBasicCircuit()
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var v1 = gameObject.AddComponent<VoltageSource>();
            var r1 = gameObject.AddComponent<Resistor>();

            v1.Voltage = 24;
            v1.PinA = -1;
            v1.PinB = 0;

            r1.Resistance = 100;
            r1.PinA = 0;
            r1.PinB = -1;

            circuit.AddComponent(v1);
            circuit.AddComponent(r1);

            circuit.ProcessTick();

            Assert.AreEqual(v1.Current.Magnitude, r1.Current.Magnitude, 0.001);
        }
    }
}

