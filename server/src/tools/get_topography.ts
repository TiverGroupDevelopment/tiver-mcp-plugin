import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerGetTopographyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_topography",
    "List topography surfaces (with site subregions) and toposolids (Revit 2024+, with subdivisions): ids, kind, type, material, level, point count, elevation range and bounding box in mm, areas in m². Optionally returns the points themselves.",
    {
      elementId: z
        .number()
        .int()
        .optional()
        .describe("Return only this element. Omit to list all topography elements."),
      includePoints: z.boolean().optional().describe("Include point coordinates in mm (default false)"),
      maxPoints: z
        .number()
        .int()
        .positive()
        .optional()
        .describe("Max points per element when includePoints (default 1000)"),
    }
  );
}
