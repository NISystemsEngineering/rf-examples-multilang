using NationalInstruments;
using NationalInstruments.RFmx.WlanMX;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public sealed class SimulatedRfSignalGenerator : IRfSignalGenerator
    {
        public void Open(string resourceName)
        {
        }

        public void ConfigureCw(double centerFrequencyHz, double powerDbm, double externalAttenuationDb)
        {
            ConfigureCw(centerFrequencyHz, powerDbm, externalAttenuationDb, null);
        }

        public void ConfigureCw(
            double centerFrequencyHz,
            double powerDbm,
            double externalAttenuationDb,
            string waveformTdmsFilePath)
        {
            LoopbackSignalState.LastConfiguredFrequencyHz = centerFrequencyHz;
            LoopbackSignalState.LastConfiguredPowerDbm = powerDbm - externalAttenuationDb;
        }

        public void SetOutputPowerDbm(double powerDbm, double externalAttenuationDb)
        {
            LoopbackSignalState.LastConfiguredPowerDbm = powerDbm - externalAttenuationDb;
        }

        public void Initiate()
        {
        }

        public void Abort()
        {
        }

        public void Close()
        {
        }
    }

    public sealed class SimulatedRfSignalAnalyzer : IRfSignalAnalyzer
    {
        private double _centerFrequencyHz;
        private double _channelBandwidthHz;

        public void Open(string resourceName)
        {
        }

        public void ConfigureOfdmModAcc(
            double centerFrequencyHz,
            double channelBandwidthHz,
            double specAnSpanHz,
            double specAnResolutionBandwidthHz,
            RFmxWlanMXStandard wlanStandard)
        {
            _centerFrequencyHz = centerFrequencyHz;
            _channelBandwidthHz = channelBandwidthHz;
        }

        public double MeasureAverageRmsEvmDb()
        {
            return MeasureAverageRmsEvmWithConstellation().AverageRmsEvmDb;
        }

        public WlanModAccMeasureResult MeasureAverageRmsEvmWithConstellation()
        {
            var hasSignal = LoopbackSignalState.LastConfiguredFrequencyHz > 0.0
                && _centerFrequencyHz > 0.0
                && _channelBandwidthHz > 0.0;
            var evm = hasSignal ? -32.0 - LoopbackSignalState.LastConfiguredPowerDbm * 0.02 : -10.0;
            var n = 256;
            var buf = new ComplexSingle[n];
            var rnd = new System.Random(0);
            for (var i = 0; i < n; i++)
            {
                var re = (float)(rnd.NextDouble() - 0.5);
                var im = (float)(rnd.NextDouble() - 0.5);
                buf[i] = new ComplexSingle(re, im);
            }

            return new WlanModAccMeasureResult(evm, buf);
        }

        public void Close()
        {
        }
    }
}
