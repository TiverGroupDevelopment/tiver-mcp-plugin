import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerEditElementTypeTool(server: McpServer) {
  registerFamilyTool(
    server,
    "edit_element_type",
    [
      "Duplicate a type (Edit Type > Duplicate) and/or set its type parameters. Works for system types (curtain wall, wall, mullion, panel) and family types. All parameters are set in one transaction; any error rolls everything back.",
      "Parameter keys: built-in name (language independent, preferred) or display name. Values: number = mm for lengths, degrees for angles; string with units = as typed in Revit; reference parameters = type or material NAME ('Family : Type' for profiles, 'none' to clear); list parameters = displayed text (e.g. 'Fixed Distance') or integer; Yes/No = true/false.",
      "Curtain wall type keys: SPACING_LAYOUT_VERT / SPACING_LENGTH_VERT (vertical grid lines), SPACING_LAYOUT_HORIZ / SPACING_LENGTH_HORIZ (horizontal grid lines), AUTO_MULLION_INTERIOR_VERT, AUTO_MULLION_BORDER1_VERT, AUTO_MULLION_BORDER2_VERT, AUTO_MULLION_INTERIOR_HORIZ, AUTO_MULLION_BORDER1_HORIZ, AUTO_MULLION_BORDER2_HORIZ (mullion type names or 'none'), AUTO_PANEL_WALL (curtain panel type).",
      "Mullion type keys: MULLION_PROFILE ('Profile family : type'), MATERIAL_ID_PARAM, RECT_MULLION_WIDTH1, RECT_MULLION_WIDTH2, RECT_MULLION_THICK. Unknown key: call get_element_types with the type name to list its parameters.",
    ].join("\n"),
    {
      type: z.string().describe("Source type name or 'Family : Type'"),
      kind: z
        .string()
        .optional()
        .describe("Narrow the search: wall | curtainWall | wallType | mullion | panel | profile | OST_... (needed when names repeat)"),
      duplicateAs: z.string().optional().describe("Create a copy with this name and edit the copy; if it already exists, it is edited instead"),
      parameters: z.record(z.any()).optional().describe("{ parameterKey: value }"),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
