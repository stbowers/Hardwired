#nullable enable

using System;
using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using Objects.Pipes;

using Transformer = Hardwired.Objects.Electrical.Transformer;

namespace Hardwired.Prefabs.ACVoltageRegulator
{
    public class ACVoltageRegulator : Assets.Scripts.Objects.Pipes.Device
    {
        public Transformer? Transformer;

        public double TargetVoltage = 240;

        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            DelayedActionInstance actionMessage = new()
            {
                ActionMessage = interactable.ContextualName
            };

            switch (interactable.Action)
            {
                case InteractableType.Button1:
                    return actionMessage.Succeed();

                case InteractableType.Button2:
                case InteractableType.Button3:
                    return actionMessage.Succeed();
            }

            return base.InteractWith(interactable, interaction, doAction);
        }

        public override void UpdateStateVisualizer(bool visualOnly = false)
        {
            base.UpdateStateVisualizer(visualOnly);

            foreach (var interactable in Interactables)
            {
                interactable.CacheBounds();
            }
        }

        public override void OnPowerTick()
        {
            base.OnPowerTick();

            if (Transformer is null) { return; }

            var inputVoltage = Transformer.PrimaryVoltage.Magnitude;
            var outputVoltage = Transformer.PrimaryVoltage.Magnitude;

            var minStep = 0.06 * inputVoltage;

            var dV = Math.Abs(TargetVoltage - outputVoltage);

            // If output voltage is out of range, change the ratio of the transformer
            if (dV > minStep)
            {
                // Determine nearest "tap point", and winding ratio
                var tap = Math.Round(20 * (TargetVoltage / inputVoltage));
                var n = tap / 20;

                Transformer.Deinitialize();
                Transformer.N = n;
                Transformer.Initialize();
            }
        }
    }
}
