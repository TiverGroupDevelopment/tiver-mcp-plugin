import { z } from "zod";
import { spawn } from "child_process";
import { existsSync } from "fs";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { getRevitTarget, setRevitTarget } from "../utils/ConnectionManager.js";
import {
  RevitStatus,
  findRevitInstances,
  firstFreePort,
  installedRevitVersions,
  probeRevit,
  revitExecutable,
  sleep,
} from "../utils/revitInstances.js";

// Запуск Revit агентом. Процессу Revit передаются переменные:
//   REVIT_MCP_AUTOSTART=1 — плагин сам поднимает сервер при старте, без кнопки и без открытого документа;
//   REVIT_MCP_PORT=<порт>  — порт сервера для этого процесса.
// Revit, запущенный человеком вручную, этих переменных не получает и порт сам не открывает.

async function waitForServer(port: number, processId: number | undefined, host: string, seconds: number): Promise<RevitStatus | null> {
  const deadline = Date.now() + seconds * 1000;
  while (Date.now() < deadline) {
    const onPort = await probeRevit(port, host);
    if (onPort && (!processId || !onPort.processId || onPort.processId === processId)) return onPort;
    // Порт могли занять между выбором и стартом: тогда плагин взял следующий свободный.
    if (processId) {
      const mine = (await findRevitInstances(host)).find((item) => item.processId === processId);
      if (mine) return mine;
    }
    await sleep(2000);
  }
  return null;
}

export function registerLaunchRevitTool(server: McpServer) {
  server.tool(
    "launch_revit",
    "Start Revit of the given version with the MCP server of the plugin enabled, wait until it answers and switch this session to it. No manual steps: the server starts by itself, no document has to be open. If a Revit of this version with the server is already running, the session is switched to it and nothing is started (reuse: false starts another one). Optionally opens a file (.rfa, .rvt) on start. Revit takes 30-120 s to start; if it is not ready within waitSeconds the tool returns the port to watch — call list_revit_instances a bit later. After a successful start continue with new_family / open_family and the other commands.",
    {
      version: z.string().regex(/^20\d\d$/).describe("Revit version (year), e.g. '2019'"),
      file: z.string().optional().describe("Absolute path of a .rfa / .rvt / .rte file to open on start"),
      reuse: z.boolean().optional().describe("Use an already running Revit of this version instead of starting a new one (default true)"),
      language: z.string().optional().describe("Revit UI language code for /language, e.g. 'RUS' or 'ENG'. Omit to keep the default."),
      waitSeconds: z.number().int().min(10).max(600).optional().describe("How long to wait for the server to answer (default 180)"),
    },
    async (args) => {
      try {
        const host = getRevitTarget().host;
        const executable = revitExecutable(args.version);
        if (!existsSync(executable)) {
          throw new Error(`Revit ${args.version} не установлен (${executable}). Установлены: ${installedRevitVersions().join(", ") || "нет"}.`);
        }
        if (args.file && !existsSync(args.file)) throw new Error(`Файл не найден: ${args.file}`);

        if (args.reuse !== false) {
          const running = (await findRevitInstances(host)).find((item) => item.revitVersion === args.version);
          if (running) {
            setRevitTarget(running.port);
            const result = {
              started: false,
              reused: true,
              instance: running,
              note: args.file
                ? "Revit этой версии уже запущен, новый не стартовал. Файл не открыт: откройте его командой open_family (семейство) или передайте reuse: false."
                : "Revit этой версии уже запущен, сессия переключена на него.",
            };
            return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
          }
        }

        const port = await firstFreePort();
        if (!port) throw new Error("Нет свободного порта в диапазоне 8080-8089: запущено слишком много экземпляров.");

        const commandLine: string[] = [];
        if (args.file) commandLine.push(args.file);
        commandLine.push("/nosplash");
        if (args.language) commandLine.push("/language", args.language);

        const child = spawn(executable, commandLine, {
          detached: true,
          stdio: "ignore",
          env: { ...process.env, REVIT_MCP_AUTOSTART: "1", REVIT_MCP_PORT: String(port) },
        });
        const spawnError = await new Promise<Error | null>((resolve) => {
          child.once("error", (error) => resolve(error));
          child.once("spawn", () => resolve(null));
        });
        if (spawnError) throw new Error(`Revit не запустился: ${spawnError.message}`);
        child.unref();

        const waitSeconds = args.waitSeconds ?? 180;
        const status = await waitForServer(port, child.pid, host, waitSeconds);
        if (!status) {
          const result = {
            started: true,
            ready: false,
            processId: child.pid,
            expectedPort: port,
            note:
              `Revit ${args.version} запущен, но сервер не ответил за ${waitSeconds} с. Возможные причины: ` +
              "Revit ещё загружается (проверьте позже: list_revit_instances); при запуске открыто окно, которое ждёт ответа " +
              "(лицензия, «Загрузить надстройку?», восстановление файлов); установлена старая сборка плагина без автозапуска " +
              "(обновите: scripts/install-local.ps1). Лог плагина: %APPDATA%\\Autodesk\\Revit\\Addins\\<год>\\revit_mcp_plugin\\Logs.",
          };
          return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
        }

        setRevitTarget(status.port);
        const result = {
          started: true,
          ready: true,
          instance: status,
          openedFile: args.file ?? null,
          note:
            status.loadProblems.length > 0
              ? "Сервер отвечает, но часть команд не загрузилась — см. loadProblems."
              : "Сервер отвечает, сессия переключена на этот Revit.",
        };
        return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `launch_revit failed: ${error instanceof Error ? error.message : String(error)}` }],
          isError: true,
        };
      }
    }
  );
}
