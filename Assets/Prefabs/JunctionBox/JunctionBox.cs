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
using Hardwired.Patches;
using LaunchPadBooster.Utils;
using LibConstruct;
using UnityEngine;

namespace Hardwired.Prefabs.JunctionBox
{
    public class JunctionBox : Device, IPlacementBoardHost, IPseudoNetworkMember<JunctionBox>, IPatchOnLoad
    {
        private static List<WeakReference<JunctionBox>> _allJunctionBoxes = new();

        private BoardRef<JunctionBoxBoard>? _board;

        public static readonly PseudoNetworkType<JunctionBox> PseudoNetworkType = new();

        public Transform? BoardOrigin;

        public JunctionBoxBoard? Board => _board?.Board;

        public List<BoxCollider> BoardColliders = new();

        private List<Connection> _boardConnections = new();

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











        private Interactable? _startTerminal;
        private CableSpan.ConductorType? _startConductor;

        private List<(Collider from, Collider to, CableSpan.ConductorType conductorType)> _connections = new();

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

            if (_startTerminal != null)
            {
                ImGuiExtensions.Rendering.LineThickness = 2;
                
                ImGuiExtensions.Rendering.RenderingColor = ImGuiColor.Integer.Green;
                ImGuiExtensions.Rendering.DrawWireSphere(_startTerminal.Collider.transform.position, 0.05f);
            }

            foreach (var connection in _connections)
            {
                ImGuiExtensions.Rendering.LineThickness = 4;
                ImGuiExtensions.Rendering.RenderingColor = CableSpan.GetLineDebugRenderColor(connection.conductorType);
            
                ImGuiExtensions.Rendering.DrawLineSimple(connection.from.transform.position, connection.to.transform.position);
            }
        }

        public void AddConnection(Collider from, Collider to, CableSpan.ConductorType conductorType)
        {
            _connections.Add((from, to, conductorType));
        }

        public DelayedActionInstance InteractWithTerminal(Interactable terminal, CableSpan.ConductorType conductorType, bool doAction)
        {
            DelayedActionInstance actionMessage = new()
            {
                ActionMessage = terminal.ContextualName
            };

            if (_startTerminal == terminal)
            {
                actionMessage.ExtendedMessage = $"Cannot connect back to source";
                return actionMessage.Fail();
            }

            if (!doAction)
            {
                return actionMessage.Succeed();
            }

            if (_startTerminal == null)
            {
                _startTerminal = terminal;
                _startConductor = conductorType;
            }
            else
            {
                AddConnection(_startTerminal.Collider, terminal.Collider, _startConductor!.Value);
                _startTerminal = null;
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