#nullable enable

using System;
using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Tests.Utility;
using Hardwired.Utility;
using MathNet.Numerics;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Simulation.Electrical.Elements
{
//    public class PowerSinkTests
//    {
//        /// <summary>
//        /// Tests a power source with target power draw of 100 W and internal resistance of 100 ohms connected to a voltage source.
//        /// </summary>
//        /// <param name="v"></param>
//        /// <param name="pExpected"></param>
//        [Test]
//        // Voltage less than V_min => no power (V_min = 50 V)
//        [TestCase(0f, 0f)]
//        [TestCase(10f, 0f)]
//        [TestCase(20f, 0f)]
//        [TestCase(30f, 0f)]
//        [TestCase(40f, 0f)]
//        // Voltage between V_min and V_nom => power based on resistance (100 ohms) (V_nom = 100 V)
//        // P = I * V = V^2 / R
//        [TestCase(50f, 25f)]
//        // Voltage between V_nom and V_max => power draw should be constant full power (100 W) (V_max = 200 V)
//        [TestCase(100f, 100f)]
//        [TestCase(125f, 100f)]
//        [TestCase(150f, 100f)]
//        [TestCase(175f, 100f)]
//        [TestCase(199f, 100f)]
//        // Voltage above V_max => no power (V_max = 200 V)
//        [TestCase(210f, 0f)]
//        [TestCase(500f, 0f)]
//        public void DrawsExpectedPower(double v, double pExpected)
//        {
//            var circuit = new Circuit();
//
//            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());
//
//            // Provides ~160 V max; 320 W max (@ ~ 80 V)
//            var vSource = new VoltageSource(circuit, nodeA, null) { SourceVoltage = v };
//
//            // max current = 1 A; resistance = 200 ohm
//            var pSink = new PowerSink(circuit, nodeA, null) { PowerTarget = 100, Profile = new() { VoltageMin = 50, VoltageNominal = 100, VoltageMax = 200 }};
//
//            circuit.ProcessTick();
//
//            Assert.AreEqual(pExpected, pSink.PowerAvailable, 0.01);
//        }
//
//        [Test]
//        public void DrawsRequestedPowerWhenAvailable()
//        {
//            var circuit = new Circuit();
//
//            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());
//
//            // Provides ~160 V max; 320 W max (@ ~ 80 V)
//            var iSource = new NortonEquivalent(circuit, nodeA, null) { CurrentShort = 8, Resistance = 20 };
//            var pSink = new PowerSink(circuit, nodeA, null) { PowerTarget = 100 };
//
//            // Check power draw over several ticks
//            for (int i = 0; i < 100; i++)
//            {
//                circuit.ProcessTick();
//
//                // Power provided by the source should always be exactly opposite power drawn by the sink
//                ComplexAssert.AreEqual(-iSource.Power, pSink.Power, 0.01);
//
//                // Energy output by the sink should always exactly match the expected energy given the power target
//                Assert.AreEqual(pSink.PowerTarget, pSink.PowerAvailable, 0.01);
//            }
//        }
//
//        [Test]
//        public void DrawsPowerFromPowerSource()
//        {
//            var circuit = new Circuit();
//
//            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());
//
//            var pSource = new PowerSource(circuit, nodeA, null) { VoltageNominal = 200, Frequency = 50, PowerAvailable = 500 };
//            var pSink = new PowerSink(circuit, nodeA, null) { PowerTarget = 100 };
//
//            // Check power draw over several ticks
//            for (int i = 0; i < 100; i++)
//            {
//                circuit.ProcessTick();
//
//                // Hardwired.LogDebug($"V = {pSource.VoltageDelta}");
//
//                // Power provided by the source should always exactly equal power drawn by the sink
//                ComplexAssert.AreEqual(-pSource.Power, pSink.Power, 0.01);
//
//                // Energy output by the sink should always exactly match the expected energy given the power target
//                Assert.AreEqual(pSink.PowerTarget, pSink.PowerAvailable, 0.01);
//            }
//        }
//
//        /// <summary>
//        /// This is testing a specific bug I ran in to - the bug ended up having more to do with cleaning up components from the circuit than with the power source/sink itself,
//        /// but it wasn't worth rewriting a more representative test in CircuitTests.cs...
//        /// </summary>
//        [Test]
//        public void RemoveFromCircuitWorks()
//        {
//            var circuit = new Circuit();
//
//            var nodeA = RefCounted.Create(circuit.Solver.AddUnknown());
//
//            var pSource = new PowerSource(circuit, nodeA, null) { VoltageNominal = 200, Frequency = 50, PowerAvailable = 800 };
//            var pSink = new PowerSink(circuit, nodeA, null) { PowerTarget = 100, Profile = new() { VoltageMin = 100, VoltageNominal = 200, VoltageMax = 800, MinimumPowerDrawRatio = 1.0}};
//
//            // Allow a few ticks to settle
//            for (int i = 0; i < 20; i++)
//            {
//                circuit.ProcessTick();
//            }
//
//            // Remove power source
//            // circuit.RemoveComponent(pSource);
//            pSource.Dispose();
//
//            // Check power draw over several ticks
//            for (int i = 0; i < 100; i++)
//            {
//                circuit.ProcessTick();
//
//                Assert.AreEqual(0, pSink.PowerAvailable, 0.1);
//            }
//        }
//    }
}

