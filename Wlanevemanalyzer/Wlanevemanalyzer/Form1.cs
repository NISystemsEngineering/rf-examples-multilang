// =============================================================================
// WlanEvmAnalyzer.cs
// RFmx WLAN 802.11be — Mean RMS EVM + Data Constellation Display
//
// References required in your project:
//   Assemblies (Right-click References → Add Reference → Assemblies):
//     - System.Windows.Forms.DataVisualization
//
//   NI Driver DLLs (Right-click References → Add Reference → Browse):
//     - NationalInstruments.RFmx.InstrMX.Fx40.dll
//     - NationalInstruments.RFmx.WlanMX.Fx40.dll
//     Typically found under:
//     C:\Program Files\National Instruments\MeasurementStudioVS20xx\
//         DotNET\Assemblies\CurrentVersion\
// =============================================================================

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.WlanMX;


    public class MainForm : Form
    {
        // ---------------------------------------------------------------
        // Instrument Configuration
        // ---------------------------------------------------------------
        private const string RESOURCE_NAME = "RFSA";
        private const string FREQUENCY_REFERENCE_SOURCE = RFmxInstrMXConstants.OnboardClock;
        private const double FREQUENCY_REFERENCE_FREQUENCY = 10e6;

        // ---------------------------------------------------------------
        // RF Configuration
        // ---------------------------------------------------------------
        private const double CENTER_FREQUENCY = 5.180e9;   // Hz
        private const double REFERENCE_LEVEL = 0.0;       // dBm
        private const double EXTERNAL_ATTENUATION = 0.0;       // dB

        // ---------------------------------------------------------------
        // Trigger Configuration
        // ---------------------------------------------------------------
        private const RFmxWlanMXIQPowerEdgeTriggerSlope TRIGGER_SLOPE
                                                            = RFmxWlanMXIQPowerEdgeTriggerSlope.Rising;
        private const double TRIGGER_LEVEL = -20.0;     // dBm (relative)
        private const double TRIGGER_DELAY = 0.0;       // seconds
        private const RFmxWlanMXTriggerMinimumQuietTimeMode MINIMUM_QUIET_TIME_MODE
                                                            = RFmxWlanMXTriggerMinimumQuietTimeMode.Auto;
        private const double MINIMUM_QUIET_TIME = 5e-6;      // seconds
        private const bool IQ_POWER_EDGE_ENABLED = true;

        // ---------------------------------------------------------------
        // WLAN Signal Configuration
        // ---------------------------------------------------------------
        private const RFmxWlanMXStandard STANDARD = RFmxWlanMXStandard.Standard802_11be;
        private const double CHANNEL_BANDWIDTH = 320e6;     // Hz — max for 802.11be

        // ---------------------------------------------------------------
        // OfdmModAcc Measurement Configuration
        // ---------------------------------------------------------------
        private const int MEASUREMENT_OFFSET = 0;         // symbols
        private const int MAXIMUM_MEASUREMENT_LENGTH = 16;     // seconds
        private const RFmxWlanMXOfdmModAccFrequencyErrorEstimationMethod FREQUENCY_ERROR_ESTIMATION_METHOD
                                                            = RFmxWlanMXOfdmModAccFrequencyErrorEstimationMethod.PreambleAndPilots;
        private const RFmxWlanMXOfdmModAccAmplitudeTrackingEnabled AMPLITUDE_TRACKING_ENABLED
                                                            = RFmxWlanMXOfdmModAccAmplitudeTrackingEnabled.False;
        private const RFmxWlanMXOfdmModAccPhaseTrackingEnabled PHASE_TRACKING_ENABLED
                                                            = RFmxWlanMXOfdmModAccPhaseTrackingEnabled.False;
        private const RFmxWlanMXOfdmModAccSymbolClockErrorCorrectionEnabled SYMBOL_CLOCK_ERROR_CORRECTION_ENABLED
                                                            = RFmxWlanMXOfdmModAccSymbolClockErrorCorrectionEnabled.True;
        private const RFmxWlanMXOfdmModAccChannelEstimationType CHANNEL_ESTIMATION_TYPE
                                                            = RFmxWlanMXOfdmModAccChannelEstimationType.Reference;
        private const RFmxWlanMXOfdmModAccAveragingEnabled AVERAGING_ENABLED
                                                            = RFmxWlanMXOfdmModAccAveragingEnabled.False;
        private const int AVERAGING_COUNT = 10;
        private const double MEASUREMENT_TIMEOUT = 10.0;      // seconds

        // ---------------------------------------------------------------
        // UI Controls
        // ---------------------------------------------------------------
        private Button btnMeasure;
        private Label lblEvmCaption;
        private Label lblEvmValue;
        private Label lblSymbolCount;
        private Label lblStatus;
        private GroupBox grpResults;
        private GroupBox grpConstellation;
        private Chart chart;

        // ---------------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------------
        public MainForm()
        {
            InitializeComponents();
        }

        // ---------------------------------------------------------------
        // UI Layout
        // ---------------------------------------------------------------
        private void InitializeComponents()
        {
            this.Text = "RFmx WLAN 802.11be Analyzer";
            this.Size = new Size(900, 700);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // -- Measure button ------------------------------------------
            btnMeasure = new Button
            {
                Text = "Measure",
                Location = new Point(12, 12),
                Size = new Size(120, 36),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 114, 188),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnMeasure.FlatAppearance.BorderSize = 0;
            btnMeasure.Click += BtnMeasure_Click;

            // -- Results group box ----------------------------------------
            grpResults = new GroupBox
            {
                Text = "Measurement Results",
                Location = new Point(148, 6),
                Size = new Size(360, 52)
            };

            lblEvmCaption = new Label
            {
                Text = "Mean RMS EVM:",
                Location = new Point(8, 22),
                Size = new Size(110, 20),
                TextAlign = ContentAlignment.MiddleRight
            };

            lblEvmValue = new Label
            {
                Text = "---",
                Location = new Point(122, 22),
                Size = new Size(130, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 114, 188),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblSymbolCount = new Label
            {
                Text = "",
                Location = new Point(258, 22),
                Size = new Size(95, 20),
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            grpResults.Controls.Add(lblEvmCaption);
            grpResults.Controls.Add(lblEvmValue);
            grpResults.Controls.Add(lblSymbolCount);

            // -- Status label ---------------------------------------------
            lblStatus = new Label
            {
                Text = "Ready. Configure RESOURCE_NAME and click Measure.",
                Location = new Point(12, 62),
                Size = new Size(860, 20),
                ForeColor = Color.DimGray
            };

            // -- Constellation group box ----------------------------------
            grpConstellation = new GroupBox
            {
                Text = "Data Constellation",
                Location = new Point(12, 86),
                Size = new Size(860, 566),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                           AnchorStyles.Left | AnchorStyles.Right
            };

            // -- Chart control --------------------------------------------
            chart = new Chart { Dock = DockStyle.Fill };
            InitializeChart();
            grpConstellation.Controls.Add(chart);

            // -- Add controls to form ------------------------------------
            this.Controls.Add(btnMeasure);
            this.Controls.Add(grpResults);
            this.Controls.Add(lblStatus);
            this.Controls.Add(grpConstellation);

            // -- Resize handler ------------------------------------------
            this.Resize += (s, e) =>
            {
                int w = this.ClientSize.Width;
                int h = this.ClientSize.Height;
                grpConstellation.Size = new Size(w - 24, h - 96);
                lblStatus.Size = new Size(w - 24, 20);
            };
        }

        // ---------------------------------------------------------------
        // Chart Initialization
        // ---------------------------------------------------------------
        private void InitializeChart()
        {
            chart.BackColor = Color.White;

            var area = new ChartArea("ConstellationArea");
            area.BackColor = Color.White;
            area.BorderColor = Color.LightGray;
            area.BorderWidth = 1;

            // X axis — In-Phase (I)
            area.AxisX.Title = "In-Phase (I)";
            area.AxisX.TitleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            area.AxisX.LineColor = Color.Gray;
            area.AxisX.Crossing = 0;

            // Y axis — Quadrature (Q)
            area.AxisY.Title = "Quadrature (Q)";
            area.AxisY.TitleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            area.AxisY.LineColor = Color.Gray;
            area.AxisY.Crossing = 0;

            chart.ChartAreas.Add(area);

            chart.Titles.Add(new Title(
                "I/Q Data Constellation",
                Docking.Top,
                new Font("Segoe UI", 10f, FontStyle.Bold),
                Color.FromArgb(50, 50, 50)));

            // SeriesChartType.Point = scatter plot, ideal for constellation
            var series = new Series("Constellation")
            {
                ChartType = SeriesChartType.Point,
                ChartArea = "ConstellationArea",
                Color = Color.FromArgb(0, 114, 188),
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 4,
                MarkerColor = Color.FromArgb(0, 114, 188),
                IsXValueIndexed = false
            };
            chart.Series.Add(series);
        }

        // ---------------------------------------------------------------
        // Measure Button Handler
        // ---------------------------------------------------------------
        private void BtnMeasure_Click(object sender, EventArgs e)
        {
            btnMeasure.Enabled = false;
            lblEvmValue.Text = "---";
            lblSymbolCount.Text = "";
            ClearConstellation();
            Application.DoEvents();

            RFmxInstrMX instrSession = null;
            RFmxWlanMX wlan = null;
            try
            {
                // 1. Open instrument session and get WLAN personality
                SetStatus("Opening RFmx session...");
                instrSession = new RFmxInstrMX(RESOURCE_NAME, "");
                wlan = instrSession.GetWlanSignalConfiguration();

                // 2. Configure frequency reference
                instrSession.ConfigureFrequencyReference(
                    "",
                    FREQUENCY_REFERENCE_SOURCE,
                    FREQUENCY_REFERENCE_FREQUENCY);

                // 3. Configure RF
                SetStatus("Configuring RF...");
                wlan.ConfigureFrequency("", CENTER_FREQUENCY);
                wlan.ConfigureReferenceLevel("", REFERENCE_LEVEL);
                wlan.ConfigureExternalAttenuation("", EXTERNAL_ATTENUATION);

                // 4. Configure trigger
                
                wlan.ConfigureIQPowerEdgeTrigger(
                    "",
                    "0",
                    TRIGGER_SLOPE,
                    TRIGGER_LEVEL,
                    TRIGGER_DELAY,
                    MINIMUM_QUIET_TIME_MODE,
                    MINIMUM_QUIET_TIME,
                    RFmxWlanMXIQPowerEdgeTriggerLevelType.Relative,
                    IQ_POWER_EDGE_ENABLED);

                wlan.DisableTrigger("");

                // 5. Configure standard and channel bandwidth
                wlan.ConfigureStandard("", STANDARD);
                wlan.ConfigureChannelBandwidth("", CHANNEL_BANDWIDTH);

                // 6. Configure OfdmModAcc measurement
                SetStatus("Configuring OfdmModAcc...");
                wlan.SelectMeasurements("", RFmxWlanMXMeasurementTypes.OfdmModAcc, true);
                wlan.OfdmModAcc.Configuration.ConfigureMeasurementLength(
                    "", MEASUREMENT_OFFSET, MAXIMUM_MEASUREMENT_LENGTH);
                wlan.OfdmModAcc.Configuration.ConfigureFrequencyErrorEstimationMethod(
                    "", FREQUENCY_ERROR_ESTIMATION_METHOD);
                wlan.OfdmModAcc.Configuration.ConfigureAmplitudeTrackingEnabled(
                    "", AMPLITUDE_TRACKING_ENABLED);
                wlan.OfdmModAcc.Configuration.ConfigurePhaseTrackingEnabled(
                    "", PHASE_TRACKING_ENABLED);
                wlan.OfdmModAcc.Configuration.ConfigureSymbolClockErrorCorrectionEnabled(
                    "", SYMBOL_CLOCK_ERROR_CORRECTION_ENABLED);
                wlan.OfdmModAcc.Configuration.ConfigureChannelEstimationType(
                    "", CHANNEL_ESTIMATION_TYPE);
                wlan.OfdmModAcc.Configuration.ConfigureAveraging(
                    "", AVERAGING_ENABLED, AVERAGING_COUNT);

                // Enable traces so constellation data is returned
                wlan.OfdmModAcc.Configuration.SetAllTracesEnabled("", true);

                // 7. Initiate measurement
                SetStatus("Measuring — waiting for trigger...");
                wlan.Initiate("", "");

                // 8. Fetch EVM result
                wlan.OfdmModAcc.Results.GetCompositeRmsEvmMean("", out double evmMean);
                lblEvmValue.Text = $"{evmMean:F4} % rms";

                // 9. Fetch constellation trace
                SetStatus("Fetching constellation trace...");
                NationalInstruments.ComplexSingle[] constellation = null;
                wlan.OfdmModAcc.Results.FetchDataConstellationTrace(
                    "",
                    MEASUREMENT_TIMEOUT,
                    ref constellation);

                // 10. Plot constellation
                SetStatus("Rendering constellation...");
                PlotConstellation(constellation);

                lblSymbolCount.Text = $"({constellation.Length} symbols)";
                SetStatus(
                    $"Done.  Mean RMS EVM = {evmMean:F4} % rms  |  " +
                    $"Symbols plotted: {constellation.Length}");
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.Crimson;
                lblStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Close instrument session — this also releases the WLAN personality
                try { instrSession?.Close(); }
                catch { /* suppress close errors */ }

                btnMeasure.Enabled = true;
            }
        }

        // ---------------------------------------------------------------
        // Constellation Plot Helpers
        // ---------------------------------------------------------------
        private void PlotConstellation(NationalInstruments.ComplexSingle[] constellation)
        {
            var series = chart.Series["Constellation"];
            series.Points.Clear();
            series.Points.SuspendUpdates();

            foreach (var point in constellation)
                series.Points.AddXY(point.Real, point.Imaginary);

            series.Points.ResumeUpdates();

            AutoScaleAxes(constellation);
            chart.Invalidate();
        }

        private void ClearConstellation()
        {
            chart.Series["Constellation"].Points.Clear();
            var area = chart.ChartAreas["ConstellationArea"];
            area.AxisX.Minimum = double.NaN;
            area.AxisX.Maximum = double.NaN;
            area.AxisY.Minimum = double.NaN;
            area.AxisY.Maximum = double.NaN;
            chart.Invalidate();
        }

        private void AutoScaleAxes(NationalInstruments.ComplexSingle[] constellation)
        {
            // Find the largest absolute I or Q value across all symbols
            double max = 0;
            foreach (var p in constellation)
            {
                double absI = Math.Abs(p.Real);
                double absQ = Math.Abs(p.Imaginary);
                if (absI > max) max = absI;
                if (absQ > max) max = absQ;
            }

            // Apply symmetrically with 10% padding so plot stays square
            double limit = max * 1.1;
            var area = chart.ChartAreas["ConstellationArea"];
            area.AxisX.Minimum = -limit;
            area.AxisX.Maximum = limit;
            area.AxisY.Minimum = -limit;
            area.AxisY.Maximum = limit;
        }

        // ---------------------------------------------------------------
        // Status Helper
        // ---------------------------------------------------------------
        private void SetStatus(string message)
        {
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Text = message;
            Application.DoEvents();
        }

    }