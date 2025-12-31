using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using Assets.Scripts.UI.HelperHints.Extensions;
using Assets.Scripts.Util;
using Hardwired.Utility;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Component : SmallSingleGrid, ISmartRotatable
    {
        // Patch prefab in static constructor to add references to base game content
        static Component()
        {
            Hardwired.LogDebug("Adding Component prefab setup...");

            // Set exit tool to wire cutters (can't be done direclty in unity, since we don't have access to the base game prefabs)
            var prefabSetup = Hardwired.MOD.SetupPrefabs<Component>();
            prefabSetup.SetExitTool(PrefabNames.WireCutters);
        }

        public Circuit Circuit;

        public override void OnRegistered(Cell cell)
        {
            base.OnRegistered(cell);

            var connected = Connected();
            // Hardwired.LogDebug($"Component registered - {connected.Count} connections.");

            foreach (var connection in Connected())
            {
                if (connection.GetComponent<Component>() is not Component connectedComponent)
                {
                    continue;
                }

                Circuit = Circuit.Merge(Circuit, connectedComponent.Circuit);
            }

            Circuit ??= new();
            Circuit.AddComponent(this);
        }

        public override void OnDeregistered()
        {
            base.OnDeregistered();

            Circuit?.RemoveComponent(this);
            Circuit = null;
        }

        #region Network Info Tooltip
        [Header("Tooltip")]
        public Collider InfoCollider;

        public override PassiveTooltip GetPassiveTooltip(Collider hitCollider)
        {
            if (hitCollider != InfoCollider)
            {
                return base.GetPassiveTooltip(hitCollider);
            }

            Tooltip.ToolTipStringBuilder.Clear();
            BuildPassiveToolTip(Tooltip.ToolTipStringBuilder);

            PassiveTooltip result = new PassiveTooltip(toDefault: true)
            {
                Title = DisplayName,
                Extended = Tooltip.ToolTipStringBuilder.ToString(),
            };

            return result;
        }

        protected virtual void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            if (Circuit == null)
            {
                stringBuilder.AppendColorText("red", "Network Not Found");
            }
            else
            {
                stringBuilder.AppendLine($"Circuit network: {Circuit.ReferenceId}");
                // Tooltip.ToolTipStringBuilder.Append(GameStrings.CableAnalyserRequired.AsString(RequiredLoad.ToStringPrefix("W", "yellow")));
            }
        }
        #endregion

        #region ISmartRotatable

        [Header("ISmartRotation")]
        public SmartRotate.ConnectionType ConnectionType = SmartRotate.ConnectionType.Exhaustive;

        public int[] OpenEndsPermutation = new int[6] { 0, 1, 2, 3, 4, 5 };
        
        public SmartRotate.ConnectionType GetConnectionType()
        {
            return ConnectionType;
        }

        public void SetOpenEndsPermutation(int[] permutation)
        {
            OpenEndsPermutation = (int[])permutation.Clone();
        }

        public int[] GetOpenEndsPermutation()
        {
            return (int[])OpenEndsPermutation.Clone();
        }

        public void SetConnectionType(SmartRotate.ConnectionType connectionType)
        {
            ConnectionType = connectionType;
        }

        #endregion
    }
}
