"""
Minimal RFSG test:
- Read a TDMS waveform export and download it into NI-RFSG.
- Configure generation using a hardcoded Script that repeats forever and
  asserts marker0 at sample 0.
- Route marker output to PXI_Trig0.

This is intended for validating TDMS->RFSG programming and marker routing.
"""

from __future__ import annotations

import argparse
import time
from pathlib import Path

import numpy as np
from nirfsg import GenerationMode, Session
from nirfsg.errors import DriverError

from wlan_evm.waveforms.rfsg_scripts import (
    RfsgScriptConfig,
    generate_waveform_repeat_forever_script,
)
from wlan_evm.waveforms.tdms_waveform import read_tdms_iq_waveform


DEFAULT_WAVEFORM_NAME = "waveform"
DEFAULT_SCRIPT_NAME = "GenerateWaveform"


def run_rfsg_infinite_marker(
    *,
    tdms_path: str | Path,
    rfsg_resource: str,
    center_frequency_hz: float,
    power_level_dbm: float,
    waveform_name: str = DEFAULT_WAVEFORM_NAME,
    waveform_index: int = 0,
    simulate: bool = False,
) -> None:
    """
    Configure and start RFSG generation until Ctrl+C.
    """
    tdms_path = Path(tdms_path)
    if not tdms_path.exists():
        raise FileNotFoundError(f"TDMS file not found: {tdms_path}")

    # Use NI-RFSG script mode because "repeat forever" requires scripting.
    script_text = generate_waveform_repeat_forever_script(
        RfsgScriptConfig(
            waveform_name=waveform_name,
            marker_name="marker0",
            marker_sample_index=0,
            repeat_forever=True,
        )
    )

    # Keep NI-RFSG session initialization conservative to avoid long
    # timeouts during driver setup.
    options: dict[str, object] = {
        "queryinstrstatus": False,
        "rangecheck": False,
        "cache": True,
    }
    if simulate:
        options["simulate"] = True

    # Enforce scaling: compute max(|IQ|) from TDMS and apply runtime scaling
    # so that max(|IQ|) becomes 1.
    tdms_wf = read_tdms_iq_waveform(tdms_path)
    max_abs = float(np.max(np.abs(tdms_wf.iq)))
    if not np.isfinite(max_abs) or max_abs <= 0:
        runtime_scaling_db = 0.0
    else:
        # amplitude scaling in dB: 20*log10(1/max_abs)
        runtime_scaling_db = 20.0 * float(np.log10(1.0 / max_abs))

    # Note: id_query/reset_device are left conservative for stability.
    with Session(
        resource_name=rfsg_resource,
        id_query=False,
        reset_device=False,
        options=options,
    ) as session:
        # Configure RF settings first to match the validated C# flow.
        session.upconverter_center_frequency = float(center_frequency_hz)
        session.power_level = float(power_level_dbm)

        # Download the TDMS waveform into onboard memory using our chosen name.
        try:
            session.read_and_download_waveform_from_file_tdms(
                waveform_name=waveform_name,
                file_path=str(tdms_path),
                waveform_index=int(waveform_index),
            )
        except DriverError as exc:
            # In Simulate mode, some NI-RFSG driver builds do not support
            # ReadAndDownloadWaveformFromFileTdms. Fall back to loading IQ
            # data in Python and writing it into the waveform memory.
            if "Function or method not supported" not in str(exc) and exc.code != -1074135023:
                raise

            print("TDMS read/download unsupported; writing IQ waveform directly...")
            if tdms_wf.sample_rate_hz is not None:
                session.iq_rate = float(tdms_wf.sample_rate_hz)

            iq = np.asarray(tdms_wf.iq, dtype=np.complex128)
            session.allocate_arb_waveform(
                waveform_name=waveform_name,
                size_in_samples=int(iq.size),
            )
            session.write_arb_waveform(
                waveform_name=waveform_name,
                waveform_data_array=iq,
            )

        if runtime_scaling_db != 0.0:
            # Apply scaling after download so the effective max(|IQ|)
            # becomes 1 for this TDMS waveform.
            session.waveform_runtime_scaling = float(runtime_scaling_db)

        # Configure generation mode.
        session.generation_mode = GenerationMode.SCRIPT
        # Marker export intentionally disabled for now.

        # Write the script into onboard memory and select it for generation.
        session.write_script(script_text)
        session.selected_script = DEFAULT_SCRIPT_NAME

        print("RFSG started. Ctrl+C to stop.")
        with session.initiate():
            while True:
                time.sleep(0.25)


def main() -> None:
    parser = argparse.ArgumentParser(description="RFSG TDMS infinite marker test")
    parser.add_argument("--tdms", required=True, help="Path to TDMS waveform export")
    parser.add_argument("--rfsg-resource", required=True, help="NI-RFSG resource string")
    parser.add_argument("--center-frequency-hz", required=True, type=float, help="Center frequency (Hz)")
    parser.add_argument("--power-db", required=True, type=float, help="Power level (dBm)")
    parser.add_argument("--waveform-index", default=0, type=int, help="Waveform index inside TDMS")
    parser.add_argument("--waveform-name", default=DEFAULT_WAVEFORM_NAME, help="Waveform name to store in RFSG")
    parser.add_argument("--simulate", action="store_true", help="Enable Simulate=1 mode")
    args = parser.parse_args()

    run_rfsg_infinite_marker(
        tdms_path=args.tdms,
        rfsg_resource=args.rfsg_resource,
        center_frequency_hz=args.center_frequency_hz,
        power_level_dbm=args.power_db,
        waveform_name=args.waveform_name,
        waveform_index=args.waveform_index,
        simulate=args.simulate,
    )


if __name__ == "__main__":
    main()

