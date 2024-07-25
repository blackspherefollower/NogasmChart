using Newtonsoft.Json.Converters;
using System;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using System.Globalization;

namespace NogasmChart
{
    [JsonConverter(typeof(NogasmChartPropertyConverter))]
    internal class NogasmChartProperty
    {
        internal enum NogasmChartPropertyType
        {
            INT,
            DOUBLE,
            STRING
        }

        internal string Name;
        internal NogasmChartPropertyType PropertyType;

        internal NogasmChartProperty(string name, NogasmChartPropertyType pType)
        {
            Name = name;
            PropertyType = pType;
        }

        internal virtual int IntValue
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        internal virtual double DoubleValue
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        internal virtual string StringValue
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    internal class NogasmChartIntProperty : NogasmChartProperty
    {
        private int defaultValue;
        private int minValue;
        private int maxValue;

        internal NogasmChartIntProperty(string name, int defaultValue, int minValue, int maxValue) : base(name, NogasmChartPropertyType.INT)
        {
            this.defaultValue = defaultValue;
            this.minValue = minValue;
            this.maxValue = maxValue;
            IntValue = defaultValue;
        }

        internal override int IntValue { get; set; }
    }

    internal class NogasmChartDoubleProperty : NogasmChartProperty
    {
        private double defaultValue;
        private double minValue;
        private double maxValue;

        internal NogasmChartDoubleProperty(string name, double defaultValue, double minValue, double maxValue) : base(name, NogasmChartPropertyType.DOUBLE)
        {
            this.defaultValue = defaultValue;
            this.minValue = minValue;
            this.maxValue = maxValue;
            DoubleValue = defaultValue;
        }
        internal override double DoubleValue { get; set; }
    }

    internal class NogasmChartStringProperty : NogasmChartProperty
    {
        private string defaultValue;

        internal NogasmChartStringProperty(string name, string defaultValue) : base(name, NogasmChartPropertyType.STRING)
        {
            this.defaultValue = defaultValue;
            StringValue = defaultValue;
        }

        internal override string StringValue { get; set; }
    }

    internal class NogasmChartPropertyConverter : JsonConverter<NogasmChartProperty>
    {
        public override void WriteJson(JsonWriter writer, NogasmChartProperty? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteRawValue("");
                return;
            }

            switch (value.PropertyType)
            {
                case NogasmChartProperty.NogasmChartPropertyType.INT:
                    writer.WriteValue(((NogasmChartIntProperty)value).IntValue.ToString());
                    break;
                case NogasmChartProperty.NogasmChartPropertyType.DOUBLE:
                    writer.WriteValue(((NogasmChartDoubleProperty)value).DoubleValue.ToString());
                    break;
                case NogasmChartProperty.NogasmChartPropertyType.STRING:
                    writer.WriteValue(((NogasmChartStringProperty)value).StringValue);
                    break;
            }
        }

        public override NogasmChartProperty? ReadJson(JsonReader reader, Type objectType, NogasmChartProperty? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return null;
        }
    }
}