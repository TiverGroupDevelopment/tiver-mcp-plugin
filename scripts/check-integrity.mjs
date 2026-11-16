#!/usr/bin/env node
// Сверка слоёв проекта. Запуск: node scripts/check-integrity.mjs [--json]
//
// Команда живёт в трёх местах, и имена должны совпадать:
//   1) запись в манифесте набора (command.json),
//   2) C#-класс с CommandName,
//   3) инструмент сервера (server/src/tools/*.ts), который эту команду вызывает.
// Скрипт проверяет это, уникальность имён между наборами, метки section / status / project
// и профили правил (наследование, файлы, ключи проверок). Код возврата 1 — есть ошибки.

import { existsSync, readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const asJson = process.argv.includes("--json");
const errors = [];
const warnings = [];
const error = (text) => errors.push(text);
const warn = (text) => warnings.push(text);

const read = (path) => readFileSync(path, "utf8").replace(/^﻿/, "");
const readJson = (path) => JSON.parse(read(path));
const rel = (path) => relative(root, path).replace(/\\/g, "/");

function walk(dir, filter, found = []) {
  if (!existsSync(dir)) return found;
  for (const name of readdirSync(dir)) {
    if (name === "bin" || name === "obj" || name === "node_modules") continue;
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, filter, found);
    else if (filter(name)) found.push(path);
  }
  return found;
}

// ---------- разделы ----------
const sections = readJson(join(root, "sections.json"));
const sectionKeys = Object.keys(sections);

// ---------- манифесты ----------
// Код всех C#-команд — в одной сборке, манифест — command.json в корне. Инструменты, реализованные
// в самом сервере, — в server/command.json. К каким наборам относится команда, говорит её поле sets;
// сами наборы (имя, описание, профиль проекта) описаны в commandSets корневого манифеста.
// csharp: папки с C#-командами; null — инструменты сервера.
const manifests = [
  { path: "command.json", csharp: ["commandset/Commands", "commandset/Family/Commands"] },
  { path: "server/command.json", csharp: null },
];

const commands = []; // { command, tool, manifest, serverOnly, section, status, sets, uses }
const commandSets = new Map(); // имя набора → { name, description, project }
for (const manifest of manifests) {
  const path = join(root, manifest.path);
  if (!existsSync(path)) {
    error(`Нет манифеста ${manifest.path}`);
    continue;
  }
  const data = readJson(path);
  for (const set of data.commandSets ?? []) {
    if (!set?.name) error(`${manifest.path}: в commandSets набор без имени`);
    else if (commandSets.has(set.name)) error(`${manifest.path}: набор '${set.name}' описан дважды`);
    else commandSets.set(set.name, set);
  }
  for (const item of data.commands ?? []) {
    if ("project" in item || "includes" in data) {
      error(`${manifest.path}: '${item.commandName}' — поля project/includes больше не используются, наборы задаёт поле sets`);
    }
    commands.push({
      command: item.commandName,
      tool: item.tool ?? item.commandName,
      manifest: manifest.path,
      serverOnly: manifest.csharp === null,
      section: item.section,
      status: item.status,
      sets: item.sets,
      parameters: item.parameters,
      uses: item.uses ?? [],
    });
  }
}

// Уникальность имён команд и инструментов во всех наборах: реестр плагина плоский.
for (const key of ["command", "tool"]) {
  const seen = new Map();
  for (const item of commands) {
    const previous = seen.get(item[key]);
    if (previous) {
      error(`Дубль имени ${key === "command" ? "команды" : "инструмента"} '${item[key]}': ${previous.manifest} и ${item.manifest}`);
    } else {
      seen.set(item[key], item);
    }
  }
}

if (!commandSets.has("Global")) error("command.json: в commandSets нет набора Global");

// ---------- профили правил (нужны и для наборов проектов) ----------
const rulesDir = join(root, "rules");
const profiles = new Map(); // id → { dir, folder, profile }
for (const folder of existsSync(rulesDir) ? readdirSync(rulesDir) : []) {
  const dir = join(rulesDir, folder);
  if (folder.startsWith("_") || folder.startsWith(".") || !statSync(dir).isDirectory()) continue;
  const path = join(dir, "profile.json");
  if (!existsSync(path)) continue;
  try {
    const profile = readJson(path);
    if (!profile.id) error(`${rel(path)}: нет id`);
    else if (profiles.has(profile.id)) error(`Дубль id профиля правил '${profile.id}': ${rel(profiles.get(profile.id).dir)} и ${rel(dir)}`);
    else profiles.set(profile.id, { dir, folder, profile });
  } catch (problem) {
    error(`${rel(path)}: не читается как JSON — ${problem.message}`);
  }
}
const findProfile = (key) => {
  const wanted = String(key).toLowerCase();
  return [...profiles.values()].find(
    (p) => p.profile.id.toLowerCase() === wanted || p.folder.toLowerCase() === wanted || String(p.profile.name ?? "").toLowerCase() === wanted
  );
};
const parentsOf = (profile) => {
  const value = profile.extends ?? profile.base;
  return value ? (Array.isArray(value) ? value : [value]) : [];
};

// ---------- метки команд ----------
for (const item of commands) {
  const where = `${item.manifest}: ${item.command}`;
  if (!item.section) error(`${where} — нет раздела (section)`);
  else if (!sectionKeys.includes(item.section)) error(`${where} — раздела '${item.section}' нет в sections.json`);
  if (!["validated", "experimental"].includes(item.status)) error(`${where} — status должен быть validated или experimental, сейчас '${item.status}'`);
  if (!Array.isArray(item.sets) || item.sets.length === 0) {
    error(`${where} — нет наборов (sets): например ["Global"] или ["Global", "Alterplast"]`);
  } else {
    for (const set of item.sets) {
      if (!commandSets.has(set)) error(`${where} — набора '${set}' нет в commandSets (command.json)`);
    }
    if (new Set(item.sets).size !== item.sets.length) error(`${where} — набор повторяется в sets`);
  }
  if (!Array.isArray(item.parameters)) warn(`${where} — нет parameters: node scripts/sync-command-parameters.mjs`);
}

// Набор проекта связан с профилем правил проекта (поле project в commandSets).
for (const set of commandSets.values()) {
  if (!set.project) continue;
  const owner = findProfile(set.project);
  if (!owner) error(`command.json: набор '${set.name}' — профиль правил '${set.project}' не найден (rules/*/profile.json)`);
  else if ((owner.profile.kind ?? "project") !== "project") error(`command.json: набор '${set.name}' — '${set.project}' не проект, а ${owner.profile.kind}`);
}

// ---------- C#: классы команд ----------
const csharpCommands = new Map(); // имя → файл
for (const manifest of manifests.filter((m) => m.csharp)) {
  for (const file of manifest.csharp.flatMap((dir) => walk(join(root, dir), (name) => name.endsWith(".cs")))) {
    const match = /CommandName\s*=>\s*"([^"]+)"/.exec(read(file)) ?? /CommandName\b[^"\n;]*"([^"]+)"/.exec(read(file));
    if (!match) continue;
    const name = match[1];
    if (csharpCommands.has(name)) error(`Дубль CommandName '${name}' в C#: ${rel(csharpCommands.get(name).file)} и ${rel(file)}`);
    csharpCommands.set(name, { file, manifest: manifest.path });
  }
}

for (const item of commands.filter((c) => !c.serverOnly)) {
  const found = csharpCommands.get(item.command);
  if (!found) error(`${item.manifest}: '${item.command}' есть в манифесте, но C#-класса с таким CommandName нет`);
  else if (found.manifest !== item.manifest) {
    error(`'${item.command}': в манифесте ${item.manifest}, а C#-класс в наборе манифеста ${found.manifest} (${rel(found.file)})`);
  }
}
for (const [name, found] of csharpCommands) {
  if (!commands.some((c) => !c.serverOnly && c.command === name)) {
    error(`C#-команда '${name}' (${rel(found.file)}) не записана в манифест ${found.manifest}: плагин её не загрузит`);
  }
}

// ---------- сервер: инструменты ----------
const tools = new Map(); // имя инструмента → { file, sends }
const toolsDir = join(root, "server/src/tools");
for (const file of walk(toolsDir, (name) => name.endsWith(".ts") && name !== "register.ts")) {
  const text = read(file);
  const names = [
    ...[...text.matchAll(/server\.tool\(\s*"([^"]+)"/g)].map((m) => m[1]),
    ...[...text.matchAll(/registerFamilyTool\(\s*server,\s*"([^"]+)"/g)].map((m) => m[1]),
    ...[...text.matchAll(/register\w*Tool\(\s*server,\s*"([^"]+)"/g)].map((m) => m[1]),
  ];
  const sends = new Set([...text.matchAll(/sendCommand\(\s*"([^"]+)"/g)].map((m) => m[1]));
  // registerFamilyTool отправляет команду с именем инструмента.
  for (const m of text.matchAll(/registerFamilyTool\(\s*server,\s*"([^"]+)"/g)) sends.add(m[1]);
  // Обёртки вида register<Что-то>Tool(server, "имя", …) из utils тоже отправляют команду с этим именем.
  for (const m of text.matchAll(/register\w*Tool\(\s*server,\s*"([^"]+)"/g)) sends.add(m[1]);
  if (names.length === 0) {
    if (text.trim() === "") warn(`${rel(file)} — пустой файл инструмента`);
    else warn(`${rel(file)} — не найдено имя инструмента`);
    continue;
  }
  for (const name of new Set(names)) {
    if (tools.has(name)) error(`Дубль инструмента '${name}': ${rel(tools.get(name).file)} и ${rel(file)}`);
    tools.set(name, { file, sends });
  }
}

const plugin = new Set(commands.filter((c) => !c.serverOnly).map((c) => c.command));
// Встроенные методы плагина: обрабатываются самим сервером сокета, в наборах их нет.
const builtIn = new Set(["mcp_status"]);

for (const [name, tool] of tools) {
  const entry = commands.find((c) => c.tool === name);
  if (!entry) {
    error(`Инструмент '${name}' (${rel(tool.file)}) не записан ни в один манифест: нет раздела, статуса и владельца`);
    continue;
  }
  for (const sent of tool.sends) {
    if (!plugin.has(sent) && !builtIn.has(sent)) {
      error(`Инструмент '${name}' вызывает команду '${sent}', которой нет в манифестах наборов`);
    }
  }
  if (!entry.serverOnly && !tool.sends.has(entry.command)) {
    error(`Инструмент '${name}' записан за командой '${entry.command}', но не вызывает её (${rel(tool.file)})`);
  }
  if (entry.serverOnly) {
    for (const used of entry.uses) {
      if (!plugin.has(used)) error(`server/command.json: '${name}' использует '${used}', которой нет в наборах`);
      else if (!tool.sends.has(used)) warn(`server/command.json: '${name}' заявляет uses '${used}', но не вызывает её`);
    }
  }
}
for (const item of commands) {
  if (!tools.has(item.tool)) {
    (item.serverOnly ? error : warn)(
      `${item.manifest}: у команды '${item.command}' нет инструмента сервера '${item.tool}' — агент её не увидит`
    );
  }
}

// ---------- правила ----------
// Ключи проверок, которые понимает check_family_rules, — из C#.
const checkerPath = join(root, "commandset/Family/Utils/FamilyEditor/FamilyRuleChecker.cs");
const knownChecks = existsSync(checkerPath) ? new Set([...read(checkerPath).matchAll(/\bAdd\("([^"]+)"/g)].map((m) => m[1])) : new Set();
// Разделы проверок и список разделов на сервере.
const rulesModule = read(join(root, "server/src/utils/familyRules.ts"));
const block = (name) => new RegExp(`${name}[^=]*=\\s*\\{([\\s\\S]*?)\\n\\};`).exec(rulesModule)?.[1] ?? "";
const serverSections = Object.fromEntries([...block("SECTIONS").matchAll(/(\w+):\s*"([^"]+)"/g)].map((m) => [m[1], m[2]]));
const checkSections = Object.fromEntries([...block("CHECK_SECTIONS").matchAll(/(\w+):\s*"([^"]+)"/g)].map((m) => [m[1], m[2]]));

if (JSON.stringify(serverSections) !== JSON.stringify(sections)) {
  error("Разделы в server/src/utils/familyRules.ts (SECTIONS) не совпадают с sections.json");
}
for (const key of knownChecks) {
  if (!checkSections[key]) error(`Проверка '${key}' есть в FamilyRuleChecker.cs, но у неё нет раздела в CHECK_SECTIONS (familyRules.ts)`);
  else if (!sectionKeys.includes(checkSections[key])) error(`CHECK_SECTIONS: у '${key}' раздел '${checkSections[key]}', которого нет в sections.json`);
}

function layersOf(item, chain = []) {
  if (chain.includes(item.profile.id)) {
    error(`Циклическое наследование правил: ${[...chain, item.profile.id].join(" → ")}`);
    return [];
  }
  const layers = [];
  for (const parentKey of parentsOf(item.profile)) {
    const parent = findProfile(parentKey);
    if (!parent) {
      error(`${rel(item.dir)}/profile.json: родитель '${parentKey}' не найден`);
      continue;
    }
    for (const layer of layersOf(parent, [...chain, item.profile.id])) {
      if (!layers.includes(layer)) layers.push(layer);
    }
  }
  layers.push(item);
  return layers;
}

const profileSummary = [];
for (const item of profiles.values()) {
  const { profile, dir } = item;
  const where = `${rel(dir)}/profile.json`;
  if (profile.kind && !["global", "pack", "project"].includes(profile.kind)) error(`${where}: kind '${profile.kind}' — нужно global, pack или project`);
  if (profile.status && !["draft", "validated"].includes(profile.status)) error(`${where}: status '${profile.status}' — нужно draft или validated`);
  if (profile.base && !profile.extends) warn(`${where}: поле base устарело, используйте extends`);

  const layers = layersOf(item);
  // Сложенные проверки и ФОП.
  const effective = {};
  let fop = null;
  for (const layer of layers) {
    for (const [key, spec] of Object.entries(layer.profile.checks ?? {})) {
      if (spec === null) delete effective[key];
      else effective[key] = { ...(effective[key] ?? {}), ...spec };
    }
    if (layer.profile.sharedParameterFile) fop = resolve(layer.dir, layer.profile.sharedParameterFile);
  }

  for (const [key, spec] of Object.entries(profile.checks ?? {})) {
    if (!knownChecks.has(key)) error(`${where}: проверки '${key}' нет в check_family_rules (FamilyRuleChecker.cs)`);
    if (spec === null && !layers.slice(0, -1).some((layer) => layer.profile.checks?.[key])) {
      warn(`${where}: '${key}': null отключает проверку, которой нет у родителей`);
    }
  }
  for (const [key, spec] of Object.entries(effective)) {
    if (!spec.rule) warn(`${where}: у проверки '${key}' нет номера правила (rule)`);
  }

  if (profile.rulesFiles) {
    for (const [section, file] of Object.entries(profile.rulesFiles)) {
      if (!sectionKeys.includes(section)) error(`${where}: rulesFiles — раздела '${section}' нет в sections.json`);
      if (!existsSync(resolve(dir, file))) error(`${where}: rulesFiles.${section} — нет файла ${file}`);
    }
  } else if (!existsSync(resolve(dir, profile.rulesFile ?? "ПРАВИЛА.md"))) {
    error(`${where}: нет файла правил ${profile.rulesFile ?? "ПРАВИЛА.md"}`);
  }
  // Эталоны лежат вне git, у каждого разработчика в своей папке: путь — относительно REVIT_MCP_DATA_DIR.
  for (const [kind, file] of Object.entries(profile.examples ?? {})) {
    if (/^[A-Za-z]:[\\/]|^[\\/]{2}/.test(String(file))) {
      error(`${where}: examples.${kind} — абсолютный путь '${file}'. Пишите относительно корня данных (REVIT_MCP_DATA_DIR)`);
    }
  }
  for (const rule of profile.manualRules ?? []) {
    if (rule.section && !sectionKeys.includes(rule.section)) error(`${where}: manualRules ${rule.rule} — раздела '${rule.section}' нет в sections.json`);
  }

  if (profile.sharedParameterFile && !existsSync(resolve(dir, profile.sharedParameterFile))) {
    error(`${where}: нет файла общих параметров ${profile.sharedParameterFile}`);
  }
  const required = effective.requiredParameters?.parameters ?? [];
  if ((profile.kind ?? "project") === "project" && required.length > 0) {
    // У черновика проекта ФОП может ещё не быть — это предупреждение, у проверенного профиля — ошибка.
    if (!fop) {
      (profile.status === "draft" ? warn : error)(
        `${where}: есть обязательные параметры, но ФОП не задан ни в профиле, ни у родителей (sharedParameterFile)`
      );
    }
    else if (existsSync(fop)) {
      const buffer = readFileSync(fop);
      const text = buffer[0] === 0xff && buffer[1] === 0xfe ? buffer.toString("utf16le") : buffer.toString("utf8");
      const names = new Set(text.split(/\r?\n/).map((line) => line.split("\t")).filter((cells) => cells[0] === "PARAM").map((cells) => cells[2]));
      const missing = required.map((p) => p.name).filter((name) => !names.has(name));
      if (missing.length > 0) error(`${where}: обязательных параметров нет в ФОП ${rel(fop)}: ${missing.join(", ")}`);
    }
  }

  profileSummary.push({
    id: profile.id,
    kind: profile.kind ?? "project",
    status: profile.status ?? "validated",
    layers: layers.map((layer) => layer.profile.id),
    ownChecks: Object.keys(profile.checks ?? {}).length,
    effectiveChecks: Object.keys(effective).length,
  });
}

// ---------- язык исходников ----------
// Китайского текста в проекте нет: код, комментарии и сообщения — на английском или русском.
// Иероглифы CJK (U+4E00–U+9FFF), китайская пунктуация (U+3000–U+303F) и полноширинные знаки
// (U+FF00–U+FFEF) — ошибка. Диапазоны собраны из кодов, чтобы в самом скрипте иероглифов не было.
const cjkRanges = [[0x4e00, 0x9fff], [0x3000, 0x303f], [0xff00, 0xffef]];
const cjk = new RegExp(`[${cjkRanges.map(([from, to]) => `${String.fromCharCode(from)}-${String.fromCharCode(to)}`).join("")}]`);
const sourceFile = /\.(cs|ts|js|mjs|json|md|xaml|csproj|ps1|yml|props|targets)$/i;
const skipDirs = new Set(["node_modules", "bin", "obj", "build", ".git", "фоп"]);
function walkSources(dir, found = []) {
  for (const name of readdirSync(dir)) {
    if (skipDirs.has(name)) continue;
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walkSources(path, found);
    else if (sourceFile.test(name)) found.push(path);
  }
  return found;
}
let cjkLines = 0;
for (const file of walkSources(root)) {
  const lines = read(file).split(/\r?\n/);
  lines.forEach((line, index) => {
    if (!cjk.test(line)) return;
    cjkLines++;
    if (cjkLines <= 20) error(`${rel(file)}:${index + 1} — китайский текст: ${line.trim().slice(0, 80)}`);
  });
}
if (cjkLines > 20) error(`Китайский текст ещё в ${cjkLines - 20} строках (показаны первые 20)`);

// ---------- итог ----------
const bySet = {};
for (const set of commandSets.keys()) {
  const members = commands.filter((c) => (c.sets ?? []).includes(set));
  bySet[set] = {
    commands: members.length,
    experimental: members.filter((c) => c.status === "experimental").length,
    onlyHere: members.filter((c) => c.sets.length === 1).length,
  };
}
const bySection = Object.fromEntries(sectionKeys.map((key) => [key, commands.filter((c) => c.section === key).length]));

if (asJson) {
  console.log(JSON.stringify({ ok: errors.length === 0, errors, warnings, sets: bySet, sections: bySection, profiles: profileSummary }, null, 2));
} else {
  console.log(`Наборы команд (всего команд и инструментов: ${commands.length}):`);
  for (const [set, stats] of Object.entries(bySet)) {
    console.log(`  ${set}: команд ${stats.commands}, только в этом наборе ${stats.onlyHere}, экспериментальных ${stats.experimental}`);
  }
  console.log(`Разделы: ${Object.entries(bySection).map(([key, count]) => `${key} ${count}`).join(", ")}`);
  console.log(`C#-команд: ${csharpCommands.size}, инструментов сервера: ${tools.size}`);
  console.log("Профили правил:");
  for (const item of profileSummary) {
    console.log(`  ${item.id} (${item.kind}, ${item.status}): слои ${item.layers.join(" → ")}; своих проверок ${item.ownChecks}, всего ${item.effectiveChecks}`);
  }
  for (const text of warnings) console.log(`ПРЕДУПРЕЖДЕНИЕ: ${text}`);
  for (const text of errors) console.log(`ОШИБКА: ${text}`);
  console.log(errors.length === 0 ? `Сверка пройдена (предупреждений: ${warnings.length}).` : `Сверка не пройдена: ошибок ${errors.length}, предупреждений ${warnings.length}.`);
}
process.exit(errors.length === 0 ? 0 : 1);
