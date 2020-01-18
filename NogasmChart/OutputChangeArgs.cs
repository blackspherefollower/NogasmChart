using System;

namespace NogasmChart
{
    public class OutputChangeArgs
    {
        public double Intensity;

        public OutputChangeArgs(double v)
        {
            // Enforce range of 0-1
            Intensity = Math.Max(Math.Min(v, 1), 0);
        }
    }
}