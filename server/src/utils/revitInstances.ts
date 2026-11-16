import * as net from "net";
import { existsSync, readdirSync } from "fs";
import { join } from "path";

// Поиск запущенных Revit с сервером плагина и запуск новых.
// Плагин слушает первый свободный порт из диапазона 8080–8089 (или начиная с REVIT_MCP_PORT)
// и отвечает на встроенный метод mcp_status без обращения к Revit API.

export const DEFAULT_PORT = 8080;
export const PORT_RANGE = 10;

export type RevitStatus = {
  port: number;
  /** Версия Revit, например "2019". Пусто у старого плагина без mcp_status. */
  revitVersion: string;
  processId: number | null;
  startedBy: string | null;
  commandCount: number | null;
  loadProblems: string[];
  /** Старая сборка плагина: порт отвечает, но mcp_status не знает. */
  legacy: boolean;
};

function sendOnce(host: string, port: number, method: string, timeoutMs: number): Promise<any | null> {
  return new Promise((resolve) => {
    const socket = new net.Socket();
    let buffer = "";
    let done = false;
    const finish = (value: any | null) => {
      if (done) return;
      done = true;
      socket.destroy();
      resolve(value);
    };
    socket.setTimeout(timeoutMs, () => finish(null));
    socket.on("error", () => finish(null));
    socket.on("close", () => finish(null));
    socket.on("data", (data) => {
      buffer += data.toString();
      try {
        finish(JSON.parse(buffer));
      } catch {
        // ответ ещё не дочитан
      }
    });
    socket.connect(port, host, () => {
      socket.write(JSON.stringify({ jsonrpc: "2.0", method, params: {}, id: `probe-${port}-${Date.now()}` }));
    });
  });
}

/** Состояние сервера плагина на порту или null, если порт не отвечает как плагин. */
export async function probeRevit(port: number, host = "127.0.0.1", timeoutMs = 1500): Promise<RevitStatus | null> {
  const response = await sendOnce(host, port, "mcp_status", timeoutMs);
  if (!response || response.jsonrpc !== "2.0") return null;
  // "tiver-mcp" — метка плагина; "revit-mcp" — она же у сборок плагина до 16.11.2026.
  if (response.result?.plugin === "tiver-mcp" || response.result?.plugin === "revit-mcp") {
    return {
      port,
      revitVersion: String(response.result.revitVersion ?? ""),
      processId: response.result.processId ?? null,
      startedBy: response.result.startedBy ?? null,
      commandCount: response.result.commandCount ?? null,
      loadProblems: response.result.loadProblems ?? [],
      legacy: false,
    };
  }
  // Плагин до mcp_status отвечает «Method not found» — экземпляр есть, подробностей нет.
  if (response.error) {
    return { port, revitVersion: "", processId: null, startedBy: null, commandCount: null, loadProblems: [], legacy: true };
  }
  return null;
}

export function portRange(base = DEFAULT_PORT): number[] {
  return Array.from({ length: PORT_RANGE }, (_, index) => base + index);
}

export async function findRevitInstances(host = "127.0.0.1"): Promise<RevitStatus[]> {
  const found = await Promise.all(portRange().map((port) => probeRevit(port, host)));
  return found.filter((item): item is RevitStatus => item !== null);
}

/** Занят ли порт кем угодно (не обязательно плагином). */
export function isPortBusy(port: number, host = "127.0.0.1", timeoutMs = 700): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = new net.Socket();
    const finish = (busy: boolean) => {
      socket.destroy();
      resolve(busy);
    };
    socket.setTimeout(timeoutMs, () => finish(false));
    socket.on("error", () => finish(false));
    socket.connect(port, host, () => finish(true));
  });
}

export async function firstFreePort(): Promise<number | null> {
  for (const port of portRange()) {
    if (!(await isPortBusy(port))) return port;
  }
  return null;
}

function autodeskRoot(): string {
  return process.env.REVIT_INSTALL_ROOT || join(process.env.ProgramFiles || "C:\\Program Files", "Autodesk");
}

export function revitExecutable(version: string): string {
  return join(autodeskRoot(), `Revit ${version}`, "Revit.exe");
}

export function installedRevitVersions(): string[] {
  const root = autodeskRoot();
  if (!existsSync(root)) return [];
  return readdirSync(root)
    .map((name) => /^Revit (20\d\d)$/.exec(name)?.[1])
    .filter((version): version is string => !!version && existsSync(revitExecutable(version)))
    .sort();
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
