#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Hardwired.Simulation.Electrical;
using Util.Commands;

namespace Hardwired
{
    public partial class Hardwired
    {
        private static void RegisterCommands()
        {
            CommandLine.AddCommand("hardwired-print-debug", new PrintDebugInfoCommand());
        }

        private class PrintDebugInfoCommand : CommandBase
        {
            private static readonly string MSG_SUCCESS = "Success!";

            public override string HelpText => "Prints debug info about a Hardwired circuit to the log (level = 0: basic info, 1: ... & component list, 2: ... & A matrix)";
            public override string[] Arguments { get; } = new[] { "id", "level" };
            public override bool IsLaunchCmd => false;

            public override string Execute(string[] args)
            {
                if (args.Length < 1)
                {
                    return ERROR_INVALID_SYNTAX;
                }

                if (!Get(args, 0, "id", out int circuitId))
                {
                    return ERROR_INVALID_ARGUMENTS;
                }

                if (!Get(args, 0, "level", out int level))
                {
                    return ERROR_INVALID_ARGUMENTS;
                }

                if (!TryFindCircuit(circuitId, out var circuit))
                {
                    return $"Circuit not found: {circuitId}";
                }
                
                LogDebug($"===== Circuit {circuitId} Debug Info =====");
                LogDebug($"Frequency: {circuit.Frequency}");
                LogDebug($"Unknowns: {circuit.Solver.Unknowns.Count}");
                LogDebug($"Det(A): {circuit.Solver.A.Determinant()}");

                if (level < 1) { return MSG_SUCCESS; }

                LogDebug($"--- Components: {circuit.Components.Count} ---");

                foreach (var component in circuit.Components)
                {
                    var a = circuit.GetNode(component, component.PinA);
                    var b = circuit.GetNode(component, component.PinB);
                    var vA = circuit.Solver.GetValue(a);
                    var vB = circuit.Solver.GetValue(b);

                    LogDebug($"  {component.GetType()} -- {component.DebugInfo()}");
                }

                if (level < 2) { return MSG_SUCCESS; }

                LogDebug($"--- A Matrix ---\n{circuit.Solver.A}");

                return MSG_SUCCESS;
            }

            private static bool TryFindCircuit(int circuitId, [NotNullWhen(true)] out Circuit? circuit)
            {
                foreach (var weakRef in Circuit._allCircuits.ToList())
                {
                    // Check if reference is still alive
                    if (weakRef.TryGetTarget(out circuit))
                    {
                        // Check if id matches, and if so return this circuit, otherwise keep checking
                        if (circuit.Id == circuitId) { return true; }
                    }
                    // If not, remove from the list and keep checking
                    else
                    {
                        Circuit._allCircuits.Remove(weakRef);
                    }
                }

                circuit = null;
                return false;
            }
        }
    }
}
