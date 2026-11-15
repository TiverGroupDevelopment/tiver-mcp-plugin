import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerOpenFamilyInEditorTool(server: McpServer) {
  registerFamilyTool(
    server,
    "open_family_in_editor",
    "Show a family in a Revit family editor tab, like 'Edit Family': from the pinned session (saved, closed and reopened in a window; pass save: true if it has changes), from an .rfa file (path), or from a family loaded in the active project (familyName: a copy is opened from a temp file; bring changes back with load_family_into_project). Activates a view, turns a 3D view isometric with shading and zooms to the forms. Afterwards family tools work on the active family document.",
    {
      path: z.string().optional().describe(".rfa file to open"),
      familyName: z.string().optional().describe("Family loaded in the active project"),
      save: z
        .boolean()
        .optional()
        .describe("Pinned session with unsaved changes: save them to the family file before opening (default false — refuse)"),
      view: z.string().optional().describe("View to activate (default: first 3D view)"),
      isometric: z.boolean().optional().describe("Turn the 3D view isometric with shading and edges (default true)"),
      zoom: z.boolean().optional().describe("Zoom to the forms (default true)"),
    }
  );
}
