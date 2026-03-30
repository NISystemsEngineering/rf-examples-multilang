using System;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using CSharpRfGenAndAnal.Core.Config;
using CSharpRfGenAndAnal.Core.Drivers;
using CSharpRfGenAndAnal.Core.Measurement;

namespace CSharpRfGenAndAnal.WinForms
{
    public sealed class MainForm : Form
    {
        private const double ConstellationAxisLimit = 1.5;
        private static readonly Color ChartPlotBackColor = Color.Black;
        private static readonly Color ChartDataPointColor = Color.FromArgb(160, 230, 160);
        /// <summary>Cap constellation points drawn per step (full trace is still measured).</summary>
        private const int ConstellationMaxDisplayPoints = 2500;

        private readonly TextBox _resourceName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly TextBox _centerFreqHzInput = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly Label _centerFreqHzDisplay = new Label { AutoSize = true, ForeColor = Color.DimGray };
        private readonly ComboBox _standard = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        private readonly TextBox _channelBandwidthHzInput = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly Label _channelBandwidthHzDisplay = new Label { AutoSize = true, ForeColor = Color.DimGray };
        private readonly TextBox _waveformPath = new TextBox();
        private readonly TextBox _sgStartDbm = new TextBox { Width = 80, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly TextBox _sgEndDbm = new TextBox { Width = 80, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly TextBox _powerStepDb = new TextBox { Width = 80, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        private readonly Button _run = new Button { Text = "Run sweep", Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        private readonly Label _status = new Label { AutoSize = true, Text = "Ready." };
        private readonly Label _currentSgPower = new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            Text = "Current SG power: — dBm",
        };

        private readonly Chart _constellationChart = new Chart { MinimumSize = new Size(360, 360) };
        private readonly Chart _bathtubChart = new Chart { MinimumSize = new Size(420, 360) };
        private Series _constellationSeries;
        private Series _bathtubLineSeries;

        public MainForm()
        {
            Text = "WLAN loopback — power sweep";
            Width = 1100;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;

            foreach (var label in WlanStandardCatalog.DisplayLabels)
            {
                _standard.Items.Add(label);
            }

            if (_standard.Items.Count > 0)
            {
                _standard.SelectedIndex = 7;
            }

            _resourceName.Text = "VST";
            _centerFreqHzInput.Text = "5G";
            _channelBandwidthHzInput.Text = "320M";
            _waveformPath.Text = CwLoopbackConfig.Default().WaveformTdmsFilePath;
            _sgStartDbm.Text = "-50";
            _sgEndDbm.Text = "10";
            _powerStepDb.Text = "2.5";

            _centerFreqHzInput.TextChanged += (_, __) => UpdateCenterFrequencyHzDisplay();
            _channelBandwidthHzInput.TextChanged += (_, __) => UpdateChannelBandwidthHzDisplay();
            UpdateCenterFrequencyHzDisplay();
            UpdateChannelBandwidthHzDisplay();

            BuildCharts();
            SetChartDoubleBuffered(_constellationChart);
            SetChartDoubleBuffered(_bathtubChart);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            root.Controls.Add(BuildInputPanel(), 0, 0);

            var statusPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };
            statusPanel.Controls.Add(_currentSgPower);
            statusPanel.Controls.Add(_status);
            root.Controls.Add(statusPanel, 0, 1);

            var charts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
            };
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            charts.Controls.Add(Wrap("Constellation (current step)", _constellationChart), 0, 0);
            charts.Controls.Add(Wrap("EVM vs SG power (bathtub)", _bathtubChart), 1, 0);
            root.Controls.Add(charts, 0, 2);

            Controls.Add(root);

            _run.Click += async (_, __) => await RunSweepAsync();
        }

        private void UpdateCenterFrequencyHzDisplay()
        {
            try
            {
                var hz = ParseEngineeringHz(_centerFreqHzInput.Text);
                _centerFreqHzDisplay.Text = "= " + FormatEngineeringHz(hz);
            }
            catch
            {
                _centerFreqHzDisplay.Text = "= (invalid)";
            }
        }

        private void UpdateChannelBandwidthHzDisplay()
        {
            try
            {
                var hz = ParseEngineeringHz(_channelBandwidthHzInput.Text);
                _channelBandwidthHzDisplay.Text = "= " + FormatEngineeringHz(hz);
            }
            catch
            {
                _channelBandwidthHzDisplay.Text = "= (invalid)";
            }
        }

        /// <summary>Engineering-style Hz (exponent multiple of 3), e.g. 5.000e+09 Hz.</summary>
        private static string FormatEngineeringHz(double hz)
        {
            if (double.IsNaN(hz) || double.IsInfinity(hz))
            {
                return "— Hz";
            }

            if (Math.Abs(hz) < 1e-30)
            {
                return "0 Hz";
            }

            var sign = hz < 0 ? -1.0 : 1.0;
            var a = Math.Abs(hz);
            var exp = (int)Math.Floor(Math.Log10(a) / 3.0) * 3;
            var mant = sign * a / Math.Pow(10.0, exp);
            return string.Format(CultureInfo.InvariantCulture, "{0:F3}e{1:+0;-0;+0} Hz", mant, exp);
        }

        private static Control Wrap(string title, Control inner)
        {
            var p = new Panel { Dock = DockStyle.Fill };
            var l = new Label { Text = title, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
            inner.Dock = DockStyle.Fill;
            p.Controls.Add(inner);
            p.Controls.Add(l);
            return p;
        }

        private Control BuildInputPanel()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 5,
                Padding = new Padding(0, 0, 0, 4),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));

            int r = 0;
            grid.Controls.Add(MakeLabel("Resource"), 0, r);
            grid.Controls.Add(_resourceName, 1, r);
            grid.Controls.Add(MakeLabel("802.11 standard"), 2, r);
            grid.Controls.Add(_standard, 3, r);
            grid.Controls.Add(_run, 4, r);
            r++;

            grid.Controls.Add(MakeLabel("Center frequency (Hz)"), 0, r);
            grid.Controls.Add(_centerFreqHzInput, 1, r);
            grid.Controls.Add(_centerFreqHzDisplay, 2, r);
            grid.SetColumnSpan(_centerFreqHzDisplay, 2);
            r++;

            grid.Controls.Add(MakeLabel("Channel bandwidth (Hz)"), 0, r);
            grid.Controls.Add(_channelBandwidthHzInput, 1, r);
            grid.Controls.Add(_channelBandwidthHzDisplay, 2, r);
            grid.SetColumnSpan(_channelBandwidthHzDisplay, 2);
            r++;

            grid.Controls.Add(MakeLabel("Waveform (TDMS)"), 0, r);
            var browse = new Button
            {
                Text = "Browse…",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 0, 6, 0),
            };
            browse.Click += (_, __) =>
            {
                using (var dlg = new OpenFileDialog
                {
                    Filter = "TDMS files (*.tdms)|*.tdms|All files (*.*)|*.*",
                    Title = "Select SG waveform (TDMS)",
                    RestoreDirectory = true,
                })
                {
                    if (!string.IsNullOrWhiteSpace(_waveformPath.Text))
                    {
                        var full = _waveformPath.Text.Trim();
                        try
                        {
                            var dir = Path.GetDirectoryName(full);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            {
                                dlg.InitialDirectory = dir;
                            }

                            dlg.FileName = Path.GetFileName(full);
                        }
                        catch (ArgumentException)
                        {
                            dlg.FileName = full;
                        }
                    }

                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _waveformPath.Text = dlg.FileName;
                    }
                }
            };

            var pathPair = new TableLayoutPanel
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };
            pathPair.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66.67f));
            pathPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            pathPair.Controls.Add(browse, 0, 0);
            _waveformPath.Dock = DockStyle.Fill;
            pathPair.Controls.Add(_waveformPath, 1, 0);

            grid.Controls.Add(pathPair, 1, r);
            grid.SetColumnSpan(pathPair, 4);
            r++;

            var sweepInner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Padding = new Padding(0),
            };
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sweepInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            sweepInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sweepInner.Controls.Add(MakeLabel("Start (dBm)"), 0, 0);
            sweepInner.Controls.Add(_sgStartDbm, 1, 0);
            sweepInner.Controls.Add(MakeLabel("End (dBm)"), 2, 0);
            sweepInner.Controls.Add(_sgEndDbm, 3, 0);
            sweepInner.Controls.Add(MakeLabel("Step (dB)"), 4, 0);
            sweepInner.Controls.Add(_powerStepDb, 5, 0);

            var sweepGroup = new GroupBox
            {
                Text = "SG power sweep",
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 88,
                MinimumSize = new Size(520, 88),
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(0, 4, 0, 0),
            };
            sweepGroup.Controls.Add(sweepInner);

            grid.Controls.Add(sweepGroup, 0, r);
            grid.SetColumnSpan(sweepGroup, 5);

            return grid;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private static void SetChartDoubleBuffered(Chart chart)
        {
            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                chart,
                new object[] { true });
        }

        private void BuildCharts()
        {
            _constellationChart.BackColor = ChartPlotBackColor;
            _constellationChart.Legends.Clear();
            var caC = new ChartArea("const");
            StyleDarkChartArea(caC);
            caC.AxisX.Title = "I";
            caC.AxisY.Title = "Q";
            caC.AxisX.Minimum = -ConstellationAxisLimit;
            caC.AxisX.Maximum = ConstellationAxisLimit;
            caC.AxisY.Minimum = -ConstellationAxisLimit;
            caC.AxisY.Maximum = ConstellationAxisLimit;
            caC.AxisX.MajorGrid.Enabled = true;
            caC.AxisY.MajorGrid.Enabled = true;
            _constellationChart.ChartAreas.Add(caC);
            _constellationSeries = new Series("data")
            {
                ChartType = SeriesChartType.FastPoint,
                MarkerSize = 4,
                MarkerStyle = MarkerStyle.Circle,
                Color = ChartDataPointColor,
                BorderWidth = 0,
            };
            _constellationChart.Series.Add(_constellationSeries);
            _constellationChart.AntiAliasing = AntiAliasingStyles.None;

            _bathtubChart.BackColor = ChartPlotBackColor;
            _bathtubChart.Legends.Clear();
            var caB = new ChartArea("bath");
            StyleDarkChartArea(caB);
            caB.AxisX.Title = "SG power (dBm)";
            caB.AxisY.Title = "Avg RMS EVM (dB)";
            caB.AxisX.MajorGrid.Enabled = true;
            caB.AxisY.MajorGrid.Enabled = true;
            caB.AxisY.Interval = 5;
            caB.AxisY.MinorGrid.Enabled = true;
            caB.AxisY.MinorGrid.Interval = 1;
            caB.AxisY.MinorGrid.LineColor = Color.FromArgb(36, 36, 40);
            caB.AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            _bathtubChart.ChartAreas.Add(caB);
            _bathtubLineSeries = new Series("evm")
            {
                ChartType = SeriesChartType.FastPoint,
                BorderWidth = 0,
                Color = ChartDataPointColor,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 6,
            };
            _bathtubChart.Series.Add(_bathtubLineSeries);
            _bathtubChart.AntiAliasing = AntiAliasingStyles.None;
        }

        private static void StyleDarkChartArea(ChartArea area)
        {
            area.BackColor = ChartPlotBackColor;
            area.BorderColor = Color.FromArgb(80, 80, 80);
            StyleDarkAxis(area.AxisX);
            StyleDarkAxis(area.AxisY);
        }

        private static void StyleDarkAxis(Axis axis)
        {
            axis.LabelStyle.ForeColor = Color.FromArgb(200, 200, 200);
            axis.TitleForeColor = Color.FromArgb(220, 220, 220);
            axis.LineColor = Color.FromArgb(100, 100, 100);
            axis.MajorTickMark.LineColor = Color.FromArgb(120, 120, 120);
            axis.MajorGrid.LineColor = Color.FromArgb(48, 48, 52);
        }

        private async Task RunSweepAsync()
        {
            _run.Enabled = false;
            _status.Text = "Running…";
            _currentSgPower.Text = "Current SG power: — dBm";
            _constellationSeries.Points.Clear();
            _bathtubLineSeries.Points.Clear();

            try
            {
                var sweepMin = ParseDouble(_sgStartDbm.Text);
                var sweepMax = ParseDouble(_sgEndDbm.Text);
                if (sweepMin > sweepMax)
                {
                    (sweepMin, sweepMax) = (sweepMax, sweepMin);
                }

                var step = ParseDouble(_powerStepDb.Text);
                if (step <= 0)
                {
                    throw new FormatException("Power step must be positive.");
                }

                var cfg = new CwLoopbackConfig
                {
                    ResourceName = _resourceName.Text.Trim(),
                    CenterFrequencyHz = ParseEngineeringHz(_centerFreqHzInput.Text),
                    ChannelBandwidthHz = ParseEngineeringHz(_channelBandwidthHzInput.Text),
                    WaveformTdmsFilePath = _waveformPath.Text.Trim(),
                };
                cfg.SetWlanStandardFromLabel(_standard.SelectedItem?.ToString() ?? "802.11be");

                _bathtubChart.ChartAreas[0].AxisX.Minimum = sweepMin;
                _bathtubChart.ChartAreas[0].AxisX.Maximum = sweepMax;

                var sg = new NiRfsgIviCRfSignalGenerator();
                var sa = new RfmxWlanSignalAnalyzer();
                var runner = new CwLoopbackRunner(cfg, sg, sa);

                await Task.Run(() =>
                {
                    runner.RunPowerSweep(
                        sweepMin,
                        sweepMax,
                        step,
                        info =>
                        {
                            BeginInvoke(new Action(() =>
                            {
                                ApplyIteration(info);
                            }));
                        });
                }).ConfigureAwait(true);

                _status.Text = "Sweep finished.";
            }
            catch (Exception ex)
            {
                _status.Text = "Error.";
                MessageBox.Show(this, ex.ToString(), "Sweep failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _run.Enabled = true;
            }
        }

        private void ApplyIteration(PowerSweepIterationInfo info)
        {
            _constellationSeries.Points.Clear();
            var pts = info.DataConstellation;
            var n = pts.Length;
            if (n > 0)
            {
                var stride = n <= ConstellationMaxDisplayPoints
                    ? 1
                    : (int)Math.Ceiling((double)n / ConstellationMaxDisplayPoints);
                for (var i = 0; i < n; i += stride)
                {
                    var c = pts[i];
                    _constellationSeries.Points.AddXY(c.Real, c.Imaginary);
                }
            }

            _bathtubLineSeries.Points.Clear();
            foreach (var (p, e) in info.PointsSoFar)
            {
                if (!double.IsNaN(e) && !double.IsInfinity(e))
                {
                    _bathtubLineSeries.Points.AddXY(p, e);
                }
            }

            UpdateBathtubYAxis(info.PointsSoFar);

            _currentSgPower.Text = $"Current SG power: {info.SgPowerDbm:F2} dBm";
            _status.Text =
                $"Step {info.StepIndex + 1}/{info.TotalSteps}: EVM = {info.EvmDb:F2} dB";

            RefreshCharts();
        }

        /// <summary>
        /// Y-axis: next multiple of 5 at or above max EVM through next multiple of 5 at or below min EVM.
        /// </summary>
        private void UpdateBathtubYAxis(System.Collections.Generic.IReadOnlyList<(double SgPowerDbm, double EvmDb)> points)
        {
            var ys = points.Select(p => p.EvmDb).Where(y => !double.IsNaN(y) && !double.IsInfinity(y)).ToList();
            if (ys.Count == 0)
            {
                return;
            }

            var minE = ys.Min();
            var maxE = ys.Max();
            var yMin = RoundDownToMultipleOf5(minE);
            var yMax = RoundUpToMultipleOf5(maxE);
            if (yMin >= yMax)
            {
                yMin -= 5;
                yMax += 5;
            }

            var axisY = _bathtubChart.ChartAreas[0].AxisY;
            axisY.Minimum = yMin;
            axisY.Maximum = yMax;
            axisY.Interval = 5;
        }

        private static double RoundUpToMultipleOf5(double x)
        {
            return Math.Ceiling(x / 5.0) * 5.0;
        }

        private static double RoundDownToMultipleOf5(double x)
        {
            return Math.Floor(x / 5.0) * 5.0;
        }

        private void RefreshCharts()
        {
            // Do not call RecalculateAxesScale here — it would override fixed constellation limits and manual bathtub Y range.
            _constellationChart.Invalidate();
            _bathtubChart.Invalidate();
        }

        private static double ParseDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new FormatException("A numeric field is empty.");
            }

            if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            throw new FormatException($"Invalid number: \"{s}\".");
        }

        /// <summary>
        /// Parses a frequency in Hz with optional SI suffix k/M/G (e.g. <c>5G</c>, <c>300M</c>, <c>2.4G</c>) or plain Hz.
        /// Optional trailing <c>Hz</c> is ignored (e.g. <c>5 GHz</c>).
        /// </summary>
        private static double ParseEngineeringHz(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new FormatException("A numeric field is empty.");
            }

            var t = s.Trim();
            if (t.EndsWith("Hz", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring(0, t.Length - 2).Trim();
            }

            double multiplier = 1.0;
            if (t.Length > 0 && char.IsLetter(t[t.Length - 1]))
            {
                multiplier = char.ToUpperInvariant(t[t.Length - 1]) switch
                {
                    'K' => 1e3,
                    'M' => 1e6,
                    'G' => 1e9,
                    _ => throw new FormatException(
                        $"Unknown suffix (use k, M, G for ×10³, ×10⁶, ×10⁹ or enter plain Hz): \"{s.Trim()}\"."),
                };
                t = t.Substring(0, t.Length - 1).Trim();
            }

            if (t.Length == 0)
            {
                throw new FormatException("Missing numeric value before suffix.");
            }

            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Invalid number: \"{s.Trim()}\".");
            }

            var hz = value * multiplier;
            if (double.IsNaN(hz) || double.IsInfinity(hz) || hz <= 0.0)
            {
                throw new FormatException("Value must be a positive finite frequency in Hz.");
            }

            return hz;
        }
    }
}
