from PySide6.QtWidgets import QMainWindow, QTabWidget

from .evm_tab import EvmTab
from .aclr_tab import AclrTab
from .settings_tab import SettingsTab


class MainWindow(QMainWindow):
    """Main application window with a single tabbed central panel."""

    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("WLAN Optimized EVM")
        self.resize(1400, 900)

        self._init_tabs()
        self._init_status_bar()

    def _init_tabs(self) -> None:
        tabs = QTabWidget()
        tabs.addTab(EvmTab(self), "EVM")
        tabs.addTab(AclrTab(self), "ACLR")
        tabs.addTab(SettingsTab(self), "Settings")
        self.setCentralWidget(tabs)

    def _init_status_bar(self) -> None:
        self.statusBar().showMessage("Ready")

