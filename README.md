# Unity Custom MCP

An end-to-end [Model Context Protocol](https://modelcontextprotocol.io) (MCP)
integration for the Unity Editor, built in-house. It lets LLM clients — VS Code
Copilot, Claude Desktop, Cursor — drive Unity: create and edit scenes and
GameObjects, manage assets and prefabs, read the console, control play mode, and
author C# scripts.

## Architecture

```
┌────────────┐  stdio   ┌────────────────────┐  TCP 127.0.0.1:6400  ┌─────────────────────┐
│ MCP client │ <──────> │ unity-mcp (Python) │ <──────────────────> │ Unity Editor bridge │
│ (Copilot…) │          │  FastMCP server    │  length-prefixed JSON │  (C# UPM package)    │
└────────────┘          └────────────────────┘                       └─────────────────────┘
```

- **`server/`** — the Python MCP server (`unity-mcp`). Exposes one MCP tool per
  Unity capability and forwards each as a framed JSON command over TCP.
- **`unity-bridge/com.wethinks.mcp-bridge/`** — the Unity Editor package. Runs a
  loopback TCP listener, marshals commands onto Unity's main thread, executes
  them, and returns structured responses. Survives domain reloads.

## Quick start

### 1. Install the Unity bridge package

In your Unity 6 (6000.x) project: **Window > Package Manager > + > Add package
from disk…** and select
`unity-bridge/com.wethinks.mcp-bridge/package.json`.

(Or add from a Git URL once this repo is hosted:
`https://…/unity_custom_mcp.git?path=/unity-bridge/com.wethinks.mcp-bridge`.)

Then open **Window > MCP Bridge** and confirm it shows
`Listening on 127.0.0.1:6400` (it auto-starts on load).

### 2. Install and run the Python server

```bash
cd server
python3 -m venv .venv          # needs Python 3.10+
. .venv/bin/activate
pip install -e .
```

### 3. Point your MCP client at it

This repo ships a ready-to-use [`.vscode/mcp.json`](.vscode/mcp.json) for VS
Code. For other clients, configure an MCP **stdio** server whose command is the
`unity-mcp` console script inside `server/.venv`.

### 4. Try it

Ask your assistant: *"Use ping to check Unity, then create a red cube."* The
`ping` tool reports connectivity even when Unity is closed, so it's the first
thing to call when debugging setup.

## Tool groups

| Group        | Examples                                                        |
| ------------ | --------------------------------------------------------------- |
| `scene_*`    | open, save, new, active scene, hierarchy                        |
| `gameobject_*` | create, find, components, transform, properties, parent, delete |
| `asset_* / prefab_*` | find, info, folders, delete, instantiate, create, apply  |
| `script_*`   | list, read, create, update, delete (sandboxed to `Assets/`)     |
| `console_*`  | get logs (by level), clear                                       |
| `editor_*`   | state, enter/exit play, pause, step                              |
| `menu_*`     | execute allowlisted menu items, list allowlist                   |

## Development

```bash
cd server
pip install -e '.[dev]'
pytest
```

See [`server/README.md`](server/README.md) and
[`unity-bridge/com.wethinks.mcp-bridge/README.md`](unity-bridge/com.wethinks.mcp-bridge/README.md)
for details.

## Security notes

- The bridge listens only on `127.0.0.1` (loopback).
- Script and asset writes are sandboxed to the project's `Assets/` folder; path
  traversal (`..`) is rejected.
- Menu execution is restricted to an allowlist of non-destructive items.
- Arbitrary C# execution is intentionally **not** included in v1.

## License

Released under the [MIT License](LICENSE) — free and open source for any use.
