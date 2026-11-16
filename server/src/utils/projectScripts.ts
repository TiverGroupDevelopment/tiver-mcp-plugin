import { existsSync, readFileSync, readdirSync, statSync } from "fs";
import { join, resolve } from "path";
import { repoRoot } from "./commandCatalog.js";

// Скрипты проекта — первая ступень перед командой: C#-код для send_code_to_revit,
// сохранённый в файл projects/<id проекта>/scripts/<имя>.cs. Сборка и перезапуск Revit не нужны.
// Скрипт, который пригодился второй раз или второму проекту, — кандидат в команду (см. PROMOTION.md).
//
// Формат файла: шапка из строк «// ключ: значение», затем тело метода
// Execute(Document document, object[] parameters).
//   // description: что делает
//   // parameters: имя1, имя2          (по порядку, как в parameters[0], parameters[1])
//   // transaction: auto | none        (по умолчанию auto)
//   // usings: using Autodesk.Revit.DB.Plumbing;
// Дополнительные классы — в файле <имя>.classes.cs рядом.

export type ProjectScript = {
  project: string;
  name: string;
  path: string;
  description: string;
  parameters: string[];
  transaction: "auto" | "none";
  usings?: string;
  classesPath?: string;
};

export function projectsRoot(): string {
  if (process.env.REVIT_MCP_PROJECTS_DIR) return resolve(process.env.REVIT_MCP_PROJECTS_DIR);
  return join(repoRoot(), "projects");
}

function parseScript(project: string, path: string, name: string): ProjectScript {
  const header: Record<string, string> = {};
  for (const line of readFileSync(path, "utf8").replace(/^﻿/, "").split(/\r?\n/)) {
    const match = /^\s*\/\/\s*([\wа-яё]+)\s*:\s*(.*)$/i.exec(line);
    if (!match) {
      if (line.trim() === "" || line.trim().startsWith("//")) continue;
      break;
    }
    header[match[1].toLowerCase()] = match[2].trim();
  }
  const classesPath = path.replace(/\.cs$/, ".classes.cs");
  return {
    project,
    name,
    path,
    description: header.description ?? header["описание"] ?? "",
    parameters: (header.parameters ?? header["параметры"] ?? "")
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean),
    transaction: (header.transaction ?? "auto").toLowerCase() === "none" ? "none" : "auto",
    usings: header.usings || undefined,
    classesPath: existsSync(classesPath) ? classesPath : undefined,
  };
}

/** Проекты, чьи скрипты смотрим: указанный явно или все папки в projects/. */
export function scriptProjects(project?: string): string[] {
  if (project && project.trim()) return [project.trim().toLowerCase()];
  const root = projectsRoot();
  if (!existsSync(root)) return [];
  return readdirSync(root)
    .filter((name) => !name.startsWith(".") && !name.startsWith("_") && statSync(join(root, name)).isDirectory())
    .map((name) => name.toLowerCase());
}

export function listScripts(project?: string): ProjectScript[] {
  const root = projectsRoot();
  const scripts: ProjectScript[] = [];
  for (const id of scriptProjects(project)) {
    const dir = join(root, id, "scripts");
    if (!existsSync(dir) || !statSync(dir).isDirectory()) continue;
    for (const file of readdirSync(dir)) {
      if (!file.endsWith(".cs") || file.endsWith(".classes.cs")) continue;
      scripts.push(parseScript(id, join(dir, file), file.replace(/\.cs$/, "")));
    }
  }
  return scripts;
}

/** Скрипт по имени: в указанном проекте или во всех; одно имя в нескольких проектах — нужно указать project. */
export function findScript(name: string, project?: string): ProjectScript {
  const wanted = name.trim().replace(/\.cs$/i, "").toLowerCase();
  const projects = scriptProjects(project);
  if (projects.length === 0) {
    throw new Error(`Папки проектов не найдены (${projectsRoot()}).`);
  }
  const all = listScripts(project);
  const matches = all.filter((script) => script.name.toLowerCase() === wanted);
  if (matches.length === 1) return matches[0];
  if (matches.length > 1) {
    throw new Error(`Скрипт '${name}' есть в нескольких проектах: ${matches.map((script) => script.project).join(", ")}. Укажите project.`);
  }
  throw new Error(
    `Скрипт '${name}' не найден в ${projects.map((id) => `projects/${id}/scripts`).join(", ")}. Есть: ${all.map((script) => script.name).join(", ") || "нет скриптов"}`
  );
}

export function readScriptBody(script: ProjectScript): { code: string; classes?: string } {
  return {
    code: readFileSync(script.path, "utf8").replace(/^﻿/, ""),
    classes: script.classesPath ? readFileSync(script.classesPath, "utf8").replace(/^﻿/, "") : undefined,
  };
}
