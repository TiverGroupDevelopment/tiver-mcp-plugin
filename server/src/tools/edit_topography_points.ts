import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";
import { planPoint, point3d, topographyElementIdField } from "../utils/topographyTool.js";

export function registerEditTopographyPointsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "edit_topography_points",
    "Edit points of a topography surface or toposolid in one transaction: delete, then add, then change elevations. Existing points are matched by plan position within toleranceMm. Deleting toposolid points requires Revit 2025+; toposolid corner points cannot be deleted.",
    {
      elementId: topographyElementIdField,
      delete: z.array(planPoint).optional().describe("Points to delete (matched in plan)"),
      add: z
        .array(point3d)
        .optional()
        .describe("Points to add; for toposolids they must lie inside the boundary"),
      setElevation: z
        .array(point3d)
        .optional()
        .describe("Existing points (matched by x,y) and their new absolute elevation z"),
      toleranceMm: z
        .number()
        .positive()
        .optional()
        .describe("Plan search radius for existing points, mm (default 50)"),
    }
  );
}
