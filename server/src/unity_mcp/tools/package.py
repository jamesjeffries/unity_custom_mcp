"""Unity Package Manager (UPM) tools: list, add, and remove packages."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def package_list() -> dict[str, Any]:
        """List the packages declared in the project's Packages/manifest.json.

        Reads the manifest directly (synchronous and reliable) and also reports
        the status of any in-flight add/remove request kicked off this session.
        """
        return await connection.send_command("package.list", {})

    @mcp.tool()
    async def package_add(identifier: str) -> dict[str, Any]:
        """Add a Unity package by identifier.

        identifier may be a registry name ('com.unity.cinemachine'), a name with
        a pinned version ('com.unity.inputsystem@1.7.0'), or a git URL. The
        request is asynchronous: Unity resolves the package, may recompile, and
        triggers a domain reload. Call package_list afterwards to confirm it
        landed. Keep the Unity Editor focused while it resolves so the bridge
        keeps pumping through the reload.
        """
        return await connection.send_command(
            "package.add", {"identifier": identifier}
        )

    @mcp.tool()
    async def package_remove(identifier: str) -> dict[str, Any]:
        """Remove a Unity package by identifier (e.g. 'com.unity.cinemachine').

        Asynchronous, like package_add; call package_list to confirm removal.
        """
        return await connection.send_command(
            "package.remove", {"identifier": identifier}
        )
