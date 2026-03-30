using System;
using NationalInstruments;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.SpecAnMX;
using NationalInstruments.RFmx.WlanMX;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public sealed class RfmxWlanSignalAnalyzer : IRfSignalAnalyzer
    {
        private RFmxInstrMX _instrSession;
        private RFmxWlanMX _wlan;
        private bool _isOpen;

        private double _centerFrequencyHz;
        private double _channelBandwidthHz;
        private double _specAnSpanHz;
        private double _specAnRbwHz;

        public void Open(string resourceName)
        {
            _instrSession = new RFmxInstrMX(resourceName, string.Empty);
            _wlan = _instrSession.GetWlanSignalConfiguration();
            _isOpen = true;
        }

        public void ConfigureOfdmModAcc(
            double centerFrequencyHz,
            double channelBandwidthHz,
            double specAnSpanHz,
            double specAnResolutionBandwidthHz,
            RFmxWlanMXStandard wlanStandard)
        {
            EnsureOpen();

            _centerFrequencyHz = centerFrequencyHz;
            _channelBandwidthHz = channelBandwidthHz;
            _specAnSpanHz = specAnSpanHz;
            _specAnRbwHz = specAnResolutionBandwidthHz;

            _instrSession.ConfigureFrequencyReference(string.Empty, RFmxInstrMXConstants.OnboardClock, 10e6);
            _wlan.ConfigureFrequency(string.Empty, centerFrequencyHz);
            _wlan.ConfigureReferenceLevel(string.Empty, 0.0);
            _wlan.ConfigureExternalAttenuation(string.Empty, 0.0);

            _wlan.ConfigureDigitalEdgeTrigger(
                string.Empty,
                "PXI_Trig0",
                RFmxWlanMXDigitalEdgeTriggerEdge.Rising,
                0.0,
                true);

            _wlan.ConfigureStandard(string.Empty, wlanStandard);
            _wlan.ConfigureChannelBandwidth(string.Empty, channelBandwidthHz);
            _wlan.SelectMeasurements(string.Empty, RFmxWlanMXMeasurementTypes.OfdmModAcc, true);

            _wlan.OfdmModAcc.Configuration.ConfigureMeasurementLength(string.Empty, 0, 16);
            _wlan.OfdmModAcc.Configuration.ConfigureFrequencyErrorEstimationMethod(
                string.Empty,
                RFmxWlanMXOfdmModAccFrequencyErrorEstimationMethod.PreambleAndPilots);
            _wlan.OfdmModAcc.Configuration.ConfigureAmplitudeTrackingEnabled(
                string.Empty,
                RFmxWlanMXOfdmModAccAmplitudeTrackingEnabled.False);
            _wlan.OfdmModAcc.Configuration.ConfigurePhaseTrackingEnabled(
                string.Empty,
                RFmxWlanMXOfdmModAccPhaseTrackingEnabled.True);
            _wlan.OfdmModAcc.Configuration.ConfigureSymbolClockErrorCorrectionEnabled(
                string.Empty,
                RFmxWlanMXOfdmModAccSymbolClockErrorCorrectionEnabled.True);
            _wlan.OfdmModAcc.Configuration.ConfigureChannelEstimationType(
                string.Empty,
                RFmxWlanMXOfdmModAccChannelEstimationType.ReferenceAndData);
            _wlan.OfdmModAcc.Configuration.ConfigureAveraging(
                string.Empty,
                RFmxWlanMXOfdmModAccAveragingEnabled.False,
                1);
        }

        public double MeasureAverageRmsEvmDb()
        {
            return MeasureAverageRmsEvmWithConstellation().AverageRmsEvmDb;
        }

        public WlanModAccMeasureResult MeasureAverageRmsEvmWithConstellation()
        {
            EnsureOpen();

            const double timeoutSeconds = 10.0;

            ApplySpecAnAutoLevelReferenceToWlan();

            _wlan.OfdmModAcc.Configuration.AutoLevel(string.Empty, timeoutSeconds);
            _wlan.Initiate(string.Empty, string.Empty);

            _wlan.OfdmModAcc.Results.FetchCompositeRmsEvm(
                string.Empty,
                timeoutSeconds,
                out var compositeRmsEvmMeanDb,
                out _,
                out _);

            ComplexSingle[] constellation = null;
            _wlan.OfdmModAcc.Results.FetchDataConstellationTrace(
                string.Empty,
                timeoutSeconds,
                ref constellation);

            return new WlanModAccMeasureResult(compositeRmsEvmMeanDb, constellation ?? Array.Empty<ComplexSingle>());
        }

        private void ApplySpecAnAutoLevelReferenceToWlan()
        {
            RFmxSpecAnMX specAn = null;
            try
            {
                specAn = _instrSession.GetSpecAnSignalConfiguration();
                specAn.ConfigureFrequency(string.Empty, _centerFrequencyHz);
                specAn.ConfigureReferenceLevel(string.Empty, 0.0);
                specAn.Spectrum.Configuration.ConfigureSpan(string.Empty, _specAnSpanHz);
                specAn.Spectrum.Configuration.ConfigureRbwFilter(
                    string.Empty,
                    RFmxSpecAnMXSpectrumRbwAutoBandwidth.False,
                    _specAnRbwHz,
                    RFmxSpecAnMXSpectrumRbwFilterType.Gaussian);
                specAn.Spectrum.Configuration.ConfigurePowerUnits(
                    string.Empty,
                    RFmxSpecAnMXSpectrumPowerUnits.dBm);
                specAn.SelectMeasurements(string.Empty, RFmxSpecAnMXMeasurementTypes.Spectrum, true);

                const double autoLevelMeasurementIntervalSeconds = 2e-3;
                specAn.AutoLevel(
                    string.Empty,
                    _channelBandwidthHz,
                    autoLevelMeasurementIntervalSeconds,
                    out var referenceLevelDbm);

                _wlan.ConfigureReferenceLevel(string.Empty, referenceLevelDbm);
            }
            finally
            {
                specAn?.Dispose();
            }
        }

        public void Close()
        {
            try
            {
                _wlan?.Dispose();
            }
            finally
            {
                _wlan = null;
            }

            try
            {
                _instrSession?.Close();
            }
            finally
            {
                _instrSession = null;
                _isOpen = false;
            }
        }

        private void EnsureOpen()
        {
            if (!_isOpen || _instrSession == null || _wlan == null)
            {
                throw new InvalidOperationException("RFmx WLAN session is not open.");
            }
        }
    }
}
