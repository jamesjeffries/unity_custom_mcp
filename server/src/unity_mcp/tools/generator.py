"""High-level scene generators that compose many Unity operations at once."""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from ..connection import connection


def register(mcp: FastMCP) -> None:
    @mcp.tool()
    async def generate_first_person_player(
        name: str = "FPSPlayer",
        position: list[float] | None = None,
        walk_speed: float | None = None,
        crawl_speed: float | None = None,
        jump_height: float | None = None,
        mouse_sensitivity: float | None = None,
    ) -> dict[str, Any]:
        """Create a ready-to-play first-person character in one call.

        Builds a capsule with a CharacterController, an eye-height camera child,
        and a precompiled controller (WASD move, mouse look, Space jump, and
        Left Ctrl / C crawl). The controller uses the legacy Input Manager, so
        the project's "Active Input Handling" must be "Input Manager (Old)" or
        "Both". Enter Play mode to walk around.
        """
        return await connection.send_command(
            "generator.first_person_player",
            {
                "name": name,
                "position": position,
                "walk_speed": walk_speed,
                "crawl_speed": crawl_speed,
                "jump_height": jump_height,
                "mouse_sensitivity": mouse_sensitivity,
            },
        )

    @mcp.tool()
    async def generate_terrain(
        name: str = "Terrain",
        width: float = 500.0,
        length: float = 500.0,
        height: float = 120.0,
        resolution: int = 257,
        seed: int = 0,
        mountain_strength: float = 0.8,
        meadow_fraction: float = 0.4,
        position: list[float] | None = None,
    ) -> dict[str, Any]:
        """Generate a Unity Terrain with procedural hills, a mountain range, and meadows.

        Heights come from layered Perlin/ridged noise: rolling base hills, a
        mountain band running across the terrain (scaled by mountain_strength),
        and flattened low areas controlled by meadow_fraction. The resolution is
        snapped to a valid heightmap size (2^n + 1). A TerrainData asset is saved
        under Assets/MCP/Terrain. Use the same seed to reproduce a layout.
        """
        return await connection.send_command(
            "generator.terrain",
            {
                "name": name,
                "width": width,
                "length": length,
                "height": height,
                "resolution": resolution,
                "seed": seed,
                "mountain_strength": mountain_strength,
                "meadow_fraction": meadow_fraction,
                "position": position,
            },
        )

    @mcp.tool()
    async def generate_scatter(
        count: int = 50,
        prefab: str | None = None,
        primitive: str | None = None,
        area_center: list[float] | None = None,
        area_size: list[float] | None = None,
        parent: str | None = None,
        align_to_ground: bool = True,
        random_yaw: bool = True,
        min_scale: float = 1.0,
        max_scale: float = 1.0,
        seed: int = 0,
    ) -> dict[str, Any]:
        """Scatter many instances of a prefab or primitive across an area in one call.

        Provide either a prefab asset path (e.g. 'Assets/Trees/Pine.prefab') or a
        primitive ('Cube', 'Sphere', 'Capsule', 'Cylinder', 'Plane', 'Quad').
        Instances are placed randomly within area_size (x,z) around area_center.
        When align_to_ground is true, each instance is raycast down onto the
        nearest collider (e.g. a generated terrain) so it sits on the surface.
        random_yaw and min/max_scale add variation; seed makes it reproducible.
        """
        return await connection.send_command(
            "generator.scatter",
            {
                "count": count,
                "prefab": prefab,
                "primitive": primitive,
                "area_center": area_center,
                "area_size": area_size,
                "parent": parent,
                "align_to_ground": align_to_ground,
                "random_yaw": random_yaw,
                "min_scale": min_scale,
                "max_scale": max_scale,
                "seed": seed,
            },
        )
