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
        target: str,
        component_type: str,
        property_name: str,
        value: Any,
        value_type: str | None = None,
    ) -> dict[str, Any]:
        """Set a serialized property on a component of a GameObject.

        Supports primitives (int, bool, float, string), Vector3 ([x, y, z]),
        enums, LayerMasks, and object references. For enums pass the value name
        (e.g. 'On') or its index. For LayerMasks pass a layer name, a list of
        names, an int bitmask, or 'Everything'/'Nothing'. For object references
        pass an asset path ('Assets/Audio/clip.mp3') or a scene GameObject
        path/name; set value_type to the expected component or asset type (e.g.
        'AudioClip', 'Material', 'AudioSource') so the right object is resolved.
        """
        return await connection.send_command(
            "gameobject.set_property",
            {
                "target": target,
                "component_type": component_type,
                "property_name": property_name,
                "value": value,
                "value_type": value_type,
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
    async def gameobject_set_tag(
        target: str, tag: str, create_if_missing: bool = True
    ) -> dict[str, Any]:
        """Set a GameObject's tag (e.g. 'MainCamera', 'Player').

        When create_if_missing is true, an unknown tag is added to the project's
        TagManager first so the assignment succeeds.
        """
        return await connection.send_command(
            "gameobject.set_tag",
            {"target": target, "tag": tag, "create_if_missing": create_if_missing},
        )

    @mcp.tool()
    async def gameobject_set_layer(
        target: str,
        layer: str | None = None,
        layer_index: int = 0,
        include_children: bool = False,
        create_if_missing: bool = True,
    ) -> dict[str, Any]:
        """Set a GameObject's layer by name (or by layer_index if no name is given).

        When include_children is true the whole hierarchy is moved to the layer.
        When create_if_missing is true, an unknown layer name is added to the
        first free user layer slot (8..31) before assignment. Useful for tagging
        terrain/ground so generate_scatter can target it with ground_layer.
        """
        return await connection.send_command(
            "gameobject.set_layer",
            {
                "target": target,
                "layer": layer,
                "layer_index": layer_index,
                "include_children": include_children,
                "create_if_missing": create_if_missing,
            },
        )

    @mcp.tool()
    async def gameobject_remove_component(
        target: str, component_type: str
    ) -> dict[str, Any]:
        """Remove a component from a GameObject by type name (e.g. 'SphereCollider').

        The Transform component cannot be removed.
        """
        return await connection.send_command(
            "gameobject.remove_component",
            {"target": target, "component_type": component_type},
        )

    @mcp.tool()
    async def gameobject_delete(target: str) -> dict[str, Any]:
        """Delete a GameObject from the active scene."""
        return await connection.send_command("gameobject.delete", {"target": target})
