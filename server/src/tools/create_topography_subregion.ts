import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";
import { planLoops, topographyElementIdField } from "../utils/topographyTool.js";

export function registerCreateTopographySubregionTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_topography_subregion",
    "Mark an area on the terrain (roads, crane pads, lawns): a site subregion on a topography surface or a subdivision on a toposolid.",
    {
      elementId: topographyElementIdField,
      boundary: planLoops,
      material: z.string().optional().describe("Topography surface subregion: material name"),
      typeName: z.string().optional().describe("Toposolid subdivision: toposolid type name"),
    }
  );
}
