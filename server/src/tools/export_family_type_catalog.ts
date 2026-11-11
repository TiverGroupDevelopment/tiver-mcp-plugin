import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerExportFamilyTypeCatalogTool(server: McpServer) {
  registerFamilyTool(
    server,
    "export_family_type_catalog",
    `Write a type catalog (.txt) from the family's types: Revit offers the listed types for selection when the family is loaded, instead of loading all of them. The file must sit next to the .rfa with the same name, which is the default path. Parameters driven by formulas, reporting parameters and unsupported types are left out. ${sessionNote}`,
    {
      familyName: familyNameField,
      path: z
        .string()
        .optional()
        .describe("Target .txt path (default: next to the family file, same name)"),
      overwrite: z.boolean().optional().describe("Replace an existing file (default false)"),
      parameters: z
        .array(z.string())
        .optional()
        .describe("Limit the catalog to these parameters (default: all suitable ones)"),
    }
  );
}
