from PySide6.QtWidgets import QWidget, QVBoxLayout, QLabel


class SettingsTab(QWidget):
    """Placeholder widget for global application settings."""

    def __init__(self, parent=None) -> None:
        super().__init__(parent)

        layout = QVBoxLayout(self)
        layout.addWidget(QLabel("Global settings, enums, and checkboxes will go here."))

