"""Unity Editor console log tools."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def console_get_logs(
        level: str = "all", count: int = 50
    ) -> dict[str, Any]:
        """Read recent Unity console log entries.

        level filters by severity: 'all', 'log', 'warning', or 'error'.
        count caps how many of the most recent entries are returned.
        """
        return await connection.send_command(
            "console.get_logs", {"level": level, "count": count}
        )

    @mcp.tool()
    async def console_clear() -> dict[str, Any]:
        """Clear the Unity Editor console."""
        return await connection.send_command("console.clear")
