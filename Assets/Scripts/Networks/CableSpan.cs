#nullable enable

using System;
using System.Collections.Generic;
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
        private bool _isConnectionsInitialized;

        private static int _nextId;

        public int Id { get; private set; } = Interlocked.Add(ref _nextId, 1);

        public IReadOnlyList<HardwiredCable> Cables => _cables.AsReadOnly();

        public Conductor? Line1 { get; private set; }

        public Conductor? Line2 { get; private set; }

        public Conductor? Line3 { get; private set; }

        public Conductor? NeutralLine { get; private set; }

        public double Resistance { get; private set; }

        public Connection? ConnectionA { get; private set; }

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

        public void AddCable(HardwiredCable cable, Connection? connectionA, Connection? connectionB)
        {
            _cables.Add(cable);
            cable.CableSpan = this;

            Resistance += cable.Resistance;

            UpdateConnections(connectionA, connectionB);
        }

        private void UpdateConnections(Connection? a, Connection? b)
        {
            a?.SetCableSpan(this);
            b?.SetCableSpan(this);

            if (_cables.Count == 1)
            {
                ConnectionA = a;
                ConnectionB = b;
                return;
            }

            var peerA = a?.GetPeer();
            var peerB = b?.GetPeer();

            if (ConnectionA != null && ConnectionA == peerA)
            {
                ConnectionA = b ?? a;
            }
            else if (ConnectionA != null && ConnectionA == peerB)
            {
                ConnectionA = a ?? b;
            }
            else if (ConnectionB != null && ConnectionB == peerA)
            {
                ConnectionB = b ?? a;
            }
            else if (ConnectionB != null && ConnectionB == peerB)
            {
                ConnectionB = a ?? b;
            }
        }

        public RefCounted<MNASolver.Unknown>? GetNode(Connection connection, ConductorType conductorType)
        {
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