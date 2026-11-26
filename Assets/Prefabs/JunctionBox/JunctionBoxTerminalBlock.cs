#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.UI.ImGuiUi;
using Assets.Scripts.Util;
using Hardwired.Networks;
using Hardwired.Patches;
using LaunchPadBooster.Utils;
using LibConstruct;
using UnityEngine;

namespace Hardwired.Prefabs.JunctionBox
{
    public class JunctionBoxTerminalBlock : SmallGrid, IPlacementBoardStructure, IPatchOnLoad
    {
        public PlacementBoard? Board { get; set;}
        public PlacementBoard.BoardCell[] BoardCells { get; set; } = Array.Empty<PlacementBoard.BoardCell>();

        /// <summary>
        /// In same order as interactables (neutral, line1, line2, line3)
        /// </summary>
        public List<Transform> TerminalBasePositions = new();

        /// <summary>
        /// Gets the world position for the "terminal" of the given connection for the given wireType, for rendering wires inside the junction box.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetTerminalConnectionPosition(Connection connection, CableSpan.ConductorType wireType)
        {
            return wireType switch
            {
                CableSpan.ConductorType.Neutral => TerminalBasePositions.ElementAtOrDefault(0).position,
                CableSpan.ConductorType.Line1 => TerminalBasePositions.ElementAtOrDefault(1).position,
                CableSpan.ConductorType.Line2 => TerminalBasePositions.ElementAtOrDefault(2).position,
                CableSpan.ConductorType.Line3 => TerminalBasePositions.ElementAtOrDefault(3).position,
                _ => Vector3.zero
            };
        }

        public override ThingSaveData SerializeSave()
        {
            var saveData = new JunctionBoxTerminalBlockSaveData();
            var baseData = saveData as ThingSaveData;
            InitialiseSaveData(ref baseData);
            return saveData;
        }

        protected override void InitialiseSaveData(ref ThingSaveData baseData)
        {
            base.InitialiseSaveData(ref baseData);

            if (baseData is not JunctionBoxTerminalBlockSaveData saveData)
            {
                return;
            }

            saveData.Board = BoardStructureHooks.SerializeSave(this);
        }

        public override void DeserializeSave(ThingSaveData baseData)
        {
            base.DeserializeSave(baseData);

            if (baseData is not JunctionBoxTerminalBlockSaveData saveData)
            {
                return;
            }

            BoardStructureHooks.DeserializeSave(this, saveData.Board);
        }

        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            CableSpan.ConductorType? conductorType = interactable.Action switch
            {
                InteractableType.Button1 => CableSpan.ConductorType.Neutral,
                InteractableType.Button2 => CableSpan.ConductorType.Line1,
                InteractableType.Button3 => CableSpan.ConductorType.Line2,
                InteractableType.Button4 => CableSpan.ConductorType.Line3,
                _ => null
            };

            if (conductorType != null && Board?.PrimaryHost is JunctionBox junctionBox)
            {
                return junctionBox.InteractWithTerminal(interactable, conductorType.Value, doAction);
            }

            var returnValue = base.InteractWith(interactable, interaction, doAction);
            returnValue.ExtendedMessage = $"Interactable.Action = {interactable.Action}\nBoard = {Board?.GetHashCode()}\nBoard.PrimaryHost = {Board?.PrimaryHost?.GetHashCode()}";

            return returnValue;
        }

        public override void UpdateStateVisualizer(bool visualOnly = false)
        {
            base.UpdateStateVisualizer(visualOnly);

            foreach (var interactable in Interactables)
            {
                interactable.CacheBounds();
            }
        }

        public override void Awake()
        {
            base.Awake();

            BoardStructureHooks.Awake(this);
        }

        public override CanConstructInfo CanConstruct()
        {
            if (Board != null)
            {
                Transform.LookAt(Board.Origin);
                Transform.Rotate(-90, 0, 90);
            }
            return BoardStructureHooks.CanConstruct(this);
        }

        public override void OnDeregistered()
        {
            base.OnDeregistered();
            BoardStructureHooks.OnDeregistered(this);
        }

        public override void SerializeOnJoin(RocketBinaryWriter writer)
        {
            base.SerializeOnJoin(writer);
            BoardStructureHooks.SerializeOnJoin(writer, this);
        }

        public override void DeserializeOnJoin(RocketBinaryReader reader)
        {
            base.DeserializeOnJoin(reader);
            BoardStructureHooks.DeserializeOnJoin(reader, this);
        }

        public void PatchOnLoad()
        {
            this.ReplaceMaterialWithColor(PaintableMaterial, ColorType.Black);
            this.SetExitTool(PrefabNames.Drill);
        }
    }
}