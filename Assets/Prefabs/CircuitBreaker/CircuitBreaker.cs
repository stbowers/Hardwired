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

        public CircuitBreakerType Type;

        public double MaxCurrent = 20f;

        public double MinVoltage = 200f;

        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            DelayedActionInstance actionMessage = new()
            {
                ActionMessage = interactable.ContextualName
            };

            switch (interactable.Action)
            {
                case InteractableType.Button1:
                case InteractableType.Button2:
                    if (Type == CircuitBreakerType.OverCurrent)
                    {
                        actionMessage.ExtendedMessage = $"Max current: {MaxCurrent.ToStringPrefix("A", "yellow")}";
                    }
                    else
                    {
                        actionMessage.ExtendedMessage = $"Min voltage: {MinVoltage.ToStringPrefix("V", "yellow")}";
                    }

                    if (!doAction) { return actionMessage.Succeed(); }

                    if (GameManager.RunSimulation && Breaker != null && Type == CircuitBreakerType.OverCurrent)
                    {
                        MaxCurrent += interactable.Action == InteractableType.Button1 ? 1f : -1f;
                        MaxCurrent = Math.Clamp(MaxCurrent, 0f, 20f);
                    }
                    else if (GameManager.RunSimulation && Breaker != null && Type == CircuitBreakerType.UnderVoltage)
                    {
                        MinVoltage += interactable.Action == InteractableType.Button1 ? 5f : -5f;
                        MinVoltage = Math.Clamp(MinVoltage, 0f, 1000f);
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

            if (Type == CircuitBreakerType.OverCurrent && Breaker?.Current.Magnitude > MaxCurrent)
            {
                OnOff = false;
                Breaker.Closed = false;
            }
            else if (Type == CircuitBreakerType.UnderVoltage && Breaker?.VoltageGround.Magnitude < MinVoltage)
            {
                OnOff = false;
                Breaker.Closed = false;
            }
        }

        public enum CircuitBreakerType
        {
            /// <summary>
            /// Trips if the current goes above the set value
            /// </summary>
            OverCurrent,

            /// <summary>
            /// Trips if the voltage goes below the set value
            /// </summary>
            UnderVoltage
        }
    }
}
