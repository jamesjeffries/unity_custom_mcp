"""Editor state and play-mode control tools."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def editor_get_state() -> dict[str, Any]:
        """Get Editor state: Unity version, play/pause status, compiling flag."""
        return await connection.send_command("editor.get_state")

    @mcp.tool()
    async def editor_enter_play() -> dict[str, Any]:
        """Enter play mode."""
        return await connection.send_command("editor.enter_play")

    @mcp.tool()
    async def editor_exit_play() -> dict[str, Any]:
        """Exit play mode."""
        return await connection.send_command("editor.exit_play")

    @mcp.tool()
    async def editor_pause(paused: bool = True) -> dict[str, Any]:
        """Pause or unpause play mode."""
        return await connection.send_command("editor.pause", {"paused": paused})

    @mcp.tool()
    async def editor_step() -> dict[str, Any]:
        """Advance one frame while paused in play mode."""
        return await connection.send_command("editor.step")
