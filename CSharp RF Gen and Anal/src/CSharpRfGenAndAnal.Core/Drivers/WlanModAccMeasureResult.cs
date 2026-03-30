using NationalInstruments;

namespace CSharpRfGenAndAnal.Core.Drivers
{
    public readonly struct WlanModAccMeasureResult
    {
        public WlanModAccMeasureResult(double averageRmsEvmDb, ComplexSingle[] dataConstellation)
        {
            AverageRmsEvmDb = averageRmsEvmDb;
            DataConstellation = dataConstellation ?? System.Array.Empty<ComplexSingle>();
        }

        public double AverageRmsEvmDb { get; }
        public ComplexSingle[] DataConstellation { get; }
    }
}
