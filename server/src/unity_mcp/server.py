"""FastMCP server entry point for the Unity MCP bridge.

Run over stdio (the default) so it works with VS Code Copilot, Claude Desktop,
Cursor, and any other MCP stdio client.
"""

from __future__ import annotations

from typing import Any

from mcp.server.fastmcp import FastMCP

from .config import CONFIG
from .connection import UnityConnectionError, connection
from .tools import register_all

mcp = FastMCP(
    "unity-mcp",
    instructions=(
        "Tools to drive the Unity Editor: scenes, GameObjects, assets/prefabs, "
        "C# scripts, the console, play mode, and menu items. The Unity Editor "
        "must be open with the MCP bridge package installed and started "
        "(Window > MCP Bridge). Call `ping` first to confirm connectivity."
    ),
)


@mcp.tool()
async def ping() -> dict[str, Any]:
    """Check connectivity to the Unity Editor bridge.

    Returns the configured endpoint and whether Unity responded. This never
    raises, so it is safe to call to diagnose setup problems.
    """
    endpoint = f"{CONFIG.host}:{CONFIG.port}"
    try:
        data = await connection.send_command("ping", wait_for_reconnect=False)
    except UnityConnectionError as exc:
        return {"connected": False, "endpoint": endpoint, "error": str(exc)}
    return {"connected": True, "endpoint": endpoint, "unity": data}


register_all(mcp)


def main() -> None:
    """Console-script entry point."""
    mcp.run()


if __name__ == "__main__":
    main()
