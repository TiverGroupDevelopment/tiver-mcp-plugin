import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerListProjectFamiliesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "list_project_families",
    "List families loaded in a project: category, BuiltInCategory, placement type, types with ids and instance count. Use it to find familyName for family tools and place_family_in_project. Read-only.",
    {
      project: z.string().optional().describe("Project title (default: active project)"),
      nameFilter: z.string().optional().describe("Case-insensitive part of the family name"),
      category: z.string().optional().describe("Category name as in Revit or BuiltInCategory, e.g. OST_PipeAccessory"),
      includeNonEditable: z.boolean().optional().describe("Include non-editable system-like families (default false)"),
    }
  );
}
