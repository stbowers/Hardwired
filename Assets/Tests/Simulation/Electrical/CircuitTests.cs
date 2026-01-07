#nullable enable

using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Tests.Utility;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Simulation.Electrical
{
    public class CircuitTests
    {
        /// <summary>
        /// Tests that a component added after the circuit was initialized will be correctly added to the circuit
        /// </summary>
        [Test]
        public void LateInitializedComponentIsCorrect1()
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var v1 = gameObject.AddComponent<VoltageSource>();
            var r1 = gameObject.AddComponent<Resistor>();
            var r2 = gameObject.AddComponent<Resistor>();

            v1.Voltage = 24;
            v1.PinA = -1;
            v1.PinB = 0;

            r1.Resistance = 100;
            r1.PinA = 0;
            r1.PinB = 1;

            r2.Resistance = 100;
            r2.PinA = 1;
            r2.PinB = -1;

            // Add first 2 components
            circuit.AddComponent(v1);
            circuit.AddComponent(r1);

            // Process a tick to ensure circuit is initialized
            circuit.ProcessTick();

            // Check number of elements
            Assert.AreEqual(2, circuit.Components.Count);
            Assert.AreEqual(3, circuit.Solver.Unknowns.Count);

            // Add r2
            circuit.AddComponent(r2);

            // Check number of elements
            Assert.AreEqual(3, circuit.Components.Count);
            Assert.AreEqual(3, circuit.Solver.Unknowns.Count);

            // Process another tick, and verify circuit works as expected
            circuit.ProcessTick();

            ComplexAssert.AreEqual(-v1.Current, r1.Current);
            ComplexAssert.AreEqual(-v1.Current, r2.Current);
            ComplexAssert.AreEqual(r1.Voltage, r2.Voltage);
            ComplexAssert.AreEqual(v1.Voltage, r1.Voltage + r2.Voltage);
        }

        /// <summary>
        /// Tests that a component added after the circuit was initialized will be correctly added to the circuit
        /// </summary>
        [Test]
        public void LateInitializedComponentIsCorrect2()
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var v1 = gameObject.AddComponent<VoltageSource>();
            var r1 = gameObject.AddComponent<Resistor>();
            var r2 = gameObject.AddComponent<Resistor>();

            v1.Voltage = 24;
            v1.PinA = -1;
            v1.PinB = 0;

            r1.Resistance = 100;
            r1.PinA = 0;
            r1.PinB = 1;

            r2.Resistance = 100;
            r2.PinA = 1;
            r2.PinB = -1;

            // Add resistors
            circuit.AddComponent(r1);
            circuit.AddComponent(r2);

            // Process a tick to ensure circuit is initialized
            circuit.ProcessTick();

            // Check number of elements
            Assert.AreEqual(2, circuit.Components.Count);
            Assert.AreEqual(2, circuit.Solver.Unknowns.Count);

            // Add voltage source (which has an extra unknown)
            circuit.AddComponent(v1);

            // Process a tick to ensure circuit is initialized
            circuit.ProcessTick();

            // Check number of elements
            Assert.AreEqual(3, circuit.Components.Count);
            Assert.AreEqual(3, circuit.Solver.Unknowns.Count);

            // Process another tick, and verify circuit works as expected
            circuit.ProcessTick();

            ComplexAssert.AreEqual(-v1.Current, r1.Current);
            ComplexAssert.AreEqual(-v1.Current, r2.Current);
            ComplexAssert.AreEqual(r1.Voltage, r2.Voltage);
            ComplexAssert.AreEqual(v1.Voltage, r1.Voltage + r2.Voltage);
        }

        [Test]
        public void RemovedComponentsAreCleanedUp()
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var v1 = gameObject.AddComponent<VoltageSource>();
            var r1 = gameObject.AddComponent<Resistor>();
            var r2 = gameObject.AddComponent<Resistor>();

            v1.Voltage = 24;
            v1.PinA = -1;
            v1.PinB = 0;

            r1.Resistance = 100;
            r1.PinA = 0;
            r1.PinB = 1;

            r2.Resistance = 100;
            r2.PinA = 1;
            r2.PinB = -1;

            circuit.AddComponent(v1);
            circuit.AddComponent(r1);
            circuit.AddComponent(r2);

            // Process a tick to ensure circuit is initialized
            circuit.ProcessTick();

            // Check number of elements
            Assert.AreEqual(3, circuit.Components.Count);
            Assert.AreEqual(3, circuit.Solver.Unknowns.Count);

            // Remove voltage source; should also remove the current source
            circuit.RemoveComponent(v1);
            Assert.AreEqual(2, circuit.Components.Count);
            Assert.AreEqual(2, circuit.Solver.Unknowns.Count);

            // Remove r2; should not remove any nodes, since node 1 is still referenced by r1
            circuit.RemoveComponent(r2);
            Assert.AreEqual(1, circuit.Components.Count);
            Assert.AreEqual(2, circuit.Solver.Unknowns.Count);

            // Remove r1; should also remove last 2 nodes
            circuit.RemoveComponent(r1);
            Assert.AreEqual(0, circuit.Components.Count);
            Assert.AreEqual(0, circuit.Solver.Unknowns.Count);
        }

        [Test]
        public void CanSolveTestCircuit1()
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

            ComplexAssert.AreEqual(-v1.Current, r1.Current);
        }
    }
}

