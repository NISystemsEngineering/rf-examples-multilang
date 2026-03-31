"""
RFSG waveform download adapter (TDMS -> waveform in RFSG).

Important:
- This repository does not ship NI-RFSG Python bindings and your current
  Python 3.14 environment could not install `pythonnet`.
- This module therefore provides a *gated* implementation:
    - If `pythonnet` is available and you can provide the RFSG Playback DLL,
      you can enable NI-RFSG Playback based download.
    - Otherwise, it will raise a clear error telling you what's missing.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class RfsgPlaybackConfig:
    """
    Configuration for RFSG Playback TDMS download.

    Parameters
    ----------
    playback_dll_path:
        Path to `NationalInstruments.ModularInstruments.NIRfsgPlayback.dll`.
    """

    playback_dll_path: str
    marker_index: int = 0
    marker_export_output_term: str = "PXI_Trig0"


def _try_configure_marker_export_to_pxi_trig0(
    *,
    rfsg_session_handle: int,
    playback_cfg: RfsgPlaybackConfig,
) -> bool:
    """
    Best-effort configuration of marker export routing to PXI_Trig0.

    The exact method name/signature in `NIRfsgPlayback.dll` can vary between NI
    versions, so we use reflection to look for likely APIs.
    """
    try:
        import clr  # type: ignore
        from System import IntPtr  # type: ignore
        from System.Reflection import BindingFlags  # type: ignore
    except Exception:
        return False

    session_handle = IntPtr(int(rfsg_session_handle))

    marker_index = int(playback_cfg.marker_index)
    output_term = str(playback_cfg.marker_export_output_term)

    app_domain = __import__("System").AppDomain.CurrentDomain  # type: ignore

    # Candidate method name fragments for routing marker export.
    name_fragments = [
        "ExportOutput",
        "ExportOutputTerm",
        "Marker",
        "OutputTerm",
        "OutputTerminal",
        "PXI_Trig0",
    ]

    def looks_like_marker_routing(method_name: str) -> bool:
        lowered = method_name.lower()
        return "marker" in lowered and ("export" in lowered or "output" in lowered)

    # Try to find an overload with (IntPtr session, int markerIndex, string outputTerm)
    # or close variants.
    for asm in app_domain.GetAssemblies():
        try:
            for t in asm.GetTypes():
                if t.FullName is None or "NIRfsgPlayback" not in t.FullName:
                    continue
                for m in t.GetMethods(BindingFlags.Public | BindingFlags.Static):
                    if not looks_like_marker_routing(m.Name):
                        continue
                    # Quick prune: if none of the fragments are present, skip.
                    if not any(f.lower() in m.Name.lower() for f in name_fragments if f):
                        continue
                    params = m.GetParameters()
                    if len(params) != 3:
                        continue

                    # Attempt a common signature mapping.
                    try:
                        m.Invoke(None, [session_handle, marker_index, output_term])
                        return True
                    except Exception:  # noqa: BLE001
                        # Try the alternative ordering (string last is still expected).
                        try:
                            m.Invoke(None, [session_handle, marker_index, output_term])
                            return True
                        except Exception:
                            continue
        except Exception:  # noqa: BLE001
            continue

    return False


def _require_pythonnet() -> Any:
    try:
        import clr  # type: ignore

        return clr
    except Exception as exc:  # noqa: BLE001
        raise RuntimeError(
            "pythonnet is required for NI-RFSG Playback download. "
            "On your current Python (3.14) it was not installable. "
            "Please install pythonnet in a compatible Python environment "
            "or run this code on a machine where pythonnet already exists."
        ) from exc


def load_nirfsg_playback_library(playback_dll_path: str) -> None:
    """
    Load the NI-RFSG Playback .NET assembly using pythonnet.
    """
    clr = _require_pythonnet()

    dll_path = Path(playback_dll_path)
    if not dll_path.exists():
        raise FileNotFoundError(f"RFSG Playback DLL not found: {dll_path}")

    clr.AddReference(str(dll_path))


def read_and_download_waveform_from_tdms(
    *,
    rfsg_session_handle: int,
    tdms_path: str | Path,
    waveform_name: str,
    playback_cfg: RfsgPlaybackConfig,
) -> Any:
    """
    Read TDMS and download a waveform into RFSG using NI-RFSG Playback.

    This function expects:
    - `rfsg_session_handle` as an integer pointer value for an active RFSG session.
      (The exact handle type depends on the NI-RFSG programming environment.)
    - `ReadAndDownloadWaveformFromFile` method to exist in the loaded
      `NationalInstruments.ModularInstruments.NIRfsgPlayback` assembly.

    Returns
    -------
    Any
        Whatever the underlying NI-RFSG Playback method returns.
    """
    # Load assembly first.
    load_nirfsg_playback_library(playback_cfg.playback_dll_path)

    # Ensure marker0 export is routed to PXI_Trig0 (required for PXI_Trig0 marker assertion).
    # If we can't find a suitable API in your NI version, we'll raise a helpful error.
    configured = _try_configure_marker_export_to_pxi_trig0(
        rfsg_session_handle=rfsg_session_handle,
        playback_cfg=playback_cfg,
    )
    if not configured:
        raise RuntimeError(
            "Could not auto-configure marker0 export routing to PXI_Trig0 via the "
            "loaded NIRfsgPlayback.dll API.\n\n"
            "Manual fallback:\n"
            "- In NI-RFSG, set `Events:Marker:Output Terminal` / `MarkerEvent.ExportOutputTerm` "
            "to `PXI_Trig0` for marker0.\n"
            "Then rerun this function."
        )

    # Resolve and call the correct overload via reflection.
    # This avoids hard-coding the exact class name for NIRfsgPlayback.dll
    # which can vary between NI versions.
    import clr  # type: ignore
    from System import IntPtr, Type  # type: ignore
    from System.Reflection import BindingFlags  # type: ignore

    tdms_path = str(tdms_path)

    # Convert numeric handle to IntPtr for pythonnet.
    session_handle = IntPtr(int(rfsg_session_handle))

    # Scan all loaded types in the AppDomain and locate a public static method
    # named ReadAndDownloadWaveformFromFile with parameters matching our
    # expected shape.
    target_method_name = "ReadAndDownloadWaveformFromFile"
    expected_argc = 3
    assemblies = []
    for a in clr.GetClrType(object).Assembly.GetReferencedAssemblies():  # type: ignore
        assemblies.append(a)

    # In case referenced assemblies doesn't include the loaded DLL (implementation detail),
    # fall back to scanning AppDomain assemblies via System.Type.
    try:
        loaded_assemblies = Type.GetType("System.AppDomain").Assembly  # pragma: no cover
        _ = loaded_assemblies  # silence unused-variable warnings
    except Exception:  # noqa: BLE001
        pass

    # Most robust: directly enumerate current AppDomain assemblies.
    app_domain = __import__("System").AppDomain.CurrentDomain  # type: ignore
    for asm in app_domain.GetAssemblies():
        try:
            for t in asm.GetTypes():
                # Only consider types in the NIRfsgPlayback namespace (reduces noise).
                if "NIRfsgPlayback" not in t.FullName:
                    continue
                for m in t.GetMethods(BindingFlags.Public | BindingFlags.Static):
                    if m.Name != target_method_name:
                        continue
                    params = m.GetParameters()
                    if len(params) != expected_argc:
                        continue
                    # Attempt to invoke as:
                    #   (IntPtr sessionHandle, string tdmsFile, string waveformName)
                    return m.Invoke(None, [session_handle, tdms_path, waveform_name])
        except Exception:  # noqa: BLE001
            continue

    raise RuntimeError(
        f"Could not find a static .NET method '{target_method_name}(IntPtr,string,string)' "
        "in your loaded NIRfsgPlayback.dll. If this still fails, inspect "
        "the available overloads in that DLL and update the arg mapping."
    )

