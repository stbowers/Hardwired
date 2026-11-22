#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI.ImGuiUi;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility;
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
    public partial class HardwiredCable : ElectricalComponent
    {
        /// <summary>
        /// Gets the junction type
        /// </summary>
        public JunctionType JunctionType;

        public double Resistance;

        /// <summary>
        /// The specific heat capacity of this cable, in J/K (i.e. how much energy needs to be added to the cable in order to raise it's temperature by 1 K)
        /// </summary>
        public double SpecificHeat;

        /// <summary>
        /// How much power can this cable dissipate into the void, based on it's temperature (W/K).
        /// 
        /// Each tick, if PowerDissipated is greater than this, the temperature will increase.
        /// If PowerDissipated is lower, the temperature will decrease (to ~20 C)
        /// 
        /// This is a temporary system that approximates the power dissipation of a cable, but eventually I'd like to
        /// replace this with a more accurate calculation (in particular, cables should heat up the room they're in, and
        /// the temperature around them will determine how effectively they can convect heat)
        /// </summary>
        public double DissipationCapacity;

        /// <summary>
        /// The maximum voltage rating for this cable - represents the ability for the cable's insulation to prevent arcing between the wires.
        /// 
        /// If the voltage between any node and ground exceeds this value, the cable will have a random chance of burning up.
        /// </summary>
        public double MaximumVoltageRating;

        /// <summary>
        /// The maximum rated current this cable can sustain before it burns up.
        /// </summary>
        public double MaximumCurrentRating;

        /// <summary>
        /// The current temperature (K) of this segment of cable.
        /// Will slowly rise due to resitive heating
        /// </summary>
        public double Temperature { get; private set; } = 273.15;

        public double MaximumVoltage { get; private set; }

        public double Current { get; private set; }

        public double PowerDissapated { get; private set;}

        public bool IsJunction => JunctionType != JunctionType.None;

        public override CableNetwork? InputCableNetwork => Cable?.CableNetwork;

        public override CableNetwork? OutputCableNetwork => Cable?.CableNetwork;

        [NotNull]
        public override Cable? Cable => base.Cable ?? throw new InvalidOperationException("Invalid component setup - Could not find Cable component.");

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            bool altKey = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altKey)
            {
                stringBuilder.AppendLine($"Transfers electrical power between devices.");
                stringBuilder.AppendLine($"Each cable is modeled as a resistor, causing a voltage drop that increases with current flow.");
                stringBuilder.AppendLine($"Power dissipated as heat is given by P = I^2 · R.");
                stringBuilder.AppendLine($"Resistive heating raises the cable's temperature; if it exceeds 100 °C, the cable will burn up.");
                stringBuilder.AppendLine($"Each cable has a maximum rated current, limited by heating from resistive losses.");
                stringBuilder.AppendLine($"Each cable also has a maximum rated voltage to ground, representing insulation strength.");
                stringBuilder.AppendLine($"If the voltage to ground exceeds the rated value, the cable may fail due to an arc fault.");
                stringBuilder.AppendLine($"ΔV(avg) is the average voltage drop between the cable's nodes.");
                stringBuilder.AppendLine($"Vg(avg) is the average voltage to ground.");

                stringBuilder.AppendLine($"\n");
            }
            else
            {
                stringBuilder.AppendLine($"Press [alt] for description");
            }

            double tCelsius = Temperature - 273.15;

            stringBuilder.AppendLine($"Resistance: {Resistance.ToStringPrefix("Ω", "yellow")}");
            stringBuilder.AppendLine($"Current: {Current.ToStringPrefix("A", "yellow")} | I_max: {MaximumCurrentRating.ToStringPrefix("A", "yellow")}");
            stringBuilder.AppendLine($"Vg: {MaximumVoltage.ToStringPrefix("V", "yellow")} | Vg_max: {MaximumVoltageRating.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"Temperature: {tCelsius.ToStringPrefix("°C", "yellow")} | Dissapated: {PowerDissapated.ToStringPrefix("W", "yellow")}");
        }

        public override void DrawDebugOverlay()
        {
            if (JunctionType == JunctionType.Parallel)
            {
                for (int i = 0; i < Cable.OpenEnds.Count; i++)
                {
                    for (int j = i; j < Cable.OpenEnds.Count; j++)
                    {
                        var connectionA = Cable.OpenEnds[i];
                        var connectionB = Cable.OpenEnds[j];

                        var cableSpan = connectionA.GetCableSpan();
                        var endpoint = new CableSpan.Endpoint(this, connectionA);

                        var line1Node = cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line1);
                        var line2Node = cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line2);
                        var line3Node = cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line3);

                        if (line1Node != null)
                        {
                            DrawLineDebugOverlay(connectionA, CableSpan.ConductorType.Line1, connectionB, CableSpan.ConductorType.Line1);
                        }

                        if (line2Node != null)
                        {
                            DrawLineDebugOverlay(connectionA, CableSpan.ConductorType.Line2, connectionB, CableSpan.ConductorType.Line2);
                        }

                        if (line3Node != null)
                        {
                            DrawLineDebugOverlay(connectionA, CableSpan.ConductorType.Line3, connectionB, CableSpan.ConductorType.Line3);
                        }

                        DrawLineDebugOverlay(connectionA, CableSpan.ConductorType.Neutral, connectionB, CableSpan.ConductorType.Neutral);
                    }
                }
            }
            else if (JunctionType == JunctionType.Series)
            {
                // Inputs
                for (int i = 1; i < Cable.OpenEnds.Count - 1; i++)
                {
                    var connectionA = Cable.OpenEnds[i - 1];
                    var connectionB = Cable.OpenEnds[i];

                    DrawLineDebugOverlay(connectionA, CableSpan.ConductorType.Line1, connectionB, CableSpan.ConductorType.Neutral);
                }
                
                // Output
                var firstInput = Cable.OpenEnds[0];
                var lastInput = Cable.OpenEnds[Cable.OpenEnds.Count - 2];
                var output = Cable.OpenEnds[Cable.OpenEnds.Count - 1];

                DrawLineDebugOverlay(firstInput, CableSpan.ConductorType.Neutral, output, CableSpan.ConductorType.Neutral);
                DrawLineDebugOverlay(lastInput, CableSpan.ConductorType.Line1, output, CableSpan.ConductorType.Line1);
            }
        }

        private void DrawLineDebugOverlay(Connection a, CableSpan.ConductorType aType, Connection b, CableSpan.ConductorType bType)
        {
            Vector3 posA = a.Transform.position;
            Vector3 posB = b.Transform.position;

            Vector3 shiftA = CableSpan.GetLineDebugRenderOffset(aType);
            Vector3 shiftB = CableSpan.GetLineDebugRenderOffset(bType);

            ImGuiExtensions.Rendering.RenderingColor = CableSpan.GetLineDebugRenderColor(aType);

            Vector3 screenA = ImGuiExtensions.WorldToScreen(posA + shiftA);
            Vector3 screenB = ImGuiExtensions.WorldToScreen(posB + shiftB);
            ImGuiExtensions.Rendering.DrawClippedLine(screenA, screenB);
        }

        public void UpdateSpanNodes()
        {
            if (JunctionType == JunctionType.Parallel)
            {
                RefCounted<MNASolver.Unknown>? line1Node = null;
                RefCounted<MNASolver.Unknown>? line2Node = null;
                RefCounted<MNASolver.Unknown>? line3Node = null;
                RefCounted<MNASolver.Unknown>? neutralNode = null;

                foreach (var connection in Cable.OpenEnds)
                {
                    var cableSpan = connection.GetCableSpan();
                    var endpoint = new CableSpan.Endpoint(this, connection);

                    line1Node ??= cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line1);
                    line2Node ??= cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line2);
                    line3Node ??= cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Line3);
                    neutralNode ??= cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Neutral);
                }

                foreach (var connection in Cable.OpenEnds)
                {
                    var cableSpan = connection.GetCableSpan();
                    var endpoint = new CableSpan.Endpoint(this, connection);

                    if (line1Node != null)
                    {
                        cableSpan?.SetNode(line1Node, endpoint, CableSpan.ConductorType.Line1);
                    }

                    if (line2Node != null)
                    {
                        cableSpan?.SetNode(line2Node, endpoint, CableSpan.ConductorType.Line2);
                    }

                    if (line3Node != null)
                    {
                        cableSpan?.SetNode(line3Node, endpoint, CableSpan.ConductorType.Line3);
                    }

                    if (neutralNode != null)
                    {
                        cableSpan?.SetNode(neutralNode, endpoint, CableSpan.ConductorType.Neutral);
                    }
                }
            }
            else if (JunctionType == JunctionType.Series)
            {
                // Input connections
                RefCounted<MNASolver.Unknown>? firstNeutralNode = null;
                RefCounted<MNASolver.Unknown>? lastPositiveNode = null;
                for (int i = 0; i < Cable.OpenEnds.Count - 1; i++)
                {
                    var connection = Cable.OpenEnds[i];
                    var cableSpan = connection.GetCableSpan();
                    var endpoint = new CableSpan.Endpoint(this, connection);

                    if (i == 0)
                    {
                        firstNeutralNode = cableSpan?.GetNode(endpoint, CableSpan.ConductorType.Neutral);
                    }

                    if (lastPositiveNode != null)
                    {
                        cableSpan?.SetNode(lastPositiveNode, endpoint, CableSpan.ConductorType.Neutral);
                    }
                    
                    lastPositiveNode = cableSpan?.GetOrCreateNode(endpoint, CableSpan.ConductorType.Line1);
                }

                // Output connection
                var outputConnection = Cable.OpenEnds.Last();
                var outputCableSpan = outputConnection.GetCableSpan();
                var outputEndpoint = new CableSpan.Endpoint(this, outputConnection);

                if (firstNeutralNode != null)
                {
                    outputCableSpan?.SetNode(firstNeutralNode, outputEndpoint, CableSpan.ConductorType.Neutral);
                }

                if (lastPositiveNode != null)
                {
                    outputCableSpan?.SetNode(lastPositiveNode, outputEndpoint, CableSpan.ConductorType.Line1);
                }
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            PowerDissapated = 0;
            Current = 0;

            MaximumVoltage = 0;

            if (IsJunction)
            {
                // For junction cables, each connection point may correspond to a different cable span.
                foreach (var connection in Cable?.OpenEnds ?? Enumerable.Empty<Connection>())
                {
                    var cableSpan = connection.GetCableSpan();

                    double p = 0;
                    p += cableSpan?.Line1?.Resistor?.Power.Real ?? 0;
                    p += cableSpan?.Line2?.Resistor?.Power.Real ?? 0;
                    p += cableSpan?.Line3?.Resistor?.Power.Real ?? 0;
                    p += cableSpan?.NeutralLine?.Resistor?.Power.Real ?? 0;

                    // Normalize power dissapated so each individual segment only dissapates it's share of the power
                    var r = (Resistance / cableSpan?.Resistance) ?? 1;
                    p *= r;
                    PowerDissapated += p;

                    Current = Math.Max(Current, cableSpan?.Line1?.Resistor?.Current.Magnitude ?? 0);
                    Current = Math.Max(Current, cableSpan?.Line2?.Resistor?.Current.Magnitude ?? 0);
                    Current = Math.Max(Current, cableSpan?.Line3?.Resistor?.Current.Magnitude ?? 0);
                }
            }
            else
            {
                // For straight through cables, both ends should be in the same cable span
                var cableSpan = PowerInput?.GetCableSpan();

                PowerDissapated += cableSpan?.Line1?.Resistor?.Power.Real ?? 0;
                PowerDissapated += cableSpan?.Line2?.Resistor?.Power.Real ?? 0;
                PowerDissapated += cableSpan?.Line3?.Resistor?.Power.Real ?? 0;
                PowerDissapated += cableSpan?.NeutralLine?.Resistor?.Power.Real ?? 0;

                Current = Math.Max(Current, cableSpan?.Line1?.Resistor?.Current.Magnitude ?? 0);
                Current = Math.Max(Current, cableSpan?.Line2?.Resistor?.Current.Magnitude ?? 0);
                Current = Math.Max(Current, cableSpan?.Line3?.Resistor?.Current.Magnitude ?? 0);

                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line1?.Resistor?.VoltageA.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line1?.Resistor?.VoltageB.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line2?.Resistor?.VoltageA.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line2?.Resistor?.VoltageB.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line3?.Resistor?.VoltageA.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.Line3?.Resistor?.VoltageB.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.NeutralLine?.Resistor?.VoltageA.Magnitude ?? 0);
                MaximumVoltage = Math.Max(MaximumVoltage, cableSpan?.NeutralLine?.Resistor?.VoltageB.Magnitude ?? 0);

                // Normalize power dissapated so each individual segment only dissapates it's share of the power
                var r = (Resistance / cableSpan?.Resistance) ?? 1;
                PowerDissapated *= r;
            }

            // Calculate temperature change due to resistive heating
            // note - time delta is always 0.5s (constant tick time)
            double pRadiated = DissipationCapacity * (Temperature - 293.15f);
            double dE = (PowerDissapated - pRadiated) * 0.5;
            double dT = dE / SpecificHeat;

            // Cap max change in temperature per tick
            // NOTE: this is a workaround for a bug caused by the current implementation of the power sink... If a circuit is suddenly disconnected
            // from any other power sources, and a power sink's current source becomes the only source of voltage in the circuit, a wire may have
            // the full voltage of the power sink applied, so Voltage would be much higher than usual (i.e. ~100-200 V, when it's usually < 1 V, since
            // it's the voltage drop across the resistor), causing current/power to be much higher than it should be.
            // Eventually I'd like to replace the power sink with a non-linear solution that can exactly determine the voltage/current/power draw in
            // one tick, after which this shouldn't be an issue.
            dT = Math.Min(dT, 10f);

            // Update temperature
            Temperature += dT;

            // Randomly break cables that are over temp, with a higher chance the hotter they are
            // 100 C ~ 0%
            // 150 C ~ 100%
            double breakChance = (Temperature - 373.15) / 50f;
            breakChance = Math.Clamp(breakChance, 0f, 1f);

            bool shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
            if (shouldBreak)
            {
                Cable?.Break();
                Hardwired.LogDebug($"Burning cable (too hot/too much current!) -- i: {Current} | T: {Temperature}");
            }

            // If there is any current (above some floor value) in the cable, check if it's over voltage...
            // Note - we don't check for over-voltage in cables with no (or very little) current, because sometimes
            // when a circuit changes suddenly (such as a breaker opening/closing, a cable burning, etc), there may be
            // a transient voltage spike but with 0 or very little current, due to the way the circuit solver works...
            if (Current > 0.1)
            {
                // Randomly break cables that are over voltage, with a higher chance the further away from their design voltage they are
                // 100% of MaximumVoltageRating ~ 0%
                // 150% of MaximumVoltageRating ~ 100%
                var voltageCapacity = MaximumVoltage / MaximumVoltageRating;
                breakChance = (voltageCapacity - 1.0) / 0.5f;
                breakChance = Math.Clamp(breakChance, 0f, 1f);

                shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
                if (shouldBreak)
                {
                    Cable?.Break();
                    Hardwired.LogDebug($"Burning cable (too much voltage!) -- V: {MaximumVoltage} / {MaximumVoltageRating} (I = {Current} A)");
                }
            }

            // Check fuses
            var fuses = Cable?.AttachedDevices.OfType<CableFuse>() ?? Enumerable.Empty<CableFuse>();
            foreach (var fuse in fuses)
            {
                var cable = fuse.SmallCell.Cable;

                var vNominal = 1000f;
                var iLimit = fuse.PowerBreak / vNominal;

                if (Current > iLimit)
                {
                    fuse.Break();
                    Hardwired.LogDebug($"Breaking fuse -- i: {Current} | iLimit: {iLimit}, PowerBreak: {fuse.PowerBreak}");
                }
            }
        }
    }
}
