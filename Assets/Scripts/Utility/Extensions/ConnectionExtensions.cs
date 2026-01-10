#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;

namespace Hardwired.Objects
{
    public static class ConnectionExtensions
    {
        /// <summary>
        /// Given a connection, get the peer connection if it exists and is connected
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static Connection? GetPeer(this Connection? connection)
        {
            connection?.Initialize();
            var peerStructure = connection?.GetOther(false);
            return peerStructure?.OpenEnds.FirstOrDefault(c => c.LocalGrid == connection?.FacingGrid);
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
    }
}