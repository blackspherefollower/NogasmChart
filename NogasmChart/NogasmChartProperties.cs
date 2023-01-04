using System.Collections.Concurrent;

namespace NogasmChart
{
    internal class NogasmChartProperties : ConcurrentDictionary<string, NogasmChartProperty>
    {
        private static NogasmChartProperties _default = new NogasmChartProperties();
        public static NogasmChartProperties Default => _default;

        NogasmChartProperties()
        {
            TryAdd("LinearDurationMin", new NogasmChartIntProperty("LinearDurationMin", 200, 0, 5000));
            TryAdd("LinearDurationMax", new NogasmChartIntProperty("LinearDurationMax", 1000, 0, 5000));
            TryAdd("LinearPositionMin", new NogasmChartDoubleProperty("LinearPositionMin", 0.0, 0.0, 1.0));
            TryAdd("LinearPositionMax", new NogasmChartDoubleProperty("LinearPositionMax", 1.0, 0.0, 1.0));
            TryAdd("LinearSpeedThreshold", new NogasmChartDoubleProperty("LinearSpeedThreshold", 0.01, 0.0, 1.0));
            TryAdd("LinearTimeMultiplier", new NogasmChartDoubleProperty("LinearTimeMultiplier", 1.5, 0.0, 10.0));
        }

        internal void Reload()
        {

        }

        internal void Save()
        {

        }
    }
}