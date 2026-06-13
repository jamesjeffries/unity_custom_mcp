# Unity MCP Server

A Model Context Protocol (MCP) server that lets LLM clients (VS Code Copilot,
Claude Desktop, Cursor) drive the Unity Editor through a local socket bridge.

This is the Python server half. The Unity Editor half lives in
[`../unity-bridge`](../unity-bridge).

## How it works

```
MCP client  <--stdio-->  unity-mcp (this server)  <--TCP 127.0.0.1:6400-->  Unity Editor bridge
```

The server exposes one MCP tool per Unity capability. Each tool serializes a
command into a length-prefixed JSON frame, sends it to the Unity bridge, and
returns the structured response.

## Requirements

- Python 3.10+
- A Unity 6 (6000.x) project with the `com.wethinks.mcp-bridge` package installed
  and the bridge started (Window > MCP Bridge).

## Install & run

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -e .

# Run over stdio (what MCP clients launch):
unity-mcp
```

## Configuration

Environment variables (all optional):

| Variable                     | Default     | Meaning                                   |
| ---------------------------- | ----------- | ----------------------------------------- |
| `UNITY_MCP_HOST`             | `127.0.0.1` | Host the Unity bridge listens on          |
| `UNITY_MCP_PORT`             | `6400`      | Port the Unity bridge listens on          |
| `UNITY_MCP_CONNECT_TIMEOUT`  | `5.0`       | Seconds to wait when connecting           |
| `UNITY_MCP_REQUEST_TIMEOUT`  | `30.0`      | Seconds to wait for a command response    |

## Tools

- `ping` — check connectivity (safe to call without Unity running)
- `scene_*` — open/save/new, active scene, hierarchy
- `gameobject_*` — create, find, components, transform, properties, parenting, delete
- `asset_*` / `prefab_*` — find, info, folders, delete, instantiate/create/apply prefabs
- `script_*` — list/read/create/update/delete C# scripts under `Assets/`
- `console_*` — read and clear the Editor console
- `editor_*` — editor state and play-mode control
- `menu_*` — execute allowlisted Editor menu items

## Develop

```bash
pip install -e '.[dev]'
pytest
```
