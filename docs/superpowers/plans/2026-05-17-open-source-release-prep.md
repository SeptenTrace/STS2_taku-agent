# STS2 Taku Agent Open Source Release Prep Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare STS2 Taku Agent for public release with organized directories, user-facing docs, and Windows/macOS CLI and mod artifacts.

**Architecture:** Keep the repo root small and classify the project into `cli/`, `mod/`, `scripts/`, and `docs/`. Build release artifacts from source with one script that produces npm CLI packages plus platform-labeled mod zips.

**Tech Stack:** TypeScript on Node.js 24, .NET 9 C# mod assembly, Godot headless PCK packaging, shell release automation.

---

### Task 1: Establish A Clean Baseline

**Files:**
- Read: `package.json`
- Read: `README.md`
- Read: `scripts/**`
- Read: `mod/**`

- [x] **Step 1: Install dependencies**

Run: `npm install`

Expected: dependencies install and npm reports no vulnerabilities.

- [x] **Step 2: Run baseline CLI verification**

Run: `npm run verify:cli`

Expected: type-check succeeds and the CLI unit suite passes.

- [x] **Step 3: Scan for personal paths and secrets**

Run: `rg` for absolute home paths and credential-like strings, excluding generated dependency and build output directories.

Expected: no private credentials, personal directories, or tokens are present.

### Task 2: Reorganize Repository Layout

**Files:**
- Move: `tools/sts-cli/**` -> `cli/**`
- Move: `src/**` -> `mod/src/**`
- Move: `pack/**` -> `mod/pack/**`
- Move: `tools/build_pck.gd` -> `scripts/release/build_pck.gd`
- Move: root development scripts -> `scripts/dev/**`

- [x] **Step 1: Move source directories**

Run: `git mv` commands for CLI, mod source, mod pack files, and scripts.

Expected: `git status --short` shows renames into the new directory groups.

- [x] **Step 2: Update config and scripts**

Update `package.json`, `tsconfig*.json`, `bin/sts.js`, `sts`, and `scripts/**` to use the new paths.

Expected: `npm run verify:cli` still passes and `node bin/sts.js help` works.

### Task 3: Build Release Artifacts

**Files:**
- Modify: `scripts/build-release.sh`
- Create: `dist/release/cli/*`
- Create: `dist/release/mod/*`

- [x] **Step 1: Build CLI outputs**

Run: `npm run build:cli` and `npm pack`.

Expected: npm tarball is generated under `dist/release/cli/`.

- [x] **Step 2: Package CLI for macOS and Windows**

Run: `scripts/build-release.sh`.

Expected: `sts-cli-v0.1.0-macos.zip`, `sts-cli-v0.1.0-windows.zip`, and checksum files exist.

- [x] **Step 3: Package mod for macOS and Windows**

Run: `scripts/build-release.sh`.

Expected: `taku_agent-v0.1.0-macos.zip`, `taku_agent-v0.1.0-windows.zip`, and checksum files exist.

### Task 4: Publish-Friendly Docs

**Files:**
- Modify: `README.md`
- Modify: `INSTALL.md`
- Add: `LICENSE`

- [x] **Step 1: Rewrite README as a public landing page**

Include project name, badges, short feature summary, quick install, common CLI commands, release artifact list, repository layout, and contribution notes.

- [x] **Step 2: Keep technical depth in docs**

README links to detailed phase docs instead of embedding internal implementation history.

- [x] **Step 3: Re-scan for private data**

Run the personal path and secret scan again.

Expected: no personal directories or secret-like values remain.

### Task 5: Final Verification

**Files:**
- Verify: whole repository

- [x] **Step 1: Run CLI verification**

Run: `npm run verify:cli`

Expected: all tests pass.

- [x] **Step 2: Run release packaging**

Run: `scripts/build-release.sh`

Expected: all CLI and mod release zips are generated with SHA-256 files.

- [x] **Step 3: Check package contents**

Run: `find dist/release -maxdepth 2 -type f | sort`

Expected: release files are organized under `cli/` and `mod/`.

- [x] **Step 4: Review git diff**

Run: `git diff --stat` and `git diff --check`

Expected: no whitespace errors, no unrelated files staged, and changes match the goal.
