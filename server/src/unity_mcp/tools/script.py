"""C# script file tools (sandboxed to the Assets/ folder by the bridge)."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def script_list(folder: str = "Assets") -> dict[str, Any]:
        """List C# script (.cs) files under the given folder."""
        return await connection.send_command("script.list", {"folder": folder})

    @mcp.tool()
    async def script_read(path: str) -> dict[str, Any]:
        """Read the text contents of a C# script under Assets/."""
        return await connection.send_command("script.read", {"path": path})

    @mcp.tool()
    async def script_create(path: str, contents: str) -> dict[str, Any]:
        """Create a new C# script under Assets/ and refresh the asset database.

        Fails if the file already exists. Triggers a Unity recompile.
        """
        return await connection.send_command(
            "script.create", {"path": path, "contents": contents}
        )

    @mcp.tool()
    async def script_update(path: str, contents: str) -> dict[str, Any]:
        """Overwrite an existing C# script under Assets/ and trigger a recompile."""
        return await connection.send_command(
            "script.update", {"path": path, "contents": contents}
        )

    @mcp.tool()
    async def script_delete(path: str) -> dict[str, Any]:
        """Delete a C# script under Assets/."""
        return await connection.send_command("script.delete", {"path": path})
