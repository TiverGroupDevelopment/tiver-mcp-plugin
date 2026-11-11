import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyElementVisibility,
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerSetFamilyElementGraphicsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_family_element_graphics",
    `Assign a subcategory and/or visibility settings (detail levels, view directions) to family forms, model lines and symbolic lines. Typical use: hide heavy geometry at coarse detail and show symbolic lines instead. ${sessionNote}`,
    {
      familyName: familyNameField,
      elementIds: z.array(z.number().int()).min(1),
      subcategory: z.string().optional().describe("Subcategory name of the family category"),
      visibility: familyElementVisibility.optional(),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
