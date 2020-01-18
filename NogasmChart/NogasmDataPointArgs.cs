using System;

namespace NogasmChart
{
    public class NogasmDataPointArgs : EventArgs
    {
        public long TimeOffset;
        public double CurrentPressure;
        public double AvaeragePressure;
        public double MotorSpeed;

        public NogasmDataPointArgs(long aTimeOffset, double aCurrentPressure, double aAvaeragePressure, double aMotorSpeed)
        {
            TimeOffset = aTimeOffset;
            CurrentPressure = aCurrentPressure;
            AvaeragePressure = aAvaeragePressure;
            MotorSpeed = aMotorSpeed;
        }
    }
}