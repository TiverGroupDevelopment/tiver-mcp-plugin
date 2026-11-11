import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerCloseFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "close_family",
    "Finish the pinned family session: optionally save, load the family into a project, then close the document. Refuses to drop unsaved changes unless discardChanges is true. A family opened from a project is loaded back into it by default.",
    {
      save: z.boolean().optional().describe("Save to the existing file path (default false)"),
      savePath: z.string().optional().describe("Save as this .rfa path; the folder must exist"),
      overwrite: z.boolean().optional().describe("Replace an existing file at savePath (default false)"),
      loadIntoProject: z
        .boolean()
        .optional()
        .describe(
          "Load the family into the project it came from, or into the active project for new/file sessions. Default: true for families opened from a project, false otherwise."
        ),
      overwriteProjectValues: z
        .boolean()
        .optional()
        .describe("When loading: overwrite type parameter values in the project (default true)"),
      discardChanges: z
        .boolean()
        .optional()
        .describe("Allow closing without saving or loading when the family has unsaved changes"),
      keepOpen: z
        .boolean()
        .optional()
        .describe("Only unpin the session and leave the document open in Revit (default false)"),
    }
  );
}
