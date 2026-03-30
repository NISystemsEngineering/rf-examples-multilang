using System;
using System.Collections.Generic;
using CSharpRfGenAndAnal.Core.Config;
using CSharpRfGenAndAnal.Core.Drivers;
using NationalInstruments;

namespace CSharpRfGenAndAnal.Core.Measurement
{
    public sealed class CwLoopbackRunner
    {
        /// <summary>Retry only when SG power is at or below this level (dBm).</summary>
        private const double EvmSpikeRetrySgPowerMaxDbm = 0.0;
        private const double EvmSpikeVsPreviousDb = 20.0;
        private const double EvmSpikeAcceptWithinPreviousDb = 5.0;
        private const int EvmSpikeMaxRetries = 5;

        private readonly CwLoopbackConfig _cfg;
        private readonly IRfSignalGenerator _sg;
        private readonly IRfSignalAnalyzer _sa;

        public CwLoopbackRunner(
            CwLoopbackConfig cfg,
            IRfSignalGenerator sg,
            IRfSignalAnalyzer sa)
        {
            _cfg = cfg;
            _sg = sg;
            _sa = sa;
        }

        public PowerSweepResult RunPowerSweep(
            double startPowerDbm,
            double stopPowerDbm,
            double stepDb,
            Action<PowerSweepIterationInfo> onIteration = null)
        {
            var points = new List<(double SgPowerDbm, double EvmDb)>();
            var bestEvm = double.PositiveInfinity;
            var bestPower = startPowerDbm;
            ComplexSingle[] bestConstellation = Array.Empty<ComplexSingle>();

            _sg.Open(_cfg.ResourceName);
            _sa.Open(_cfg.ResourceName);
            try
            {
                var stepCount = (int)Math.Round((stopPowerDbm - startPowerDbm) / stepDb);
                if (stepCount < 0)
                {
                    throw new ArgumentException("Power sweep requires stop >= start and positive step.");
                }

                var totalSteps = stepCount + 1;
                var spanHz = _cfg.EffectiveSpecAnSpanHz();
                var first = true;
                var prevEvmDb = double.NaN;
                for (var i = 0; i <= stepCount; i++)
                {
                    var p = startPowerDbm + i * stepDb;
                    if (first)
                    {
                        _sg.ConfigureCw(
                            _cfg.CenterFrequencyHz,
                            p,
                            _cfg.ExternalAttenuationDb,
                            _cfg.WaveformTdmsFilePath);
                        _sg.Initiate();
                        _sa.ConfigureOfdmModAcc(
                            _cfg.CenterFrequencyHz,
                            _cfg.ChannelBandwidthHz,
                            spanHz,
                            _cfg.ResolutionBandwidthHz,
                            _cfg.WlanStandard);
                        first = false;
                    }
                    else
                    {
                        _sg.SetOutputPowerDbm(p, _cfg.ExternalAttenuationDb);
                    }

                    var m = _sa.MeasureAverageRmsEvmWithConstellation();
                    if (i > 0)
                    {
                        m = MaybeRetryEvmAfterSpike(p, prevEvmDb, m);
                    }

                    points.Add((p, m.AverageRmsEvmDb));

                    onIteration?.Invoke(
                        new PowerSweepIterationInfo(
                            i,
                            totalSteps,
                            p,
                            m.AverageRmsEvmDb,
                            m.DataConstellation,
                            points));

                    if (m.AverageRmsEvmDb < bestEvm && !double.IsNaN(m.AverageRmsEvmDb))
                    {
                        bestEvm = m.AverageRmsEvmDb;
                        bestPower = p;
                        bestConstellation = m.DataConstellation.Length > 0
                            ? (ComplexSingle[])m.DataConstellation.Clone()
                            : Array.Empty<ComplexSingle>();
                    }

                    prevEvmDb = m.AverageRmsEvmDb;
                }
            }
            finally
            {
                _sg.Abort();
                _sg.Close();
                _sa.Close();
            }

            return new PowerSweepResult(
                points,
                bestPower,
                bestEvm == double.PositiveInfinity ? double.NaN : bestEvm,
                bestConstellation);
        }

        public CwLoopbackResult Run()
        {
            _sg.Open(_cfg.ResourceName);
            _sa.Open(_cfg.ResourceName);
            try
            {
                _sg.ConfigureCw(
                    _cfg.CenterFrequencyHz,
                    _cfg.TargetPowerDbm,
                    _cfg.ExternalAttenuationDb,
                    _cfg.WaveformTdmsFilePath);
                _sg.Initiate();

                _sa.ConfigureOfdmModAcc(
                    _cfg.CenterFrequencyHz,
                    _cfg.ChannelBandwidthHz,
                    _cfg.EffectiveSpecAnSpanHz(),
                    _cfg.ResolutionBandwidthHz,
                    _cfg.WlanStandard);
                var averageRmsEvmDb = _sa.MeasureAverageRmsEvmDb();

                return new CwLoopbackResult(averageRmsEvmDb);
            }
            finally
            {
                _sg.Abort();
                _sg.Close();
                _sa.Close();
            }
        }

        /// <summary>
        /// At lower SG power, if EVM jumps worse by at least <see cref="EvmSpikeVsPreviousDb"/> relative to the prior step,
        /// re-measure up to <see cref="EvmSpikeMaxRetries"/> times. Accept the first reading within
        /// <see cref="EvmSpikeAcceptWithinPreviousDb"/> dB of the previous step; otherwise keep the worst (highest) EVM seen.
        /// </summary>
        private WlanModAccMeasureResult MaybeRetryEvmAfterSpike(
            double sgPowerDbm,
            double previousStepEvmDb,
            WlanModAccMeasureResult first)
        {
            var ev = first.AverageRmsEvmDb;
            if (sgPowerDbm > EvmSpikeRetrySgPowerMaxDbm
                || !IsFiniteEvm(previousStepEvmDb)
                || !IsFiniteEvm(ev)
                || ev < previousStepEvmDb + EvmSpikeVsPreviousDb)
            {
                return first;
            }

            var worstEv = ev;
            var worst = first;
            for (var attempt = 0; attempt < EvmSpikeMaxRetries; attempt++)
            {
                var m = _sa.MeasureAverageRmsEvmWithConstellation();
                var e = m.AverageRmsEvmDb;
                if (!IsFiniteEvm(e))
                {
                    continue;
                }

                if (e > worstEv)
                {
                    worstEv = e;
                    worst = m;
                }

                if (Math.Abs(e - previousStepEvmDb) < EvmSpikeAcceptWithinPreviousDb)
                {
                    return m;
                }
            }

            return worst;
        }

        private static bool IsFiniteEvm(double evmDb)
        {
            return !double.IsNaN(evmDb) && !double.IsInfinity(evmDb);
        }
    }

    public readonly struct CwLoopbackResult
    {
        public CwLoopbackResult(double averageRmsEvmDb)
        {
            AverageRmsEvmDb = averageRmsEvmDb;
        }

        public double AverageRmsEvmDb { get; }
    }
}
