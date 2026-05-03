#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility;
using Hardwired.Utility.Extensions;
using TerrainSystem;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    /// <summary>
    /// Base class for electrical components.
    /// 
    /// Each pin (PinA and PinB) should be assigned to a Connection in the editor.
    /// If either pin is assigned `-1`, it will be assumed to be connected to the common ground pin for the network.
    /// 
    /// Note that in order to make simulation easier, every component in the network shares the same ground, i.e. in particular
    /// internal resistance in the ground wire is not explicitly simulated. Instead internal resistance on the "live" wire is
    /// doubled, which should have the same effect in practice since the live and ground wires are always paired in a single
    /// cable (so i.e. it's impossible to have one without the other, and they will always end up in series, meaning their
    /// resistances can be added together).
    /// </summary>
    public abstract class ElectricalComponent : MonoBehaviour
    {
        /// <summary>
        /// Identifies a wire type in a connection, which can be used to get different nodes for different "wires" in a cable
        /// </summary>
        public enum WireType
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

        private Device? _device;
        private Cable? _cable;
        private Connection? _powerInput;
        private Connection? _powerOutput;
        
        public Device? Device => _device ??= GetComponent<Device>();

        public Cable? Cable => _cable ??= GetComponent<Cable>();

        protected List<Connection>? OpenEnds => Device?.OpenEnds ?? Cable?.OpenEnds;

        public virtual Connection? PowerInput => _powerInput ??= OpenEnds?.FirstOrDefault(c => c.ConnectionType.HasFlag(NetworkType.Power) && c.ConnectionRole != ConnectionRole.Output);

        public virtual Connection? PowerOutput => _powerOutput ??= OpenEnds?.FirstOrDefault(c => c.ConnectionType.HasFlag(NetworkType.Power) && c.ConnectionRole != ConnectionRole.Input && c != PowerInput);

        public virtual CableNetwork? InputCableNetwork => PowerInput?.GetCable()?.CableNetwork;

        public virtual CableNetwork? OutputCableNetwork => PowerOutput?.GetCable()?.CableNetwork;

        public virtual Circuit? InputCircuit => (InputCableNetwork?.PowerTick as HardwiredPowerTick)?.CircuitTick?.Circuit;

        public virtual Circuit? OutputCircuit => (OutputCableNetwork?.PowerTick as HardwiredPowerTick)?.CircuitTick?.Circuit;

        /// <summary>
        /// For debugging - add info to the passive tooltip
        /// </summary>
        /// <param name="stringBuilder"></param>
        public virtual void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            stringBuilder.Append($"-- {GetType().Name} [");

            if (InputCircuit is Circuit inputCircuit)
            {
                string id = inputCircuit.Id.ToString().AsColor("green");
                stringBuilder.Append(id);

                if (inputCircuit.LastTickStatus == Circuit.TickProcessingStatus.Success)
                {
                    stringBuilder.Append($"(f = {inputCircuit.Frequency.ToStringPrefix("Hz", "yellow")})");
                }
                else
                {
                    stringBuilder.Append($"({inputCircuit.LastTickStatus.ToString().AsColor("red")})");
                }
            }

            if (OutputCircuit is Circuit outputCircuit && outputCircuit != InputCircuit)
            {
                string id = outputCircuit.Id.ToString().AsColor("green") ?? "N/A".AsColor("red");
                stringBuilder.Append($" -> {id}");

                if (outputCircuit.LastTickStatus == Circuit.TickProcessingStatus.Success)
                {
                    stringBuilder.Append($"(f = {outputCircuit.Frequency.ToStringPrefix("Hz", "yellow")})");
                }
                else
                {
                    stringBuilder.Append($"({outputCircuit.LastTickStatus.ToString().AsColor("red")})");
                }
            }

            stringBuilder.AppendLine($"]");

            // string nodesDebugText = string.Join(" | ", Nodes.Select(n => $"{n.Key.connection.ConnectionRole}<{n.Key.circuit.Id}|{n.Key.wireType}> = {n.Value.Value.Index}"));
            // stringBuilder.AppendLine(nodesDebugText);
        }

        public virtual string DebugInfo()
        {
            return string.Empty;
        }

        /// <summary>
        /// Called by a postfix patch on `Thing.DeserialiseSave()`, allowing any attached electrical components to load save data.
        /// 
        /// See `SerializeSave()` for more info on how hardwired components generally add data to `ThingSaveData`.
        /// </summary>
        /// <param name="saveData"></param>
        public virtual void DeserializeSave(ThingSaveData saveData)
        {
        }

        /// <summary>
        /// Called by a postfix patch on `Thing.InitialiseSaveData()`, allowing any attached electrical components to write data to be saved.
        /// 
        /// Note, Sationeers' save system is fairly rigid, and doesn't provide many places to add custom data to the save file...
        /// The best method I have found so far is to add custom "interactable states" to `ThingSaveData.States`.
        /// Each state has a name and int value, meaning we can use `ThingSaveData.States` as a budget `Dictionary<string, int>` :')
        /// 
        /// When loading save data, `Thing.DeserialiseSave()` ignores any interactable states with an unknown name, so generally hardwired
        /// components will add states with names like "Hardwired.Objects.Electrical.PowerSink:ActivePowerProfileIndex" to ensure there are
        /// no conflicts.
        /// 
        /// Note that using this system, hardwired components are limited to only saving/loading integer values (or other values that can be
        /// converted to a 4-byte representation).
        /// In theory if more complex data was really needed, and it couldn't be converted into a series of integer/4-byte properties, more
        /// complex data could be stored in the `StateName` string, serialized as JSON or some other format...
        /// 
        /// See extension methods in `ThingSaveDataExtensions.cs` for helper methods for storing values in the `ThingSaveData.States` list.
        /// </summary>
        /// <param name="saveData"></param>
        public virtual void SerializeSave(ThingSaveData saveData)
        {
        }

        /// <summary>
        /// Gets a list of cable networks that should be considered to be in the same circuit as the input network
        /// </summary>
        /// <param name="network"></param>
        /// <returns></returns>
        public virtual IEnumerable<CableNetwork> GetBridgedNetworks(CableNetwork network)
        {
            return Enumerable.Empty<CableNetwork>();
        }

        /// <summary>
        /// Called by the network manager to add this component to a circuit
        /// </summary>
        /// <param name="solver"></param>
        public virtual void AddTo(Circuit circuit)
        {
        }

        /// <summary>
        /// Called by the network manager to remove this component from the given circuit.
        /// </summary>
        /// <param name="circuit"></param>
        public virtual void RemoveFrom(Circuit circuit)
        {
            if (this == null || !TryGetComponent<SmallGrid>(out var smallGrid))
            {
                return;
            }

            var nodes = smallGrid.GetNodes();

            foreach (var port in nodes.ToList())
            {
                if (port.Key.circuit != circuit) { continue; }

                port.Value.Dispose();
                nodes.Remove(port.Key);
            }
        }

        /// <summary>
        /// Called by the network manager during a power update to update the MNA solver's z vector with this component's values.
        /// 
        /// Compared to `InitializeSolver()`, this function is called for every power update tick. In order to avoid slowing down the simulation,
        /// this function should only make changes to the solver's z vector (set of inputs). The solver can then use the existing A matrix
        /// decomposition to quickly solve for the x vector (outputs).
        /// </summary>
        /// <param name="solver"></param>
        public virtual void UpdateState(Circuit circuit)
        {
        }

        /// <summary>
        /// Called by the network manager during a power update after the MNA solver has been updated with a new solution.
        /// 
        /// This function should retrieve any required solved values, such as voltage at a given node, in order to update this component.
        /// </summary>
        /// <param name="solver"></param>
        public virtual void ApplyState(Circuit circuit)
        {
        }

        protected RefCounted<MNASolver.Unknown>? GetNode(Circuit circuit, Connection? connection, WireType wireType)
        {
            var nodes = GetComponent<SmallGrid>().GetNodes();

            RefCounted<MNASolver.Unknown>? node;

            if (connection == null) { return null; }

            if (nodes.TryGetValue((circuit, connection, wireType), out node)) { return node; }

            if (TryGetPeerNode(circuit, connection, wireType, out node))
            {
                node = node.Clone();
            }
            else
            {
                node = RefCounted.Create(circuit.Solver.AddUnknown());
            }

            nodes.Add((circuit, connection, wireType), node);

            return node;
        }

        protected bool TryGetPeerNode(Circuit circuit, Connection connection, WireType wireType, [NotNullWhen(true)] out RefCounted<MNASolver.Unknown>? node)
        {
            if (TryGetComponent<SmallGrid>(out var smallGrid)
                && connection.GetPeer() is Connection peerConnection
                && peerConnection.Parent.TryGetComponent<SmallGrid>(out var peerDevice)
                && peerDevice.GetNodes().TryGetValue((circuit, peerConnection, wireType), out node))
            {
                return true;
            }

            node = null;
            return false;
        }
    }
}
