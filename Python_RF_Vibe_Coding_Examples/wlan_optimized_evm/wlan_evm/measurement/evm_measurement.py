"""
WLAN EVM measurement routines.

For now this module provides a synthetic 802.11be 320 MHz 1024-QAM
EVM measurement so the GUI can be exercised without hardware.

Later, the synthetic generator can be replaced with calls into
RFmxInstr / RFmxWLAN drivers.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from wlan_evm.drivers.rfmx_wlan import WlanEvmConfig, measure_11be_320mhz_1024qam_evm


@dataclass
class EvmResult:
    """Container for EVM measurement results."""

    iq_data: np.ndarray  # complex samples for plotting
    avg_rms_evm_percent: float


def run_evm_11be_320mhz_1024qam(resource_name: str = "RFSA") -> EvmResult:
    """
    Run a real 802.11be 320 MHz 1024-QAM EVM measurement using RFmx.

    This wraps the RFmx WLAN driver and returns data-subcarrier
    constellation points plus average RMS EVM (data) in percent.
    """
    cfg = WlanEvmConfig(resource_name=resource_name)
    results = measure_11be_320mhz_1024qam_evm(cfg)
    return EvmResult(
        iq_data=results.iq_data,
        avg_rms_evm_percent=results.avg_rms_evm_percent,
    )

