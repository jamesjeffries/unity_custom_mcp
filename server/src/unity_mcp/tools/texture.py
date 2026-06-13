"""AI texture generation (optional; requires an image model endpoint).

Configure with UNITY_MCP_IMAGE_ENDPOINT and UNITY_MCP_IMAGE_API_KEY (plus
UNITY_MCP_IMAGE_MODEL for the deployed model name). The server calls the OpenAI
v1 images surface, so no api-version is required. When unconfigured, the tool
returns a friendly notice instead of failing.
"""

from __future__ import annotations

import base64
from typing import Any

import httpx
from mcp.server.fastmcp import FastMCP

from ..config import CONFIG
from ..connection import connection
from ._media import import_bytes, post_with_retry, slugify


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def texture_generate(
        prompt: str,
        name: str | None = None,
        size: str = "1024x1024",
        target: str | None = None,
    ) -> dict[str, Any]:
        """Generate a texture from a text prompt and import it into Unity.

        Requires an image model to be configured (see the README "AI asset
        generation" section). The generated PNG is saved under
        Assets/MCP/Textures. If `target` (a GameObject name or hierarchy path) is
        given, a material using the texture is created and assigned to it.

        Returns {"configured": false, ...} with setup guidance when no image
        endpoint is configured, so the rest of the toolset keeps working.
        """
        if not CONFIG.is_image_configured:
            return {
                "configured": False,
                "message": (
                    "Image generation is not configured. Set UNITY_MCP_IMAGE_ENDPOINT "
                    "and UNITY_MCP_IMAGE_API_KEY (and UNITY_MCP_IMAGE_MODEL for the "
                    "deployed model name)."
                ),
            }

        try:
            image = await _generate_image(prompt, size)
        except httpx.HTTPStatusError as exc:
            return {
                "configured": True,
                "ok": False,
                "error": f"{exc.response.status_code}: {exc.response.text[:300]}",
            }
        except httpx.HTTPError as exc:
            return {"configured": True, "ok": False, "error": f"Image request failed: {exc}"}

        slug = slugify(name or prompt)
        path = f"{CONFIG.texture_folder}/{slug}.png"
        result: dict[str, Any] = {
            "configured": True,
            "ok": True,
            "texture": path,
            "import": await import_bytes(path, image),
        }

        if target:
            result["material"] = await connection.send_command(
                "material.create_from_texture",
                {"name": slug, "texture": path, "target": target},
            )

        return result


async def _generate_image(prompt: str, size: str) -> bytes:
    """Call the configured images API (OpenAI v1 surface) and return PNG bytes."""
    endpoint = CONFIG.image_endpoint.rstrip("/")
    if endpoint.endswith("/images/generations"):
        url = endpoint
    elif endpoint.endswith("/v1"):
        url = f"{endpoint}/images/generations"
    else:
        url = f"{endpoint}/openai/v1/images/generations"

    headers = {"Authorization": f"Bearer {CONFIG.image_api_key}"}
    payload: dict[str, Any] = {
        "prompt": prompt,
        "size": size,
        "n": 1,
        "output_format": "png",
    }
    if CONFIG.image_model:
        payload["model"] = CONFIG.image_model

    resp = await post_with_retry(
        url, headers=headers, json=payload, timeout=CONFIG.http_timeout
    )
    data = resp.json()

    item = (data.get("data") or [{}])[0]
    if item.get("b64_json"):
        return base64.b64decode(item["b64_json"])

    if item.get("url"):
        async with httpx.AsyncClient(timeout=CONFIG.http_timeout) as client:
            img = await client.get(item["url"])
            img.raise_for_status()
            return img.content

    raise httpx.HTTPError("Image API returned no image data")
