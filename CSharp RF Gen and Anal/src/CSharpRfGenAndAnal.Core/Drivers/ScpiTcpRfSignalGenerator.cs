using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public sealed class ScpiTcpRfSignalGenerator : IRfSignalGenerator
    {
        private TcpClient _tcpClient;
        private StreamWriter _writer;

        public void Open(string resourceName)
        {
            var parts = resourceName.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                throw new ArgumentException(
                    "Resource name for SCPI TCP SG must be in 'host:port' format, e.g. 192.168.1.100:5025.");
            }

            _tcpClient = new TcpClient();
            _tcpClient.Connect(parts[0], port);
            _writer = new StreamWriter(_tcpClient.GetStream(), Encoding.ASCII)
            {
                NewLine = "\n",
                AutoFlush = true
            };
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
            EnsureConnected();

            var appliedPowerDbm = powerDbm - externalAttenuationDb;
            WriteLine(":SOUR:FREQ:CW " + centerFrequencyHz.ToString("R", CultureInfo.InvariantCulture));
            WriteLine(":SOUR:POW " + appliedPowerDbm.ToString("R", CultureInfo.InvariantCulture));

            LoopbackSignalState.LastConfiguredFrequencyHz = centerFrequencyHz;
            LoopbackSignalState.LastConfiguredPowerDbm = appliedPowerDbm;
        }

        public void SetOutputPowerDbm(double powerDbm, double externalAttenuationDb)
        {
            EnsureConnected();
            var appliedPowerDbm = powerDbm - externalAttenuationDb;
            WriteLine(":SOUR:POW " + appliedPowerDbm.ToString("R", CultureInfo.InvariantCulture));
            LoopbackSignalState.LastConfiguredPowerDbm = appliedPowerDbm;
        }

        public void Initiate()
        {
            EnsureConnected();
            WriteLine(":OUTP ON");
        }

        public void Abort()
        {
            if (_writer != null)
            {
                WriteLine(":OUTP OFF");
            }
        }

        public void Close()
        {
            _writer?.Dispose();
            _writer = null;

            _tcpClient?.Dispose();
            _tcpClient = null;
        }

        private void EnsureConnected()
        {
            if (_writer == null)
            {
                throw new InvalidOperationException("SG session is not open.");
            }
        }

        private void WriteLine(string command)
        {
            _writer.WriteLine(command);
        }
    }
}
