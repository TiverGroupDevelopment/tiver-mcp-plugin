import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
} from "../utils/familyTool.js";

export function registerSetFamilyParameterValuesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_family_parameter_values",
    "Set values of family parameters. Target types: current type by default, or typeNames, or allTypes. Atomic: any error rolls back everything. Parameters driven by formulas cannot be set.",
    {
      familyName: familyNameField,
      values: z
        .record(z.union([z.number(), z.string(), z.boolean(), z.null()]))
        .describe("Map 'parameter name' -> value (mm/m²/m³/degrees, material name, true/false, null clears reference)"),
      typeNames: z.array(z.string()).optional(),
      allTypes: z.boolean().optional(),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
