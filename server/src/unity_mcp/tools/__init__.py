"""Tool groups exposed by the Unity MCP server.

Each module provides a `register(mcp)` function that attaches its tools to the
shared FastMCP instance.
"""

from __future__ import annotations

from mcp.server.fastmcp import FastMCP

from . import asset, console, editor, gameobject, menu, scene, script


def register_all(mcp: FastMCP) -> None:
    """Register every tool group with the given FastMCP server."""
    scene.register(mcp)
    gameobject.register(mcp)
    asset.register(mcp)
    script.register(mcp)
    console.register(mcp)
    editor.register(mcp)
    menu.register(mcp)


__all__ = ["register_all"]
