#nullable enable

using System;
using System.Numerics;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Tests.Utility;
using NUnit.Framework;
using UnityEngine;

namespace Hardwired.Tests.Objects.Electrical
{
    public class PowerSinkTests
    {
        /// <summary>
        /// Tests a power source with target power draw of 100 W and internal resistance of 100 ohms connected to a voltage source.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="pExpected"></param>
        [Test]
        // Voltage less than V_min => no power (V_min = 50 V)
        [TestCase(0f, 0f)]
        [TestCase(10f, 0f)]
        [TestCase(20f, 0f)]
        [TestCase(30f, 0f)]
        [TestCase(40f, 0f)]
        // Voltage between V_min and V_nom => power based on resistance (100 ohms) (V_nom = 100 V)
        // P = I * V = V^2 / R
        [TestCase(50f, 25f)]
        // Voltage between V_nom and V_max => power draw should be constant full power (100 W) (V_max = 200 V)
        [TestCase(100f, 100f)]
        [TestCase(125f, 100f)]
        [TestCase(150f, 100f)]
        [TestCase(175f, 100f)]
        [TestCase(199f, 100f)]
        // Voltage above V_max => no power (V_max = 200 V)
        [TestCase(210f, 0f)]
        [TestCase(500f, 0f)]
        public void DrawsExpectedPower(double v, double pExpected)
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var vSource = gameObject.AddComponent<VoltageSource>();
            var pSink = gameObject.AddComponent<PowerSink>();

            // Provides ~160 V max; 320 W max (@ ~ 80 V)
            vSource.Voltage = v;
            vSource.Frequency = 50;
            vSource.PinA = -1;
            vSource.PinB = 0;

            pSink.PowerTarget = 100;
            pSink.MaxPower = 100;
            pSink.VoltageMin = 50;
            pSink.VoltageNominal = 100;
            pSink.VoltageMax = 200;
            pSink.PinA = 0;
            pSink.PinB = -1;

            circuit.AddComponent(vSource);
            circuit.AddComponent(pSink);

            // Simulate several ticks to allow power draw to settle
            for (int i = 0; i < 50; i++)
            {
                circuit.ProcessTick();
            }

            Assert.AreEqual(pExpected, pSink.Power, 0.0001);
        }

        [Test]
        public void DrawsRequestedPowerWhenAvailable()
        {
            var circuit = new Circuit();

            var gameObject = new GameObject();
            var iSource = gameObject.AddComponent<CurrentSource>();
            var pSink = gameObject.AddComponent<PowerSink>();

            // Provides ~160 V max; 320 W max (@ ~ 80 V)
            iSource.Current = 8;
            iSource.Frequency = 50;
            iSource.PinA = -1;
            iSource.PinB = 0;
            iSource.InternalResistance = 20;

            pSink.PowerTarget = 100;
            pSink.MaxPower = 100;
            pSink.VoltageNominal = 100;
            pSink.PinA = 0;
            pSink.PinB = -1;

            circuit.AddComponent(iSource);
            circuit.AddComponent(pSink);

            // Check power draw over several ticks
            for (int i = 0; i < 100; i++)
            {
                circuit.ProcessTick();

                // Power provided by the source should always exactly equal power drawn by the sink
                ComplexAssert.AreEqual(iSource.PowerDraw, pSink.Power);

                // Energy output by the sink should always exactly match the expected energy given the power target
                Assert.AreEqual(pSink.EnergyInput, pSink.PowerTarget * circuit.TimeDelta);
            }
        }
    }
}

