import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerGetCurtainWallInfoTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_curtain_wall_info",
    "Read curtain walls: wall axis, vertical grid lines as distances along the wall from its start (mm), horizontal grid lines as elevations (mm), spacings, mullions grouped by type and orientation with min/max/total length, panels grouped by type and size. details: true lists every mullion (with end points) and panel; use the points to place brackets. Read-only.",
    {
      ids: z.array(z.number().int()).optional().describe("Wall ids (default: all curtain walls, or by wallType)"),
      wallType: z.string().optional().describe("Only curtain walls of this type"),
      details: z.boolean().optional().describe("List every mullion and panel (default false)"),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
