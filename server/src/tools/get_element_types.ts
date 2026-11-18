import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerGetElementTypesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_element_types",
    "List element types in the project, including SYSTEM types (wall, curtain wall, mullion, curtain panel) and loadable ones (profiles, panels). With name (or includeParameters) returns every type parameter: display name, built-in name (e.g. SPACING_LENGTH_VERT), storage, value (lengths in mm, angles in degrees, references as names) and the text shown in Revit. Use before edit_element_type to see exact parameter names and allowed values.",
    {
      kind: z
        .string()
        .optional()
        .describe("wall | curtainWall | wallType (both) | mullion | panel | profile | any | OST_... category (default any)"),
      name: z.string().optional().describe("Type name or 'Family : Type'"),
      includeParameters: z.boolean().optional().describe("Include type parameters (default true when name is given)"),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
