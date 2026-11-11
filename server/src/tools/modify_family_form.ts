import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerModifyFamilyFormTool(server: McpServer) {
  registerFamilyTool(
    server,
    "modify_family_form",
    `Change an existing family form: extrusion start/end, blend bottom/top, revolution angles, solid/void, visibility, material. The sketch profile cannot be edited: delete the form and create it again. ${sessionNote}`,
    {
      familyName: familyNameField,
      elementId: z.number().int().describe("Form id from get_family_geometry"),
      start: z.number().optional().describe("Extrusion start offset, mm"),
      end: z.number().optional().describe("Extrusion end offset, mm"),
      bottom: z.number().optional().describe("Blend bottom offset, mm"),
      top: z.number().optional().describe("Blend top offset, mm"),
      startAngle: z.number().optional().describe("Revolution start angle, degrees"),
      endAngle: z.number().optional().describe("Revolution end angle, degrees"),
      isSolid: z.boolean().optional().describe("true = solid, false = void"),
      visible: z.boolean().optional().describe("Visible property of the form"),
      material: z.string().optional().describe("Material name in the family document (solids only)"),
      cutIntersectingSolids: z
        .boolean()
        .optional()
        .describe("When switching to void: cut intersecting solids (default true)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
