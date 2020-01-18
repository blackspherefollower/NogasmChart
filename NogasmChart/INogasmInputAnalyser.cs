namespace NogasmChart
{
    internal interface INogasmInputAnalyser : IInputAnalyser
    {
        void HandleNogasmData(object sender, NogasmDataPointArgs args);
    }
}