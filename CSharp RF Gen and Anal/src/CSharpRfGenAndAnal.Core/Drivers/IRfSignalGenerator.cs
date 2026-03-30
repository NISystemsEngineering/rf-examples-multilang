namespace CSharpRfGenAndAnal.Core.Drivers
{
    public interface IRfSignalGenerator
    {
        void Open(string resourceName);
        void ConfigureCw(double centerFrequencyHz, double powerDbm, double externalAttenuationDb);
        /// <param name="waveformTdmsFilePath">Full path to TDMS waveform; if null/empty, implementation default is used.</param>
        void ConfigureCw(double centerFrequencyHz, double powerDbm, double externalAttenuationDb, string waveformTdmsFilePath);
        void SetOutputPowerDbm(double powerDbm, double externalAttenuationDb);
        void Initiate();
        void Abort();
        void Close();
    }
}
