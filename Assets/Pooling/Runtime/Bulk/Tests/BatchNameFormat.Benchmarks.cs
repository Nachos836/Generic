using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Pooling.Bulk.Tests
{
    // ReSharper disable once InconsistentNaming
    internal sealed class BatchNameFormat_Benchmarks
    {
        [Performance]
        [Test]
        public void CreateNameFormat([Values("TestPrefab")] string name, [Values(42)] int number)
        {
            Measure.Method(() =>
            {
                _ = $"[Pool bucket: {name}, {number}]";

            }).WarmupCount(10)
            .MeasurementCount(10)
            .IterationsPerMeasurement(5)
            .GC()
            .Run();
        }
    }
}
