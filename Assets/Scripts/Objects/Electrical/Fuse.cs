#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using Objects.Pipes;
using UnityEngine;

namespace Hardwired.Objects.Electrical
{
    public class Fuse : ElectricalComponent
    {
        private CableFuse? _cableFuse;
        private int _ticksOverCurrent;

        protected CableFuse? CableFuse => _cableFuse ??= GetComponent<CableFuse>();

        protected Cable? MountedTo => CableFuse?.SmallCell?.Cable;

        public double CurrentLimit = 0;

        public double Current = 0;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"I: {Current.ToStringPrefix("A", "yellow")} (I_max: {CurrentLimit.ToStringPrefix("A", "yellow")})");
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            var hardwiredCable = MountedTo?.GetComponent<HardwiredCable>();
            if (hardwiredCable == null)
            {
                Current = 0;
                return;
            }

            Current = hardwiredCable.Current;

            if (Current > CurrentLimit)
            {
                _ticksOverCurrent += 1;
            }
            else
            {
                _ticksOverCurrent = 0;
            }

            // Break fuse if over current for more than one tick
            if (_ticksOverCurrent > 1)
            {
                CableFuse?.Break();
            }
        }
    }
}
