#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility;
using UnityEngine.Assertions;

namespace Hardwired.Objects
{
    public static class ConnectionExtensions
    {
        /// <summary>
        /// Helper class which stores references to "nodes" (solver unknowns) for a pair of connections.
        /// Both connections in a "pair" will reference the same instance of this class, since they logically share the same nodes.
        /// </summary>
        private class ConnectionNodes
        {
            public Dictionary<ElectricalComponent.WireType, RefCounted<MNASolver.Unknown>> AttachedNodes = new();

            public RefCounted<MNASolver.Unknown>? GetNode(Circuit circuit, ElectricalComponent.WireType wireType)
            {
                // For the time being, the "neutral" line is hard-coded to return null, which represents the common ground node.
                // This is correct the kinds of simple circuits that are currently supported, but eventually it should be possible
                // to have "islands" of components that share different ground points (for example, wiring some components in series).
                // TODO - write logic to correctly determine ground points for series components
                if (wireType == ElectricalComponent.WireType.Neutral)
                {
                    return null;
                }

                RefCounted<MNASolver.Unknown>? node = null;

                if (AttachedNodes.TryGetValue(wireType, out node))
                {
                    // Check if our cached node is for the correct circuit - if so return
                    if (node.Value.Solver == circuit.Solver)
                    {
                        return node;
                    }

                    // Otherwise, whatever node we have cached is presumably no longer valid, so dispose it and create a new node for the new circuit
                    node.Dispose();
                }

                // Node either didn't exist, or was no longer valid.. Create a new node and add/update to the dictionary
                node = new(circuit.Solver.AddUnknown());
                AttachedNodes[wireType] = node;

                return node;
            }
        }

        private static ConditionalWeakTable<Connection, ConnectionNodes> _connectionNodes = new();

        private static ConnectionNodes? GetConnectionNodes(this Connection connection)
        {
            if (_connectionNodes.TryGetValue(connection, out var value))
            {
                return value;
            }

            return null;
        }

        private static void SetConnectionNodes(this Connection connection, ConnectionNodes? value)
        {
            if (value != null)
            {
                _connectionNodes.AddOrUpdate(connection, value);
            }
            else
            {
                _connectionNodes.Remove(connection);
            }
        }

        private static ConnectionNodes GetOrInitializeConnectionNodes(this Connection connection)
        {
            // Try to get cached instance first, return early if found
            ConnectionNodes? value = connection.GetConnectionNodes();
            if (value != null)
            {
                return value;
            }

            // If not yet cached, check if the peer connection has a cached instance, or otherwise create a new instance
            value = connection.GetPeer()?.GetConnectionNodes();
            value ??= new();

            // Cache the resolved instance and return
            connection.SetConnectionNodes(value);
            return value;
        }

        /// <summary>
        /// Given a connection, get the peer connection if it exists and is connected
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static Connection? GetPeer(this Connection connection)
        {
            // There may be more than one structure in the cell this connection represents (for example, a pipe and a pipe heater, etc).
            // Therefore, we need to check for multiple kinds of structures to ensure we find the one with the actual peer connection.
            return TryFindPeerConnection(connection.GetDevice(true))
                ?? TryFindPeerConnection(connection.GetCable(true))
                ?? TryFindPeerConnection(connection.GetOther(true));

            Connection? TryFindPeerConnection(SmallGrid? structure)
            {
                return structure?.OpenEnds.FirstOrDefault(c => c.GetLocalGrid() == connection.FacingGrid);
            }
        }

        /// <summary>
        /// Given a connection, gets the index of the peer connection if it exists and is connected, or -1 otherwise
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static int GetPeerIndex(this Connection? connection)
        {
            var peerStructure = connection?.GetOther(false);
            var peerIndex = peerStructure?.OpenEnds.FindIndex(c => c.LocalGrid == connection?.FacingGrid);

            return peerIndex ?? -1;
        }

        public static RefCounted<MNASolver.Unknown>? GetNode(this Connection connection, Circuit circuit, ElectricalComponent.WireType wireType)
        {
            var nodes = connection.GetOrInitializeConnectionNodes();
            return nodes.GetNode(circuit, wireType);
        }
    }
}