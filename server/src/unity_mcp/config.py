"""Runtime configuration for the Unity MCP server.

All values can be overridden with environment variables so the same server
binary works across machines and client configurations.
"""

from __future__ import annotations

import os
from dataclasses import dataclass


def _get_int(name: str, default: int) -> int:
    raw = os.environ.get(name)
    if raw is None or raw.strip() == "":
        return default
    try:
        return int(raw)
    except ValueError:
        return default


def _get_float(name: str, default: float) -> float:
    raw = os.environ.get(name)
    if raw is None or raw.strip() == "":
        return default
    try:
        return float(raw)
    except ValueError:
        return default


@dataclass(frozen=True)
class Config:
    """Connection settings for talking to the Unity Editor bridge."""

    host: str = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
    port: int = _get_int("UNITY_MCP_PORT", 6400)
    # Seconds to wait when establishing the TCP connection.
    connect_timeout: float = _get_float("UNITY_MCP_CONNECT_TIMEOUT", 5.0)
    # Seconds to wait for a single command response from Unity.
    request_timeout: float = _get_float("UNITY_MCP_REQUEST_TIMEOUT", 30.0)
    # Total seconds to keep retrying a command while the bridge is temporarily
    # unreachable. This covers Unity domain reloads (every script recompile
    # drops the socket and the bridge restarts a few seconds later).
    reconnect_timeout: float = _get_float("UNITY_MCP_RECONNECT_TIMEOUT", 60.0)
    # Seconds to wait between reconnect attempts during that window.
    reconnect_interval: float = _get_float("UNITY_MCP_RECONNECT_INTERVAL", 0.5)


CONFIG = Config()
