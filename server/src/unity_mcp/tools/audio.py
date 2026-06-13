"""AI audio generation via ElevenLabs (optional).

Configure with UNITY_MCP_ELEVENLABS_API_KEY. When unconfigured, each tool
returns a friendly notice instead of failing. Generated MP3s are imported into
Unity under Assets/MCP/Audio as AudioClips.
"""

from __future__ import annotations

import re
from typing import Any

import httpx
from mcp.server.fastmcp import FastMCP

from ..config import CONFIG
from ._media import get_with_retry, import_bytes, post_with_retry, slugify

# Words that add no search value when falling back to per-keyword matching.
_VOICE_STOPWORDS = frozenset(
    {
        "a", "an", "the", "and", "or", "of", "for", "with", "to", "in",
        "voice", "voices", "sounding", "sound", "like", "type", "kind",
        "please", "that", "this", "is", "very", "really", "some", "me",
    }
)


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def audio_search_voices(
        query: str,
        limit: int = 10,
        source: str = "library",
    ) -> dict[str, Any]:
        """Search the available ElevenLabs voices by keyword.

        The query is matched against each voice's name, description, labels and
        category, so prompts like "gruff orc", "calm british narrator" or
        "deep villain" work well. `source` is "library" (the public ElevenLabs
        voice library, where most character voices live — the default) or
        "account" (only voices already in your account). Returns a compact list
        of matches ({voice_id, name, description, labels, category,
        preview_url}); pass a chosen voice_id to audio_generate_speech.
        """
        if not CONFIG.is_audio_configured:
            return {
                "configured": False,
                "message": (
                    "Audio generation is not configured. Set UNITY_MCP_ELEVENLABS_API_KEY."
                ),
            }
        try:
            voices = await _search_voices(query, limit, source)
        except httpx.HTTPStatusError as exc:
            return {
                "configured": True,
                "ok": False,
                "error": f"{exc.response.status_code}: {exc.response.text[:300]}",
            }
        except httpx.HTTPError as exc:
            return {"configured": True, "ok": False, "error": f"Voice search failed: {exc}"}

        return {"configured": True, "ok": True, "count": len(voices), "voices": voices}

    @mcp.tool()
    async def audio_generate_speech(
        text: str,
        name: str | None = None,
        voice_id: str | None = None,
        voice_search: str | None = None,
    ) -> dict[str, Any]:
        """Generate spoken speech (text-to-speech) and import it as an AudioClip.

        Uses ElevenLabs text-to-speech. Provide a voice in one of three ways
        (most specific first): an explicit `voice_id`; a `voice_search` keyword
        (e.g. "gruff orc") which picks the best-matching voice from the public
        voice library; or neither, to use the configured default
        (UNITY_MCP_ELEVENLABS_VOICE_ID). The MP3 is saved under Assets/MCP/Audio.
        """
        if not CONFIG.is_audio_configured:
            return {
                "configured": False,
                "message": (
                    "Audio generation is not configured. Set UNITY_MCP_ELEVENLABS_API_KEY."
                ),
            }

        voice = voice_id
        matched: dict[str, Any] | None = None
        if not voice and voice_search:
            try:
                results = await _search_voices(voice_search, limit=1)
            except httpx.HTTPError as exc:
                return {
                    "configured": True,
                    "ok": False,
                    "error": f"Voice search failed: {exc}",
                }
            if not results:
                return {
                    "configured": True,
                    "ok": False,
                    "error": f"No voice matched '{voice_search}'.",
                }
            matched = results[0]
            voice = matched["voice_id"]

        voice = voice or CONFIG.elevenlabs_voice_id
        url = f"{CONFIG.elevenlabs_base_url.rstrip('/')}/v1/text-to-speech/{voice}"
        payload = {"text": text, "model_id": CONFIG.elevenlabs_model}
        result = await _generate_and_import("speech", url, payload, name or text)
        result["voice_id"] = voice
        if matched is not None:
            result["voice"] = matched
        return result

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


async def _search_voices(
    query: str, limit: int, source: str = "library"
) -> list[dict[str, Any]]:
    """Query ElevenLabs for voices matching `query` and return compact records.

    `source` selects the public voice library ("library", default) or the
    caller's own account ("account"). Library voice IDs can be used directly
    for text-to-speech.

    ElevenLabs matches the whole search string as one term, so a descriptive
    phrase like "gruff orc" often finds nothing even when "gruff" and "orc"
    each match voices. When the full phrase yields no results we fall back to
    searching each keyword and rank voices by how many keywords they match.
    """
    page_size = max(1, min(int(limit), 100))

    primary = await _search_once(query, page_size, source)
    if primary:
        return [_normalize_voice(v) for v in primary]

    keywords = [
        word
        for word in re.findall(r"[a-zA-Z']+", query.lower())
        if word not in _VOICE_STOPWORDS and len(word) > 2
    ]
    if len(keywords) < 2:
        return []

    candidates: dict[str, dict[str, Any]] = {}
    for word in keywords:
        for voice in await _search_once(word, page_size, source):
            vid = voice.get("voice_id")
            if vid and vid not in candidates:
                candidates[vid] = voice

    def score(voice: dict[str, Any]) -> int:
        text = _voice_text(voice)
        return sum(1 for word in keywords if word in text)

    ranked = sorted(candidates.values(), key=score, reverse=True)
    return [_normalize_voice(voice) for voice in ranked[:page_size]]


async def _search_once(query: str, page_size: int, source: str) -> list[dict[str, Any]]:
    """Run a single voice-search request and return the raw voice records."""
    base = CONFIG.elevenlabs_base_url.rstrip("/")
    headers = {"xi-api-key": CONFIG.elevenlabs_api_key}
    url = f"{base}/v2/voices" if source == "account" else f"{base}/v1/shared-voices"
    params = {"search": query, "page_size": page_size}
    resp = await get_with_retry(
        url, headers=headers, params=params, timeout=CONFIG.http_timeout
    )
    return resp.json().get("voices") or []


def _voice_text(voice: dict[str, Any]) -> str:
    """Lower-cased searchable text (name, description, labels) for a voice."""
    parts = [str(voice.get("name") or ""), str(voice.get("description") or "")]
    labels = voice.get("labels")
    if isinstance(labels, dict):
        parts.extend(str(value) for value in labels.values())
    parts.extend(
        str(voice.get(key) or "")
        for key in ("gender", "age", "accent", "descriptive", "use_case", "category")
    )
    return " ".join(parts).lower()


def _normalize_voice(v: dict[str, Any]) -> dict[str, Any]:
    """Flatten a voice record into a compact, consistent shape.

    Account voices carry metadata under `labels`; library voices expose it as
    top-level fields, so we synthesise `labels` when it is absent.
    """
    labels = v.get("labels")
    if not labels:
        labels = {
            key: v[key]
            for key in ("gender", "age", "accent", "descriptive", "use_case", "language")
            if v.get(key)
        }
    return {
        "voice_id": v.get("voice_id"),
        "name": v.get("name"),
        "description": v.get("description"),
        "labels": labels,
        "category": v.get("category"),
        "preview_url": v.get("preview_url"),
    }
