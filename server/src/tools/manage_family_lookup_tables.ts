import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerManageFamilyLookupTablesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "manage_family_lookup_tables",
    `List, import, export or remove lookup tables (size tables) of a family. A lookup table is a CSV whose rows are read by formulas with size_lookup(...), common in pipe and duct fittings. On import the table is named after the CSV file. ${sessionNote}`,
    {
      familyName: familyNameField,
      action: z.enum(["list", "import", "export", "remove"]).optional().describe("Default: list"),
      name: z.string().optional().describe("Table name, for export and remove"),
      path: z.string().optional().describe("CSV path on the Revit machine, for import and export"),
      overwrite: z.boolean().optional().describe("For export: replace an existing file (default false)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
