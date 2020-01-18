using System;

namespace NogasmChart
{
    public class OrgasmDataPointArgs : EventArgs
    {
        public long TimeOffset;

        public OrgasmDataPointArgs(long aTimeOffset)
        {
            TimeOffset = aTimeOffset;
        }
    }
}