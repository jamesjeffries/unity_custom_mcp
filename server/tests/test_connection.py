"""Integration tests for UnityConnection against an in-process fake bridge."""

from __future__ import annotations

import asyncio
from typing import Any

import pytest

from unity_mcp.config import Config
from unity_mcp.connection import UnityConnection, UnityConnectionError
from unity_mcp.protocol import HEADER_SIZE, decode_body, decode_header, encode_frame


class FakeBridge:
    """A minimal TCP server that speaks the framed-JSON protocol."""

    def __init__(self, responder, port: int = 0) -> None:
        self._responder = responder
        self._server: asyncio.AbstractServer | None = None
        self.host = "127.0.0.1"
        self.port = port
        self.requests: list[dict[str, Any]] = []
        self._writers: list[asyncio.StreamWriter] = []

    async def start(self) -> None:
        self._server = await asyncio.start_server(
            self._handle, self.host, self.port
        )
        self.port = self._server.sockets[0].getsockname()[1]

    async def stop(self) -> None:
        # Close active client connections too, simulating a Unity domain reload
        # that drops every socket.
        for writer in self._writers:
            try:
                writer.close()
            except OSError:
                pass
        self._writers.clear()
        if self._server is not None:
            self._server.close()
            await self._server.wait_closed()
            self._server = None

    async def _handle(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        self._writers.append(writer)
        try:
            while True:
                header = await reader.readexactly(HEADER_SIZE)
                length = decode_header(header)
                body = await reader.readexactly(length)
                request = decode_body(body)
                self.requests.append(request)
                response = self._responder(request)
                writer.write(encode_frame(response))
                await writer.drain()
        except (asyncio.IncompleteReadError, ConnectionResetError):
            pass
        finally:
            try:
                writer.close()
            except OSError:
                pass


def _ok_responder(request: dict[str, Any]) -> dict[str, Any]:
    return {"id": request["id"], "success": True, "data": {"echo": request["command"]}}


async def _make_connection(bridge: FakeBridge) -> UnityConnection:
    config = Config(host=bridge.host, port=bridge.port)
    return UnityConnection(config)


async def test_send_command_success() -> None:
    bridge = FakeBridge(_ok_responder)
    await bridge.start()
    try:
        conn = await _make_connection(bridge)
        data = await conn.send_command("ping", {"a": 1})
        assert data == {"echo": "ping"}
        assert bridge.requests[0]["command"] == "ping"
        assert bridge.requests[0]["params"] == {"a": 1}
        await conn.close()
    finally:
        await bridge.stop()


async def test_send_command_error_response_raises() -> None:
    def responder(request: dict[str, Any]) -> dict[str, Any]:
        return {"id": request["id"], "success": False, "error": "nope"}

    bridge = FakeBridge(responder)
    await bridge.start()
    try:
        conn = await _make_connection(bridge)
        with pytest.raises(UnityConnectionError, match="nope"):
            await conn.send_command("scene.open", {"path": "x"})
        await conn.close()
    finally:
        await bridge.stop()


async def test_send_command_raises_when_unreachable() -> None:
    # Port 1 is reserved/unused; connection should fail and raise cleanly.
    config = Config(host="127.0.0.1", port=1, connect_timeout=0.5)
    conn = UnityConnection(config)
    with pytest.raises(UnityConnectionError):
        await conn.send_command("ping")


async def test_reconnects_after_drop() -> None:
    bridge = FakeBridge(_ok_responder)
    await bridge.start()
    conn = await _make_connection(bridge)
    try:
        assert await conn.send_command("ping") == {"echo": "ping"}

        # Simulate a Unity domain reload: drop all sockets, then bring the
        # bridge back up on the same port.
        port = bridge.port
        await bridge.stop()
        bridge2 = FakeBridge(_ok_responder, port=port)
        await bridge2.start()
        try:
            data = await conn.send_command("editor.get_state")
            assert data == {"echo": "editor.get_state"}
        finally:
            await bridge2.stop()
    finally:
        await conn.close()
        await bridge.stop()
