"""
Enums describing common WLAN configuration options.
"""

from enum import Enum, auto


class Standard(Enum):
    """Supported WLAN standards."""

    IEEE_802_11A = auto()
    IEEE_802_11N = auto()
    IEEE_802_11AC = auto()
    IEEE_802_11AX = auto()


class ChannelBandwidth(Enum):
    """Supported WLAN channel bandwidths."""

    BW_20_MHZ = auto()
    BW_40_MHZ = auto()
    BW_80_MHZ = auto()
    BW_160_MHZ = auto()

