"""
RFSG script text helpers.

These functions generate the dynamic-generation script text; they do not
upload it to hardware (that requires NI-RFSG Python/.NET APIs).
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class RfsgScriptConfig:
    waveform_name: str = "waveform"
    marker_name: str = "marker0"
    marker_sample_index: int = 0
    repeat_forever: bool = True


def generate_waveform_repeat_forever_script(cfg: RfsgScriptConfig | None = None) -> str:
    """
    Generate a script that repeats a single waveform forever and asserts a marker
    at the first sample of each generated waveform.

    Notes
    -----
    - The routing of the marker to `PXI_Trig0` is typically configured in the
      RFSG/marker output configuration (outside the script text).
    - The script syntax below matches the snippet you provided.
    """
    if cfg is None:
        cfg = RfsgScriptConfig()

    if not cfg.repeat_forever:
        raise NotImplementedError("Only repeat_forever scripts are implemented.")

    # Matches the user-provided skeleton:
    #   script GenerateWaveform
    #    repeat forever
    #     generate waveform marker0(0)
    #    end repeat
    #   end script
    #
    # We keep the marker position explicit: marker_name(marker_sample_index).
    lines = [
        "script GenerateWaveform",
        "  repeat forever",
        f"    generate {cfg.waveform_name} {cfg.marker_name}({cfg.marker_sample_index})",
        "  end repeat",
        "end script",
    ]
    return "\n".join(lines) + "\n"


def write_rfsg_script_text(script_text: str, path: str) -> None:
    """
    Write RFSG script text to disk.

    Depending on your NI-RFSG workflow, you may need to upload this script
    using NI-RFSG tooling/APIs.
    """
    with open(path, "w", encoding="utf-8") as f:
        f.write(script_text)

