"""Runtime configuration for the Unity MCP server.

All values can be overridden with environment variables so the same server
binary works across machines and client configurations.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

# Load variables from a .env file before reading any configuration. We look in
# the current working directory (and its parents) and in the server project
# root, so `unity-mcp` picks up secrets without exporting them globally. This is
# best-effort: if python-dotenv isn't installed, real environment variables are
# still used.
try:
    from dotenv import find_dotenv, load_dotenv

    load_dotenv(find_dotenv(usecwd=True))
    load_dotenv(Path(__file__).resolve().parents[2] / ".env")
except ImportError:
    pass


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

    # --- Optional AI asset generation (leave unset to disable the feature) ---
    # Image generation: Azure AI Foundry / Azure OpenAI or any OpenAI-compatible
    # images endpoint. Set the resource (or base) endpoint; the server appends
    # the OpenAI v1 route /openai/v1/images/generations when needed and sends the
    # model name in the request body. No api-version is required for the v1
    # surface. UNITY_MCP_IMAGE_DEPLOYMENT is accepted as an alias for the model
    # name for backward compatibility.
    image_endpoint: str = os.environ.get("UNITY_MCP_IMAGE_ENDPOINT", "")
    image_api_key: str = os.environ.get("UNITY_MCP_IMAGE_API_KEY", "")
    image_model: str = (
        os.environ.get("UNITY_MCP_IMAGE_MODEL", "")
        or os.environ.get("UNITY_MCP_IMAGE_DEPLOYMENT", "")
    )

    # Audio generation: ElevenLabs (speech, sound effects, music).
    elevenlabs_api_key: str = os.environ.get("UNITY_MCP_ELEVENLABS_API_KEY", "")
    elevenlabs_base_url: str = os.environ.get(
        "UNITY_MCP_ELEVENLABS_BASE_URL", "https://api.elevenlabs.io"
    )
    elevenlabs_voice_id: str = os.environ.get(
        "UNITY_MCP_ELEVENLABS_VOICE_ID", "21m00Tcm4TlvDq8ikWAM"
    )
    elevenlabs_model: str = os.environ.get(
        "UNITY_MCP_ELEVENLABS_MODEL", "eleven_multilingual_v2"
    )

    # Where generated assets are written inside the Unity project.
    texture_folder: str = os.environ.get("UNITY_MCP_TEXTURE_FOLDER", "Assets/MCP/Textures")
    audio_folder: str = os.environ.get("UNITY_MCP_AUDIO_FOLDER", "Assets/MCP/Audio")
    # Seconds to wait on outbound HTTP calls to the AI providers.
    http_timeout: float = _get_float("UNITY_MCP_HTTP_TIMEOUT", 120.0)

    @property
    def is_image_configured(self) -> bool:
        """True when an image-generation endpoint and key are available."""
        return bool(self.image_endpoint and self.image_api_key)

    @property
    def is_audio_configured(self) -> bool:
        """True when an ElevenLabs API key is available."""
        return bool(self.elevenlabs_api_key)


CONFIG = Config()
