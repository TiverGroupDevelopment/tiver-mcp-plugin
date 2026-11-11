import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
} from "../utils/familyTool.js";

export function registerAssociateFamilyParametersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "associate_family_parameters",
    "Associate element parameters with family parameters, making the family parametric: form parameters (e.g. EXTRUSION_END_PARAM, MATERIAL_ID_PARAM, IS_VISIBLE_PARAM), parameters of nested family instances, and connector sizes. Get ids and associable parameters from get_family_geometry, or from the associableParameters returned by place_family_instance and create_family_connector. familyParameter omitted/null removes the association.",
    {
      familyName: familyNameField,
      associations: z
        .array(
          z.object({
            elementId: z.number().int(),
            elementParameter: z.string().describe("BuiltInParameter name or parameter display name"),
            familyParameter: z.string().nullable().optional(),
          })
        )
        .min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
