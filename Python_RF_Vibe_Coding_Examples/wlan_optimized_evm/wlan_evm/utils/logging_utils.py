"""
Basic logging configuration helpers.
"""

import logging


def configure_logging(level: int = logging.INFO) -> None:
    """Configure root logger with a simple console handler."""
    logging.basicConfig(
        level=level,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    )

