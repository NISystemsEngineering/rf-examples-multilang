using System;
using System.Numerics;
using NationalInstruments;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.NRMX;

namespace NrSweepDemo
{
    /// <summary>
    /// RFmx NR ModAcc acquisition (rfmx.mdc): analyzer session, dual auto-level, composite RMS EVM, PDSCH constellation.
    /// </summary>
    public sealed class RfmxNrAnalyzer : IDisposable
    {
        private RFmxInstrMX _instr;
        private RFmxNRMX _nr;
        private const string SignalName = "nr";

        public void Open(string resourceName)
        {
            Dispose();
            _instr = new RFmxInstrMX(resourceName, "DriverSetup= ");
            _nr = _instr.GetNRSignalConfiguration(SignalName);
        }

        public void ConfigureAnalyzer(
            double centerFrequencyHz,
            double channelBandwidthHz,
            RFmxInstrMXFrequencyReferenceSource freqRef,
            RFmxNRMXLinkDirection linkDirection)
        {
            _instr.SetFrequencyReferenceSource("", freqRef);
            // VST RF input — adjust if your RF path / port name differs.
            _nr.SetSelectedPorts("", "rf/0");
            _nr.SetCenterFrequency("", centerFrequencyHz);
            _nr.SetLinkDirection("", linkDirection);
            _nr.SetChannelBandwidth("", channelBandwidthHz);
            // Demo default; match your waveform if needed.
            _nr.SetSubcarrierSpacing("", RFmxNRMXSubcarrierSpacing.Hz30000);

            _nr.SelectMeasurements("", RFmxNRMXMeasurementTypes.ModAcc, true);
            _nr.ModAcc.Configuration.SetEvmUnit("", RFmxNRMXModAccEvmUnit.Percentage);
            _nr.ModAcc.Configuration.SetMeasurementLength("", 1e-3);

            _nr.SetStartTriggerType("", RFmxNRMXStartTriggerType.DigitalEdge);
            _nr.SetDigitalEdgeStartTriggerSource("", RFmxNRMXDigitalEdgeStartTriggerSource.RfsgStartTrigger);
        }

        /// <summary>Standard RFmx NR auto-level (input power / reference level).</summary>
        public void RunStandardAutoLevel(double timeoutSeconds)
        {
            _nr.Commit("");
            _nr.AutoLevel("", timeoutSeconds);
        }

        /// <summary>OFDM ModAcc auto-level (min composite RMS EVM vs reference level).</summary>
        public void RunModAccAutoLevel(double timeoutSeconds)
        {
            _nr.Commit("");
            _nr.ModAcc.Configuration.AutoLevel("", timeoutSeconds);
        }

        public void CommitConfiguration()
        {
            _nr.Commit("");
        }

        public void InitiateMeasurement()
        {
            _nr.Initiate("");
        }

        public void WaitForMeasurementComplete(double timeoutSeconds)
        {
            _nr.WaitForMeasurementComplete("", timeoutSeconds);
        }

        /// <summary>Composite RMS and peak EVM (%).</summary>
        public void FetchCompositeEvm(double timeoutSeconds, out double rmsPercent, out double peakPercent)
        {
            _nr.ModAcc.Results.FetchCompositeEvm("", timeoutSeconds, out rmsPercent, out peakPercent);
        }

        public Complex[] FetchPdschConstellation(double timeoutSeconds)
        {
            var raw = Array.Empty<ComplexSingle>();
            _nr.ModAcc.Results.FetchPdschDataConstellationTrace("", timeoutSeconds, ref raw);
            if (raw == null || raw.Length == 0)
                return Array.Empty<Complex>();

            var pts = new Complex[raw.Length];
            for (var i = 0; i < raw.Length; i++)
                pts[i] = new Complex(raw[i].Real, raw[i].Imaginary);
            return pts;
        }

        public void Dispose()
        {
            try
            {
                _nr = null;
                _instr?.Close();
            }
            catch
            {
                // Demo: ignore dispose errors.
            }
            finally
            {
                _instr = null;
            }
        }
    }
}
