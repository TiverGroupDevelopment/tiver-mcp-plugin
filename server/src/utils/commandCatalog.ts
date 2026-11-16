import { existsSync, readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

// Каталог команд. Код всех команд — в одной сборке, манифест — command.json в корне репозитория.
// К каким наборам относится команда, говорит её поле sets: одна команда может входить
// в несколько наборов (Global и Alterplast), другая — только в один. Наборы описаны в commandSets.
// Инструменты, реализованные в самом сервере, записаны в server/command.json с теми же полями.
// Каталог справочный: агенту доступны все инструменты.

export type CatalogEntry = {
  /** Имя команды в манифесте (CommandName в C#). */
  command: string;
  /** Имя инструмента MCP: совпадает с командой, если в манифесте нет поля tool. */
  tool: string;
  /** Наборы, в которые входит команда. */
  sets: string[];
  section: string | null;
  /** validated — проверена; experimental — в работе, не проверена. */
  status: string;
  description: string;
  /** Инструмент реализован в сервере, отдельной C#-команды нет. */
  serverOnly: boolean;
  /** Входные параметры по порядку схемы инструмента; «*» — обязательный (scripts/sync-command-parameters.mjs). */
  parameters: string[];
};

export type CommandSetInfo = {
  name: string;
  description: string;
  /** Профиль правил проекта, если набор — набор проекта. */
  project?: string;
};

export const GLOBAL_SET = "Global";

const MANIFESTS: { path: string; serverOnly: boolean }[] = [
  { path: "command.json", serverOnly: false },
  { path: "server/command.json", serverOnly: true },
];

export function repoRoot(): string {
  if (process.env.REVIT_MCP_REPO_ROOT) return resolve(process.env.REVIT_MCP_REPO_ROOT);
  // server/build/utils → корень репозитория
  return resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
}

let cached: CatalogEntry[] | null = null;
let cachedSets: CommandSetInfo[] = [];

/** Все команды. Если манифестов рядом нет (сервер установлен отдельно), каталог пуст. */
export function loadCatalog(): CatalogEntry[] {
  if (cached) return cached;
  const root = repoRoot();
  const entries: CatalogEntry[] = [];
  const sets: CommandSetInfo[] = [];
  for (const manifest of MANIFESTS) {
    const path = join(root, manifest.path);
    if (!existsSync(path)) continue;
    try {
      const data = JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""));
      for (const set of data.commandSets ?? []) {
        if (set?.name && !sets.some((known) => known.name === set.name)) {
          sets.push({ name: set.name, description: set.description ?? "", project: set.project });
        }
      }
      for (const item of data.commands ?? []) {
        if (!item?.commandName) continue;
        entries.push({
          command: item.commandName,
          tool: item.tool ?? item.commandName,
          sets: Array.isArray(item.sets) && item.sets.length > 0 ? item.sets : [GLOBAL_SET],
          section: item.section ?? null,
          status: item.status ?? "validated",
          description: item.description ?? "",
          serverOnly: manifest.serverOnly,
          parameters: (item.parameters ?? []).map((p: { name: string; required?: boolean }) => (p.required ? `${p.name}*` : p.name)),
        });
      }
    } catch (error) {
      console.error(`Манифест ${path} не прочитан:`, error);
    }
  }
  cached = entries;
  cachedSets = sets;
  return entries;
}

/** Наборы команд с описаниями (из commandSets манифеста). */
export function loadCommandSets(): CommandSetInfo[] {
  loadCatalog();
  return cachedSets;
}

export function inSet(entry: CatalogEntry, set: string): boolean {
  return entry.sets.some((name) => name.toLowerCase() === set.toLowerCase());
}

/** Общая команда — входит в Global. */
export function isGlobal(entry: CatalogEntry): boolean {
  return inSet(entry, GLOBAL_SET);
}
