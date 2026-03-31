"""
Helpers for saving and loading measurement results.
"""

from pathlib import Path
from typing import Any
import json


def save_json(data: Any, path: str | Path) -> None:
    """Save measurement data to a JSON file."""
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open("w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def load_json(path: str | Path) -> Any:
    """Load measurement data from a JSON file."""
    p = Path(path)
    with p.open("r", encoding="utf-8") as f:
        return json.load(f)

