"""
Simple RFSG GUI to:
1) Load a TDMS exported waveform.
2) Configure NI-RFSG in Script mode with:
   - repeat forever
   - generate waveform marker0(0)
3) Route marker output to PXI_Trig0.

This is a minimal GUI for validating waveform + marker behavior.
"""

from __future__ import annotations

import os
import traceback
import time
from dataclasses import dataclass
from pathlib import Path

from nirfsg import GenerationMode, ResetWithOptionsStepsToOmit, Session
from nirfsg.errors import DriverError
from PySide6.QtCore import QThread, Qt, Signal
from PySide6.QtWidgets import (
    QApplication,
    QCheckBox,
    QFileDialog,
    QDoubleSpinBox,
    QGridLayout,
    QGroupBox,
    QLabel,
    QLineEdit,
    QMainWindow,
    QPushButton,
    QVBoxLayout,
    QWidget,
)

from wlan_evm.rfsg.rfsg_tdms_infinite_marker_test import (
    DEFAULT_SCRIPT_NAME,
    DEFAULT_WAVEFORM_NAME,
)
from wlan_evm.waveforms.rfsg_scripts import (
    RfsgScriptConfig,
    generate_waveform_repeat_forever_script,
)
from wlan_evm.waveforms.tdms_waveform import read_tdms_iq_waveform

import numpy as np


@dataclass(frozen=True)
class RfsgGuiInputs:
    tdms_path: Path
    rfsg_resource: str
    center_frequency_hz: float
    power_level_dbm: float
    simulate: bool = False
    use_dotnet_playback: bool = False
    clear_stale_state: bool = False


class RfsgWorker(QThread):
    status_changed = Signal(str)
    error_occurred = Signal(str)

    def __init__(self, inputs: RfsgGuiInputs, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._inputs = inputs
        self._stop_requested = False

    def request_stop(self) -> None:
        self._stop_requested = True

    def _try_load_ni_dotnet_assemblies(self) -> list[str]:
        """
        Load NI RFSG .NET assemblies using absolute paths.

        Returns a list of searched base directories for diagnostics.
        """
        import clr  # type: ignore

        # Optional user overrides.
        env_roots: list[Path] = []
        for key in ("NIRFSG_DOTNET_DIR", "NIRFSG_PLAYBACK_DOTNET_DIR", "NIDOTNET_DIR"):
            val = os.environ.get(key)
            if val:
                env_roots.append(Path(val))

        default_roots = [
            Path(r"C:\Program Files\IVI Foundation\IVI\Microsoft.NET\Framework64\v4.0.30319"),
            Path(r"C:\Program Files\National Instruments\Shared\Measurement Studio\DotNET\Assemblies\Current"),
            Path(r"C:\Program Files (x86)\National Instruments\MeasurementStudioVS2010\DotNET\Assemblies\Current"),
        ]

        candidate_roots: list[Path] = []
        seen = set()
        for p in [*env_roots, *default_roots]:
            key = str(p).lower()
            if key not in seen:
                candidate_roots.append(p)
                seen.add(key)

        rfsg_simple = "NationalInstruments.ModularInstruments.NIRfsg.Fx40.dll"
        playback_simple = "NationalInstruments.ModularInstruments.NIRfsgPlayback.Fx40.dll"

        # Exact and one-level-deep candidates to handle versioned folders.
        rfsg_candidates: list[Path] = []
        playback_candidates: list[Path] = []
        for root in candidate_roots:
            rfsg_candidates.append(root / rfsg_simple)
            playback_candidates.append(root / playback_simple)
            try:
                for child in root.iterdir():
                    if child.is_dir():
                        rfsg_candidates.append(child / rfsg_simple)
                        playback_candidates.append(child / playback_simple)
            except Exception:  # noqa: BLE001
                continue

        rfsg_path = next((p for p in rfsg_candidates if p.exists()), None)
        playback_path = next((p for p in playback_candidates if p.exists()), None)

        searched = [str(p) for p in candidate_roots]
        if rfsg_path is None or playback_path is None:
            raise FileNotFoundError(
                "Could not locate NI RFSG .NET assemblies.\n"
                f"Missing: {'NIRfsg' if rfsg_path is None else ''}"
                f"{' and ' if rfsg_path is None and playback_path is None else ''}"
                f"{'NIRfsgPlayback' if playback_path is None else ''}\n"
                "Searched base folders:\n- "
                + "\n- ".join(searched)
                + "\n\nSet one of these environment variables to your NI .NET folder and retry:\n"
                "- NIRFSG_DOTNET_DIR\n- NIRFSG_PLAYBACK_DOTNET_DIR\n- NIDOTNET_DIR"
            )

        clr.AddReference(str(rfsg_path))
        clr.AddReference(str(playback_path))
        return searched

    def _run_with_dotnet_playback(self, inputs: RfsgGuiInputs, script_text: str) -> None:
        if inputs.simulate:
            raise RuntimeError(
                ".NET Playback mode does not currently support Simulate. "
                "Uncheck Simulate for hardware runs, or use the nirfsg mode."
            )

        self.status_changed.emit("Loading .NET RFSG assemblies...")
        self._try_load_ni_dotnet_assemblies()

        import NationalInstruments.ModularInstruments.NIRfsg as DotNetRfsg  # type: ignore
        import NationalInstruments.ModularInstruments.NIRfsgPlayback as DotNetPlayback  # type: ignore

        self.status_changed.emit("Opening RFSG session via .NET...")
        rfsg = DotNetRfsg.NIRfsg(str(inputs.rfsg_resource), True, False)
        initiated = False
        try:
            self.status_changed.emit("Configuring RF settings...")
            rfsg.RF.Configure(float(inputs.center_frequency_hz), float(inputs.power_level_dbm))
            rfsg.RF.PowerLevelType = DotNetRfsg.RfsgRFPowerLevelType.PeakPower

            # Marker export intentionally disabled for now.

            self.status_changed.emit("Downloading TDMS waveform via RFSGPlayback...")
            rfsg_handle = rfsg.GetInstrumentHandle().DangerousGetHandle()
            DotNetPlayback.NIRfsgPlayback.ReadAndDownloadWaveformFromFile(
                rfsg_handle,
                str(inputs.tdms_path),
                DEFAULT_WAVEFORM_NAME,
            )

            self.status_changed.emit("Setting generation mode to Script...")
            rfsg.Arb.GenerationMode = DotNetRfsg.RfsgWaveformGenerationMode.Script

            self.status_changed.emit("Writing script via RFSGPlayback...")
            DotNetPlayback.NIRfsgPlayback.SetScriptToGenerateSingleRfsg(
                rfsg_handle,
                script_text,
            )

            self.status_changed.emit("Initiating generation. Running until Stop...")
            rfsg.Initiate()
            initiated = True
            while not self._stop_requested:
                time.sleep(0.1)
        finally:
            if initiated:
                try:
                    rfsg.Abort()
                except Exception:  # noqa: BLE001
                    pass
            try:
                rfsg.Close()
            except Exception:  # noqa: BLE001
                pass

    def run(self) -> None:
        try:
            inputs = self._inputs
            if not inputs.tdms_path.exists():
                raise FileNotFoundError(f"TDMS file not found: {inputs.tdms_path}")

            script_text = generate_waveform_repeat_forever_script(
                RfsgScriptConfig(
                    waveform_name=DEFAULT_WAVEFORM_NAME,
                    marker_name="marker0",
                    marker_sample_index=0,
                    repeat_forever=True,
                )
            )

            if inputs.use_dotnet_playback:
                self._run_with_dotnet_playback(inputs, script_text)
                self.status_changed.emit("Stopped.")
                return

            # Enforce scaling: compute max(|IQ|) from TDMS and apply runtime
            # scaling so that max(|IQ|) becomes 1.
            tdms_wf = read_tdms_iq_waveform(inputs.tdms_path)
            max_abs = float(np.max(np.abs(tdms_wf.iq)))
            if not np.isfinite(max_abs) or max_abs <= 0:
                runtime_scaling_db = 0.0
            else:
                runtime_scaling_db = 20.0 * float(np.log10(1.0 / max_abs))

            # Keep NI-RFSG session initialization conservative to avoid long
            # timeouts during driver setup.
            options: dict[str, object] = {
                "queryinstrstatus": False,
                "rangecheck": False,
                "cache": True,
            }
            if inputs.simulate:
                options["simulate"] = True

            self.status_changed.emit(
                f"Opening RFSG session... resource={inputs.rfsg_resource} simulate={'1' if inputs.simulate else '0'}"
            )
            with Session(
                resource_name=inputs.rfsg_resource,
                id_query=False,
                reset_device=False,
                options=options,
            ) as session:
                self.status_changed.emit("RFSG session opened.")
                if inputs.clear_stale_state:
                    # Optional: some setups need explicit stale-state clearing,
                    # but this can fail on Session Access style environments.
                    self.status_changed.emit("Clearing stale RFSG state...")
                    try:
                        session.abort()
                    except Exception:  # noqa: BLE001
                        pass
                    try:
                        session.reset_with_options(ResetWithOptionsStepsToOmit.ROUTES)
                    except Exception:  # noqa: BLE001
                        pass
                self.status_changed.emit("Setting center frequency and power...")
                session.upconverter_center_frequency = float(inputs.center_frequency_hz)
                session.power_level = float(inputs.power_level_dbm)

                self.status_changed.emit("Downloading waveform from TDMS...")
                try:
                    session.read_and_download_waveform_from_file_tdms(
                        waveform_name=DEFAULT_WAVEFORM_NAME,
                        file_path=str(inputs.tdms_path),
                        waveform_index=0,
                    )
                except DriverError as exc:
                    # In Simulate mode, some NI-RFSG driver builds do not support
                    # ReadAndDownloadWaveformFromFileTdms. Fall back to loading IQ
                    # data in Python and writing it into the waveform memory.
                    if "Function or method not supported" not in str(exc) and exc.code != -1074135023:
                        raise

                    self.status_changed.emit(
                        "TDMS read/download unsupported; writing IQ waveform directly..."
                    )
                    if tdms_wf.sample_rate_hz is not None:
                        self.status_changed.emit(
                            f"Setting IQ rate to {tdms_wf.sample_rate_hz:.3f} Hz..."
                        )
                        session.iq_rate = float(tdms_wf.sample_rate_hz)

                    iq = np.asarray(tdms_wf.iq, dtype=np.complex128)
                    session.allocate_arb_waveform(
                        waveform_name=DEFAULT_WAVEFORM_NAME,
                        size_in_samples=int(iq.size),
                    )
                    session.write_arb_waveform(
                        waveform_name=DEFAULT_WAVEFORM_NAME,
                        waveform_data_array=iq,
                    )

                if runtime_scaling_db != 0.0:
                    self.status_changed.emit(
                        f"Applying runtime scaling {runtime_scaling_db:.3f} dB..."
                    )
                    session.waveform_runtime_scaling = float(runtime_scaling_db)

                self.status_changed.emit("Configuring RFSG generation mode...")
                session.generation_mode = GenerationMode.SCRIPT
                # Marker export intentionally disabled for now.

                self.status_changed.emit("Writing and selecting script...")
                session.write_script(script_text)
                session.selected_script = DEFAULT_SCRIPT_NAME

                self.status_changed.emit("Initiating generation. Running until Stop...")
                with session.initiate():
                    while not self._stop_requested:
                        time.sleep(0.1)

            self.status_changed.emit("Stopped.")

        except Exception as exc:  # noqa: BLE001
            # Some NI-related exceptions don't provide a helpful `str(exc)`.
            # Emit full traceback so the GUI shows the actual failing call.
            tb = traceback.format_exc()
            detail = f"{type(exc).__name__}: {exc}\n\n{tb}"
            # Also print + log, so you can copy/paste even if the GUI label
            # isn't selectable.
            print(detail)
            try:
                log_path = Path.cwd() / "rfsg_tdms_gui_error.log"
                log_path.write_text(detail, encoding="utf-8")
            except Exception:
                # If logging fails, the GUI label still gets the traceback.
                pass
            self.error_occurred.emit(detail)


class RfsgTdmsGui(QMainWindow):
    def __init__(self) -> None:
        super().__init__()
        self.setWindowTitle("RFSG TDMS Infinite Marker GUI")
        self.resize(720, 360)

        self._worker: RfsgWorker | None = None

        root = QWidget()
        self.setCentralWidget(root)
        layout = QVBoxLayout(root)

        form = QGroupBox("Inputs")
        form_layout = QGridLayout(form)

        self._tdms_path = QLineEdit()
        browse_btn = QPushButton("Browse TDMS...")
        browse_btn.clicked.connect(self._on_browse_tdms)

        self._resource = QLineEdit()
        self._resource.setPlaceholderText("e.g. 5841 or PXI1Slot2/0 (depends on MAX/IVI)")

        self._center_freq = QDoubleSpinBox()
        self._center_freq.setDecimals(3)
        self._center_freq.setRange(1.0, 50_000_000_000.0)
        self._center_freq.setValue(2.412e9)

        self._power_dbm = QDoubleSpinBox()
        self._power_dbm.setDecimals(2)
        self._power_dbm.setRange(-200.0, 50.0)
        self._power_dbm.setValue(-10.0)

        self._simulate = QCheckBox("Simulate (no hardware)")
        self._use_dotnet_playback = QCheckBox("Use .NET Playback mode (hardware)")
        self._clear_stale_state = QCheckBox("Clear stale RFSG state (Abort/Reset)")

        form_layout.addWidget(QLabel("TDMS file:"), 0, 0)
        form_layout.addWidget(self._tdms_path, 0, 1)
        form_layout.addWidget(browse_btn, 0, 2)

        form_layout.addWidget(QLabel("RFSG Resource:"), 1, 0)
        form_layout.addWidget(self._resource, 1, 1, 1, 2)

        form_layout.addWidget(QLabel("Center Frequency (Hz):"), 2, 0)
        form_layout.addWidget(self._center_freq, 2, 1, 1, 2)

        form_layout.addWidget(QLabel("Power Level (dBm):"), 3, 0)
        form_layout.addWidget(self._power_dbm, 3, 1, 1, 2)

        form_layout.addWidget(self._simulate, 4, 1, 1, 2)
        form_layout.addWidget(self._use_dotnet_playback, 5, 1, 1, 2)
        form_layout.addWidget(self._clear_stale_state, 6, 1, 1, 2)

        layout.addWidget(form)

        buttons = QGroupBox("Control")
        buttons_layout = QGridLayout(buttons)

        self._run_btn = QPushButton("Run")
        self._stop_btn = QPushButton("Stop")
        self._stop_btn.setEnabled(False)

        self._run_btn.clicked.connect(self._on_run_clicked)
        self._stop_btn.clicked.connect(self._on_stop_clicked)

        buttons_layout.addWidget(self._run_btn, 0, 0)
        buttons_layout.addWidget(self._stop_btn, 0, 1)

        layout.addWidget(buttons)

        self._status_label = QLabel("Idle.")
        self._status_label.setAlignment(Qt.AlignLeft | Qt.AlignVCenter)
        self._status_label.setWordWrap(True)
        layout.addWidget(self._status_label)

    def _on_browse_tdms(self) -> None:
        path, _ = QFileDialog.getOpenFileName(
            self,
            "Select TDMS waveform file",
            str(Path.home()),
            "TDMS files (*.tdms);;All files (*.*)",
        )
        if path:
            self._tdms_path.setText(path)

    def _on_run_clicked(self) -> None:
        if self._worker is not None and self._worker.isRunning():
            return

        tdms_text = self._tdms_path.text().strip()
        if not tdms_text:
            self._status_label.setText("Please select a TDMS file.")
            return
        rfsg_resource = self._resource.text().strip()
        if not rfsg_resource:
            self._status_label.setText("Please enter the RFSG resource string.")
            return

        inputs = RfsgGuiInputs(
            tdms_path=Path(tdms_text),
            rfsg_resource=rfsg_resource,
            center_frequency_hz=float(self._center_freq.value()),
            power_level_dbm=float(self._power_dbm.value()),
            simulate=bool(self._simulate.isChecked()),
            use_dotnet_playback=bool(self._use_dotnet_playback.isChecked()),
            clear_stale_state=bool(self._clear_stale_state.isChecked()),
        )

        self._worker = RfsgWorker(inputs, parent=self)
        self._worker.status_changed.connect(self._status_label.setText)
        self._worker.error_occurred.connect(self._on_worker_error)

        self._run_btn.setEnabled(False)
        self._stop_btn.setEnabled(True)
        self._status_label.setText("Starting...")
        self._worker.start()

    def _on_stop_clicked(self) -> None:
        if self._worker is None:
            return
        self._worker.request_stop()
        self._stop_btn.setEnabled(False)
        self._status_label.setText("Stopping...")

    def _on_worker_error(self, msg: str) -> None:
        self._run_btn.setEnabled(True)
        self._stop_btn.setEnabled(False)
        self._status_label.setText(f"Error: {msg}")


def main() -> None:
    app = QApplication([])
    w = RfsgTdmsGui()
    w.show()
    app.exec()


if __name__ == "__main__":
    main()

