"""Shared helpers for the AI-generated media tools (textures and audio)."""

from __future__ import annotations

import asyncio
import base64
import re
from typing import Any

import httpx

from ..connection import connection


def slugify(text: str, fallback: str = "asset") -> str:
    """Turn an arbitrary prompt into a safe, short asset file stem."""
    slug = re.sub(r"[^A-Za-z0-9]+", "_", text).strip("_")
    return slug[:48] or fallback


async def post_with_retry(
    url: str,
    *,
    headers: dict[str, str],
    json: dict[str, Any],
    timeout: float,
    max_retries: int = 6,
) -> httpx.Response:
    """POST with bounded retries on transient provider failures.

    Image and audio providers commonly rate-limit (429) or briefly route
    requests to a backend that has not loaded the deployment yet (400
    ``unknown_model``). We retry those, honouring the ``Retry-After`` header when
    present and otherwise falling back to exponential backoff, then raise for
    status on the final attempt.
    """
    return await _request_with_retry(
        "POST", url, headers=headers, timeout=timeout, max_retries=max_retries, json=json
    )


async def get_with_retry(
    url: str,
    *,
    headers: dict[str, str],
    timeout: float,
    params: dict[str, Any] | None = None,
    max_retries: int = 6,
) -> httpx.Response:
    """GET with the same bounded-retry behaviour as :func:`post_with_retry`."""
    return await _request_with_retry(
        "GET", url, headers=headers, timeout=timeout, max_retries=max_retries, params=params
    )


async def _request_with_retry(
    method: str,
    url: str,
    *,
    headers: dict[str, str],
    timeout: float,
    max_retries: int,
    **kwargs: Any,
) -> httpx.Response:
    async with httpx.AsyncClient(timeout=timeout) as client:
        for attempt in range(max_retries + 1):
            resp = await client.request(method, url, headers=headers, **kwargs)
            retryable = resp.status_code in (429, 503) or (
                resp.status_code == 400 and "unknown_model" in resp.text
            )
            if not retryable or attempt == max_retries:
                resp.raise_for_status()
                return resp

            retry_after = resp.headers.get("retry-after")
            try:
                delay = float(retry_after) if retry_after else 0.0
            except ValueError:
                delay = 0.0
            delay = min(max(delay, 2.0 * (2**attempt)), 30.0)
            await asyncio.sleep(delay)

    # Unreachable, but keeps type checkers happy.
    resp.raise_for_status()
    return resp


async def import_bytes(path: str, data: bytes) -> dict[str, Any]:
    """Send raw bytes to Unity to be written under Assets/ and imported."""
    b64 = base64.b64encode(data).decode("ascii")
    return await connection.send_command(
        "asset.import_binary", {"path": path, "data_base64": b64}
    )
