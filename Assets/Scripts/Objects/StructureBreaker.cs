#nullable enable

using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
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
                if (!doAction)
                {
                    return DelayedActionInstance.Success(interactable.ContextualName);
                }

                if (GameManager.RunSimulation && Breaker != null)
                {
                    Breaker.Closed = !Breaker.Closed;
                }

                OnServer.Interact(interactable, (Breaker?.Closed == true) ? 1 : 0);
                return DelayedActionInstance.Success(interactable.ContextualName);
            }
            return base.InteractWith(interactable, interaction, doAction);
        }
    }
}
