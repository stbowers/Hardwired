#nullable enable

using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using UnityEngine;

namespace Hardwired.Objects
{
    public class ElectricalStructure : SmallSingleGrid, ISmartRotatable
    {
        public override void OnRegistered(Cell cell)
        {
            base.OnRegistered(cell);

            foreach (var component in GetComponents<ElectricalComponent>())
            {
                component.ConnectCircuit();
            }
        }

        public override void OnDeregistered()
        {
            base.OnDeregistered();

            foreach (var component in GetComponents<ElectricalComponent>())
            {
                component.DisconnectCircuit();
            }
        }

        #region Network Info Tooltip
        public override PassiveTooltip GetPassiveTooltip(Collider? hitCollider)
        {
            if (hitCollider?.gameObject == gameObject)
            {
                return base.GetPassiveTooltip(hitCollider);
            }

            Tooltip.ToolTipStringBuilder.Clear();

            foreach (var component in GetComponents<ElectricalComponent>())
            {
                component.BuildPassiveToolTip(Tooltip.ToolTipStringBuilder);
            }

            PassiveTooltip result = new PassiveTooltip(toDefault: true)
            {
                Title = DisplayName,
                Extended = Tooltip.ToolTipStringBuilder.ToString(),
            };

            return result;
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
