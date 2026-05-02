#nullable enable

using System;
using Assets.Scripts;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Objects.Electrical;
using Objects.Pipes;

namespace Hardwired.Prefabs.CircuitBreaker
{
    public class PowerConverter : Assets.Scripts.Objects.Pipes.Device
    {
        public PowerSink? PowerSink;

        public PowerSource? Generator;

        public double OutputVoltage = 200;

        public double Step = 50;

        public float Charge = 0;

        public float MaxCharge = 5000;

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
                    actionMessage.ExtendedMessage = $"Output Voltage: {OutputVoltage.ToStringPrefix("V", "yellow")} (charge: {Charge} Wt)";

                    if (!doAction) { return actionMessage.Succeed(); }

                    if (GameManager.RunSimulation)
                    {
                        OutputVoltage += interactable.Action == InteractableType.Button1 ? Step : -Step;
                        OutputVoltage = Math.Clamp(OutputVoltage, 0f, 500f);
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

            if (PowerSink != null && PowerSink.PowerProfiles.Count >= 1)
            {
                PowerSink.PowerProfiles[0].VoltageMaximum = 1.5 * OutputVoltage;
                PowerSink.PowerProfiles[0].VoltageNominal = OutputVoltage;
                PowerSink.PowerProfiles[0].VoltageMinimum = OutputVoltage;
            }

            if (Generator != null)
            {
                Generator.PowerProfile.VoltageNominal = OutputVoltage;
            }

            OnOff = true;
        }

        public override float GetUsedPower(CableNetwork cableNetwork)
        {
            UsedPower = Math.Max(0, MaxCharge - Charge);
            return UsedPower;
        }

        public override float GetGeneratedPower(CableNetwork? cableNetwork)
        {
            return Charge;
        }

        public override void ReceivePower(CableNetwork cableNetwork, float powerAdded)
        {
            Charge = Math.Min(Charge + powerAdded, MaxCharge);
        }

        public override void UsePower(CableNetwork cableNetwork, float powerUsed)
        {
            Charge = Math.Max(Charge - powerUsed, 0);
        }
    }
}
