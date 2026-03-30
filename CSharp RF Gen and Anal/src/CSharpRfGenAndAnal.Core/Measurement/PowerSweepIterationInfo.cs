using System.Collections.Generic;
using NationalInstruments;

namespace CSharpRfGenAndAnal.Core.Measurement
{
    public sealed class PowerSweepIterationInfo
    {
        public PowerSweepIterationInfo(
            int stepIndex,
            int totalSteps,
            double sgPowerDbm,
            double evmDb,
            ComplexSingle[] dataConstellation,
            IReadOnlyList<(double SgPowerDbm, double EvmDb)> pointsSoFar)
        {
            StepIndex = stepIndex;
            TotalSteps = totalSteps;
            SgPowerDbm = sgPowerDbm;
            EvmDb = evmDb;
            DataConstellation = dataConstellation ?? System.Array.Empty<ComplexSingle>();
            PointsSoFar = pointsSoFar ?? System.Array.Empty<(double, double)>();
        }

        public int StepIndex { get; }
        public int TotalSteps { get; }
        public double SgPowerDbm { get; }
        public double EvmDb { get; }
        public ComplexSingle[] DataConstellation { get; }
        public IReadOnlyList<(double SgPowerDbm, double EvmDb)> PointsSoFar { get; }
    }
}
