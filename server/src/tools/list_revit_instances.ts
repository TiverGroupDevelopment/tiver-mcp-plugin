import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { getRevitTarget } from "../utils/ConnectionManager.js";
import { RevitClientConnection } from "../utils/SocketClient.js";
import { RevitStatus, findRevitInstances, installedRevitVersions, probeRevit, sleep } from "../utils/revitInstances.js";

/** Открытые документы экземпляра — через diagnose_revit; ждём недолго: Revit может быть занят. */
async function describeDocuments(port: number, host: string, timeoutMs = 15000): Promise<unknown> {
  const client = new RevitClientConnection(host, port);
  try {
    await new Promise<void>((resolve, reject) => {
      client.socket.once("connect", () => resolve());
      client.socket.once("error", () => reject(new Error("нет соединения")));
      client.connect();
    });
    const response: any = await Promise.race([
      client.sendCommand("diagnose_revit", {}),
      sleep(timeoutMs).then(() => {
        throw new Error(`Revit не ответил за ${timeoutMs / 1000} с: занят или ждёт ответа в диалоге`);
      }),
    ]);
    return { active: response?.active ?? null, pinnedSession: response?.pinnedSession ?? null, documents: response?.documents ?? [] };
  } catch (error) {
    return { error: error instanceof Error ? error.message : String(error) };
  } finally {
    client.disconnect();
  }
}

export function registerListRevitInstancesTool(server: McpServer) {
  server.tool(
    "list_revit_instances",
    "List running Revit instances that have the MCP server of the plugin started (ports 8080-8089): port, Revit version, process id, how the server was started, number of loaded commands and command loading problems (duplicate names, missing assemblies). Shows which instance this session talks to and which Revit versions are installed. With details: true also lists the open documents of each instance. Use it when a command cannot reach Revit, before launch_revit, or to pick an instance for use_revit_instance. Does not start anything.",
    {
      details: z.boolean().optional().describe("Also list open documents of each instance (asks Revit, may take a few seconds). Default false."),
    },
    async (args) => {
      const target = getRevitTarget();
      const found = await findRevitInstances(target.host);
      // Порт сессии может быть вне стандартного диапазона (REVIT_PORT).
      if (!found.some((item) => item.port === target.port)) {
        const extra = await probeRevit(target.port, target.host);
        if (extra) found.push(extra);
      }

      const instances = [];
      for (const item of found as RevitStatus[]) {
        instances.push({
          ...item,
          isCurrent: item.port === target.port,
          note: item.legacy ? "Старая сборка плагина: версия и команды неизвестны. Обновите плагин (scripts/install-local.ps1)." : undefined,
          ...(args.details && !item.legacy ? { state: await describeDocuments(item.port, target.host) } : {}),
        });
      }

      const result = {
        current: target,
        currentAnswers: instances.some((item) => item.isCurrent),
        instances,
        installedRevitVersions: installedRevitVersions(),
        hint:
          instances.length === 0
            ? "Ни один Revit с сервером не найден. Запустите: launch_revit version: \"<год>\"."
            : instances.some((item) => item.isCurrent)
              ? undefined
              : "Сессия смотрит на порт, где Revit не отвечает. Переключитесь: use_revit_instance port: <порт>.",
      };
      return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
    }
  );
}
