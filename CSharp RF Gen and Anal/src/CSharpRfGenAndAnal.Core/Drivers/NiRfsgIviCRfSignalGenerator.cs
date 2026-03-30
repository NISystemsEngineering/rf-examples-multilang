using System;
using NationalInstruments.ModularInstruments.NIRfsg;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public sealed class NiRfsgIviCRfSignalGenerator : IRfSignalGenerator
    {
        private const string WaveformName = "waveform";
        private static readonly string DefaultTdmsFilePath =
            @"C:\Charlie\WLAN_be_320M_MCS13_11p2_PAPR_14Symbols.tdms";
        private const string ScriptText = @"script GenWaveform
    repeat forever
        generate waveform marker0(0)
    end repeat
end script";

        private NIRfsg _session;
        private bool _isOpen;
        private double _centerFrequencyHz;
        private double _externalAttenuationDb;

        public void Open(string resourceName)
        {
            _session = new NIRfsg(resourceName, true, false, string.Empty);
            _isOpen = true;
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
            EnsureOpen();

            _centerFrequencyHz = centerFrequencyHz;
            _externalAttenuationDb = externalAttenuationDb;
            var appliedPowerDbm = powerDbm - externalAttenuationDb;
            _session.RF.Configure(centerFrequencyHz, appliedPowerDbm);
            _session.FrequencyReference.Configure(RfsgFrequencyReferenceSource.OnboardClock, 10e6);
            _session.RF.ExternalGain = -externalAttenuationDb;
            _session.RF.PowerLevelType = RfsgRFPowerLevelType.PeakPower;

            var tdmsPath = string.IsNullOrWhiteSpace(waveformTdmsFilePath)
                ? DefaultTdmsFilePath
                : waveformTdmsFilePath.Trim();
            _session.Arb.ReadAndDownloadWaveformFromFileTdms(WaveformName, tdmsPath, 0);
            _session.Arb.GenerationMode = RfsgWaveformGenerationMode.Script;
            _session.Arb.Scripting.WriteScript(ScriptText);

            _session.Utility.ExportSignal(
                RfsgSignalType.MarkerEvent,
                RfsgSignalIdentifier.Marker0,
                RfsgOutputTerminal.PxiTriggerLine0);

            LoopbackSignalState.LastConfiguredFrequencyHz = centerFrequencyHz;
            LoopbackSignalState.LastConfiguredPowerDbm = appliedPowerDbm;
        }

        public void SetOutputPowerDbm(double powerDbm, double externalAttenuationDb)
        {
            EnsureOpen();
            _externalAttenuationDb = externalAttenuationDb;
            var appliedPowerDbm = powerDbm - externalAttenuationDb;

            _session.Abort();
            _session.RF.Configure(_centerFrequencyHz, appliedPowerDbm);
            _session.RF.ExternalGain = -externalAttenuationDb;
            _session.RF.OutputEnabled = true;
            _session.Utility.Commit();
            _session.Initiate();

            LoopbackSignalState.LastConfiguredPowerDbm = appliedPowerDbm;
        }

        public void Initiate()
        {
            EnsureOpen();
            _session.RF.OutputEnabled = true;
            _session.Utility.Commit();
            _session.Initiate();
        }

        public void Abort()
        {
            if (!_isOpen)
            {
                return;
            }

            _session.Abort();
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _session.RF.OutputEnabled = false;
            _session.Utility.Commit();
            _session.Arb.ClearWaveform(WaveformName);
            _session.Close();

            _isOpen = false;
            _session = null;
        }

        private void EnsureOpen()
        {
            if (!_isOpen)
            {
                throw new InvalidOperationException("NI-RFSG session is not open.");
            }
        }
    }
}
