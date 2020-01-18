using System;

namespace NogasmChart
{
    internal interface IInputAnalyser
    {
        event EventHandler<OutputChangeArgs> OutputChange;

        void HandleOrgasmData(object sender, OrgasmDataPointArgs args);
    }
}