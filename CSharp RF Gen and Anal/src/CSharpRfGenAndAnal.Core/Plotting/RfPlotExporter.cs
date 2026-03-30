using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using NationalInstruments;

namespace CSharpRfGenAndAnal.Core.Plotting
{
    public static class RfPlotExporter
    {
        /// <param name="axisHalfRange">If &gt; 0, I/Q axes span [-axisHalfRange, +axisHalfRange]; otherwise auto-scale.</param>
        public static void SaveConstellationPlot(ComplexSingle[] samples, string filePath, float axisHalfRange = 0f)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            const int size = 640;
            const int pad = 50;

            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            using (var axisPen = new Pen(Color.DimGray, 1f))
            using (var gridPen = new Pen(Color.FromArgb(60, 60, 60), 1f) { DashStyle = DashStyle.Dot })
            using (var pointBrush = new SolidBrush(Color.DodgerBlue))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float plot = size - 2 * pad;
                float cx = pad + plot / 2f;
                float cy = pad + plot / 2f;
                float scale;
                if (axisHalfRange > 0f)
                {
                    scale = (plot / 2f) / axisHalfRange;
                }
                else
                {
                    float max = 1e-6f;
                    foreach (var s in samples)
                    {
                        var a = Math.Max(Math.Abs(s.Real), Math.Abs(s.Imaginary));
                        if (a > max)
                        {
                            max = a;
                        }
                    }

                    max *= 1.15f;
                    if (max < 1e-6f)
                    {
                        max = 1f;
                    }

                    scale = (plot / 2f) / max;
                }

                g.DrawLine(gridPen, cx - plot / 2f, cy, cx + plot / 2f, cy);
                g.DrawLine(gridPen, cx, cy - plot / 2f, cx, cy + plot / 2f);
                g.DrawRectangle(axisPen, pad, pad, plot, plot);

                foreach (var s in samples)
                {
                    float x = cx + s.Real * scale;
                    float y = cy - s.Imaginary * scale;
                    g.FillEllipse(pointBrush, x - 1.5f, y - 1.5f, 3f, 3f);
                }

                using (var font = new Font("Segoe UI", 9f))
                using (var titleBrush = new SolidBrush(Color.Black))
                {
                    g.DrawString(
                        "Data constellation",
                        font,
                        titleBrush,
                        pad,
                        12f);
                }

                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static void SaveEvmVsPowerPlot(
            IReadOnlyList<(double SgPowerDbm, double EvmDb)> points,
            string filePath)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            const int w = 820;
            const int h = 480;
            const int padL = 70;
            const int padR = 30;
            const int padT = 45;
            const int padB = 60;

            var pMin = points.Min(p => p.SgPowerDbm);
            var pMax = points.Max(p => p.SgPowerDbm);
            var eMin = points.Min(p => p.EvmDb);
            var eMax = points.Max(p => p.EvmDb);
            var ePad = Math.Max(1.0, (eMax - eMin) * 0.15);
            eMin -= ePad;
            eMax += ePad;

            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            using (var axisPen = new Pen(Color.Black, 1.2f))
            using (var gridPen = new Pen(Color.LightGray, 1f))
            using (var linePen = new Pen(Color.DarkBlue, 2f))
            using (var markerBrush = new SolidBrush(Color.Crimson))
            using (var font = new Font("Segoe UI", 9f))
            using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.Black))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float plotW = w - padL - padR;
                float plotH = h - padT - padB;

                g.DrawRectangle(axisPen, padL, padT, plotW, plotH);

                double MapX(double p) => padL + (p - pMin) / (pMax - pMin) * plotW;
                double MapY(double e) => padT + plotH - (e - eMin) / (eMax - eMin) * plotH;

                for (var i = 0; i <= 4; i++)
                {
                    var py = padT + plotH * i / 4f;
                    g.DrawLine(gridPen, padL, py, padL + plotW, py);
                }

                var pts = points.Select(p => new PointF((float)MapX(p.SgPowerDbm), (float)MapY(p.EvmDb))).ToArray();
                if (pts.Length >= 2)
                {
                    g.DrawLines(linePen, pts);
                }

                foreach (var p in points)
                {
                    var fx = (float)MapX(p.SgPowerDbm);
                    var fy = (float)MapY(p.EvmDb);
                    g.FillEllipse(markerBrush, fx - 3f, fy - 3f, 6f, 6f);
                }

                g.DrawString(
                    "Average RMS EVM (dB) vs SG output power (dBm)",
                    titleFont,
                    brush,
                    padL,
                    10f);

                g.DrawString(
                    "SG power (dBm)",
                    font,
                    brush,
                    padL + plotW / 3f,
                    h - 38f);
                g.DrawString(
                    "Avg RMS EVM (dB)",
                    font,
                    brush,
                    12f,
                    padT + plotH / 3f,
                    new StringFormat { FormatFlags = StringFormatFlags.DirectionVertical });

                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
