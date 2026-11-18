#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Hardwired.Objects;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility;

namespace Hardwired.Networks
{
    /// <summary>
    /// Represents a contiguous span of cables in a cable network.
    /// A straight run of cables without any breaks, junctions, etc, can be treated as a single cable for calculations.
    /// </summary>
    public class CableSpan
    {
        /// <summary>
        /// Identifies a wire type in a connection, which can be used to get different nodes for different "wires" in a cable
        /// </summary>
        public enum ConductorType
        {
            /// <summary>
            /// The positive (DC) or "live" (AC) wire.
            /// </summary>
            Line1,

            /// <summary>
            /// The "live" wire for phase 2 for multi-phase AC cables (not used for DC or single phase AC)
            /// </summary>
            Line2,

            /// <summary>
            /// The "live" wire for phase 3 for multi-phase AC cables (not used for DC or single phase AC)
            /// </summary>
            Line3,

            /// <summary>
            /// The negative/common (DC) or neutral (AC) wire.
            /// 
            /// This is usually not used explicitly, because the common pin does not need to be referenced in most cases
            /// </summary>
            Neutral,
        }

        /// <summary>
        /// Represents a "conductor" or individual wire within a cable span.
        /// Each conductor is modeled as a resistor between two nodes.
        /// </summary>
        public class Conductor
        {
            public RefCounted<MNASolver.Unknown> NodeA { get; }

            public RefCounted<MNASolver.Unknown> NodeB { get; }

            public Resistor Resistor { get; }

            public Conductor(Circuit circuit)
            {
                NodeA = RefCounted.Create(circuit.Solver.AddUnknown());
                NodeB = RefCounted.Create(circuit.Solver.AddUnknown());
                Resistor = new Resistor(circuit, NodeA, NodeB);
            }
        }

        private List<HardwiredCable> _cables = new();
        private static int _nextId;

        public int Id { get; private set; } = Interlocked.Add(ref _nextId, 1);

        public IReadOnlyList<HardwiredCable> Cables => _cables.AsReadOnly();

        public Conductor? Line1 { get; private set; }

        public Conductor? Line2 { get; private set; }

        public Conductor? Line3 { get; private set; }

        public Conductor? NeutralLine { get; private set; }

        /// <summary>
        /// Gets the resistance, in ohms, of a single conductor in this cable span (assumes all conductors are the same)
        /// </summary>
        public double Resistance { get; private set; }

        /// <summary>
        /// Gets the connection that represents "side A" of this span, or null if not yet initialized.
        /// 
        /// This connection may either be the termination of a straight section of cable, potentially attaching to a device, or
        /// the endpoint of a cable junction attached to the end of this span.
        /// </summary>
        public Connection? ConnectionA { get; private set; }

        /// <summary>
        /// Gets the connection that represents "side B" of this span, or null if not yet initialized.
        /// 
        /// This connection may either be the termination of a straight section of cable, potentially attaching to a device, or
        /// the endpoint of a cable junction attached to the end of this span.
        /// </summary>
        public Connection? ConnectionB { get; private set; }

        public Circuit Circuit { get; private set; }

        public RefCounted<MNASolver.Unknown>? CommonNode { get; private set; }

        public CableSpan(Circuit circuit)
        {
            Circuit = circuit;
        }

        public void UpdateState()
        {
            if (Line1 != null)
            {
                Line1.Resistor.Resistance = Resistance;
                Line1.Resistor.UpdateState();
            }

            if (Line2 != null)
            {
                Line2.Resistor.Resistance = Resistance;
                Line2.Resistor.UpdateState();
            }

            if (Line3 != null)
            {
                Line3.Resistor.Resistance = Resistance;
                Line3.Resistor.UpdateState();
            }

            if (NeutralLine != null)
            {
                NeutralLine.Resistor.Resistance = Resistance;
                NeutralLine.Resistor.UpdateState();
            }
        }

        public void ApplyState()
        {
            Line1?.Resistor.ApplyState();
            Line2?.Resistor.ApplyState();
            Line3?.Resistor.ApplyState();
            NeutralLine?.Resistor.ApplyState();
        }

        public void Clear()
        {
            _cables.Clear();
            ConnectionA = null;
            ConnectionB = null;
            Resistance = 0;
        }

        public void AddCable(HardwiredCable cable, Connection connectionA, Connection connectionB)
        {
            _cables.Add(cable);

            Resistance += cable.Resistance;

            UpdateConnections(connectionA, connectionB);
        }

        public void AddSpan(CableSpan other)
        {
            if (this == other) { return; }

            _cables.AddRange(other.Cables);
            Resistance += other.Resistance;

            foreach (var cable in other.Cables)
            {
                if (cable.IsJunction)
                {
                    foreach (var connection in cable.Cable?.OpenEnds ?? Enumerable.Empty<Connection>())
                    {
                        if (connection.GetCableSpan() == other)
                        {
                            connection.SetCableSpan(this);
                        }
                    }
                }
                else
                {
                    var connectionA = cable.Cable?.OpenEnds[0];
                    var connectionB = cable.Cable?.OpenEnds[1];
                    connectionA?.SetCableSpan(this);
                    connectionB?.SetCableSpan(this);
                }
            }

            // Update the endpoints of this span given the endpoints of the other span
            if (other.ConnectionA != null && other.ConnectionB != null)
            {
                UpdateConnections(other.ConnectionA, other.ConnectionB);
            }
        }

        /// <summary>
        /// Updates the connections that mark the endpoints of this span (ConnectionA and ConnectionB) assuming another cable (or span) was
        /// just added with the given endpoints `a` and `b`.
        /// If either `a` or `b` matches an existing end for this span, that end will be replaced with the opposite
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private void UpdateConnections(Connection a, Connection b)
        {
            a.SetCableSpan(this);
            b.SetCableSpan(this);

            // If endpoints are uninitialized, set them to the endpoints of this section
            if (ConnectionA == null || ConnectionB == null)
            {
                ConnectionA = a;
                ConnectionB = b;
                return;
            }

            // Otherwise, determine if either 'a' or 'b' match either existing endpoint of this span,
            // and if so update that endpoint to the opposite of whichever one matched.

            var peerA = a.GetPeer();
            var peerB = b.GetPeer();

            if (ConnectionA == peerA)
            {
                ConnectionA = b;
            }
            else if (ConnectionA == peerB)
            {
                ConnectionA = a;
            }
            else if (ConnectionB == peerA)
            {
                ConnectionB = b;
            }
            else if (ConnectionB == peerB)
            {
                ConnectionB = a;
            }
        }

        /// <summary>
        /// Get the solver node for the given connection to this span for the given conductor.
        /// Creates the conductor/node if it does not yet exist.
        /// 
        /// May return `null` (common ground) for the neutral connector, if the neutral line is not being simulated.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="conductorType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public RefCounted<MNASolver.Unknown>? GetNode(Connection connection, ConductorType conductorType)
        {
            // TODO: Allow actually simulating the neutral line, instead of always returning null
            if (conductorType == ConductorType.Neutral)
            {
                return null;
            }

            var conductor = GetConductor(conductorType);

            var peer = connection.GetPeer();

            if (connection == ConnectionA || peer == ConnectionA)
            {
                return conductor.NodeA;
            }
            else if (connection == ConnectionB || peer == ConnectionB)
            {
                return conductor.NodeB;
            }
            else
            {
                throw new InvalidOperationException($"The provided connection does not attach to this cable span.");
            }
        }

        private Conductor GetConductor(ConductorType conductorType)
        {
            return conductorType switch
            {
                ConductorType.Line1 => Line1 ??= new(Circuit),
                ConductorType.Line2 => Line2 ??= new(Circuit),
                ConductorType.Line3 => Line3 ??= new(Circuit),
                _ => NeutralLine ??= new(Circuit),
            };
        }
    }
}