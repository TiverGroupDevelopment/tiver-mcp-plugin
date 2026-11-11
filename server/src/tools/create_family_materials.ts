import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  colorSpec,
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerCreateFamilyMaterialsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_materials",
    `Create materials in a family document (or update existing ones by name): color, transparency, class. Use the name as \`material\` in form tools, or as the value of a Material family parameter. ${sessionNote}`,
    {
      familyName: familyNameField,
      materials: z
        .array(
          z.object({
            name: z.string(),
            duplicateFrom: z
              .string()
              .optional()
              .describe("Create as a copy of this existing material (keeps its appearance assets)"),
            color: colorSpec.optional().describe("Shading color"),
            transparency: z.number().int().min(0).max(100).optional().describe("0-100"),
            materialClass: z.string().optional().describe("Material class, e.g. Metal, Glass"),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
