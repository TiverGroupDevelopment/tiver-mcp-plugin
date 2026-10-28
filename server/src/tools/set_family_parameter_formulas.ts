import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
} from "../utils/familyTool.js";

export function registerSetFamilyParameterFormulasTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_family_parameter_formulas",
    "Set or clear formulas of family parameters. Use Revit formula syntax with units, e.g. 'Width * 2', '(Length - 50 mm) / 2', 'if(Count > 2, 1, 0)'. Empty string or null clears the formula.",
    {
      familyName: familyNameField,
      formulas: z.record(z.union([z.string(), z.null()])).describe("Map 'parameter name' -> formula"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
