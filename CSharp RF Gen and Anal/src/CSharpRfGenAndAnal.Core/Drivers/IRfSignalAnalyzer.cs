using NationalInstruments.RFmx.WlanMX;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public interface IRfSignalAnalyzer
    {
        void Open(string resourceName);
        void ConfigureOfdmModAcc(
            double centerFrequencyHz,
            double channelBandwidthHz,
            double specAnSpanHz,
            double specAnResolutionBandwidthHz,
            RFmxWlanMXStandard wlanStandard);
        double MeasureAverageRmsEvmDb();
        WlanModAccMeasureResult MeasureAverageRmsEvmWithConstellation();
        void Close();
    }
}
