using System;
using NationalInstruments.ModularInstruments.NIRfsg;
using NationalInstruments.ModularInstruments.NIRfsgPlayback;

namespace NrSweepDemo
{
    /// <summary>
    /// NI-RFSG + Playback (rfsg.mdc): load TDMS NR waveform, set ref/frequency, run continuously; sweep power by updating RF.PowerLevel only.
    /// </summary>
    public sealed class RfsgNrGenerator : IDisposable
    {
        private NIRfsg _rfsg;
        private const string WaveformName = "nrUserWave";

        public void Open(string resourceName)
        {
            Dispose();
            _rfsg = new NIRfsg(resourceName, true, false, "DriverSetup= ");
        }

        public void ConfigureFrequencyReference(NIRfsgFrequencyReferenceSource source, double referenceFrequencyHz)
        {
            _rfsg.FrequencyReference.ConfigureSource(source, referenceFrequencyHz);
        }

        public void LoadNrWaveformFromTdms(string filePath)
        {
            // TDMS / RFmx waveform file — adjust overload if your Playback library version differs slightly.
            NIRfsgPlayback.DownloadUserWaveform(_rfsg, WaveformName, filePath, true);
        }

        public void ConfigureRf(double centerFrequencyHz, double powerDbm)
        {
            _rfsg.RF.ConfigureFrequency(centerFrequencyHz);
            _rfsg.RF.PowerLevel = powerDbm;
        }

        public void SelectArbitraryWaveform()
        {
            _rfsg.Arb.SelectedWaveform = WaveformName;
            _rfsg.Arb.GenerationMode = NIRfsgGenerationMode.ArbitraryWaveform;
        }

        public void CommitAndStart()
        {
            _rfsg.Utility.Commit();
            _rfsg.Initiate();
        }

        public void SetOutputPower(double powerDbm)
        {
            _rfsg.RF.PowerLevel = powerDbm;
        }

        public void Dispose()
        {
            try
            {
                if (_rfsg != null)
                {
                    try
                    {
                        _rfsg.Abort();
                    }
                    catch
                    {
                        // Demo: ignore.
                    }

                    _rfsg.Close();
                }
            }
            catch
            {
                // Demo: ignore dispose errors.
            }
            finally
            {
                _rfsg = null;
            }
        }
    }
}
