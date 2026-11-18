import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerSetOpenViewsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_open_views",
    "Make one view active in a Revit document (project or family) and close its other open view tabs, e.g. to leave only the 3D view for a presentation. document: title of an open document (switches to it if it was opened from a file); default the active document. view: view name; default the first 3D view. Zooms the 3D view to fit.",
    {
      document: z.string().optional().describe("Open document title (default: active)"),
      view: z.string().optional().describe("View name (default: first 3D view)"),
      closeOthers: z.boolean().optional().describe("Close the other open views of this document (default true)"),
      zoom: z.boolean().optional().describe("Zoom a 3D view to fit (default true)"),
    }
  );
}
