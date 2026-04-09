using System;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using NationalInstruments.RFmx.InstrMX;
using NationalInstruments.RFmx.NRMX;
using NationalInstruments.ModularInstruments.NIRfsg;

namespace NrSweepDemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NrSweepMainForm());
        }
    }

    /// <summary>
    /// WinForms + charts + background sweep (ui.mdc).
    /// </summary>
    public sealed class NrSweepMainForm : Form
    {
        private readonly TextBox _txtRfsa = new TextBox { Width = 280 };
        private readonly TextBox _txtRfsg = new TextBox { Width = 280 };
        private readonly ComboBox _cmbFreqRef = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _txtBandwidthHz = new TextBox { Width = 120, Text = "100000000" };
        private readonly TextBox _txtWaveformPath = new TextBox { Width = 400 };
        private readonly Button _btnBrowse = new Button { Text = "Browse...", AutoSize = true };
        private readonly TextBox _txtCenterHz = new TextBox { Width = 160, Text = "3500000000" };
        private readonly TextBox _txtPwrStart = new TextBox { Width = 80, Text = "-30" };
        private readonly TextBox _txtPwrStop = new TextBox { Width = 80, Text = "-10" };
        private readonly TextBox _txtPwrStep = new TextBox { Width = 80, Text = "1" };
        private readonly Button _btnStart = new Button { Text = "Start sweep", AutoSize = true };
        private readonly Label _lblStatus = new Label { AutoSize = true, Text = "Idle." };
        private readonly Chart _chartConstellation = new Chart();
        private readonly Chart _chartBathtub = new Chart();

        private CancellationTokenSource _cts;

        public NrSweepMainForm()
        {
            Text = "NR power sweep (RFmx ModAcc + RFSG)";
            Width = 1100;
            Height = 750;
            StartPosition = FormStartPosition.CenterScreen;

            foreach (var s in Enum.GetNames(typeof(RFmxInstrMXFrequencyReferenceSource)))
                _cmbFreqRef.Items.Add(s);
            _cmbFreqRef.SelectedItem = nameof(RFmxInstrMXFrequencyReferenceSource.PxiClock);

            _btnBrowse.Click += (_, __) =>
            {
                using (var d = new OpenFileDialog { Filter = "Waveform files|*.tdms;*.TDMS|All files|*.*" })
                    if (d.ShowDialog() == DialogResult.OK)
                        _txtWaveformPath.Text = d.FileName;
            };

            _btnStart.Click += async (_, __) =>
            {
                try
                {
                    await StartSweepAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Sweep error");
                }
            };

            var panelTop = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            panelTop.Controls.Add(new Label { Text = "RFSA resource", AutoSize = true, Anchor = AnchorStyles.Left });
            panelTop.Controls.Add(_txtRfsa);
            panelTop.Controls.Add(new Label { Text = "RFSG resource", AutoSize = true, Anchor = AnchorStyles.Left });
            panelTop.Controls.Add(_txtRfsg);

            var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            row2.Controls.Add(new Label { Text = "Freq ref", AutoSize = true });
            row2.Controls.Add(_cmbFreqRef);
            row2.Controls.Add(new Label { Text = "NR channel BW (Hz)", AutoSize = true });
            row2.Controls.Add(_txtBandwidthHz);
            row2.Controls.Add(new Label { Text = "Center freq (Hz)", AutoSize = true });
            row2.Controls.Add(_txtCenterHz);

            var row3 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            row3.Controls.Add(new Label { Text = "NR waveform (TDMS)", AutoSize = true });
            row3.Controls.Add(_txtWaveformPath);
            row3.Controls.Add(_btnBrowse);
            row3.Controls.Add(new Label { Text = "Pwr start/stop/step (dBm)", AutoSize = true });
            row3.Controls.Add(_txtPwrStart);
            row3.Controls.Add(_txtPwrStop);
            row3.Controls.Add(_txtPwrStep);

            var row4 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            row4.Controls.Add(_btnStart);
            row4.Controls.Add(_lblStatus);

            _chartConstellation.Dock = DockStyle.Fill;
            _chartBathtub.Dock = DockStyle.Fill;
            InitConstellationChart(_chartConstellation);
            InitBathtubChart(_chartBathtub);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };
            split.Panel1.Controls.Add(_chartConstellation);
            split.Panel2.Controls.Add(_chartBathtub);

            Controls.Add(split);
            Controls.Add(row4);
            Controls.Add(row3);
            Controls.Add(row2);
            Controls.Add(panelTop);

            _txtRfsa.Text = "VST";
            _txtRfsg.Text = "VST";
        }

        private static void InitConstellationChart(Chart c)
        {
            c.ChartAreas.Add(new ChartArea("a"));
            var s = new Series("PDSCH") { ChartType = SeriesChartType.Point, MarkerSize = 3, MarkerStyle = MarkerStyle.Circle };
            c.Series.Add(s);
            c.ChartAreas[0].AxisX.Title = "I";
            c.ChartAreas[0].AxisY.Title = "Q";
        }

        private static void InitBathtubChart(Chart c)
        {
            c.ChartAreas.Add(new ChartArea("a"));
            var s = new Series("RMS EVM (%)") { ChartType = SeriesChartType.Line, BorderWidth = 2 };
            c.Series.Add(s);
            c.ChartAreas[0].AxisX.Title = "SG power (dBm)";
            c.ChartAreas[0].AxisY.Title = "Composite RMS EVM (%)";
        }

        private async Task StartSweepAsync()
        {
            if (_cts != null)
            {
                MessageBox.Show("A sweep is already running.");
                return;
            }

            if (!double.TryParse(_txtBandwidthHz.Text.Trim(), out var bwHz) ||
                !double.TryParse(_txtCenterHz.Text.Trim(), out var centerHz) ||
                !double.TryParse(_txtPwrStart.Text.Trim(), out var pStart) ||
                !double.TryParse(_txtPwrStop.Text.Trim(), out var pStop) ||
                !double.TryParse(_txtPwrStep.Text.Trim(), out var pStep) ||
                pStep <= 0)
            {
                MessageBox.Show("Check numeric inputs (bandwidth, center frequency, power sweep).");
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtWaveformPath.Text))
            {
                MessageBox.Show("Select a waveform file.");
                return;
            }

            if (!Enum.TryParse((string)_cmbFreqRef.SelectedItem, out RFmxInstrMXFrequencyReferenceSource rfmxRef))
            {
                MessageBox.Show("Invalid frequency reference selection.");
                return;
            }

            var rfsaName = _txtRfsa.Text.Trim();
            var rfsgName = _txtRfsg.Text.Trim();
            if (rfsaName.Length == 0 || rfsgName.Length == 0)
            {
                MessageBox.Show("Enter RFSA and RFSG resource names.");
                return;
            }

            if (pStop < pStart)
            {
                MessageBox.Show("Power stop must be >= power start.");
                return;
            }

            _cts = new CancellationTokenSource();
            _btnStart.Enabled = false;
            SetStatus("Running…");

            var token = _cts.Token;
            try
            {
                await Task.Run(() => RunHardwareSweep(
                    rfsaName, rfsgName, rfmxRef, bwHz, centerHz, _txtWaveformPath.Text.Trim(),
                    pStart, pStop, pStep, token), token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.");
            }
            catch (Exception)
            {
                SetStatus("Error.");
                throw;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _btnStart.Enabled = true;
            }
        }

        private void RunHardwareSweep(
            string rfsaResource,
            string rfsgResource,
            RFmxInstrMXFrequencyReferenceSource rfmxFreqRef,
            double channelBandwidthHz,
            double centerFrequencyHz,
            string waveformPath,
            double powerStartDbm,
            double powerStopDbm,
            double powerStepDbm,
            CancellationToken token)
        {
            using var rfsg = new RfsgNrGenerator();
            using var rfmx = new RfmxNrAnalyzer();

            rfsg.Open(rfsgResource);
            rfmx.Open(rfsaResource);

            MapRfsgFrequencyReference(rfmxFreqRef, out var rfsgRef, out var refHz);
            rfsg.ConfigureFrequencyReference(rfsgRef, refHz);

            rfsg.LoadNrWaveformFromTdms(waveformPath);
            rfsg.SelectArbitraryWaveform();
            rfsg.ConfigureRf(centerFrequencyHz, powerStartDbm);
            rfsg.CommitAndStart();

            rfmx.ConfigureAnalyzer(centerFrequencyHz, channelBandwidthHz, rfmxFreqRef, RFmxNRMXLinkDirection.Downlink);

            token.ThrowIfCancellationRequested();
            rfmx.RunStandardAutoLevel(5.0);
            token.ThrowIfCancellationRequested();
            rfmx.RunModAccAutoLevel(30.0);

            ClearChartsOnUi();
            _chartBathtub.Series[0].Points.Clear();

            for (var p = powerStartDbm; p <= powerStopDbm + 1e-6; p += powerStepDbm)
            {
                token.ThrowIfCancellationRequested();

                rfsg.SetOutputPower(p);

                rfmx.CommitConfiguration();
                rfmx.InitiateMeasurement();
                rfmx.WaitForMeasurementComplete(15.0);
                rfmx.FetchCompositeEvm(-1.0, out var rmsPct, out _);

                var iq = rfmx.FetchPdschConstellation(-1.0);

                UpdateChartsOnUi(p, rmsPct, iq);
            }

            SetStatus("Done.");
        }

        private static void MapRfsgFrequencyReference(
            RFmxInstrMXFrequencyReferenceSource rfmxRef,
            out NIRfsgFrequencyReferenceSource rfsgRef,
            out double referenceFrequencyHz)
        {
            // Demo mapping — extend if you use other RFmx reference sources.
            switch (rfmxRef)
            {
                case RFmxInstrMXFrequencyReferenceSource.PxiClock:
                    rfsgRef = NIRfsgFrequencyReferenceSource.PxiClock;
                    referenceFrequencyHz = 10e6;
                    break;
                case RFmxInstrMXFrequencyReferenceSource.OnboardClock:
                    rfsgRef = NIRfsgFrequencyReferenceSource.OnboardClock;
                    referenceFrequencyHz = 10e6;
                    break;
                default:
                    rfsgRef = NIRfsgFrequencyReferenceSource.PxiClock;
                    referenceFrequencyHz = 10e6;
                    break;
            }
        }

        private void ClearChartsOnUi()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClearChartsOnUi));
                return;
            }

            _chartConstellation.Series[0].Points.Clear();
        }

        private void UpdateChartsOnUi(double powerDbm, double rmsEvmPercent, Complex[] constellation)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateChartsOnUi(powerDbm, rmsEvmPercent, constellation)));
                return;
            }

            _chartBathtub.Series[0].Points.AddXY(powerDbm, rmsEvmPercent);

            const int maxPts = 4000;
            var step = Math.Max(1, constellation.Length / maxPts);
            _chartConstellation.Series[0].Points.Clear();
            for (var i = 0; i < constellation.Length; i += step)
            {
                var c = constellation[i];
                _chartConstellation.Series[0].Points.AddXY(c.Real, c.Imaginary);
            }

            _lblStatus.Text = $"Last: {powerDbm:0.###} dBm, RMS EVM = {rmsEvmPercent:0.###} %";
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStatus(text)));
                return;
            }

            _lblStatus.Text = text;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}
