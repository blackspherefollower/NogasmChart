using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

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
            TryAdd("LinearDurationDelayMultiplier", new NogasmChartDoubleProperty("LinearDurationDelayMultiplier", 1.0, -10.0, 10.0));
            TryAdd("LinearPositionMin", new NogasmChartDoubleProperty("LinearPositionMin", 0.05, 0.0, 1.0));
            TryAdd("LinearPositionMax", new NogasmChartDoubleProperty("LinearPositionMax", 0.95, 0.0, 1.0));
            TryAdd("LinearSpeedThreshold", new NogasmChartDoubleProperty("LinearSpeedThreshold", 0.01, 0.0, 1.0));

            Reload();
        }

        internal void Reload()
        {
            if (File.Exists(@"config.json"))
            {
                bool missing = false;
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        File.ReadAllText(@"config.json"));

                    foreach (var key in this.Keys)
                    {
                        if (dict.ContainsKey(key) && TryGetValue(key, out var value))
                        {
                            switch (value.PropertyType)
                            {
                                case NogasmChartProperty.NogasmChartPropertyType.INT:
                                    if (int.TryParse(dict[key], out var iv))
                                    {
                                        ((NogasmChartIntProperty)value).IntValue = iv;
                                    }
                                    else
                                    {
                                        missing = true;
                                    }

                                    break;
                                case NogasmChartProperty.NogasmChartPropertyType.DOUBLE:
                                    if (double.TryParse(dict[key], out var dv))
                                    {
                                        ((NogasmChartDoubleProperty)value).DoubleValue = dv;
                                    }
                                    else
                                    {
                                        missing = true;
                                    }

                                    break;
                                case NogasmChartProperty.NogasmChartPropertyType.STRING:
                                    ((NogasmChartStringProperty)value).StringValue = dict[key];
                                    break;
                            }
                        }
                        else
                            missing = true;
                    }
                }
                catch { missing = true; }

                if (missing)
                {
                    Save();
                }
            }
            else
            {
                // Create defaults
                Save();
            }
        }

        internal void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(@"config.json", json);
        }
    }
}