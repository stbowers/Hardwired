#nullable enable

using System;
using System.Numerics;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Represents a segment of cable.
    /// In the circuit, this is just simulated as a simple resistor - this componenet however adds additional logic to track the temperature of the cable
    /// as power is dissipated through it via resistive heating, so we can melt cables when too much current goes through them.
    /// </summary>
    public class Line : Resistor
    {
        /// <summary>
        /// The volumetric specific heat capacity of the material this cable is made out of (J/cm^3/K)
        /// </summary>
        public double VolumetricSpecificHeat;

        /// <summary>
        /// The length of this segment of cable in cm.
        /// </summary>
        public double Length;

        /// <summary>
        /// The diameter of one "wire" in the cable in mm.
        /// (note that this model assumes there are two "wires" in each cable - a positive and negative; or hot and neutral... The resistance is only actually modeled in the circuit
        /// on the "positive" wire, and all devices share a common ground, so the resistance will be twice what would normally be calculated from one wire.)
        /// </summary>
        public double Diameter;

        /// <summary>
        /// The current temperature (K) of this segment of cable.
        /// Will slowly rise due to resitive heating
        /// </summary>
        [HideInInspector]
        public double Temperature;

        /// <summary>
        /// The volume of wire being modeled (based on diameter & length) in cm^3
        /// Used to calculate how fast the temperature should rise given the specific heat and power being dissipated.
        /// </summary>
        [HideInInspector]
        public double WireVolume;


        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Temperature: {Temperature.ToStringPrefix("°C", "yellow")}");
        }


        public override void Initialize()
        {
            // Calculate volume of wire in the cable (2 wires per cable)
            var dCm = Diameter / 10;
            var area = 0.25f * Math.PI * dCm * dCm;
            WireVolume = 2 * area * Length;

            base.Initialize();
        }

        public override void ApplyState()
        {
            base.ApplyState();

            if (Circuit == null) { return; }

            // Calculate temperature change due to resistive heating
            double dE = PowerDissipated * Circuit.TimeDelta;
            double dT = dE / VolumetricSpecificHeat * WireVolume;

            Temperature += dT;
        }

    }
}
