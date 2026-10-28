import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
} from "../utils/familyTool.js";

export function registerAddFamilyParametersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "add_family_parameters",
    "Add one or more family parameters in a single transaction. Values are applied to all family types; formulas are set after all parameters are created, so they may reference each other.",
    {
      familyName: familyNameField,
      parameters: z
        .array(
          z.object({
            name: z.string(),
            kind: z
              .enum(["Length", "Angle", "Area", "Volume", "Number", "Integer", "YesNo", "Text", "Material"])
              .optional()
              .describe("Default: Length"),
            group: z
              .enum(["Dimensions", "Constraints", "Data", "Text", "IdentityData", "Materials", "Graphics", "Construction", "Visibility", "Other"])
              .optional()
              .describe("Default depends on kind"),
            isInstance: z.boolean().optional().describe("Default false (type parameter)"),
            value: z
              .union([z.number(), z.string(), z.boolean()])
              .optional()
              .describe("Initial value for all types: number in mm/m²/m³/degrees, material name, true/false"),
            formula: z.string().optional().describe("Revit formula, e.g. 'Width / 2' or 'if(Height > 1000 mm, 1, 0)'"),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
