using System;

namespace NogasmChart
{
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

        internal NogasmChartDoubleProperty(string name, double defaultValue, double minValue, double maxValue) : base(name, NogasmChartPropertyType.INT)
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

        internal NogasmChartStringProperty(string name, string defaultValue) : base(name, NogasmChartPropertyType.INT)
        {
            this.defaultValue = defaultValue;
            StringValue = defaultValue;
        }

        internal override string StringValue { get; set; }
    }
}