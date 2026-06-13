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
        # Whether we have ever reached the bridge in this process. Used to tell
        # "Unity is mid-recompile, wait for it" apart from "Unity is closed".
        self._ever_connected = False

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
        self._ever_connected = True

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
        self,
        command: str,
        params: dict[str, Any] | None = None,
        *,
        wait_for_reconnect: bool = True,
    ) -> Any:
        """Send a command to Unity and return its `data`, raising on failure.

        Transparently survives Unity domain reloads: every script recompile
        drops the socket and the bridge restarts a few seconds later. While the
        bridge is unreachable, the command is retried for up to
        ``reconnect_timeout`` seconds (but only once Unity has been reached at
        least once, so a genuinely closed Editor still fails fast).

        Pass ``wait_for_reconnect=False`` for diagnostics like ``ping`` that
        should report unreachability immediately instead of blocking.
        """
        request = Request.create(command, params)
        loop = asyncio.get_event_loop()
        async with self._lock:
            wait = wait_for_reconnect and self._ever_connected
            deadline = loop.time() + (self._config.reconnect_timeout if wait else 0.0)
            while True:
                # (Re)establish the connection, tolerating long bridge outages
                # during a Unity recompile/domain reload.
                try:
                    await self._ensure_connected()
                except UnityConnectionError:
                    await self._close_locked()
                    if loop.time() < deadline:
                        await asyncio.sleep(self._config.reconnect_interval)
                        continue
                    raise

                # Send the request and read the response.
                try:
                    response = await self._send_and_receive(request)
                except (UnityConnectionError, ProtocolError, OSError) as exc:
                    await self._close_locked()
                    # A reload can drop the socket mid-flight; retry within the
                    # reconnect window before surfacing the error.
                    if loop.time() < deadline:
                        await asyncio.sleep(self._config.reconnect_interval)
                        continue
                    if isinstance(exc, UnityConnectionError):
                        raise
                    raise UnityConnectionError(str(exc)) from exc

                if not response.success:
                    raise UnityConnectionError(
                        response.error or "Unity reported an unknown error"
                    )
                return response.data


# Shared singleton used by all tools.
connection = UnityConnection()
