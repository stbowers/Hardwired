#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Objects.Pipes;
using Objects.Rockets;

namespace Hardwired.Networks
{
    /// <summary>
    /// Hardwired's replacement for the base game PowerTick class.
    /// </summary>
    public partial class HardwiredPowerTick : PowerTick
    {
        public CircuitTick? CircuitTick { get; protected set; }

        public int CurrentTick { get; protected set; } = 0;

        public void SetCircuit(CircuitTick? circuitTick)
        {
            CircuitTick = circuitTick;
            CurrentTick = 0;
        }

        public new void Initialise(CableNetwork cableNetwork)
        {
            CableNetwork = cableNetwork;
            if (CircuitTick == null)
            {
                SetCircuit(new());
            }
        }

        public new void CalculateState()
        {
            CurrentTick += 1;

            if (CircuitTick?.CurrentTick < CurrentTick)
            {
                CircuitTick.ProcessTick(CableNetwork);
            }
        }

        public new void ApplyState()
        {
            CurrentTick = CircuitTick?.CurrentTick ?? 0;

            // Note - some methods (notably Device.AssessPower()) use the potential/consumed
            // power to manage their state (for example, turning off if there isn't enough power
            // in the network).
            // We want to manage that with our own logic, so we'll just say there's always
            // enough power here...
            //
            // If, however, we set `Potential = float.MaxValue`, then some devices like the APC
            // will take that to mean there is infinite power available, and will report `float.MaxValue`
            // from `GetGeneratedPower()` (or, potentially, overflow to a negative value).
            // 
            // Therefore we set `Potential = 0`, so devices don't assume there's extra power available,
            // but `Consumed = float.MinValue` so that devices never assume all the power was
            // taken (i.e. `Potential - Consumed` should always be greater than whatever power they want
            // to draw)...
            //
            // This is really gross, and I wish there was a better way to do this... It may be worth in
            // the future investigating setting these to actual values, and seeing if we can "play nice"
            // with the built-in electrical system.
            Required = 0;
            Consumed = float.MinValue;
            Potential = 0;
        }
    }
}