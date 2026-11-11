import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerNewFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "new_family",
    "Create a new family document from a template and pin it as the family session: every following family tool edits this document until close_family, without reloading into a project after each step. The document is opened in the background (not shown in the Revit UI). Returns its reference planes, levels and views.",
    {
      template: z
        .string()
        .describe(
          "Template .rft: full path, path relative to the template folder, or file name (see list_family_templates)"
        ),
      savePath: z
        .string()
        .optional()
        .describe("Save the new family to this .rfa path right away; the folder must exist"),
      overwrite: z.boolean().optional().describe("Replace an existing file at savePath (default false)"),
    }
  );
}
