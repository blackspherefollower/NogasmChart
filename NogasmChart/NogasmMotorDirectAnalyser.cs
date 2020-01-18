using System;

namespace NogasmChart
{
    class NogasmMotorDirectAnalyser : INogasmInputAnalyser
    {
        public event EventHandler<OutputChangeArgs> OutputChange;

        readonly object _emitLock = new object();
        private long _last = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public void HandleNogasmData(object sender, NogasmDataPointArgs args)
        {
            lock (_emitLock)
            {
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (now - _last <= 10) return;
                _last = now;
                OutputChange?.Invoke(this, new OutputChangeArgs(args.MotorSpeed / 155));
            }

        }

        public void HandleOrgasmData(object sender, OrgasmDataPointArgs args)
        {
            lock (_emitLock)
            {
                // On orgasm run the output at max for 30 seconds
                _last = DateTimeOffset.Now.AddSeconds(30).ToUnixTimeMilliseconds();
                OutputChange?.Invoke(this, new OutputChangeArgs(1));
            }
        }
    }
}
