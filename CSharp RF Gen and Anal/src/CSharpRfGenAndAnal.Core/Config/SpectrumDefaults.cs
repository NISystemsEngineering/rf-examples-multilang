namespace CSharpRfGenAndAnal.Core.Config
{
    public static class SpectrumDefaults
    {
        /// <summary>SpecAn span: at least 400 MHz, or 1.25× channel bandwidth (whichever is larger).</summary>
        public static double SpecAnSpanHz(double channelBandwidthHz)
        {
            return System.Math.Max(400e6, channelBandwidthHz * 1.25);
        }
    }
}
