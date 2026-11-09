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
            // It may be worth in the future investigating setting these to actual values though...
            Required = 0;
            Consumed = 0;
            Potential = float.MaxValue;
        }
    }
}