using Hardwired.Utility;

namespace Hardwired.UnitTests;

public class MNASolverTests
{
    [Fact]
    public void Test1()
    {
        var solver = new MNASolver();

        solver.Initialize(2, 1);

        solver.AddResistance(0, 1, 100.0);
        solver.AddResistance(null, 1, 1000.0);

        solver.SetVoltage(0, 0, 24.0);

        solver.Solve();

        var v1 = solver.GetVoltage(1).Magnitude;
        var i0 = solver.GetCurrent(0).Magnitude;
        Assert.Equal(0.021818, i0, 0.000001);
    }

    [Fact]
    public void Test2()
    {
        var solver = new MNASolver();

        solver.Initialize(4, 1);

        solver.AddResistance(0, 1, 10.0);
        solver.AddResistance(1, 3, 20.0);
        solver.AddResistance(null, 3, 100.0);
        solver.AddResistance(null, 3, 40.0);

        solver.AddResistance(1, 2, 35.0);
        solver.AddResistance(null, 2, 85.0);

        solver.SetVoltage(0, 0, 24.0);

        solver.Solve();

        var v1 = solver.GetVoltage(1).Magnitude;
        var v2 = solver.GetVoltage(2).Magnitude;
        var v3 = solver.GetVoltage(3).Magnitude;
        var i0 = solver.GetCurrent(0).Magnitude;
        Assert.Equal(18.6159, v1, 0.0001);
        Assert.Equal(13.1863, v2, 0.0001);
        Assert.Equal(10.9505, v3, 0.0001);
        Assert.Equal(0.5384, i0, 0.0001);
    }
}
