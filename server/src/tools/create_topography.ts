import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";
import { planLoops, point3d } from "../utils/topographyTool.js";

export function registerCreateTopographyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_topography",
    "Create terrain from survey points (mm, internal coordinates, z = absolute elevation). In Revit 2024+ creates a toposolid by default, earlier versions create a topography surface.",
    {
      points: z.array(point3d).min(3).describe("Survey points, distinct in plan"),
      kind: z
        .enum(["auto", "topographySurface", "toposolid"])
        .optional()
        .describe("auto (default): toposolid in Revit 2024+, topographySurface otherwise"),
      material: z.string().optional().describe("topographySurface only: material name in the project"),
      typeName: z.string().optional().describe("toposolid only: toposolid type name (default: first available)"),
      levelName: z
        .string()
        .optional()
        .describe("toposolid only: host level (default: highest level not above the lowest point)"),
      boundary: planLoops
        .optional()
        .describe("toposolid only: plan boundary; by default it is derived from the points"),
    }
  );
}
