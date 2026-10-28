import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";
import { planPoint } from "../utils/topographyTool.js";

export function registerGetTopographyElevationTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_topography_elevation",
    "Get the terrain top elevation (mm) at plan points, e.g. to place equipment on the ground. Points outside the terrain return null.",
    {
      points: z.array(planPoint).min(1),
      elementId: z
        .number()
        .int()
        .optional()
        .describe("Sample only this element. Omit to take the highest of all topography elements."),
    }
  );
}
