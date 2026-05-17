# STS2 Taku Agent Install Guide

This guide is for the mod zip packages in `dist/release/mod/`.

Each package contains:

- `taku_agent/mod_manifest.json`
- `taku_agent/taku_agent.pck`
- `taku_agent/taku_agent.dll`

Keep all three files in the same `taku_agent` mod directory.

## Windows

1. Extract `taku_agent-v0.1.0-windows.zip`.
2. Open your Slay the Spire 2 install folder.
3. Create a `mods` directory if it does not already exist.
4. Copy the extracted `taku_agent` folder into `mods`.
5. Launch the game and enable mod loading when prompted.

Typical Steam path:

```text
%ProgramFiles(x86)%\Steam\steamapps\common\Slay the Spire 2\mods\taku_agent\
```

## macOS

1. Extract `taku_agent-v0.1.0-macos.zip`.
2. Open the Slay the Spire 2 app bundle's `Contents/MacOS` directory.
3. Create a `mods` directory if it does not already exist.
4. Copy the extracted `taku_agent` folder into `mods`.
5. Launch the game and enable mod loading when prompted.

Typical Steam path:

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/taku_agent/
```

## Verify

After installing the CLI and starting the game, run:

```bash
sts doctor
```

If the command cannot reach the observer server, confirm the game is running with the mod enabled.
