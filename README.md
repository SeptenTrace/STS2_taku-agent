# STS2 Taku Agent

[![CLI](https://img.shields.io/badge/CLI-Node.js%2024+-339933)](https://nodejs.org/)
[![Mod](https://img.shields.io/badge/Mod-Slay%20the%20Spire%202-red)](#install)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

STS2 Taku Agent is a Slay the Spire 2 mod and command-line tool that exposes the current game state through a local, agent-friendly interface. It is built for experiments where an AI agent reads the run, chooses legal actions, and executes them through a small `sts` CLI.

This project is unofficial and is not affiliated with Mega Crit or Slay the Spire.

## What It Includes

- A Slay the Spire 2 mod that runs a localhost observer/control server while the game is open.
- A cross-platform `sts` CLI for reading game state and executing actions.
- Release packages for macOS and Windows.
- Repo-local Codex skills for agent workflows that use the CLI.

## Install

Download the release artifacts from `dist/release/` or from a GitHub Release built from this repository.

### 1. Install The Mod

Choose the mod package for your platform:

- macOS: `dist/release/mod/taku_agent-v0.1.0-macos.zip`
- Windows: `dist/release/mod/taku_agent-v0.1.0-windows.zip`

Extract the zip and copy the `taku_agent/` folder into your Slay the Spire 2 `mods` directory.

Common locations:

- macOS: `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/`
- Windows: `%ProgramFiles(x86)%\Steam\steamapps\common\Slay the Spire 2\mods\`

The final layout should look like:

```text
mods/
  taku_agent/
    mod_manifest.json
    taku_agent.dll
    taku_agent.pck
```

### 2. Install The CLI

The CLI requires Node.js 24 or newer.

macOS:

```bash
unzip sts-cli-v0.1.0-macos.zip
./install.sh
```

Windows PowerShell:

```powershell
Expand-Archive sts-cli-v0.1.0-windows.zip
.\sts-cli-v0.1.0-windows\install.ps1
```

You can also install directly from the npm tarball:

```bash
npm install -g ./retr0-sts-cli-0.1.0.tgz
```

### 3. Verify

Start Slay the Spire 2 with the mod enabled, then run:

```bash
sts doctor
```

If the game is running and the mod is loaded, the CLI should detect the local observer server.

## Common Commands

```bash
sts help
sts context
sts actions
sts run snapshot
sts room snapshot --detail full
sts exec end_turn
sts doctor
```

By default, the CLI connects to `http://127.0.0.1:15527`. Override it with:

```bash
STS_OBSERVER_URL=http://127.0.0.1:15527 sts doctor
```

## Build From Source

Requirements:

- Node.js 24+
- .NET SDK 9+
- Slay the Spire 2 installed locally
- `zip` and `shasum` available on the release machine

Build and test the CLI:

```bash
npm install
npm run verify:cli
```

Build all release packages:

```bash
scripts/build-release.sh
```

If your game is not in the default Steam location, provide the paths explicitly:

```bash
STS2_GAME_DIR="/path/to/slay-the-spire-2-data" \
STS2_ENGINE_BIN="/path/to/slay-the-spire-2-executable" \
scripts/build-release.sh
```

Release output:

```text
dist/release/
  cli/
    retr0-sts-cli-0.1.0.tgz
    sts-cli-v0.1.0-macos.zip
    sts-cli-v0.1.0-windows.zip
  mod/
    taku_agent-v0.1.0-macos.zip
    taku_agent-v0.1.0-windows.zip
```

Each artifact also has a `.sha256` checksum file.

## Repository Layout

```text
cli/                 TypeScript source for the sts command
mod/src/             C# mod source
mod/pack/            Files packed into the mod PCK
scripts/build-release.sh
scripts/dev/         Local development helpers
scripts/release/     Release packaging helpers
docs/                Design notes and older phase plans
.codex/skills/       Optional Codex skills for agent workflows
```

## Development

Run the CLI from the repo without installing it globally:

```bash
./sts help
./sts doctor
```

Build, deploy, and restart the local macOS Steam game during development:

```bash
scripts/dev/build-and-deploy.sh
scripts/dev/restart-game.sh --wait-for-server
scripts/dev/dev-cycle.sh --smoke
```

The development scripts use environment variables for local paths and do not require committing machine-specific configuration.

## Local Data

The mod and CLI write local diagnostic logs under the user profile. These logs are for local debugging only and are not sent anywhere by this project. Set `STS_CLI_DISABLE_TELEMETRY=1` to disable CLI command telemetry logs.

## License

MIT. See [LICENSE](LICENSE).
