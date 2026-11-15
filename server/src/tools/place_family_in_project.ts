import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool, vector3 } from "../utils/familyTool.js";

export function registerPlaceFamilyInProjectTool(server: McpServer) {
  registerFamilyTool(
    server,
    "place_family_in_project",
    "Place an instance of a family in a project: by family and type name on a level by name, at a point in mm with rotation. Optionally load the family from an .rfa first (path). Works for free-standing and level-based families (not hosted or line-based). Revit and third-party updater failures are returned as text and the change is rolled back without dialogs. The instance is selected and shown in the active view.",
    {
      family: z.string().optional().describe("Family name in the project (see list_project_families)"),
      path: z.string().optional().describe("Load this .rfa into the project first, then place it"),
      type: z.string().optional().describe("Type name (default: first type)"),
      level: z
        .string()
        .optional()
        .describe("Level name (default: level of the active plan, else the level nearest to elevation 0)"),
      location: vector3.optional().describe("[x, y, z] in mm; z is the offset from the level (default 0, 0, 0)"),
      rotation: z.number().optional().describe("Rotation about the vertical axis, degrees"),
      project: z.string().optional().describe("Project title (default: active project)"),
      select: z.boolean().optional().describe("Select the new instance (default true)"),
      show: z.boolean().optional().describe("Zoom the active view to the new instance (default true)"),
      overwriteProjectValues: z.boolean().optional().describe("With path: overwrite type parameter values of an already loaded family (default true)"),
    }
  );
}
