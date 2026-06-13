"""GameObject creation and manipulation tools."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def gameobject_create(
        name: str = "GameObject",
        primitive: str | None = None,
        parent: str | None = None,
        position: list[float] | None = None,
    ) -> dict[str, Any]:
        """Create a GameObject in the active scene.

        primitive may be one of: Cube, Sphere, Capsule, Cylinder, Plane, Quad.
        Omit primitive to create an empty GameObject. parent is the path or name
        of an existing GameObject to nest under. position is an [x, y, z] world
        position.
        """
        return await connection.send_command(
            "gameobject.create",
            {
                "name": name,
                "primitive": primitive,
                "parent": parent,
                "position": position,
            },
        )

    @mcp.tool()
    async def gameobject_find(query: str) -> dict[str, Any]:
        """Find GameObjects in the active scene by name or hierarchy path."""
        return await connection.send_command("gameobject.find", {"query": query})

    @mcp.tool()
    async def gameobject_get_components(target: str) -> dict[str, Any]:
        """List the components (and their serialized properties) on a GameObject."""
        return await connection.send_command(
            "gameobject.get_components", {"target": target}
        )

    @mcp.tool()
    async def gameobject_add_component(
        target: str, component_type: str
    ) -> dict[str, Any]:
        """Add a component to a GameObject by type name (e.g. 'Rigidbody')."""
        return await connection.send_command(
            "gameobject.add_component",
            {"target": target, "component_type": component_type},
        )

    @mcp.tool()
    async def gameobject_set_transform(
        target: str,
        position: list[float] | None = None,
        rotation: list[float] | None = None,
        scale: list[float] | None = None,
    ) -> dict[str, Any]:
        """Set a GameObject's local transform. Each arg is an [x, y, z] vector.

        rotation is Euler angles in degrees. Omitted vectors are left unchanged.
        """
        return await connection.send_command(
            "gameobject.set_transform",
            {
                "target": target,
                "position": position,
                "rotation": rotation,
                "scale": scale,
            },
        )

    @mcp.tool()
    async def gameobject_set_property(
        target: str, component_type: str, property_name: str, value: Any
    ) -> dict[str, Any]:
        """Set a serialized property on a component of a GameObject."""
        return await connection.send_command(
            "gameobject.set_property",
            {
                "target": target,
                "component_type": component_type,
                "property_name": property_name,
                "value": value,
            },
        )

    @mcp.tool()
    async def gameobject_set_color(
        target: str, color: list[float]
    ) -> dict[str, Any]:
        """Set the color of a GameObject's renderer.

        color is an [r, g, b] or [r, g, b, a] array with each component in the
        0..1 range (e.g. [1, 0, 0] for red). Creates a material asset under
        Assets/MCP/Materials and assigns it, working across the Built-in, URP,
        and HDRP render pipelines.
        """
        return await connection.send_command(
            "gameobject.set_color",
            {"target": target, "color": color},
        )

    @mcp.tool()
    async def gameobject_set_parent(
        target: str, parent: str | None, keep_world_position: bool = True
    ) -> dict[str, Any]:
        """Re-parent a GameObject. Pass parent=None to move it to the scene root."""
        return await connection.send_command(
            "gameobject.set_parent",
            {
                "target": target,
                "parent": parent,
                "keep_world_position": keep_world_position,
            },
        )

    @mcp.tool()
    async def gameobject_delete(target: str) -> dict[str, Any]:
        """Delete a GameObject from the active scene."""
        return await connection.send_command("gameobject.delete", {"target": target})
