import { existsSync, readdirSync, readFileSync, statSync } from "fs";
import { dirname, isAbsolute, join, resolve } from "path";
import { fileURLToPath } from "url";

// Профили правил моделирования семейств: rules/<проект>/profile.json (+ ПРАВИЛА.md, ФОП).
// Правила подключаются только по запросу: ни одна команда не применяет их сама.

export type RuleProfile = {
  id: string;
  name: string;
  version?: string;
  updated?: string;
  scope?: string;
  activation?: string;
  revitVersion?: string;
  sharedParameterFile?: string;
  rulesFile?: string;
  manualRules?: { rule: string; title: string }[];
  conventions?: Record<string, unknown>;
  checks?: Record<string, Record<string, unknown>>;
  examples?: Record<string, string>;
};

export type LoadedProfile = { folder: string; dir: string; profile: RuleProfile };

export function rulesRoot(): string {
  if (process.env.FAMILY_RULES_DIR) return resolve(process.env.FAMILY_RULES_DIR);
  // server/build/utils → корень репозитория → rules
  return resolve(dirname(fileURLToPath(import.meta.url)), "../../../rules");
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
      profile: JSON.parse(readFileSync(join(dir, "profile.json"), "utf8").replace(/^﻿/, "")) as RuleProfile,
    }));
}

export function loadProfile(key: string): LoadedProfile {
  const wanted = key.trim().toLowerCase();
  const all = listProfiles();
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
export function checksWithGuids(loaded: LoadedProfile): { checks: Record<string, unknown>; missingInFop: string[] } {
  const checks = JSON.parse(JSON.stringify(loaded.profile.checks ?? {})) as Record<string, any>;
  const missingInFop: string[] = [];
  const fopPath = profilePath(loaded, loaded.profile.sharedParameterFile);
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
