from PySide6.QtWidgets import (
    QWidget,
    QVBoxLayout,
    QLabel,
    QPushButton,
    QHBoxLayout,
    QMessageBox,
)
from PySide6.QtCore import Qt

from matplotlib.backends.backend_qtagg import FigureCanvasQTAgg as FigureCanvas
from matplotlib.figure import Figure

from wlan_evm.measurement.evm_measurement import run_evm_11be_320mhz_1024qam
from wlan_evm.visualization.constellations import plot_evm_constellation


class EvmTab(QWidget):
    """Widget for EVM measurement and constellation display."""

    def __init__(self, parent=None) -> None:
        super().__init__(parent)

        # Make the constellation plot clearer:
        # - square plot area (width ~= height)
        # - taller canvas (about 1.8x the previous height)
        self._figure = Figure(figsize=(7.2, 7.2))
        self._canvas = FigureCanvas(self._figure)
        self._ax = self._figure.add_subplot(111)

        self._evm_label = QLabel("Average RMS EVM (data): -- %")
        self._evm_label.setAlignment(Qt.AlignCenter)
        # Light green background to make the result stand out, without affecting the plot.
        self._evm_label.setStyleSheet(
            "QLabel {"
            "  background-color: rgba(173, 216, 230, 180);"
            "  color: #000000;"
            "  font-weight: bold;"
            "  font-size: 16px;"
            "  padding: 8px 12px;"
            "  border: 1px solid rgba(0, 0, 0, 50);"
            "  border-radius: 6px;"
            "}"
        )
        self._measure_button = QPushButton("Measure EVM")
        self._measure_button.setStyleSheet(
            "QPushButton {"
            "  background-color: rgba(90, 190, 90, 210);"
            "  color: #000000;"
            "  font-weight: bold;"
            "  font-size: 14px;"
            "  padding: 8px 12px;"
            "  border: 1px solid rgba(0, 0, 0, 60);"
            "  border-radius: 6px;"
            "}"
            "QPushButton:hover {"
            "  background-color: rgba(90, 190, 90, 255);"
            "}"
        )
        self._measure_button.clicked.connect(self._on_measure_clicked)

        controls_layout = QHBoxLayout()
        controls_layout.addWidget(self._measure_button)
        controls_layout.addStretch(1)
        controls_layout.addWidget(self._evm_label)

        layout = QVBoxLayout(self)
        layout.addLayout(controls_layout)
        layout.addWidget(self._canvas)

    def _on_measure_clicked(self) -> None:
        """Trigger an RFmx-based EVM measurement and update the UI."""
        # TODO: Make resource name configurable from UI; "RFSA" is a common default.
        try:
            result = run_evm_11be_320mhz_1024qam(resource_name="RFSA")
        except Exception as exc:  # noqa: BLE001
            # Show RFmx / driver errors to the user.
            msg = QMessageBox(self)
            msg.setIcon(QMessageBox.Critical)
            msg.setWindowTitle("EVM Measurement Error")
            msg.setText("An error occurred while running the EVM measurement.")
            msg.setDetailedText(str(exc))
            msg.exec()
            return

        plot_evm_constellation(
            self._ax,
            result.iq_data,
            title="802.11be 320 MHz 1024-QAM Constellation",
        )
        self._canvas.draw_idle()

        self._evm_label.setText(
            f"Average RMS EVM (data): {result.avg_rms_evm_percent:.2f} %"
        )

        # Indicate completion without error in the main window status bar.
        window = self.window()
        if hasattr(window, "statusBar"):
            window.statusBar().showMessage("EVM measurement completed successfully.", 5000)

