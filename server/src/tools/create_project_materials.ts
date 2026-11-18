import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { colorSpec, registerFamilyTool } from "../utils/familyTool.js";

export function registerCreateProjectMaterialsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_project_materials",
    "Create or update materials in the active PROJECT (not a family): name, shading color, transparency, material class, optionally as a copy of an existing material. Existing materials with the same name are updated. Use the material name afterwards in edit_element_type (e.g. MATERIAL_ID_PARAM of a mullion type).",
    {
      materials: z
        .array(
          z.object({
            name: z.string(),
            color: colorSpec.optional(),
            transparency: z.number().int().min(0).max(100).optional(),
            materialClass: z.string().optional().describe("e.g. Ceramic, Metal, Glass"),
            duplicateFrom: z.string().optional().describe("Existing material to copy when creating"),
          })
        )
        .min(1),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
