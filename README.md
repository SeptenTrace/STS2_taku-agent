# STS2 Taku Agent

Starter repository for a Slay the Spire 2 mod.

## Repository Layout
- `src/`: C# mod source
- `pack/`: files packaged into the `.pck`
- `tools/`: helper scripts for packaging
- `build_and_deploy.sh`: build locally and copy outputs into the game `mods` folder
- `build_release.sh`: build a shareable release package

## Build For Local macOS Game
```bash
./build_and_deploy.sh
```

## Build Release Package
```bash
./build_release.sh
```

The project is scaffolded from the working structure used in `deadly_nuke_mod/`, but starts with a minimal empty mod initializer so you can add cards, patches, relics, or events incrementally.
