import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerListFamilyTemplatesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "list_family_templates",
    "List family templates (.rft) from Revit's family template folder, as paths relative to that folder. Use the result as `template` in new_family. To pick a template by family category, pass `category` (or inspect: true): each matching template is opened briefly to report its real category, placement type and hosting; narrow `filter` first, at most 15 templates are inspected.",
    {
      filter: z
        .string()
        .optional()
        .describe("Case-insensitive substring of the relative path, e.g. Generic Model or Metric"),
      inspect: z
        .boolean()
        .optional()
        .describe("Open each matching template to report category, placement type and hosting (default false)"),
      category: z
        .string()
        .optional()
        .describe(
          "Keep only templates whose family category contains this text (localized name or BuiltInCategory such as OST_Doors). Implies inspect."
        ),
    }
  );
}
