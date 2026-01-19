#nullable enable

using System;
using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using Objects.Pipes;

namespace Hardwired.Prefabs.CircuitBreaker
{
    public class CircuitBreaker : Assets.Scripts.Objects.Pipes.Device
    {
        private static readonly string STATE_CLOSED = "closed".AsColor("green");
        private static readonly string STATE_OPEN = "open".AsColor("red");

        public Breaker? Breaker;

        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            DelayedActionInstance actionMessage = new()
            {
                ActionMessage = interactable.ContextualName
            };

            switch (interactable.Action)
            {
                case InteractableType.Button1:
                    var state = Breaker?.Closed == true ? STATE_CLOSED : STATE_OPEN;
                    actionMessage.ExtendedMessage = $"State: {state}";

                    if (!doAction) { return actionMessage.Succeed(); }

                    if (GameManager.RunSimulation && Breaker != null)
                    {
                        Breaker.Closed = !Breaker.Closed;
                    }

                    return actionMessage.Succeed();

                case InteractableType.Button2:
                case InteractableType.Button3:
                    var maxCurrent = Breaker?.MaxCurrent ?? 0f;
                    actionMessage.ExtendedMessage = $"Max current: {maxCurrent.ToStringPrefix("A", "yellow")}";

                    if (!doAction) { return actionMessage.Succeed(); }

                    if (GameManager.RunSimulation && Breaker != null)
                    {
                        Breaker.MaxCurrent += interactable.Action == InteractableType.Button2 ? 1f : -1f;
                        Breaker.MaxCurrent = Math.Clamp(Breaker.MaxCurrent, 0f, 20f);
                    }

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

            OnServer.Interact(InteractButton1, (Breaker?.Closed == true) ? 1 : 0);
        }
    }
}
