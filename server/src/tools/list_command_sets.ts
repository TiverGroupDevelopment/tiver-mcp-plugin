import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { inSet, isGlobal, loadCatalog, loadCommandSets } from "../utils/commandCatalog.js";
import { SECTIONS } from "../utils/familyRules.js";

export function registerListCommandSetsTool(server: McpServer) {
  server.tool(
    "list_command_sets",
    "Command catalogue of this server. All commands live in one code base; each command lists the command sets it belongs to (field sets): Global (general commands usable in any project) and project sets (e.g. Alterplast — every command that project uses). A command can belong to several sets. For every command: sets, section, status (validated / experimental), description. All commands are available in every session; the catalogue is for reference. Use it to see what a project uses, which commands are general and which only serve one project or are still experimental, e.g. when deciding what to add to Global. Read-only, does not need Revit.",
    {
      set: z.string().optional().describe("Only the commands of this set, e.g. 'Alterplast' or 'Global'"),
      section: z.string().optional().describe(`Only this section: ${Object.keys(SECTIONS).join(", ")}`),
      status: z.enum(["validated", "experimental"]).optional().describe("Only commands with this status"),
    },
    async (args) => {
      const all = loadCatalog();
      const catalog = all.filter(
        (entry) =>
          (!args.set || inSet(entry, args.set.trim())) &&
          (!args.section || entry.section === args.section.toLowerCase()) &&
          (!args.status || entry.status === args.status)
      );

      const bySection: Record<string, unknown[]> = {};
      for (const entry of catalog) {
        const key = entry.section ?? "без раздела";
        (bySection[key] ??= []).push({
          tool: entry.tool,
          command: entry.command !== entry.tool ? entry.command : undefined,
          sets: entry.sets,
          status: entry.status,
          serverOnly: entry.serverOnly || undefined,
          parameters: entry.parameters.join(", "),
          description: entry.description,
        });
      }

      const sets = loadCommandSets().map((set) => {
        const members = all.filter((entry) => inSet(entry, set.name));
        return {
          name: set.name,
          project: set.project,
          description: set.description,
          commands: members.length,
          onlyInThisSet: members.filter((entry) => entry.sets.length === 1).map((entry) => entry.tool),
        };
      });

      const result = {
        sets,
        sections: SECTIONS,
        totals: {
          commands: catalog.length,
          global: catalog.filter(isGlobal).length,
          notInGlobal: catalog.filter((entry) => !isGlobal(entry)).length,
          experimental: catalog.filter((entry) => entry.status === "experimental").length,
        },
        commands: bySection,
      };
      return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
    }
  );
}
