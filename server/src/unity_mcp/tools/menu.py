"""Menu item execution tool (allowlisted on the Unity side)."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def menu_execute(menu_path: str) -> dict[str, Any]:
        """Execute a Unity Editor menu item by its path (e.g. 'Assets/Refresh').

        The Unity bridge enforces an allowlist of safe menu items and rejects
        destructive or unknown entries.
        """
        return await connection.send_command(
            "menu.execute", {"menu_path": menu_path}
        )

    @mcp.tool()
    async def menu_list_allowed() -> dict[str, Any]:
        """List the menu item paths the bridge permits via menu_execute."""
        return await connection.send_command("menu.list_allowed")
