"""Asset and prefab management tools."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def asset_find(
        filter: str = "", folder: str = "Assets"
    ) -> dict[str, Any]:
        """Find assets using a Unity search filter (e.g. 't:Material', 'Player').

        folder scopes the search (defaults to the whole 'Assets' folder).
        """
        return await connection.send_command(
            "asset.find", {"filter": filter, "folder": folder}
        )

    @mcp.tool()
    async def asset_get_info(path: str) -> dict[str, Any]:
        """Get metadata (type, GUID, dependencies) for an asset by path."""
        return await connection.send_command("asset.get_info", {"path": path})

    @mcp.tool()
    async def asset_create_folder(path: str) -> dict[str, Any]:
        """Create a folder under Assets (e.g. 'Assets/Prefabs')."""
        return await connection.send_command("asset.create_folder", {"path": path})

    @mcp.tool()
    async def asset_delete(path: str) -> dict[str, Any]:
        """Delete an asset by path."""
        return await connection.send_command("asset.delete", {"path": path})

    @mcp.tool()
    async def prefab_instantiate(
        path: str, position: list[float] | None = None, parent: str | None = None
    ) -> dict[str, Any]:
        """Instantiate a prefab asset into the active scene."""
        return await connection.send_command(
            "prefab.instantiate",
            {"path": path, "position": position, "parent": parent},
        )

    @mcp.tool()
    async def prefab_create_from_gameobject(
        target: str, path: str
    ) -> dict[str, Any]:
        """Save a scene GameObject as a new prefab asset at the given path."""
        return await connection.send_command(
            "prefab.create_from_gameobject", {"target": target, "path": path}
        )

    @mcp.tool()
    async def prefab_apply(target: str) -> dict[str, Any]:
        """Apply a prefab instance's overrides back to its source prefab asset."""
        return await connection.send_command("prefab.apply", {"target": target})
