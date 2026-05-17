#!/usr/bin/env node
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const sourceEntry = resolve(rootDir, "cli/main.ts");
const builtEntry = resolve(rootDir, "dist/npm/main.js");

const args = existsSync(sourceEntry)
  ? ["--experimental-strip-types", sourceEntry, ...process.argv.slice(2)]
  : existsSync(builtEntry)
    ? [builtEntry, ...process.argv.slice(2)]
    : null;

if (!args) {
  console.error(`Could not locate sts CLI entrypoint under ${rootDir}`);
  process.exit(1);
}

const result = spawnSync(process.execPath, args, { stdio: "inherit" });

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}

process.exit(result.status ?? 1);
