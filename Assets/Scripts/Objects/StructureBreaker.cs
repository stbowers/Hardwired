#nullable enable

using Assets.Scripts;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;
using Objects.Pipes;

namespace Hardwired.Objects
{
    public class StructureBreaker : Assets.Scripts.Objects.Pipes.Device
    {
        public Breaker? Breaker;

        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            if (interactable.Action == InteractableType.Button1)
            {
                if (doAction && GameManager.RunSimulation && Breaker != null)
                {
                    Breaker.Closed = !Breaker.Closed;
                }

                return DelayedActionInstance.Success(interactable.ContextualName);
            }
            return base.InteractWith(interactable, interaction, doAction);
        }
    }
}
