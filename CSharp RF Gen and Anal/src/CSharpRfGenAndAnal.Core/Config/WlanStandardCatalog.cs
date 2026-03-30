using System;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments.RFmx.WlanMX;

namespace CSharpRfGenAndAnal.Core.Config
{
    /// <summary>Maps user-facing labels to RFmx WLAN standard enum values.</summary>
    public static class WlanStandardCatalog
    {
        public static IReadOnlyList<string> DisplayLabels => Entries.Select(e => e.Label).ToList();

        public static IReadOnlyList<(string Label, RFmxWlanMXStandard Standard)> Entries { get; } =
            new (string, RFmxWlanMXStandard)[]
            {
                ("802.11a / g (OFDM)", RFmxWlanMXStandard.Standard802_11ag),
                ("802.11b (DSSS)", RFmxWlanMXStandard.Standard802_11b),
                ("802.11j", RFmxWlanMXStandard.Standard802_11j),
                ("802.11p", RFmxWlanMXStandard.Standard802_11p),
                ("802.11n", RFmxWlanMXStandard.Standard802_11n),
                ("802.11ac", RFmxWlanMXStandard.Standard802_11ac),
                ("802.11ax", RFmxWlanMXStandard.Standard802_11ax),
                ("802.11be", RFmxWlanMXStandard.Standard802_11be),
                ("802.11bn", RFmxWlanMXStandard.Standard802_11bn),
            };

        public static RFmxWlanMXStandard Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Standard is required.", nameof(text));
            }

            var t = text.Trim();
            foreach (var (label, standard) in Entries)
            {
                if (string.Equals(label, t, StringComparison.OrdinalIgnoreCase))
                {
                    return standard;
                }
            }

            if (Enum.TryParse<RFmxWlanMXStandard>(t, true, out var direct))
            {
                return direct;
            }

            var key = t.Replace(" ", string.Empty).Replace(".", string.Empty);
            var k = key.ToUpperInvariant();
            if (k.IndexOf("80211BE", StringComparison.Ordinal) >= 0 || k.IndexOf("EHT", StringComparison.Ordinal) >= 0)
            {
                return RFmxWlanMXStandard.Standard802_11be;
            }

            if (k.IndexOf("80211AX", StringComparison.Ordinal) >= 0 || k.IndexOf("WIFI6", StringComparison.Ordinal) >= 0)
            {
                return RFmxWlanMXStandard.Standard802_11ax;
            }

            if (k.IndexOf("80211AC", StringComparison.Ordinal) >= 0)
            {
                return RFmxWlanMXStandard.Standard802_11ac;
            }

            if (k.IndexOf("80211N", StringComparison.Ordinal) >= 0)
            {
                return RFmxWlanMXStandard.Standard802_11n;
            }

            if (k.IndexOf("80211B", StringComparison.Ordinal) >= 0 && k.IndexOf("80211BN", StringComparison.Ordinal) < 0)
            {
                return RFmxWlanMXStandard.Standard802_11b;
            }

            if (k.IndexOf("80211AG", StringComparison.Ordinal) >= 0 || key.Equals("80211a", StringComparison.OrdinalIgnoreCase) || key.Equals("80211g", StringComparison.OrdinalIgnoreCase))
            {
                return RFmxWlanMXStandard.Standard802_11ag;
            }

            throw new FormatException($"Unknown WLAN standard: \"{text}\".");
        }
    }
}
