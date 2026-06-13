"""Wire protocol shared by the Python server and the Unity bridge.

Frames are length-prefixed JSON:

    [4-byte big-endian unsigned length][UTF-8 JSON payload]

A request payload looks like::

    {"id": "<uuid>", "command": "gameobject.create", "params": {...}}

A response payload looks like::

    {"id": "<uuid>", "success": true, "data": {...}}
    {"id": "<uuid>", "success": false, "error": "message"}
"""

from __future__ import annotations

import json
import struct
import uuid
from dataclasses import dataclass
from typing import Any

HEADER_SIZE = 4
# Guard against a malformed/oversized length prefix (16 MiB).
MAX_FRAME_BYTES = 16 * 1024 * 1024


class ProtocolError(Exception):
    """Raised when a frame cannot be encoded or decoded."""


@dataclass
class Request:
    command: str
    params: dict[str, Any]
    id: str

    @classmethod
    def create(cls, command: str, params: dict[str, Any] | None = None) -> "Request":
        return cls(command=command, params=params or {}, id=uuid.uuid4().hex)

    def to_dict(self) -> dict[str, Any]:
        return {"id": self.id, "command": self.command, "params": self.params}


@dataclass
class Response:
    id: str
    success: bool
    data: Any = None
    error: str | None = None

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "Response":
        return cls(
            id=payload.get("id", ""),
            success=bool(payload.get("success", False)),
            data=payload.get("data"),
            error=payload.get("error"),
        )


def encode_frame(payload: dict[str, Any]) -> bytes:
    """Serialize a payload dict into a length-prefixed JSON frame."""
    try:
        body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    except (TypeError, ValueError) as exc:  # pragma: no cover - defensive
        raise ProtocolError(f"Cannot encode payload: {exc}") from exc
    if len(body) > MAX_FRAME_BYTES:
        raise ProtocolError(f"Frame too large: {len(body)} bytes")
    return struct.pack(">I", len(body)) + body


def decode_header(header: bytes) -> int:
    """Decode the 4-byte length prefix into a body length."""
    if len(header) != HEADER_SIZE:
        raise ProtocolError(f"Header must be {HEADER_SIZE} bytes, got {len(header)}")
    (length,) = struct.unpack(">I", header)
    if length > MAX_FRAME_BYTES:
        raise ProtocolError(f"Declared frame length too large: {length}")
    return length


def decode_body(body: bytes) -> dict[str, Any]:
    """Decode a JSON frame body into a dict."""
    try:
        payload = json.loads(body.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ProtocolError(f"Cannot decode frame body: {exc}") from exc
    if not isinstance(payload, dict):
        raise ProtocolError("Frame body must be a JSON object")
    return payload
