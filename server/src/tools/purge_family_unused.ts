import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerPurgeFamilyUnusedTool(server: McpServer) {
  registerFamilyTool(
    server,
    "purge_family_unused",
    `Delete unused elements from a family (unused nested family types, materials, styles), like Purge Unused, to keep the file small. Requires Revit 2024 or newer; older versions report that the API is unavailable. Use dryRun to see what would be removed. ${sessionNote}`,
    {
      familyName: familyNameField,
      dryRun: z.boolean().optional().describe("Only list unused elements, delete nothing (default false)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
