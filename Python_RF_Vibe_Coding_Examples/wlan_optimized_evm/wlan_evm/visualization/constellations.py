"""
Helpers for drawing EVM constellations using matplotlib.
"""

from collections.abc import Sequence
from typing import Iterable

import numpy as np
from matplotlib.axes import Axes


def plot_evm_constellation(
    ax: Axes,
    iq_points: Iterable[complex] | np.ndarray,
    title: str | None = None,
) -> None:
    """
    Plot a complex constellation on the given matplotlib Axes.

    Parameters
    ----------
    ax:
        Matplotlib Axes to draw on.
    iq_points:
        Iterable of complex I/Q samples representing the constellation.
    title:
        Optional plot title.
    """
    points = np.asarray(list(iq_points), dtype=np.complex128)
    ax.clear()

    # Use fixed axes limits to keep the constellation scale consistent
    # and make clustering easier to see.
    ax.set_xlim(-1.5, 1.5)
    ax.set_ylim(-1.5, 1.5)
    ax.set_aspect("equal", adjustable="box")
    ax.set_facecolor("black")

    # Higher-contrast plot chrome for readability.
    for spine in ax.spines.values():
        spine.set_color("white")
        spine.set_linewidth(2.0)
    ax.tick_params(colors="white", labelsize=10)

    # Filter out NaN/Inf points which can appear with bad/simulated data.
    if points.size > 0:
        mask = np.isfinite(points.real) & np.isfinite(points.imag)
        points = points[mask]

    if points.size > 0:
        # Brighter, thicker markers with an outline so the constellation "pops".
        ax.scatter(
            points.real,
            points.imag,
            s=10,
            alpha=0.9,
            c="#4CFF7A",
            edgecolors="white",
            linewidths=0.25,
        )

        # Subtle reference axes at 0/0.
        ax.axhline(0.0, color="white", linewidth=1.0, alpha=0.25)
        ax.axvline(0.0, color="white", linewidth=1.0, alpha=0.25)

    ax.set_xlabel("I", color="white")
    ax.set_ylabel("Q", color="white")
    ax.grid(True, linestyle="--", alpha=0.25, color="white")
    if title:
        ax.set_title(title, color="white")

