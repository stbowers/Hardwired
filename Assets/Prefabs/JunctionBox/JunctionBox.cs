#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Localization2;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.UI.ImGuiUi;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Objects.Electrical;
using Hardwired.Patches;
using LaunchPadBooster.Utils;
using LibConstruct;
using UnityEngine;

namespace Hardwired.Prefabs.JunctionBox
{
    public class JunctionBox : Cable, IGridMergeable, IPlacementBoardHost, IPseudoNetworkMember<JunctionBox>, IPatchOnLoad
    {
        private static List<WeakReference<JunctionBox>> _allJunctionBoxes = new();
        private BoardRef<JunctionBoxBoard>? _board;
        private HardwiredCable? _hardwiredCableComponent;
        private List<Connection> _boardConnections = new();
        private Interactable? _creatingWireStartTeriminal;
        private CableSpan.ConductorType? _creatingWireStartConductor;

        public static readonly PseudoNetworkType<JunctionBox> PseudoNetworkType = new();

        public Transform? BoardOrigin;

        public JunctionBoxBoard? Board => _board?.Board;

        public List<BoxCollider> BoardColliders = new();

        public HardwiredCable HardwiredCableComponent => _hardwiredCableComponent ??= GetComponent<HardwiredCable>()
            ?? throw new InvalidOperationException("JunctionBox must have a HardiwredCable component attached.");

        IEnumerable<Connection> IPseudoNetworkMember<JunctionBox>.Connections
        {
            get
            {
                foreach (var openEnd in this.OpenEnds)
                {
                    if (openEnd.ConnectionType == 0 || openEnd.ConnectionType == PseudoNetworkType.ConnectionType)
                    {
                        yield return openEnd;
                    }
                }
            }
        }

        public PseudoNetwork<JunctionBox> Network { get; } = PseudoNetworkType.Join();

        CanConstructInfo IGridMergeable.CanReplace(Assets.Scripts.Objects.MultiConstructor constructor, Item inactiveHandItem)
        {
            Grid3[] array = (Grid3[])GridBounds.GetLocalSmallGrid(base.ThingTransformPosition, base.ThingTransformRotation);
            foreach (Grid3 localGrid in array)
            {
                Thing thing = SmallCell.Get<Thing>(localGrid);
                if (thing != null)
                {
                    return CanConstructInfo.InvalidPlacement(GameStrings.PlacementBlockedByStructure.AsString(thing.DisplayName));
                }
            }

            return CanConstructInfo.ValidPlacement;
        }

        bool IGridMergeable.WillMergeWhenPlaced() => false;

        public override void OnFinishedLoad()
        {
            base.OnFinishedLoad();
            BoardHostHooks.OnFinishedLoadBoard(this, ref _board, BoardOrigin);
        }

        public override void OnRegistered(Cell cell)
        {
            base.OnRegistered(cell);
            BoardHostHooks.OnRegisteredBoard(this, ref _board, BoardOrigin);
            PseudoNetworkType.RebuildNetworkCreate(this);

            _allJunctionBoxes.Add(new WeakReference<JunctionBox>(this));
        }

        public override void OnDeregistered()
        {
            base.OnDeregistered();
            PseudoNetworkType.RebuildNetworkDestroy(this);

            _allJunctionBoxes.RemoveAll(wr => wr.TryGetTarget(out var junctionBox) && junctionBox == this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            BoardHostHooks.OnDestroyedBoard(this, _board);
        }

        public override void SerializeOnJoin(RocketBinaryWriter writer)
        {
            base.SerializeOnJoin(writer);
            BoardHostHooks.SerializeBoardOnJoin(writer, this, Board);
        }

        public override void DeserializeOnJoin(RocketBinaryReader reader)
        {
            base.DeserializeOnJoin(reader);
            BoardHostHooks.DeserializeBoardOnJoin(reader, this, out _board, BoardOrigin);
        }

        public override PassiveTooltip GetPassiveTooltip(Collider hitCollider)
        {
            if (hitCollider is not BoxCollider boxCollider || !BoardColliders.Contains(boxCollider))
            {
                return base.GetPassiveTooltip(hitCollider);
            }

            return new PassiveTooltip
            {
                Title = DisplayName,
                Extended = $"{Network.Members.Count} board structures"
            };
        }

        public IEnumerable<BoxCollider> CollidersForBoard(PlacementBoard board)
        {
            if (board == Board)
            {
                return BoardColliders;
            }

            return Enumerable.Empty<BoxCollider>();
        }

        public IEnumerable<PlacementBoard> GetPlacementBoards()
        {
            if (Board != null)
            {
                yield return Board;
            }
        }


        public void OnBoardStructureDeregistered(PlacementBoard board, IPlacementBoardStructure structure)
        {
        }

        public void OnBoardStructureRegistered(PlacementBoard board, IPlacementBoardStructure structure)
        {
        }

        public void OnMemberAdded(JunctionBox member)
        {
        }

        public void OnMemberRemoved(JunctionBox member)
        {
        }

        public void OnMembersChanged()
        {
        }

        public static void DrawImGui()
        {
            foreach (var junctionBoxRef in _allJunctionBoxes)
            {
                if (junctionBoxRef.TryGetTarget(out var junctionBox))
                {
                    junctionBox.DrawImGuiInternal();
                }
            }
        }

        private void DrawImGuiInternal()
        {
            if (!IsOpen || IsOccluded) { return; }

            if (_creatingWireStartTeriminal != null)
            {
                ImGuiExtensions.Rendering.LineThickness = 2;
                
                ImGuiExtensions.Rendering.RenderingColor = ImGuiColor.Integer.Green;
                ImGuiExtensions.Rendering.DrawWireSphere(_creatingWireStartTeriminal.Collider.transform.position, 0.05f);
            }

            // Render each connection wire
            Vector3 wireOffset = 0.04f * Vector3.up;
            foreach (var connection in HardwiredCableComponent.CustomConnections)
            {
                ImGuiExtensions.Rendering.LineThickness = 4;
                ImGuiExtensions.Rendering.RenderingColor = CableSpan.GetLineDebugRenderColor(connection.FromConductor);

                // Get start & end points
                Vector3 startPoint = GetTerminalConnectionPosition(connection.From, connection.FromConductor) + wireOffset;
                Vector3 endPoint = GetTerminalConnectionPosition(connection.To, connection.ToConductor) + wireOffset;
            
                ImGuiExtensions.Rendering.DrawLineSimple(startPoint, endPoint);

                // Offset each wire up by a small amount, to avoid clipping
                wireOffset += 0.01f * Vector3.up;
            }
        }

        /// <summary>
        /// Gets the world position for the "terminal" of the given connection for the given wireType, for rendering wires inside the junction box.
        /// </summary>
        /// <returns></returns>
        private Vector3 GetTerminalConnectionPosition(Connection connection, CableSpan.ConductorType wireType)
        {
            Vector3 wireOffset = wireType switch
            {
                CableSpan.ConductorType.Line1 => 0.04f * connection.Transform.left(),
                _ => 0.04f * connection.Transform.right,
            };

            return connection.Transform.position
                + 0.225f * connection.Transform.forward
                + wireOffset;
        }

        private Connection GetConnectionForTerminal(Interactable terminal)
        {
            double DistanceTo(Connection c)
            {
                return (terminal.Collider.transform.position - c.Transform.position).magnitude;
            }

            return OpenEnds.OrderBy(DistanceTo).First();
        }

        private void AddConnection(Interactable fromTerminal, CableSpan.ConductorType fromConductor, Interactable toTerminal, CableSpan.ConductorType toConductor)
        {
            HardwiredCableComponent.CustomConnections.Add(new HardwiredCable.CustomJunctionConnection
            {
                From = GetConnectionForTerminal(fromTerminal),
                FromConductor = fromConductor,
                To = GetConnectionForTerminal(toTerminal),
                ToConductor = toConductor,
            });
        }

        /// <summary>
        /// Called from JunctionBoxTerminalBlock to handle interactions with a terminal in the junction box.
        /// Allows creating new custom connections in the junction by clicking on the source terminal followed by the destination terminal.
        /// </summary>
        /// <param name="terminal"></param>
        /// <param name="conductorType"></param>
        /// <param name="doAction"></param>
        /// <returns></returns>
        public DelayedActionInstance InteractWithTerminal(Interactable terminal, CableSpan.ConductorType conductorType, bool doAction)
        {
            DelayedActionInstance actionMessage = new()
            {
                ActionMessage = terminal.ContextualName
            };

            if (_creatingWireStartTeriminal == terminal)
            {
                actionMessage.ExtendedMessage = $"Cannot connect back to source";
                return actionMessage.Fail();
            }

            if (!doAction)
            {
                return actionMessage.Succeed();
            }

            if (_creatingWireStartTeriminal == null || _creatingWireStartConductor == null)
            {
                _creatingWireStartTeriminal = terminal;
                _creatingWireStartConductor = conductorType;
            }
            else
            {
                AddConnection(_creatingWireStartTeriminal, _creatingWireStartConductor.Value, terminal, conductorType);
                _creatingWireStartTeriminal = null;
            }

            return actionMessage.Succeed();
        }

        public override void UpdateStateVisualizer(bool visualOnly = false)
        {
            base.UpdateStateVisualizer(visualOnly);

            foreach (var interactable in Interactables)
            {
                interactable.CacheBounds();
            }
        }

        public void PatchOnLoad()
        {
            this.ReplaceMaterialWithColor(PaintableMaterial, ColorType.Gray);
            this.SetExitTool(PrefabNames.Drill);
        }
    }
}