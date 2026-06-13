"""Async TCP client that talks to the Unity Editor bridge.

The connection is lazily established and transparently re-established after a
Unity domain reload (which happens on every script recompile and drops the
socket). Requests are serialized with a lock because a single Unity bridge
processes one command at a time on the main thread.
"""

from __future__ import annotations

import asyncio
from typing import Any

from .config import CONFIG, Config
from .protocol import (
    HEADER_SIZE,
    ProtocolError,
    Request,
    Response,
    decode_body,
    decode_header,
    encode_frame,
)


class UnityConnectionError(Exception):
    """Raised when the server cannot reach or talk to the Unity bridge."""


class UnityConnection:
    """Maintains a single reconnecting socket to the Unity bridge."""

    def __init__(self, config: Config = CONFIG) -> None:
        self._config = config
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._lock = asyncio.Lock()

    @property
    def connected(self) -> bool:
        return self._writer is not None and not self._writer.is_closing()

    async def _connect(self) -> None:
        try:
            self._reader, self._writer = await asyncio.wait_for(
                asyncio.open_connection(self._config.host, self._config.port),
                timeout=self._config.connect_timeout,
            )
        except (OSError, asyncio.TimeoutError) as exc:
            self._reader = None
            self._writer = None
            raise UnityConnectionError(
                f"Could not connect to Unity bridge at "
                f"{self._config.host}:{self._config.port}. "
                f"Is the Unity Editor open with the MCP bridge started? ({exc})"
            ) from exc

    async def _ensure_connected(self) -> None:
        if not self.connected:
            await self._connect()

    async def close(self) -> None:
        async with self._lock:
            await self._close_locked()

    async def _close_locked(self) -> None:
        if self._writer is not None:
            try:
                self._writer.close()
                await self._writer.wait_closed()
            except OSError:
                pass
        self._reader = None
        self._writer = None

    async def _read_exactly(self, count: int) -> bytes:
        assert self._reader is not None
        try:
            return await asyncio.wait_for(
                self._reader.readexactly(count),
                timeout=self._config.request_timeout,
            )
        except asyncio.IncompleteReadError as exc:
            raise UnityConnectionError("Unity bridge closed the connection") from exc
        except asyncio.TimeoutError as exc:
            raise UnityConnectionError(
                f"Timed out waiting for Unity after "
                f"{self._config.request_timeout}s"
            ) from exc

    async def _send_and_receive(self, request: Request) -> Response:
        assert self._writer is not None
        frame = encode_frame(request.to_dict())
        self._writer.write(frame)
        await self._writer.drain()

        header = await self._read_exactly(HEADER_SIZE)
        length = decode_header(header)
        body = await self._read_exactly(length) if length else b"{}"
        payload = decode_body(body)
        return Response.from_dict(payload)

    async def send_command(
        self, command: str, params: dict[str, Any] | None = None
    ) -> Any:
        """Send a command to Unity and return its `data`, raising on failure.

        Retries once after reconnecting, which covers the common case where a
        Unity domain reload silently dropped the previous socket.
        """
        request = Request.create(command, params)
        async with self._lock:
            for attempt in range(2):
                try:
                    await self._ensure_connected()
                    response = await self._send_and_receive(request)
                except (UnityConnectionError, ProtocolError, OSError) as exc:
                    await self._close_locked()
                    if attempt == 0:
                        continue
                    if isinstance(exc, UnityConnectionError):
                        raise
                    raise UnityConnectionError(str(exc)) from exc
                if not response.success:
                    raise UnityConnectionError(
                        response.error or "Unity reported an unknown error"
                    )
                return response.data
        raise UnityConnectionError("Failed to communicate with Unity bridge")


# Shared singleton used by all tools.
connection = UnityConnection()
