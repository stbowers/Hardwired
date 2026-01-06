#nullable enable

using System;
using Assets.Scripts.Networks;

namespace Hardwired.Networks
{
    /// <summary>
    /// Hardwired's replacement for the base game PowerTick class.
    /// </summary>
    public partial class HardwiredPowerTick : PowerTick
    {
        public new void Initialise(CableNetwork cableNetwork)
        {
            Hardwired.LogDebug($"Patched PowerTick.Initialise()!");
            base.Initialise(cableNetwork);
        }

        public new void CalculateState()
        {
            Hardwired.LogDebug($"Patched PowerTick.CalculateState()!");
            base.CalculateState();
        }
    }
}