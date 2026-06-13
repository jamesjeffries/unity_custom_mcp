"""Scene management tools."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def scene_get_active() -> dict[str, Any]:
        """Get the name and path of the currently active scene."""
        return await connection.send_command("scene.get_active")

    @mcp.tool()
    async def scene_get_hierarchy(include_components: bool = False) -> dict[str, Any]:
        """Get the GameObject hierarchy of the active scene as a nested tree.

        Set include_components to also list the component type names on each
        GameObject.
        """
        return await connection.send_command(
            "scene.get_hierarchy", {"include_components": include_components}
        )

    @mcp.tool()
    async def scene_open(path: str, additive: bool = False) -> dict[str, Any]:
        """Open a scene by asset path (e.g. 'Assets/Scenes/Main.unity').

        When additive is true the scene is loaded alongside the current one.
        """
        return await connection.send_command(
            "scene.open", {"path": path, "additive": additive}
        )

    @mcp.tool()
    async def scene_save(path: str | None = None) -> dict[str, Any]:
        """Save the active scene. Optionally save to a new path (Save As)."""
        return await connection.send_command("scene.save", {"path": path})

    @mcp.tool()
    async def scene_new(path: str | None = None) -> dict[str, Any]:
        """Create a new empty scene. Optionally save it to the given path."""
        return await connection.send_command("scene.new", {"path": path})
