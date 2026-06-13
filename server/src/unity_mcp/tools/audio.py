"""AI audio generation via ElevenLabs (optional).

Configure with UNITY_MCP_ELEVENLABS_API_KEY. When unconfigured, each tool
returns a friendly notice instead of failing. Generated MP3s are imported into
Unity under Assets/MCP/Audio as AudioClips.
"""

from __future__ import annotations

from typing import Any

import httpx
from mcp.server.fastmcp import FastMCP

from ..config import CONFIG
from ._media import import_bytes, post_with_retry, slugify


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def audio_generate_speech(
        text: str,
        name: str | None = None,
        voice_id: str | None = None,
    ) -> dict[str, Any]:
        """Generate spoken speech (text-to-speech) and import it as an AudioClip.

        Uses ElevenLabs text-to-speech. `voice_id` overrides the configured
        default voice (UNITY_MCP_ELEVENLABS_VOICE_ID). The MP3 is saved under
        Assets/MCP/Audio.
        """
        voice = voice_id or CONFIG.elevenlabs_voice_id
        url = f"{CONFIG.elevenlabs_base_url.rstrip('/')}/v1/text-to-speech/{voice}"
        payload = {"text": text, "model_id": CONFIG.elevenlabs_model}
        return await _generate_and_import("speech", url, payload, name or text)

    @mcp.tool()
    async def audio_generate_sound_effect(
        prompt: str,
        name: str | None = None,
        duration_seconds: float | None = None,
    ) -> dict[str, Any]:
        """Generate a sound effect from a text prompt and import it as an AudioClip.

        Uses ElevenLabs sound generation (e.g. "footsteps on gravel", "sword
        clash"). `duration_seconds` is optional; leave it unset to let the model
        choose. The MP3 is saved under Assets/MCP/Audio.
        """
        url = f"{CONFIG.elevenlabs_base_url.rstrip('/')}/v1/sound-generation"
        payload: dict[str, Any] = {"text": prompt}
        if duration_seconds is not None:
            payload["duration_seconds"] = duration_seconds
        return await _generate_and_import("sound_effect", url, payload, name or prompt)

    @mcp.tool()
    async def audio_generate_music(
        prompt: str,
        name: str | None = None,
        length_seconds: float | None = None,
    ) -> dict[str, Any]:
        """Generate a music clip from a text prompt and import it as an AudioClip.

        Uses the ElevenLabs music endpoint (e.g. "calm ambient exploration
        loop"). `length_seconds` is optional. This endpoint may require beta
        access on your account; any provider error is returned in the result.
        The MP3 is saved under Assets/MCP/Audio.
        """
        url = f"{CONFIG.elevenlabs_base_url.rstrip('/')}/v1/music"
        payload: dict[str, Any] = {"prompt": prompt}
        if length_seconds is not None:
            payload["music_length_ms"] = int(length_seconds * 1000)
        return await _generate_and_import("music", url, payload, name or prompt)


async def _generate_and_import(
    kind: str, url: str, payload: dict[str, Any], name_seed: str
) -> dict[str, Any]:
    if not CONFIG.is_audio_configured:
        return {
            "configured": False,
            "message": (
                "Audio generation is not configured. Set UNITY_MCP_ELEVENLABS_API_KEY."
            ),
        }

    try:
        audio = await _post_audio(url, payload)
    except httpx.HTTPStatusError as exc:
        return {
            "configured": True,
            "ok": False,
            "error": f"{exc.response.status_code}: {exc.response.text[:300]}",
        }
    except httpx.HTTPError as exc:
        return {"configured": True, "ok": False, "error": f"Audio request failed: {exc}"}

    slug = slugify(name_seed)
    path = f"{CONFIG.audio_folder}/{slug}.mp3"
    return {
        "configured": True,
        "ok": True,
        "kind": kind,
        "audio": path,
        "import": await import_bytes(path, audio),
    }


async def _post_audio(url: str, payload: dict[str, Any]) -> bytes:
    headers = {"xi-api-key": CONFIG.elevenlabs_api_key, "accept": "audio/mpeg"}
    resp = await post_with_retry(
        url, headers=headers, json=payload, timeout=CONFIG.http_timeout
    )
    return resp.content
