import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool } from "../utils/familyTool.js";

export function registerSaveFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "save_family",
    "Save the active family document, or save a family (active or loaded in the project) as an .rfa file. An existing file is never replaced unless overwrite is true.",
    {
      familyName: familyNameField,
      path: z.string().optional().describe("Target .rfa path on the Revit machine; the folder must exist"),
      overwrite: z.boolean().optional().describe("Replace an existing file (default false)"),
    }
  );
}
