import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerUpdateFamilyParametersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "update_family_parameters",
    `Change existing family parameters: rename, switch between type and instance, make reporting (the value is read from a dimension and can be used in formulas) or non-reporting, set the tooltip description, or remove. Values and formulas are set with set_family_parameter_values / set_family_parameter_formulas. ${sessionNote}`,
    {
      familyName: familyNameField,
      parameters: z
        .array(
          z.object({
            name: z.string().describe("Current parameter name"),
            rename: z.string().optional().describe("New name"),
            isInstance: z.boolean().optional().describe("true = instance, false = type"),
            reporting: z
              .boolean()
              .optional()
              .describe("Reporting parameter; must be an instance parameter labelling a dimension"),
            description: z.string().optional().describe("Tooltip description"),
            remove: z.boolean().optional().describe("Delete the parameter from the family"),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
