import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyElementVisibility,
  familyNameField,
  overwriteProjectValuesField,
  pathSpec,
  registerFamilyTool,
  sessionNote,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyLinesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_lines",
    `Draw model lines (3D, visible in every view) or symbolic lines (2D, visible only in views parallel to their plane — used for plan/elevation symbols) in a family. The path is drawn in the work plane (mm). ${sessionNote}`,
    {
      familyName: familyNameField,
      kind: z.enum(["model", "symbolic"]).optional().describe("Default: model"),
      workPlane: workPlaneField,
      path: pathSpec,
      subcategory: z
        .string()
        .optional()
        .describe("Subcategory of the family category (see get_family_properties / create_family_subcategories)"),
      visibility: familyElementVisibility.optional(),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
