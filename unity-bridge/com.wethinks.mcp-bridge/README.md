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
https://github.com/jamesjeffries/unity_custom_mcp.git?path=/unity-bridge/com.wethinks.mcp-bridge
```

## Use

Open **Window > MCP Bridge**. The bridge auto-starts on Editor load and listens
on `127.0.0.1:6400`. The window lets you:

- See the live status and port.
- Change the port (set `UNITY_MCP_PORT` on the Python server to match).
- Toggle auto-start.
- Start / Stop / Restart manually.

> The bridge is only half of the system. It does nothing on its own — it waits
> for the **`unity-mcp` Python server** to connect, which in turn is driven by an
> MCP client (VS Code Copilot, Claude Desktop, Cursor). Complete the steps below
> to actually run it end to end.

## Run it end to end

1. **Start the bridge** (this package). Open the project in Unity 6; the bridge
   auto-starts. Confirm **Window > MCP Bridge** shows
   `Listening on 127.0.0.1:6400`.

2. **Start the Python MCP server.** From the
   [`server/`](https://github.com/jamesjeffries/unity_custom_mcp/tree/main/server)
   folder of the repo:

   ```bash
   cd server
   python3 -m venv .venv          # needs Python 3.10+
   . .venv/bin/activate
   pip install -e .
   ```

3. **Point your MCP client at the server.** Configure an MCP **stdio** server
   whose command is the `unity-mcp` console script inside `server/.venv`. The
   repo ships a ready-to-use
   [`.vscode/mcp.json`](https://github.com/jamesjeffries/unity_custom_mcp/blob/main/.vscode/mcp.json)
   for VS Code.

4. **Try it.** Ask your assistant: *"Use ping to check Unity, then create a red
   cube."* The `ping` tool reports connectivity even when Unity is closed, so
   it's the first thing to call when debugging setup.

If you change the port in the MCP Bridge window, set `UNITY_MCP_PORT` to the same
value for the Python server.

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
