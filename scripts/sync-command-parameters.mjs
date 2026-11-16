#!/usr/bin/env node
// Входные параметры команд для манифестов: поле "parameters" в command.json и server/command.json.
//
// Источник — схемы инструментов собранного MCP-сервера (server/build): скрипт запускает сервер,
// спрашивает список инструментов (tools/list) и для каждой команды записывает её параметры
// по порядку схемы: { "name": "template", "required": true }. Окно настроек плагина показывает их
// в столбце Parameters, а list_command_sets — в каталоге.
//
// Запуск после изменения аргументов инструмента (сначала npm --prefix server run build):
//   node scripts/sync-command-parameters.mjs           — обновить манифесты
//   node scripts/sync-command-parameters.mjs --check   — только проверить, код возврата 1 при расхождении

import { spawn } from "child_process";
import { existsSync, readFileSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const checkOnly = process.argv.includes("--check");
const entry = join(root, "server", "build", "index.js");
if (!existsSync(entry)) {
  console.error(`Нет собранного сервера: ${entry}. Соберите: npm --prefix server run build`);
  process.exit(1);
}

/** Схемы всех инструментов сервера: имя → { properties, required }. */
async function loadToolSchemas() {
  const server = spawn(process.execPath, [entry], { stdio: ["pipe", "pipe", "ignore"], cwd: root });
  let buffer = "";
  const waiting = new Map();
  server.stdout.on("data", (chunk) => {
    buffer += chunk;
    let newline;
    while ((newline = buffer.indexOf("\n")) >= 0) {
      const line = buffer.slice(0, newline);
      buffer = buffer.slice(newline + 1);
      try {
        const message = JSON.parse(line);
        waiting.get(message.id)?.(message);
      } catch {
        // не сообщение протокола
      }
    }
  });
  let id = 0;
  const call = (method, params) =>
    new Promise((resolveCall, rejectCall) => {
      const current = ++id;
      const timer = setTimeout(() => rejectCall(new Error(`Сервер не ответил на ${method}`)), 30000);
      waiting.set(current, (message) => {
        clearTimeout(timer);
        resolveCall(message);
      });
      server.stdin.write(JSON.stringify({ jsonrpc: "2.0", id: current, method, params }) + "\n");
    });
  try {
    await call("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "sync-command-parameters", version: "1" } });
    const listed = await call("tools/list", {});
    return new Map(listed.result.tools.map((tool) => [tool.name, tool.inputSchema ?? {}]));
  } finally {
    server.kill();
  }
}

function parametersOf(schema) {
  const required = new Set(schema.required ?? []);
  return Object.keys(schema.properties ?? {}).map((name) => (required.has(name) ? { name, required: true } : { name }));
}

const schemas = await loadToolSchemas();
let changed = 0;
const missingTools = [];
for (const rel of ["command.json", "server/command.json"]) {
  const path = join(root, rel);
  const text = readFileSync(path, "utf8").replace(/^﻿/, "");
  const manifest = JSON.parse(text);
  for (const command of manifest.commands ?? []) {
    const tool = command.tool ?? command.commandName;
    const schema = schemas.get(tool);
    if (!schema) {
      missingTools.push(`${rel}: ${command.commandName}`);
      continue;
    }
    const parameters = parametersOf(schema);
    if (JSON.stringify(parameters) !== JSON.stringify(command.parameters ?? null)) {
      changed++;
      if (checkOnly) console.log(`Расходится: ${rel}: ${command.commandName}`);
      command.parameters = parameters;
    }
  }
  if (!checkOnly) writeFileSync(path, JSON.stringify(manifest, null, 2) + "\n");
}

if (missingTools.length) console.log(`Нет инструмента на сервере (параметры не записаны): ${missingTools.join(", ")}`);
if (checkOnly) {
  console.log(changed ? `Параметры в манифестах устарели: ${changed}. Запустите node scripts/sync-command-parameters.mjs` : "Параметры в манифестах совпадают с инструментами сервера.");
  process.exit(changed ? 1 : 0);
}
console.log(`Параметры записаны, изменено команд: ${changed}.`);
