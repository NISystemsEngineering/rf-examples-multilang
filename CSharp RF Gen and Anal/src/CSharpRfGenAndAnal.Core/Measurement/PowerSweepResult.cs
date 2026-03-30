using System.Collections.Generic;
using NationalInstruments;

namespace CSharpRfGenAndAnal.Core.Measurement
{
    public sealed class PowerSweepResult
    {
        public PowerSweepResult(
            IReadOnlyList<(double SgPowerDbm, double EvmDb)> points,
            double bestSgPowerDbm,
            double bestEvmDb,
            ComplexSingle[] bestDataConstellation)
        {
            Points = points;
            BestSgPowerDbm = bestSgPowerDbm;
            BestEvmDb = bestEvmDb;
            BestDataConstellation = bestDataConstellation ?? System.Array.Empty<ComplexSingle>();
        }

        public IReadOnlyList<(double SgPowerDbm, double EvmDb)> Points { get; }
        public double BestSgPowerDbm { get; }
        public double BestEvmDb { get; }
        public ComplexSingle[] BestDataConstellation { get; }
    }
}
