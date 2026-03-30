using NationalInstruments.RFmx.WlanMX;

namespace CSharpRfGenAndAnal.Core.Config
{
    public sealed class CwLoopbackConfig
    {
        public string ResourceName { get; set; } = "VST";
        /// <summary>TDMS file for NI-RFSG arbitrary waveform download (ModAcc loopback).</summary>
        public string WaveformTdmsFilePath { get; set; } =
            @"C:\Charlie\WLAN_be_320M_MCS13_11p2_PAPR_14Symbols.tdms";
        public double CenterFrequencyHz { get; set; } = 5.0e9;
        public double TargetPowerDbm { get; set; } = -10.0;
        public double ExternalAttenuationDb { get; set; } = 0.0;
        public RFmxWlanMXStandard WlanStandard { get; set; } = RFmxWlanMXStandard.Standard802_11be;
        public double ChannelBandwidthHz { get; set; } = 320.0e6;
        /// <summary>If zero, <see cref="SpectrumDefaults.SpecAnSpanHz"/> is used from channel bandwidth.</summary>
        public double SpanHz { get; set; } = 0.0;
        public double ResolutionBandwidthHz { get; set; } = 1.0e6;

        public double EffectiveSpecAnSpanHz()
        {
            return SpanHz > 0.0 ? SpanHz : SpectrumDefaults.SpecAnSpanHz(ChannelBandwidthHz);
        }

        public static CwLoopbackConfig Default()
        {
            return new CwLoopbackConfig();
        }

        /// <summary>Sets <see cref="WlanStandard"/> from a UI label (see <see cref="WlanStandardCatalog.DisplayLabels"/>).</summary>
        public void SetWlanStandardFromLabel(string label)
        {
            WlanStandard = WlanStandardCatalog.Parse(label);
        }
    }
}
