"""
NI-RFmx WLAN-specific configuration and measurements.

This module uses nirfmxinstr + nirfmxwlan to configure an 802.11be
signal and perform an OFDM ModAcc EVM measurement.

The exact configuration you need may differ depending on hardware
and waveform; treat this as a starting point to adapt.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import nirfmxinstr
import nirfmxwlan


@dataclass
class WlanEvmConfig:
    resource_name: str = "RFSA"
    center_frequency_hz: float = 5.4e9
    reference_level_dbm: float = 6
    external_attenuation_db: float = 0.0
    channel_bandwidth_hz: float = 320e6


@dataclass
class WlanEvmResults:
    iq_data: np.ndarray
    avg_rms_evm_percent: float


def measure_11be_320mhz_1024qam_evm(config: WlanEvmConfig) -> WlanEvmResults:
    """
    Perform an 802.11be 320 MHz 1024-QAM OFDM ModAcc EVM measurement.

    Returns complex data-subcarrier constellation points and average
    RMS EVM (data) in percent.

    Note: This function assumes the RF signal is already present and
    properly triggered; additional trigger/averaging configuration may
    be required for your setup.
    """
    instr_session = None
    wlan_signal = None

    try:
        instr_session = nirfmxinstr.Session(
            resource_name=config.resource_name,
            option_string="",
        )

        wlan_signal = instr_session.get_wlan_signal_configuration()

        # Basic frequency, level, and attenuation configuration.
        instr_session.configure_frequency_reference(
            selector_string="",
            frequency_reference_source="PXI_CLK",
            frequency_reference_frequency=10e6,
        )
        wlan_signal.configure_frequency(
            selector_string="",
            center_frequency=config.center_frequency_hz,
        )
        wlan_signal.configure_reference_level(
            selector_string="",
            reference_level=config.reference_level_dbm,
        )
        wlan_signal.configure_external_attenuation(
            selector_string="",
            external_attenuation=config.external_attenuation_db,
        )

        # Configure an IQ Power Edge Trigger (level set to -20 dB relative to the
        # configured reference level).
        wlan_signal.configure_iq_power_edge_trigger(
            selector_string="",
            iq_power_edge_source="0",
            iq_power_edge_slope=nirfmxwlan.IQPowerEdgeTriggerSlope.RISING_SLOPE,
            iq_power_edge_level=-20.0,
            trigger_delay=0.0,
            trigger_min_quiet_time_mode=nirfmxwlan.TriggerMinimumQuietTimeMode.AUTO,
            trigger_min_quiet_time_duration=5.0e-6,
            iq_power_edge_level_type=nirfmxwlan.IQPowerEdgeTriggerLevelType.RELATIVE,
            enable_trigger=True,
        )

        # Standard and bandwidth (11be + 320 MHz).
        wlan_signal.configure_standard(
            selector_string="",
            standard=nirfmxwlan.Standard.STANDARD_802_11_BE,
        )
        wlan_signal.configure_channel_bandwidth(
            selector_string="",
            channel_bandwidth=config.channel_bandwidth_hz,
        )

        # Force OFDM frequency band selection to 5 GHz.
        # (Default is 2.4 GHz.)
        wlan_signal.set_ofdm_frequency_band(
            selector_string="",
            value=nirfmxwlan.OfdmFrequencyBand.OFDM_FREQUENCY_BAND_5GHZ,
        )

        # Select OFDM ModAcc measurement (EVM, constellation, etc.).
        wlan_signal.select_measurements(
            selector_string="",
            measurements=nirfmxwlan.MeasurementTypes.OFDMMODACC,
            enable_all_traces=True,
        )

        # Configure channel estimation to use both reference and data carriers.
        # This changes the EVM channel estimation behavior from the default
        # "Reference" to "Reference and Data".
        wlan_signal.ofdmmodacc.configuration.configure_channel_estimation_type(
            selector_string="",
            channel_estimation_type=nirfmxwlan.OfdmModAccChannelEstimationType.REFERENCE_AND_DATA,
        )

        # Auto-level sequence:
        # 1) Generic RFmxWLAN auto-level to set REFERENCE_LEVEL based on peak power.
        # 2) OFDMModAcc-specific auto-level to evaluate multiple reference levels
        #    and pick the one that yields the best EVM.
        wlan_signal.auto_level(selector_string="", measurement_interval=0.01)
        wlan_signal.ofdmmodacc.configuration.auto_level(selector_string="", timeout=10.0)

        # Initiate and fetch results.
        wlan_signal.initiate(selector_string="", result_name="")

        # Fetch average RMS EVM for data (in percent) using stream 0.
        (
            _stream_rms_evm_mean,
            stream_data_rms_evm_mean,
            _stream_pilot_rms_evm_mean,
            _error_code,
        ) = wlan_signal.ofdmmodacc.results.fetch_stream_rms_evm(
            selector_string="segment0/stream0",
            timeout=10.0,
        )

        # Fetch equalized constellation for data subcarriers (segment0/stream0).
        data_constellation = np.empty(0, dtype=np.complex64)
        _err = wlan_signal.ofdmmodacc.results.fetch_data_constellation_trace(
            selector_string="segment0/stream0",
            timeout=10.0,
            data_constellation=data_constellation,
        )

        iq = np.asarray(data_constellation, dtype=np.complex128)

        return WlanEvmResults(
            iq_data=iq,
            avg_rms_evm_percent=float(stream_data_rms_evm_mean),
        )

    finally:
        if wlan_signal is not None:
            wlan_signal.dispose()
        if instr_session is not None:
            instr_session.close()
