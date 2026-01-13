#nullable enable

using System;
using System.Diagnostics;
using System.Threading;

namespace Hardwired.Utility.Extensions
{
    public static class StopwtachExtensions
    {
        public static StopwatchScope BeginScope(this Stopwatch stopwatch)
            => new StopwatchScope(stopwatch);
        
        public struct StopwatchScope : IDisposable
        {
            public Stopwatch Stopwatch { get; }

            public StopwatchScope(Stopwatch stopwatch)
            {
                Stopwatch = stopwatch;
                Stopwatch.Start();
            }

            public void Dispose()
            {
                Stopwatch.Stop();
            }
        }
    }
}