import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { colorSpec, registerFamilyTool } from "../utils/familyTool.js";

export function registerSetWallLayersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_wall_layers",
    "Set the layer structure of a BASIC wall type (Edit Type > Structure): optionally duplicate the type, then layers from EXTERIOR to INTERIOR with function, material and thickness. Missing materials are created. e.g. insulation 150 (Insulation) + concrete 250 (Structure). The wall axis stays in the middle of the total thickness.",
    {
      type: z.string().describe("Basic wall type to start from"),
      duplicateAs: z.string().optional().describe("Copy to this name and edit the copy (reused if it exists)"),
      layers: z
        .array(
          z.object({
            function: z.enum(["Structure", "Substrate", "Insulation", "Finish1", "Finish2", "Membrane"]).optional().describe("Default Structure"),
            material: z.string().optional(),
            thicknessMm: z.number().positive(),
            color: colorSpec.optional().describe("Color for a newly created material"),
          })
        )
        .min(1)
        .describe("Exterior first"),
      project: z.string().optional(),
    }
  );
}
