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

(Or add from a Git URL:
`https://github.com/jamesjeffries/unity_custom_mcp.git?path=/unity-bridge/com.wethinks.mcp-bridge`.)

Then open **Window > MCP Bridge** and confirm it shows
`Listening on 127.0.0.1:6400` (it auto-starts on load).

### 2. Install and run the Python server

```bash
cd server
python3 -m venv .venv          # needs Python 3.10+
. .venv/bin/activate
pip install -e .
```

### 3. Connect it to VS Code Copilot

> New to MCP? An **MCP server** is a small program that exposes "tools" an AI
> assistant can call. VS Code's Copilot Chat can launch these servers and let
> the model use their tools on your behalf. Below is the full setup from
> scratch.

**Prerequisites**

- VS Code **1.99 or newer** with the **GitHub Copilot** and **GitHub Copilot
  Chat** extensions installed and signed in.
- You completed steps 1–2 above (the Unity bridge is running and the Python
  server is installed into `server/.venv`).

**a. Register the server.** This repo already ships a workspace config at
[`.vscode/mcp.json`](.vscode/mcp.json):

```jsonc
{
  "servers": {
    "unity-mcp": {
      "type": "stdio",
      "command": "${workspaceFolder}/server/.venv/bin/unity-mcp",
      "env": { "UNITY_MCP_HOST": "127.0.0.1", "UNITY_MCP_PORT": "6400" }
    }
  }
}
```

Open this repo's folder in VS Code and the config is picked up automatically. On
Windows, change the command to
`${workspaceFolder}/server/.venv/Scripts/unity-mcp.exe`. To make Unity available
in *every* workspace instead, run **MCP: Open User Configuration** from the
Command Palette and add the same `unity-mcp` entry there (use an absolute path
to the `unity-mcp` script).

**b. Start the server.** Open [`.vscode/mcp.json`](.vscode/mcp.json) and click
the **Start** CodeLens that appears above the `"unity-mcp"` entry — or run
**MCP: List Servers** from the Command Palette (`Cmd/Ctrl+Shift+P`), pick
`unity-mcp`, and choose **Start Server**. A healthy server shows a green
"Running" status.

**c. Open Agent mode.** Open Copilot Chat (`Ctrl+Cmd+I` / `Ctrl+Alt+I`) and
switch the chat mode dropdown from *Ask* to **Agent**. Click the **tools**
(🛠️) icon in the chat box and confirm the `unity-mcp` tools are listed and
enabled.

**d. Verify the connection.** In the chat box, type:

> Use the unity-mcp `ping` tool to check Unity.

You should get back `connected: true` with your Unity version. `ping` reports
connectivity even when Unity is closed, so it's always the first thing to try
when debugging setup. (For Claude Desktop, Cursor, or any other client, register
an MCP **stdio** server whose command is the `unity-mcp` script inside
`server/.venv` — the same command shown above.)

### 4. Try it

Ask the agent in plain language — it will pick the right tools:

> Create a cube named "Player" at the origin, make it blue, then add a
> Rigidbody to it.

## What you can ask for

You don't call tools by name — just describe what you want and the agent maps it
to the tools below. Each example is a natural-language prompt you can paste into
Agent mode.

The toolset has two layers:

- **Low-level tools** — one focused Unity operation each (create a GameObject,
  set a property, read a script). Compose them for fine-grained control.
- **High-level generators** — single calls that compose many operations into a
  finished result (a playable first-person player, a procedural terrain, a field
  of scattered objects). See [High-level generators](#high-level-generators).

## Low-level tools

### Scenes

- *"What scene is currently open?"* — `scene_get_active`
- *"Show me the full scene hierarchy with the components on each object."* —
  `scene_get_hierarchy`
- *"Create a new empty scene and save it to Assets/Scenes/Sandbox.unity."* —
  `scene_new`, `scene_save`
- *"Open Assets/Scenes/Main.unity."* — `scene_open`
- *"Save the current scene."* — `scene_save`

### GameObjects

- *"Create a sphere named Ball two units up."* — `gameobject_create`
- *"Find every object whose name contains 'Enemy'."* — `gameobject_find`
- *"List the components and properties on the Main Camera."* —
  `gameobject_get_components`
- *"Add a Rigidbody to Ball, then set its mass property to 5."* —
  `gameobject_add_component`, `gameobject_set_property`
- *"Move Ball to (3, 1, 0), rotate it 45° around Y, and scale it to 1.5."* —
  `gameobject_set_transform`
- *"Make the Player cube red."* — `gameobject_set_color`
- *"Parent Ball under the Player object."* — `gameobject_set_parent`
- *"Delete the object named Temp."* — `gameobject_delete`

### Assets & prefabs

- *"Find all materials in the project."* — `asset_find` (`t:Material`)
- *"What's the GUID and type of Assets/Art/Hero.png?"* — `asset_get_info`
- *"Create a folder Assets/Prefabs."* — `asset_create_folder`
- *"Turn the Player object into a prefab at Assets/Prefabs/Player.prefab."* —
  `prefab_create_from_gameobject`
- *"Spawn three copies of Assets/Prefabs/Player.prefab in the scene."* —
  `prefab_instantiate`
- *"Apply my changes to this prefab instance back to the prefab asset."* —
  `prefab_apply`
- *"Delete Assets/Prefabs/Old.prefab."* — `asset_delete`

### C# scripts (sandboxed to `Assets/`)

- *"List the C# scripts under Assets/Scripts."* — `script_list`
- *"Show me the contents of Assets/Scripts/PlayerController.cs."* —
  `script_read`
- *"Create a new MonoBehaviour at Assets/Scripts/Spinner.cs that rotates the
  object every frame."* — `script_create`
- *"Update Assets/Scripts/Spinner.cs to spin twice as fast."* — `script_update`
- *"Delete Assets/Scripts/Unused.cs."* — `script_delete`

> ⚠️ Creating or updating a script makes Unity **recompile and reload**, which
> briefly drops the bridge (see *Things to know* below).

### Console

- *"Show the last 20 console messages."* — `console_get_logs`
- *"Are there any errors in the console right now?"* — `console_get_logs`
  (`level: error`)
- *"Clear the console."* — `console_clear`

### Play mode & editor

- *"What's the editor state — is it playing or compiling?"* — `editor_get_state`
- *"Enter play mode."* / *"Stop play mode."* — `editor_enter_play`,
  `editor_exit_play`
- *"Pause the game, then step forward one frame."* — `editor_pause`,
  `editor_step`

### Menu items

- *"Which Unity menu items can you run?"* — `menu_list_allowed`
- *"Refresh the asset database."* — `menu_execute` (`Assets/Refresh`)

## High-level generators

These compose many low-level operations into a single call, so one prompt
produces a finished result instead of dozens of round-trips.

- *"Add a first-person player I can walk around with."* —
  `generate_first_person_player`. Creates a capsule with a `CharacterController`,
  an eye-height camera, and a precompiled controller: **WASD** move, **mouse**
  look, **Space** jump, **Left Ctrl / C** crawl. Enter play mode and move around.
  Because the controller ships precompiled in the package's runtime assembly, it
  attaches instantly with no script recompile.
- *"Generate a 500×500 terrain with a mountain range and meadows, seed 42."* —
  `generate_terrain`. Builds a Unity `Terrain` from layered Perlin/ridged noise
  (rolling hills, a masked mountain band, flattened meadows) and saves the
  `TerrainData` under `Assets/MCP/Terrain`. Reuse the seed to reproduce a layout.
- *"Scatter 200 trees from Assets/Trees/Pine.prefab across the terrain."* —
  `generate_scatter`. Places many instances of a prefab (or a primitive) over an
  area in one call, optionally raycasting each onto the ground, with random yaw
  and scale. Reproducible via `seed`.

> ℹ️ The first-person controller uses Unity's **Input System** package (the new
> input handler) by default — the package is listed as a dependency of the MCP
> Bridge, so Unity installs it automatically. Set **Project Settings ▸ Player ▸
> Active Input Handling** to *Input System Package (New)* or *Both*. If a project
> is locked to *Input Manager (Old)*, the controller automatically falls back to
> the legacy input API, so it still works either way.

## AI asset generation (optional)

The server can call **your own** external AI services to generate textures and
audio and import them straight into the project. These tools are **off by
default** — until you set the relevant environment variables, each tool returns a
friendly `"configured": false` notice instead of failing, and everything else
keeps working.

The easiest way to configure them is a `.env` file. Copy the template and fill in
your keys (the real `.env` is gitignored; `.env.example` is safe to commit):

```bash
cd server
cp .env.example .env   # then edit .env with your endpoints/keys
```

The server loads `.env` automatically on startup (from the current directory or
the `server/` folder). You can also export the variables directly instead of
using a file.

### Textures (image model)

Point the server at an Azure OpenAI image deployment (e.g. `gpt-image-1` /
`dall-e-3`) or any OpenAI-compatible images endpoint:

| Variable                      | Purpose                                              |
| ----------------------------- | ---------------------------------------------------- |
| `UNITY_MCP_IMAGE_ENDPOINT`    | Azure resource endpoint, or a full images URL        |
| `UNITY_MCP_IMAGE_API_KEY`     | API key                                              |
| `UNITY_MCP_IMAGE_MODEL`       | Model/deployment name sent in the request body       |

- *"Generate a seamless mossy stone texture and put it on the Ground object."* —
  `texture_generate`. Saves a PNG under `Assets/MCP/Textures`; with a `target` it
  also builds a material (URP `_BaseMap` / built-in `_MainTex`) and assigns it.

### Audio (ElevenLabs)

Provide an ElevenLabs key to generate speech, sound effects, and music:

| Variable                        | Purpose                                            |
| ------------------------------- | -------------------------------------------------- |
| `UNITY_MCP_ELEVENLABS_API_KEY`  | ElevenLabs API key                                 |
| `UNITY_MCP_ELEVENLABS_VOICE_ID` | Default voice for speech (optional)                |
| `UNITY_MCP_ELEVENLABS_MODEL`    | TTS model (default `eleven_multilingual_v2`)       |
| `UNITY_MCP_ELEVENLABS_BASE_URL` | API base URL (default `https://api.elevenlabs.io`) |

- *"Have the NPC say 'Welcome, traveler.'"* — `audio_generate_speech`
- *"Make a footsteps-on-gravel sound effect."* — `audio_generate_sound_effect`
- *"Generate a calm ambient exploration loop."* — `audio_generate_music`

Generated MP3s land under `Assets/MCP/Audio` and import as `AudioClip`s. (The
music endpoint may require beta access on your account; any provider error is
returned in the result.)

Optional knobs: `UNITY_MCP_TEXTURE_FOLDER`, `UNITY_MCP_AUDIO_FOLDER`, and
`UNITY_MCP_HTTP_TIMEOUT`.

## Things to know

- **Recompiles and play mode drop the bridge briefly.** `script_create`,
  `script_update`, and entering play mode trigger a Unity **domain reload** that
  restarts the bridge a few seconds later. The Python server automatically waits
  and reconnects (up to `UNITY_MCP_RECONNECT_TIMEOUT`, default 60s), so a
  sequence like "create a script, then read it back" just works. **Unity only
  recompiles while the Editor window has focus** — if a command seems to hang
  after a script change, click into the Unity Editor (or enable
  **Preferences ▸ Asset Pipeline ▸ Auto Refresh**) to let it finish.
- **`set_property` uses serialized names.** `gameobject_set_property` takes
  Unity's internal serialized field name — e.g. `m_Mass` for a Rigidbody's mass,
  not `mass`. Use `gameobject_get_components` to discover the exact names.
- **Colors go through `set_color`, not `set_property`.** `gameobject_set_color`
  creates a material under `Assets/MCP/Materials` and assigns it, which is why
  it works across the Built-in, URP, and HDRP render pipelines.

## Tool groups

| Group        | Examples                                                        |
| ------------ | --------------------------------------------------------------- |
| `scene_*`    | open, save, new, active scene, hierarchy                        |
| `gameobject_*` | create, find, components, add component, transform, color, properties, parent, delete |
| `asset_* / prefab_*` | find, info, folders, delete, instantiate, create, apply  |
| `script_*`   | list, read, create, update, delete (sandboxed to `Assets/`)     |
| `console_*`  | get logs (by level), clear                                       |
| `editor_*`   | state, enter/exit play, pause, step                              |
| `menu_*`     | execute allowlisted menu items, list allowlist                   |
| `generate_*` | first-person player, procedural terrain, prefab/primitive scatter |
| `texture_* / audio_*` | optional AI texture & audio generation (see above)      |

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
