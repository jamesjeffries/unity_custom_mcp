"""Shared helpers for the AI-generated media tools (textures and audio)."""

from __future__ import annotations

import base64
import re
from typing import Any

from ..connection import connection


def slugify(text: str, fallback: str = "asset") -> str:
    """Turn an arbitrary prompt into a safe, short asset file stem."""
    slug = re.sub(r"[^A-Za-z0-9]+", "_", text).strip("_")
    return slug[:48] or fallback


async def import_bytes(path: str, data: bytes) -> dict[str, Any]:
    """Send raw bytes to Unity to be written under Assets/ and imported."""
    b64 = base64.b64encode(data).decode("ascii")
    return await connection.send_command(
        "asset.import_binary", {"path": path, "data_base64": b64}
    )
