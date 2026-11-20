#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Hardwired.Objects;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility;
using UnityEngine.Assertions.Must;

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
        public class Conductor : IDisposable
        {
            public RefCounted<MNASolver.Unknown>? NodeA { get; set; }

            public RefCounted<MNASolver.Unknown>? NodeB { get; set; }

            public Resistor? Resistor { get; set; }

            public void Dispose()
            {
                NodeA?.Dispose();
                NodeB?.Dispose();
                Resistor?.Dispose();
            }
        }

        /// <summary>
        /// Represents an endpoint of a cable span.
        /// Can either be a single connection, representing a endpoint of a straight section of cable,
        /// or a HardwiredCable and Connection pair, representing that endpoint connecting to a junction.
        /// 
        /// Keeping a reference to the actual junction cable in the case of a junction endpoint
        /// allows us to differentiate from the "open" end of a junction and the actual "internal" end
        /// of the junction in the case of a span containing only a junction.
        /// 
        /// Keeping a reference to the connection for junction endpoints is also required, in order to
        /// differentiate the ends of a cable span that looped back on itself, connecting to a junction in
        /// two or more places.
        /// </summary>
        public class Endpoint
        {
            public Connection? Connection { get; }

            public HardwiredCable? Junction { get; }

            public Endpoint(Connection connection)
            {
                Connection = connection;
            }

            public Endpoint(HardwiredCable junction, Connection connection)
            {
                Connection = connection;
                Junction = junction;
            }

            public override string ToString()
                => Connection != null ? $"Connection({Connection.GetHashCode()})" : $"Junction({Junction?.GetHashCode()})";

            public override bool Equals(object? obj)
                => obj is Endpoint other
                && Connection == other.Connection
                && Junction == other.Junction;

            public override int GetHashCode()
                => HashCode.Combine(Connection?.GetHashCode(), Junction?.GetHashCode());

            [return: NotNullIfNotNull("connection")]
            public static implicit operator Endpoint?(Connection? connection) => connection != null ? new(connection) : null;

            public static bool operator ==(Endpoint? a, Endpoint? b)
                => a?.Equals(b) ?? ReferenceEquals(b, null);

            public static bool operator !=(Endpoint? a, Endpoint? b)
                => !a?.Equals(b) ?? !ReferenceEquals(b, null);
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
        public Endpoint? EndpointA { get; private set; }

        /// <summary>
        /// Gets the connection that represents "side B" of this span, or null if not yet initialized.
        /// 
        /// This connection may either be the termination of a straight section of cable, potentially attaching to a device, or
        /// the endpoint of a cable junction attached to the end of this span.
        /// </summary>
        public Endpoint? EndpointB { get; private set; }

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
                UpdateConductor(Line1);
            }

            if (Line2 != null)
            {
                UpdateConductor(Line2);
            }

            if (Line3 != null)
            {
                UpdateConductor(Line3);
            }

            if (NeutralLine != null)
            {
                UpdateConductor(NeutralLine);
            }
        }

        private void UpdateConductor(Conductor conductor)
        {
            // Ensure both nodes are allocated
            conductor.NodeA ??= new(Circuit.Solver.AddUnknown("CableSpan::UpdateConductor"));
            conductor.NodeB ??= new(Circuit.Solver.AddUnknown("CableSpan::UpdateConductor"));

            // Ensure resistor is allocated
            conductor.Resistor ??= new Resistor(Circuit, conductor.NodeA, conductor.NodeB);

            // Update resistor
            conductor.Resistor.Resistance = Resistance;
            conductor.Resistor.UpdateState();
        }

        public void ApplyState()
        {
            Line1?.Resistor?.ApplyState();
            Line2?.Resistor?.ApplyState();
            Line3?.Resistor?.ApplyState();
            NeutralLine?.Resistor?.ApplyState();
        }

        public void Clear()
        {
            _cables.Clear();
            EndpointA = null;
            EndpointB = null;
            Resistance = 0;

            // Clean up conductors
            Line1?.Dispose();
            Line1 = null;

            Line2?.Dispose();
            Line2 = null;

            Line3?.Dispose();
            Line3 = null;

            NeutralLine?.Dispose();
            NeutralLine = null;

            CommonNode?.Dispose();
            CommonNode = null;
        }

        public void AddCable(HardwiredCable cable, Endpoint endpointA, Endpoint endpointB)
        {
            _cables.Add(cable);

            Resistance += cable.Resistance;

            UpdateEndpoints(endpointA, endpointB);
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
            if (other.EndpointA != null && other.EndpointB != null)
            {
                UpdateEndpoints(other.EndpointA, other.EndpointB);
            }
        }

        /// <summary>
        /// Updates the connections that mark the endpoints of this span (ConnectionA and ConnectionB) assuming another cable (or span) was
        /// just added with the given endpoints `a` and `b`.
        /// If either `a` or `b` matches an existing end for this span, that end will be replaced with the opposite
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private void UpdateEndpoints(Endpoint a, Endpoint b)
        {
            a.Connection?.SetCableSpan(this);
            b.Connection?.SetCableSpan(this);

            // If endpoints are uninitialized, set them to the endpoints of this section
            if (EndpointA == null || EndpointB == null)
            {
                EndpointA = a;
                EndpointB = b;
                return;
            }

            // Otherwise, determine if either 'a' or 'b' match either existing endpoint of this span,
            // and if so update that endpoint to the opposite of whichever one matched.

            var peerA = a.Connection?.GetPeer();
            var peerB = b.Connection?.GetPeer();

            if (EndpointA == peerA)
            {
                EndpointA = b;
            }
            else if (EndpointA == peerB)
            {
                EndpointA = a;
            }
            else if (EndpointB == peerA)
            {
                EndpointB = b;
            }
            else if (EndpointB == peerB)
            {
                EndpointB = a;
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
        public RefCounted<MNASolver.Unknown>? GetNode(Endpoint endpoint, ConductorType conductorType)
        {
            // If getting the "neutral" line, but no neutral line has actually been set,
            // return the "common node"
            if (conductorType == ConductorType.Neutral && NeutralLine == null)
            {
                return CommonNode;
            }

            var conductor = GetConductor(conductorType);

            var peer = endpoint.Connection?.GetPeer();

            if (endpoint == EndpointA || peer == EndpointA)
            {
                return conductor.NodeA;
            }
            else if (endpoint == EndpointB || peer == EndpointB)
            {
                return conductor.NodeB;
            }
            else
            {
                throw new InvalidOperationException($"The provided connection does not attach to this cable span. ({Circuit.Id}, {endpoint})");
            }
        }

        public RefCounted<MNASolver.Unknown>? GetOrCreateNode(Endpoint endpoint, ConductorType conductorType)
        {
            var node = GetNode(endpoint, conductorType);

            // Always create non-neutral nodes if they're not already created
            if (node == null && conductorType != ConductorType.Neutral)
            {
                node = new(Circuit.Solver.AddUnknown("CableSpan::GetOrCreateNode"));
                SetNode(node, endpoint, conductorType);
            }

            return node;
        }

        /// <summary>
        /// Sets the node used for the given side of the given conductor.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="connection"></param>
        /// <param name="conductorType"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SetNode(RefCounted<MNASolver.Unknown> node, Endpoint endpoint, ConductorType conductorType)
        {
            // Get current node first
            var currentNode = GetNode(endpoint, conductorType);

            // Ignore if no change
            if (currentNode == node)
            {
                return;
            }

            // Dispose current node before replacing
            currentNode?.Dispose();

            // Performance optimization - if setting the "neutral" line, but only one phase
            // is in use, use a single "common node" rather than a pair of nodes connected by resistor.
            // We can then double the resistance in the positive line to account for the current that would flow
            // through the neutral line.
            // We can't do the same generalization for 3-phase power though, since the current flowing through the neutral
            // may be unbalanced with the three phases.
            if (conductorType == ConductorType.Neutral && NeutralLine == null && Line2 == null && Line3 == null)
            {
                CommonNode = node.Clone();
                return;
            }

            var conductor = GetConductor(conductorType);

            var peer = endpoint.Connection?.GetPeer();

            if (endpoint == EndpointA || peer == EndpointA)
            {
                conductor.NodeA = node.Clone();
                conductor.Resistor?.Dispose();
                conductor.Resistor = null;
            }
            else if (endpoint == EndpointB || peer == EndpointB)
            {
                conductor.NodeB = node.Clone();
                conductor.Resistor?.Dispose();
                conductor.Resistor = null;
            }
            else
            {
                throw new InvalidOperationException($"The provided connection does not attach to this cable span. ({Circuit.Id}, {endpoint})");
            }

        }

        private Conductor GetConductor(ConductorType conductorType)
        {
            return conductorType switch
            {
                ConductorType.Line1 => Line1 ??= new(),
                ConductorType.Line2 => Line2 ??= new(),
                ConductorType.Line3 => Line3 ??= new(),
                _ => NeutralLine ??= new(),
            };
        }
    }
}