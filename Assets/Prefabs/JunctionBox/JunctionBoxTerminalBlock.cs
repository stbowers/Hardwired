#nullable enable

using System;
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
            if ((interactable == InteractButton1 || interactable == InteractButton2)
                && Board?.PrimaryHost is JunctionBox junctionBox)
            {
                var conductorType = interactable.Action switch
                {
                    InteractableType.Button1 => CableSpan.ConductorType.Line1,
                    _ => CableSpan.ConductorType.Neutral,
                };
                return junctionBox.InteractWithTerminal(interactable, conductorType, doAction);
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