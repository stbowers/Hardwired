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
using Hardwired.Networks;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility;
using UnityEngine;
using UnityEngine.Assertions;

namespace Hardwired.Objects
{
    public static class ConnectionExtensions
    {
        /// <summary>
        /// Extends `NetworkType` to allow `Connection.ConnectionType` to specify what kind of circuits this connection is capable of connecting to
        /// </summary>
        [Flags]
        public enum HardwiredPowerType
        {
            SinglePhase = 0x1000000,
            ThreePhase = 0x2000000,
        }

        private static ConditionalWeakTable<Connection, WeakReference<CableSpan>?> _cableSpan = new();

        public static CableSpan? GetCableSpan(this Connection connection)
        {
            if (_cableSpan.TryGetValue(connection, out WeakReference<CableSpan>? cableSpanWeakRef))
            {
                CableSpan? cableSpan = null;
                cableSpanWeakRef?.TryGetTarget(out cableSpan);
                return cableSpan;
            }

            return null;
        }

        public static void SetCableSpan(this Connection connection, CableSpan? value)
        {
            WeakReference<CableSpan>? weakReference
                = value != null ? new WeakReference<CableSpan>(value) : null;

            _cableSpan.AddOrUpdate(connection, weakReference);
        }

        public static RefCounted<MNASolver.Unknown>? GetNode(this Connection connection, Circuit circuit, CableSpan.ConductorType conductorType)
        {
            var peer = connection.GetPeer();
            var cableSpan = peer?.GetCableSpan();
            if (cableSpan is null) { return null; }

            if (cableSpan.Circuit != circuit) { return null; }

            return cableSpan.GetOrCreateNode(connection, conductorType);
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

        /// <summary>
        /// Gets the exact center between the two grid points this connection represents.
        /// Both this connection and it's peer should return the exact same point, which is mainly useful for custom debug overlays where
        /// we want both sides of a connection to draw something at the same point.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static Vector3 GetConnectionMidpoint(this Connection connection)
        {
            Vector3 localGrid = connection.LocalGrid.ToVector3();
            Vector3 facingGrid = connection.FacingGrid.ToVector3();
            return (localGrid + facingGrid) / 2f;
        }
    }
}