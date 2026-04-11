#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using MathNet.Numerics.LinearAlgebra;

namespace Hardwired.Utility.Extensions
{
    public static class SmallGridExtensions
    {
        private static ConditionalWeakTable<SmallGrid, Dictionary<(Circuit, Connection, ElectricalComponent.WireType), RefCounted<MNASolver.Unknown>>> _attachedNodes = new();
        
        public static Dictionary<(Circuit circuit, Connection connection, ElectricalComponent.WireType wireType), RefCounted<MNASolver.Unknown>> GetNodes(this SmallGrid smallGrid)
        {
            return _attachedNodes.GetValue(smallGrid, _ => new Dictionary<(Circuit, Connection, ElectricalComponent.WireType), RefCounted<MNASolver.Unknown>>());
        }
    }
}