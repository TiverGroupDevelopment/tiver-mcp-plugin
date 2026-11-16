import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { getRevitTarget, setRevitTarget } from "../utils/ConnectionManager.js";
import { findRevitInstances, probeRevit } from "../utils/revitInstances.js";

export function registerUseRevitInstanceTool(server: McpServer) {
  server.tool(
    "use_revit_instance",
    "Switch this session to another running Revit instance: all following commands go to it. Pick it by port or by Revit version (see list_revit_instances). The switch lasts until the MCP server of the session restarts.",
    {
      port: z.number().int().optional().describe("Port of the plugin server of the instance, e.g. 8081"),
      version: z.string().optional().describe("Revit version, e.g. '2019' — used when port is omitted; must match exactly one running instance"),
    },
    async (args) => {
      try {
        const target = getRevitTarget();
        let port = args.port;
        if (!port) {
          if (!args.version) throw new Error("Укажите port или version.");
          const matches = (await findRevitInstances(target.host)).filter((item) => item.revitVersion === args.version);
          if (matches.length === 0) throw new Error(`Revit ${args.version} с сервером не найден. Запустите: launch_revit version: "${args.version}".`);
          if (matches.length > 1) {
            throw new Error(`Найдено несколько Revit ${args.version}: порты ${matches.map((item) => item.port).join(", ")}. Укажите port.`);
          }
          port = matches[0].port;
        }

        const status = await probeRevit(port, target.host);
        if (!status) throw new Error(`На порту ${port} сервер плагина не отвечает. Смотрите list_revit_instances.`);
        setRevitTarget(port);
        return {
          content: [{ type: "text" as const, text: JSON.stringify({ switchedTo: status, previousPort: target.port }, null, 2) }],
        };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `use_revit_instance failed: ${error instanceof Error ? error.message : String(error)}` }],
          isError: true,
        };
      }
    }
  );
}
