from PySide6.QtWidgets import QWidget, QVBoxLayout, QLabel


class AclrTab(QWidget):
    """Placeholder widget for ACLR plots and related controls."""

    def __init__(self, parent=None) -> None:
        super().__init__(parent)

        layout = QVBoxLayout(self)
        layout.addWidget(QLabel("ACLR plots and controls will go here."))

