import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { familyNameField, sessionNote } from "../utils/familyTool.js";
import { checksWithGuids, loadProfile } from "../utils/familyRules.js";

export function registerCheckFamilyRulesTool(server: McpServer) {
  server.tool(
    "check_family_rules",
    `Check a family against a project rules profile (rules/<project>/profile.json): required shared parameters and GUIDs from the profile's shared parameter file, material parameter wiring, lookup table instead of types, nesting depth and the Shared flag of nested families, connector system classification, linking and Y-up orientation, number of forms and forbidden geometry, labels on reference planes vs geometry, origin at the reference plane intersection, reference plane lengths in every view, parameter groups, extra parameters, lookup defaults ("not in catalog" text), decimals in lookup tables, connector depth inside the part; for multi-port parts (collectors, tees): main run on the axis and linked, connector Y by direction, part type. Returns pass/warn/fail per rule plus the rules that need a manual check. Read-only. Check a file (path, opened in the background) or the current family. ${sessionNote}`,
    {
      profile: z.string().describe("Profile id, folder or name, e.g. 'alterplast' / 'Альтерпласт'"),
      path: z.string().optional().describe(".rfa file to check (opened in the background and closed without saving)"),
      familyName: familyNameField,
      only: z.array(z.string()).optional().describe("Run only these checks (keys of profile.checks) or rule ids, e.g. ['ALT-05', 'origin']"),
    },
    async (args) => {
      try {
        const loaded = loadProfile(args.profile);
        let { checks, missingInFop } = checksWithGuids(loaded);
        if (args.only?.length) {
          const wanted = new Set(args.only.map((s) => s.toLowerCase()));
          checks = Object.fromEntries(
            Object.entries(checks).filter(([key, spec]) => wanted.has(key.toLowerCase()) || wanted.has(String((spec as any).rule ?? "").toLowerCase()))
          );
        }

        const command: Record<string, unknown> = { checks };
        if (args.path) command.path = args.path;
        if (args.familyName) command.familyName = args.familyName;
        const response = await withRevitConnection((client) => client.sendCommand("check_family_rules", command));

        const result = {
          profile: loaded.profile.name,
          revit: response,
          manualRules: loaded.profile.manualRules ?? [],
          notes: missingInFop.length ? [`Нет в ФОП профиля: ${missingInFop.join(", ")}`] : [],
        };
        return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `check_family_rules failed: ${error instanceof Error ? error.message : String(error)}` }],
          isError: true,
        };
      }
    }
  );
}
