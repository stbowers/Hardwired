using System.Numerics;
using Hardwired.Utility;

namespace Hardwired.UnitTests;

public class MNASolverTests
{
    public static List<object[]> ExampleCircuits = new()
    {
        // Custom example 1 - 24V voltage source, 2 resistors in series
        new object[]
        {
            new CircuitDescription()
            {
                Nodes = 2,
                Admittances =
                {
                    (0, 1, 1.0 / 100.0),
                    (null, 1, 1.0 / 1000.0)
                },
                VoltageSources =
                {
                    (0, 0, 24.0)
                },
            },
            new ExpectedOutputs()
            {
                NodeVoltages = { 24.0, 21.8181 },
                VoltageSourceCurrents = { -0.021818 }
            }
        },

        // Custom example 2
        new object[]
        {
            new CircuitDescription()
            {
                Nodes = 4,
                Admittances =
                {
                    (0, 1, 1.0 / 10.0),
                    (1, 3, 1.0 / 20.0),
                    (null, 3, 1.0 / 100.0),
                    (null, 3, 1.0 / 40.0),

                    (1, 2, 1.0 / 35.0),
                    (null, 2, 1.0 / 85.0),
                },
                VoltageSources =
                {
                    (0, 0, 24.0)
                },
            },
            new ExpectedOutputs()
            {
                NodeVoltages = { 24.0, 18.6159, 13.1863, 10.9505 },
                VoltageSourceCurrents = { -0.5384 }
            }
        },

        // Custom example 2 - 24V voltage source, 1 resistor in series, 1 current source (based on example that wasn't working in game)
        new object[]
        {
            new CircuitDescription()
            {
                Nodes = 2,
                Admittances =
                {
                    // 400 ohm resistor betwen voltage source and current source
                    (0, 1, 1.0 / 400.0),
                },
                CurrentSources =
                {
                    // 0.05 A load from node 1 to ground
                    (1, null, 0.05),
                },
                VoltageSources =
                {
                    // 24 V source connected to node 0
                    (0, 0, 24.0)
                },
            },
            new ExpectedOutputs()
            {
                NodeVoltages = { 24.0, 4 },
                VoltageSourceCurrents = { -0.05 }
            }
        },

        // OpenStax University Physics II - Example 15.4.1 - RLC series circuit
        new object[]
        {
            new CircuitDescription()
            {
                Nodes = 3,
                Frequency = 200,
                Admittances =
                {
                    // 4 ohm resistor
                    (0, 1, new Complex(1f / 4f, 0)),
                    // 3 mH inductor
                    (1, 2, new Complex(0, 1f / (2f * Math.PI * 200 * 0.003))),
                    // 0.8 mF capacitor
                    (2, null, new Complex(0, -2f * Math.PI * 200 * 0.0008)),
                },
                VoltageSources =
                {
                    // 0.1 V AC @ 200 Hz
                    (0, 0, 0.1)
                },
            },
            new ExpectedOutputs()
            {
                NodeVoltages = { 0.1, new Complex(0.03249, -0.0468), new Complex(-0.01164, 0.016787) },
                VoltageSourceCurrents = { -Complex.FromPolarCoordinates(0.0205, 0.607) }
            }
        },
    };

    [Theory]
    [MemberData(nameof(ExampleCircuits))]
    public void CanSolveExampleCircuits(CircuitDescription circuit, ExpectedOutputs expectedOutputs)
    {
        var solver = new MNASolver();

        solver.Initialize(circuit.Nodes, circuit.VoltageSources.Count, circuit.Frequency);

        foreach ((int? n, int? m, Complex a) in circuit.Admittances)
        {
            solver.AddAdmittance(n, m, a);
        }

        foreach ((int? n, int? m, Complex i) in circuit.CurrentSources)
        {
            solver.SetCurrent(n, m, i);
        }

        foreach ((int n, int v, Complex e) in circuit.VoltageSources)
        {
            solver.InitializeVoltageSource(null, n, v);
            solver.SetVoltage(v, e);
        }

        solver.Solve();

        // Check outputs
        for (int n = 0; n < expectedOutputs.NodeVoltages.Count; n++)
        {
            Complex expected = expectedOutputs.NodeVoltages[n];
            Complex actual = solver.GetVoltage(n);

            Assert.Equal(expected.Real, actual.Real, 0.001);
            Assert.Equal(expected.Imaginary, actual.Imaginary, 0.001);
        }

        for (int v = 0; v < expectedOutputs.VoltageSourceCurrents.Count; v++)
        {
            Complex expected = expectedOutputs.VoltageSourceCurrents[v];
            Complex actual = solver.GetCurrent(v);

            Assert.Equal(expected.Real, actual.Real, 0.001);
            Assert.Equal(expected.Imaginary, actual.Imaginary, 0.001);
        }
    }

    public class CircuitDescription
    {
        public int Nodes { get; set; }
        public double Frequency { get; set; }
        public List<(int? n, int? m, Complex a)> Admittances { get; } = new();
        public List<(int? n, int? m, Complex i)> CurrentSources { get; } = new();
        public List<(int n, int v, Complex e)> VoltageSources { get; } = new();
    }

    public class ExpectedOutputs
    {
        public List<Complex> NodeVoltages { get; } = new();
        public List<Complex> VoltageSourceCurrents { get; } = new();
    }
}
