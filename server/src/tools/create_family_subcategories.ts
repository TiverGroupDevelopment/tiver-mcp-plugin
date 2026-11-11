import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  colorSpec,
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerCreateFamilySubcategoriesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_subcategories",
    `Create subcategories of the family category (or update existing ones by name): line color, projection line weight and material. Assign them to forms and lines with set_family_element_graphics. ${sessionNote}`,
    {
      familyName: familyNameField,
      subcategories: z
        .array(
          z.object({
            name: z.string(),
            color: colorSpec.optional().describe("Line color"),
            lineWeight: z.number().int().min(1).max(16).optional().describe("Projection line weight 1-16"),
            material: z.string().optional().describe("Material name in the family document"),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
