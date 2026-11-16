import { existsSync, readdirSync, readFileSync, statSync } from "fs";
import { dirname, isAbsolute, join, resolve } from "path";
import { fileURLToPath } from "url";

// Профили правил моделирования семейств: rules/<папка>/profile.json (+ тексты правил, ФОП).
//
// Профиль может наследовать другие (extends): общие правила (global), пакеты (adsk, …),
// профиль проекта. Слои складываются от общего к частному, поздний слой уточняет ранний.
// Правила подключаются только по запросу: ни одна команда моделирования не применяет их сама.

export type ManualRule = { rule: string; title?: string; section?: string; remove?: boolean };

export type RuleProfile = {
  id: string;
  name: string;
  version?: string;
  updated?: string;
  /** global — общие правила; pack — переиспользуемый пакет; project — профиль проекта (по умолчанию). */
  kind?: "global" | "pack" | "project";
  /** draft — ещё не проверен человеком; validated — проверен. */
  status?: "draft" | "validated";
  /** Родительские профили (id, папка или имя), от общего к частному. */
  extends?: string | string[];
  /** Устаревшее имя extends. */
  base?: string | string[];
  scope?: string;
  activation?: string;
  revitVersion?: string;
  sharedParameterFile?: string;
  /** Один файл с текстом правил. */
  rulesFile?: string;
  /** Тексты по разделам: { geometry: "правила/геометрия.md", … }. */
  rulesFiles?: Record<string, string>;
  manualRules?: ManualRule[];
  conventions?: Record<string, unknown>;
  checks?: Record<string, Record<string, unknown> | null>;
  examples?: Record<string, string>;
};

export type LoadedProfile = { folder: string; dir: string; profile: RuleProfile };

export type RuleText = { layer: string; layerName: string; section: string | null; path: string };

export type ResolvedProfile = LoadedProfile & {
  /** Слои от общего к частному; последний — сам профиль. */
  layers: LoadedProfile[];
  /** Сложенные проверки (без отключённых). */
  checks: Record<string, Record<string, unknown>>;
  /** Из какого слоя пришла проверка (последний слой, который её менял). */
  checkOrigin: Record<string, string>;
  conventions: Record<string, unknown>;
  manualRules: ManualRule[];
  revitVersion?: string;
  /** Абсолютный путь к ФОП: свой или унаследованный. */
  sharedParameterFile: string | null;
  texts: RuleText[];
};

/** Разделы — общие для наборов команд и наборов правил (см. sections.json в корне репозитория). */
export const SECTIONS: Record<string, string> = {
  session: "Сессия и файлы",
  planes: "Опорные плоскости и размеры",
  geometry: "Геометрия",
  parameters: "Параметры и свойства",
  lookup: "Таблицы выбора и каталоги",
  graphics: "Графика и материалы",
  nested: "Вложенные семейства",
  mep: "Коннекторы (MEP)",
  placement: "Семейство в проекте",
  checks: "Проверки и отчёты",
  project: "Модель проекта",
  topography: "Топография",
  code: "Код и скрипты",
  revit: "Запуск Revit и окна",
  service: "Служебные",
};

/** Раздел автоматической проверки: свойство самой проверки, а не профиля. */
export const CHECK_SECTIONS: Record<string, string> = {
  category: "parameters",
  partType: "parameters",
  requiredParameters: "parameters",
  parameterGroups: "parameters",
  extraParameters: "parameters",
  lookupTable: "parameters",
  lookupDefaults: "parameters",
  lookupDecimals: "parameters",
  materialParameter: "graphics",
  nesting: "nested",
  nestedShared: "nested",
  connectorSystem: "mep",
  connectorsLinked: "mep",
  connectorOrientation: "mep",
  connectorOrientationByDirection: "mep",
  connectorDepth: "mep",
  mainRun: "mep",
  singleGeometry: "geometry",
  labelsOnPlanes: "geometry",
  origin: "geometry",
  referencePlanes: "geometry",
};

export function checkSection(key: string, spec?: Record<string, unknown> | null): string | null {
  const own = spec && typeof spec.section === "string" ? (spec.section as string) : null;
  return own ?? CHECK_SECTIONS[key] ?? null;
}

export function rulesRoot(): string {
  if (process.env.FAMILY_RULES_DIR) return resolve(process.env.FAMILY_RULES_DIR);
  // server/build/utils → корень репозитория → rules
  return resolve(dirname(fileURLToPath(import.meta.url)), "../../../rules");
}

/**
 * Корень исходных данных проектов на этой машине (ИД, эталонные семейства, результаты) — вне git.
 * Задаётся переменной REVIT_MCP_DATA_DIR, например D:\Семейства. У каждого разработчика свой.
 */
export function dataRoot(): string | null {
  const value = process.env.REVIT_MCP_DATA_DIR?.trim();
  return value ? resolve(value) : null;
}

export type DataPath = { path: string; exists: boolean | null; note?: string };

/**
 * Путь к файлу данных проекта из профиля. В профиле он пишется относительно корня данных
 * («Альтерпласт/rvt/rfa/…»), чтобы профиль работал на любой машине.
 */
export function resolveDataPath(value: string): DataPath {
  if (isAbsolute(value)) return { path: value, exists: existsSync(value) };
  const root = dataRoot();
  if (!root) {
    return {
      path: value,
      exists: null,
      note: "Путь относительно корня данных: задайте REVIT_MCP_DATA_DIR (папка с исходными данными проектов на этой машине).",
    };
  }
  const full = join(root, value);
  return { path: full, exists: existsSync(full) };
}

function readJson(path: string): any {
  return JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""));
}

export function listProfiles(): LoadedProfile[] {
  const root = rulesRoot();
  if (!existsSync(root)) return [];
  return readdirSync(root)
    .filter((name) => !name.startsWith("_") && !name.startsWith("."))
    .map((name) => join(root, name))
    .filter((dir) => statSync(dir).isDirectory() && existsSync(join(dir, "profile.json")))
    .map((dir) => ({
      folder: dir.split(/[\\/]/).pop() as string,
      dir,
      profile: readJson(join(dir, "profile.json")) as RuleProfile,
    }));
}

function findProfile(all: LoadedProfile[], key: string): LoadedProfile {
  const wanted = key.trim().toLowerCase();
  const found = all.find(
    (p) => p.profile.id?.toLowerCase() === wanted || p.folder.toLowerCase() === wanted || p.profile.name?.toLowerCase() === wanted
  );
  if (!found) {
    throw new Error(
      `Профиль правил '${key}' не найден в ${rulesRoot()}. Есть: ${all.map((p) => `${p.profile.name} (${p.profile.id})`).join(", ") || "нет профилей"}`
    );
  }
  return found;
}

export function parentsOf(profile: RuleProfile): string[] {
  const value = profile.extends ?? profile.base;
  if (!value) return [];
  return (Array.isArray(value) ? value : [value]).filter((item) => typeof item === "string" && item.trim() !== "");
}

/** Слои профиля от общего к частному, каждый один раз. */
function collectLayers(all: LoadedProfile[], item: LoadedProfile, chain: string[], layers: LoadedProfile[]): void {
  if (chain.includes(item.profile.id)) {
    throw new Error(`Циклическое наследование правил: ${[...chain, item.profile.id].join(" → ")}`);
  }
  for (const parent of parentsOf(item.profile)) {
    collectLayers(all, findProfile(all, parent), [...chain, item.profile.id], layers);
  }
  if (!layers.some((layer) => layer.profile.id === item.profile.id)) layers.push(item);
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

/**
 * Профиль со всеми унаследованными слоями.
 *
 * Как складываются слои:
 * - checks — по ключу проверки; поля проверки дополняются и заменяются по одному,
 *   `"ключ": null` отключает унаследованную проверку, `"поле": null` убирает поле;
 * - conventions — по ключу, значение заменяется целиком; null убирает ключ;
 * - manualRules — по номеру правила; `{ "rule": "X", "remove": true }` убирает унаследованное;
 * - sharedParameterFile, revitVersion — свои или ближайшего родителя;
 * - тексты правил — всех слоёв, от общего к частному;
 * - id, name, version, scope, activation, examples — только свои.
 */
export function resolveProfile(key: string): ResolvedProfile {
  const all = listProfiles();
  const own = findProfile(all, key);
  const layers: LoadedProfile[] = [];
  collectLayers(all, own, [], layers);

  const checks: Record<string, Record<string, unknown>> = {};
  const checkOrigin: Record<string, string> = {};
  const conventions: Record<string, unknown> = {};
  const manualRules: ManualRule[] = [];
  const texts: RuleText[] = [];
  let revitVersion: string | undefined;
  let sharedParameterFile: string | null = null;

  for (const layer of layers) {
    const profile = layer.profile;

    for (const [checkKey, spec] of Object.entries(profile.checks ?? {})) {
      if (spec === null) {
        delete checks[checkKey];
        delete checkOrigin[checkKey];
        continue;
      }
      const merged: Record<string, unknown> = { ...(checks[checkKey] ?? {}) };
      for (const [field, value] of Object.entries(spec)) {
        if (value === null) delete merged[field];
        else merged[field] = clone(value);
      }
      checks[checkKey] = merged;
      checkOrigin[checkKey] = profile.id;
    }

    for (const [conventionKey, value] of Object.entries(profile.conventions ?? {})) {
      if (value === null) delete conventions[conventionKey];
      else conventions[conventionKey] = clone(value);
    }

    for (const rule of profile.manualRules ?? []) {
      const index = manualRules.findIndex((existing) => existing.rule === rule.rule);
      if (rule.remove) {
        if (index >= 0) manualRules.splice(index, 1);
      } else if (index >= 0) {
        manualRules[index] = { ...manualRules[index], ...rule };
      } else {
        manualRules.push({ ...rule });
      }
    }

    if (profile.revitVersion) revitVersion = profile.revitVersion;
    if (profile.sharedParameterFile) {
      sharedParameterFile = isAbsolute(profile.sharedParameterFile)
        ? profile.sharedParameterFile
        : join(layer.dir, profile.sharedParameterFile);
    }

    if (profile.rulesFiles) {
      for (const [section, file] of Object.entries(profile.rulesFiles)) {
        texts.push({ layer: profile.id, layerName: profile.name, section, path: isAbsolute(file) ? file : join(layer.dir, file) });
      }
    } else {
      const file = profile.rulesFile ?? "ПРАВИЛА.md";
      texts.push({ layer: profile.id, layerName: profile.name, section: null, path: isAbsolute(file) ? file : join(layer.dir, file) });
    }
  }

  return { ...own, layers, checks, checkOrigin, conventions, manualRules, revitVersion, sharedParameterFile, texts };
}

/**
 * Профиль по аргументу инструмента. Без аргумента — общие правила (global):
 * правила проекта применяются, только когда проект назван.
 */
export function resolveRequestedProfile(key: string | undefined): { resolved: ResolvedProfile; source: string } {
  if (key && key.trim()) return { resolved: resolveProfile(key), source: "аргумент profile" };
  const all = listProfiles();
  const globalProfile = all.find((p) => p.profile.kind === "global") ?? all.find((p) => p.profile.id === "global");
  if (!globalProfile) {
    throw new Error("Профиль не указан, а общих правил (kind: global) нет. Укажите profile.");
  }
  return { resolved: resolveProfile(globalProfile.profile.id), source: "общие правила: проект не назван" };
}

/** Старое имя: профиль со всеми слоями. */
export function loadProfile(key: string): ResolvedProfile {
  return resolveProfile(key);
}

export function profilePath(loaded: LoadedProfile, file: string | undefined): string | null {
  if (!file) return null;
  return isAbsolute(file) ? file : join(loaded.dir, file);
}

export type FopParameter = { name: string; guid: string; dataType: string; group: string; description: string };

/** Файл общих параметров Revit (UTF-16 или UTF-8, табуляция). */
export function parseSharedParameterFile(path: string): Map<string, FopParameter> {
  const buffer = readFileSync(path);
  const text =
    buffer[0] === 0xff && buffer[1] === 0xfe ? buffer.toString("utf16le").replace(/^﻿/, "") : buffer.toString("utf8").replace(/^﻿/, "");
  const groups = new Map<string, string>();
  const result = new Map<string, FopParameter>();
  for (const line of text.split(/\r?\n/)) {
    const cells = line.split("\t");
    if (cells[0] === "GROUP") groups.set(cells[1], cells[2]);
    if (cells[0] === "PARAM") {
      result.set(cells[2], {
        name: cells[2],
        guid: cells[1],
        dataType: cells[3],
        group: groups.get(cells[5]) ?? cells[5],
        description: cells[7] ?? "",
      });
    }
  }
  return result;
}

/** Проверки профиля с GUID обязательных параметров и параметра материала из ФОП. */
export function checksWithGuids(resolved: ResolvedProfile): { checks: Record<string, unknown>; missingInFop: string[] } {
  const checks = clone(resolved.checks) as Record<string, any>;
  const missingInFop: string[] = [];
  const fopPath = resolved.sharedParameterFile;
  if (!fopPath || !existsSync(fopPath)) return { checks, missingInFop };
  const fop = parseSharedParameterFile(fopPath);

  for (const item of checks.requiredParameters?.parameters ?? []) {
    const definition = fop.get(item.name);
    if (definition && !item.guid) item.guid = definition.guid;
    if (!definition) missingInFop.push(item.name);
  }
  if (checks.materialParameter?.name && !checks.materialParameter.guid) {
    const definition = fop.get(checks.materialParameter.name);
    if (definition) checks.materialParameter.guid = definition.guid;
  }
  return { checks, missingInFop };
}

/** Короткое описание слоёв для ответов инструментов. */
export function describeLayers(resolved: ResolvedProfile) {
  return resolved.layers.map((layer) => ({
    id: layer.profile.id,
    name: layer.profile.name,
    version: layer.profile.version,
    kind: layer.profile.kind ?? "project",
    status: layer.profile.status ?? "validated",
  }));
}
