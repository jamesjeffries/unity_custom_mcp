# MCP Bridge (com.wethinks.mcp-bridge)

Editor-only Unity package that exposes the Unity Editor to an external MCP
server over a loopback TCP socket. Part of the
[Unity Custom MCP](../../README.md) project.

## Requirements

- Unity 6 (6000.0) or newer.

## Install

**From disk:** Window > Package Manager > + > Add package from disk… and pick
this folder's `package.json`.

**From Git:** Window > Package Manager > + > Add package from git URL…

```
https://…/unity_custom_mcp.git?path=/unity-bridge/com.wethinks.mcp-bridge
```

## Use

Open **Window > MCP Bridge**. The bridge auto-starts on Editor load and listens
on `127.0.0.1:6400`. The window lets you:

- See the live status and port.
- Change the port (set `UNITY_MCP_PORT` on the Python server to match).
- Toggle auto-start.
- Start / Stop / Restart manually.

## How it works

- `McpServer` (`[InitializeOnLoad]`) runs a `TcpListener` on a background thread,
  reads length-prefixed JSON frames, and writes framed JSON responses.
- `MainThreadDispatcher` marshals each command onto Unity's main thread via
  `EditorApplication.update`, because the Unity API is main-thread-only.
- `CommandRegistry` maps command names (e.g. `gameobject.create`) to handlers in
  `Editor/Handlers/`.
- On `AssemblyReloadEvents.beforeAssemblyReload` the listener stops cleanly and
  restarts after the reload, so script recompiles don't leak the socket.

## Wire protocol

```
[4-byte big-endian length][UTF-8 JSON body]
```

Request: `{"id": "...", "command": "...", "params": {...}}`
Response: `{"id": "...", "success": true, "data": {...}}` or
`{"id": "...", "success": false, "error": "..."}`

## Safety

- Loopback-only listener.
- `AssetPathGuard` confines file writes to `Assets/` and rejects `..`.
- `MenuHandler` only runs allowlisted menu items.
