import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
  vector3,
} from "../utils/familyTool.js";

export function registerCreateFamilyArrayTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_array",
    `Create a linear or radial array of family elements (forms or nested instances). Pass either \`translation\` (linear) or \`radial\`. The number of members can be driven by an Integer family parameter via countParameter, which is how a family gets a variable number of repeated parts. ${sessionNote}`,
    {
      familyName: familyNameField,
      elementIds: z.array(z.number().int()).min(1),
      count: z.number().int().min(2).describe("Number of members, including the original"),
      translation: vector3.optional().describe("Linear array: offset [dx, dy, dz], mm"),
      radial: z
        .object({
          angle: z.number().describe("Angle in degrees"),
          origin: vector3.optional().describe("Point on the rotation axis, mm (default origin)"),
          axis: vector3.optional().describe("Axis direction (default Z)"),
        })
        .optional()
        .describe("Radial array"),
      anchor: z
        .enum(["second", "last"])
        .optional()
        .describe("What translation / angle measures to: the second member (default) or the last one"),
      countParameter: z.string().optional().describe("Integer family parameter that drives the member count"),
      view: z.string().optional().describe("View to build the array in (default: first floor plan)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
