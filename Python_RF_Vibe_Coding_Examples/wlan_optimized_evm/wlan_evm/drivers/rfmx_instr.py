"""
NI-RFmxInstr instrument session management.

This module provides thin wrappers around nirfmxinstr so that the
rest of the application is insulated from driver details.
"""

from __future__ import annotations

from contextlib import contextmanager
from typing import Iterator

import nirfmxinstr


@contextmanager
def open_session(resource_name: str) -> Iterator[nirfmxinstr.Session]:
    """
    Context manager that opens and closes an RFmxInstr session.

    Parameters
    ----------
    resource_name:
        NI-RFSA/RFmx resource name, e.g. "RFSA", "Dev1", "PXI1Slot2".
    """
    session: nirfmxinstr.Session | None = None
    try:
        session = nirfmxinstr.Session(resource_name=resource_name, option_string="")
        yield session
    finally:
        if session is not None:
            session.close()
