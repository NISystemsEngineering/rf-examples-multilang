using System;
using System.IO;
using CSharpRfGenAndAnal.Core.Config;
using CSharpRfGenAndAnal.Core.Drivers;
using CSharpRfGenAndAnal.Core.Measurement;
using CSharpRfGenAndAnal.Core.Plotting;

namespace CSharpRfGenAndAnal.Console
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var cfg = CwLoopbackConfig.Default();
                cfg.SetWlanStandardFromLabel("802.11be");

                var sg = new NiRfsgIviCRfSignalGenerator();
                var sa = new RfmxWlanSignalAnalyzer();

                var runner = new CwLoopbackRunner(cfg, sg, sa);
                const double sweepStartDbm = -50.0;
                const double sweepStopDbm = 10.0;
                const double sweepStepDb = 2.5;

                var sweep = runner.RunPowerSweep(sweepStartDbm, sweepStopDbm, sweepStepDb);

                var plotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RfPlots");
                Directory.CreateDirectory(plotDir);
                var evmPlotPath = Path.Combine(plotDir, "EvmVsSgPower.png");
                var constellationPlotPath = Path.Combine(plotDir, "DataConstellationBestEvm.png");

                RfPlotExporter.SaveEvmVsPowerPlot(sweep.Points, evmPlotPath);
                RfPlotExporter.SaveConstellationPlot(sweep.BestDataConstellation, constellationPlotPath, 1.5f);

                System.Console.WriteLine("Scripted waveform loopback — SG power sweep + EVM (NI-RFSG + RFmx WLAN)");
                System.Console.WriteLine($"SG resource: {cfg.ResourceName}");
                System.Console.WriteLine(
                    $"SG power sweep: {sweepStartDbm:F1} .. {sweepStopDbm:F1} dBm, step {sweepStepDb:F1} dB");
                System.Console.WriteLine(
                    $"SA: OFDM ModAcc @ {cfg.CenterFrequencyHz / 1.0e9:F3} GHz, channel BW {cfg.ChannelBandwidthHz / 1.0e6:F0} MHz");
                System.Console.WriteLine();
                System.Console.WriteLine("SG (dBm)\tAvg RMS EVM (dB)");
                foreach (var (p, evm) in sweep.Points)
                {
                    System.Console.WriteLine($"{p:F2}\t\t{evm:F3}");
                }

                System.Console.WriteLine();
                System.Console.WriteLine(
                    $"Best (min) EVM: {sweep.BestEvmDb:F3} dB at SG power {sweep.BestSgPowerDbm:F2} dBm");
                System.Console.WriteLine($"EVM vs power plot: {evmPlotPath}");
                System.Console.WriteLine($"Constellation (best EVM): {constellationPlotPath}");
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to terminate...");
                System.Console.ReadKey(intercept: true);
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("ERROR:");
                System.Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
