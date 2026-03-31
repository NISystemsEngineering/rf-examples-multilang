"""
TDMS -> I/Q waveform extraction helpers.

This project targets NI-RFSG workflows where RF waveforms are exported from
NI RF Waveform Creator as TDMS, then replayed by NI-RFSG.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from nptdms import TdmsFile


@dataclass(frozen=True)
class IqWaveform:
    """Extracted complex baseband waveform samples."""

    iq: np.ndarray  # complex64/128
    sample_rate_hz: float | None
    metadata: dict[str, Any]

    def scaled_to_max_abs_1(self) -> "IqWaveform":
        iq = np.asarray(self.iq, dtype=np.complex128)
        max_abs = np.max(np.abs(iq)) if iq.size else 0.0
        if not np.isfinite(max_abs) or max_abs <= 0:
            return IqWaveform(iq=iq, sample_rate_hz=self.sample_rate_hz, metadata=dict(self.metadata))
        iq_scaled = iq / max_abs
        return IqWaveform(
            iq=iq_scaled,
            sample_rate_hz=self.sample_rate_hz,
            metadata=dict(self.metadata),
        )


def _pick_first_channel(group: Any) -> Any:
    # nptdms group.channel can return a list-like.
    try:
        return group.channels[0]
    except Exception:  # noqa: BLE001
        return None


def read_tdms_iq_waveform(
    tdms_path: str | Path,
    *,
    i_channel_name: str | None = None,
    q_channel_name: str | None = None,
) -> IqWaveform:
    """
    Read I/Q waveform samples from an RF Waveform Creator TDMS export.

    This function is intentionally defensive: TDMS exports vary depending on
    how RF Waveform Creator was configured. It attempts:
    - If I/Q channel names are provided, read those channels.
    - Otherwise, try common channel name patterns: "I", "Q", "I0", "Q0".
    - As a fallback, if a single complex channel exists, use it as the IQ array.

    Returns the complex waveform plus basic metadata when available.
    """
    tdms_path = Path(tdms_path)
    tdms = TdmsFile.read(str(tdms_path))

    # Build quick lookup of channels by name.
    channels_by_name: dict[str, Any] = {}
    for group in tdms.groups():
        for ch in group.channels():
            channels_by_name[ch.name] = ch

    def get_channel(name_candidates: list[str]) -> Any | None:
        for n in name_candidates:
            if n in channels_by_name:
                return channels_by_name[n]
        return None

    i_ch = None
    q_ch = None
    if i_channel_name:
        i_ch = channels_by_name.get(i_channel_name)
    if q_channel_name:
        q_ch = channels_by_name.get(q_channel_name)

    if i_ch is None:
        i_ch = get_channel(["I", "I0", "InPhase", "In_Phase", "ChannelI", "Channel_I"])
    if q_ch is None:
        q_ch = get_channel(["Q", "Q0", "Quadrature", "ChannelQ", "Channel_Q"])

    metadata: dict[str, Any] = {"source_tdms": str(tdms_path)}
    sample_rate_hz = None

    # Try to extract sample rate from group and channel properties.
    # Properties naming varies; in your TDMS sample, it is stored as `NI_RF_IQRate`.
    sample_rate_keys = [
        # common patterns
        "SampleRate",
        "sample_rate",
        "SamplingRate",
        "fs",
        "I_SampleRate",
        # NI RF Waveform Creator naming (seen in your TDMS file)
        "NI_RF_IQRate",
        "IQRate",
        "IQ_rate",
        "IQ Rate",
        "IQ_Rate",
        # other plausible variants
        "NI_RF_IQ_RATE",
    ]

    for group in tdms.groups():
        group_props = getattr(group, "properties", {}) or {}
        for key in sample_rate_keys:
            if key in group_props:
                try:
                    sample_rate_hz = float(group_props[key])
                    metadata["sample_rate_source"] = f"group:{group.name}:{key}"
                except Exception:  # noqa: BLE001
                    pass

    # Channel properties are often where the useful RF Waveform Creator values live.
    for group in tdms.groups():
        for ch in group.channels():
            ch_props = getattr(ch, "properties", {}) or {}
            for key in sample_rate_keys:
                if key in ch_props:
                    try:
                        sample_rate_hz = float(ch_props[key])
                        metadata["sample_rate_source"] = (
                            f"channel:{group.name}/{ch.name}:{key}"
                        )
                    except Exception:  # noqa: BLE001
                        pass

    if i_ch is not None and q_ch is not None:
        i = np.asarray(i_ch[:], dtype=np.float64)
        q = np.asarray(q_ch[:], dtype=np.float64)
        n = min(i.size, q.size)
        iq = i[:n] + 1j * q[:n]
        return IqWaveform(iq=iq, sample_rate_hz=sample_rate_hz, metadata=metadata)

    # Fallback: single channel might already be complex (less common).
    ch = get_channel(["IQ", "iq", "ComplexIQ", "complexIQ"])
    if ch is not None:
        iq = np.asarray(ch[:], dtype=np.complex128)
        return IqWaveform(iq=iq, sample_rate_hz=sample_rate_hz, metadata=metadata)

    # Last resort: infer from the raw TDMS channel layout.
    #
    # RF Waveform Creator exports commonly keep waveform samples under a
    # `waveforms` group, while other groups may contain metadata/settings.
    # Prefer the `waveforms` group to avoid incorrectly interpreting metadata
    # as IQ samples.
    waveform_group = None
    for group in tdms.groups():
        if group.name.lower() == "waveforms":
            waveform_group = group
            break

    candidate_channels = []
    groups_to_scan = [waveform_group] if waveform_group is not None else list(tdms.groups())
    for group in groups_to_scan:
        try:
            candidate_channels.extend(list(group.channels()))
        except Exception:  # noqa: BLE001
            pass

    if len(candidate_channels) == 1:
        # Interleaved I/Q in one channel (common for our TDMS export).
        ch = candidate_channels[0]
        data = np.asarray(ch[:], dtype=np.float64)
        if data.size >= 2 and data.size % 2 == 0:
            i = data[0::2]
            q = data[1::2]
            iq = i + 1j * q
            metadata["channel_inference"] = (
                f"Using interleaved IQ in single channel {ch.name} "
                "(I,Q repeating)."
            )
            return IqWaveform(
                iq=iq,
                sample_rate_hz=sample_rate_hz,
                metadata=metadata,
            )

    if len(candidate_channels) >= 2:
        # Split I/Q into two channels.
        i = np.asarray(candidate_channels[0][:], dtype=np.float64)
        q = np.asarray(candidate_channels[1][:], dtype=np.float64)
        n = min(i.size, q.size)
        iq = i[:n] + 1j * q[:n]
        metadata["channel_inference"] = (
            f"Using {candidate_channels[0].name} as I and {candidate_channels[1].name} as Q"
        )
        return IqWaveform(iq=iq, sample_rate_hz=sample_rate_hz, metadata=metadata)

    raise ValueError(
        f"Could not infer I/Q channels from TDMS file: {tdms_path}. "
        f"Provide i_channel_name/q_channel_name explicitly."
    )

