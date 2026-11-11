import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerAddFamilySharedParametersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "add_family_shared_parameters",
    `Add shared parameters to a family from a shared parameter file (needed for parameters that must appear in schedules and tags). Uses the shared parameter file set in Revit, or \`sharedParameterFile\` for this call only (the user's setting is restored afterwards). Set values and formulas with the regular family parameter tools. ${sessionNote}`,
    {
      familyName: familyNameField,
      sharedParameterFile: z.string().optional().describe("Path to a shared parameter .txt file on the Revit machine"),
      parameters: z
        .array(
          z.object({
            name: z.string().optional().describe("Parameter name in the shared parameter file"),
            guid: z.string().optional().describe("Parameter GUID; takes priority over name"),
            definitionGroup: z
              .string()
              .optional()
              .describe("Group inside the shared parameter file, when the name is not unique"),
            group: z
              .string()
              .optional()
              .describe(
                "Properties palette group: Dimensions, Constraints, Data, Text, IdentityData, Materials, Graphics, Construction, Visibility, Other (default Data)"
              ),
            isInstance: z.boolean().optional().describe("Instance parameter (default false = type)"),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
