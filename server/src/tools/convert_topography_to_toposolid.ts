import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";
import { topographyElementIdField } from "../utils/topographyTool.js";

export function registerConvertTopographyToToposolidTool(server: McpServer) {
  registerFamilyTool(
    server,
    "convert_topography_to_toposolid",
    "Revit 2024+: create a toposolid from a topography surface; its site subregions become toposolid subdivisions.",
    {
      elementId: topographyElementIdField,
      typeName: z.string().optional().describe("Toposolid type name (default: first available)"),
      levelName: z
        .string()
        .optional()
        .describe("Host level (default: highest level not above the lowest point)"),
      deleteSource: z.boolean().optional().describe("Delete the source topography surface (default false)"),
    }
  );
}
