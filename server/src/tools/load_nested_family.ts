import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerLoadNestedFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "load_nested_family",
    `Load another family (.rfa) into the family being edited, so it can be placed as a nested component with place_family_instance. Returns its types. ${sessionNote}`,
    {
      familyName: familyNameField,
      path: z.string().describe("Path to the .rfa to nest, on the Revit machine"),
      overwriteParameterValues: z
        .boolean()
        .optional()
        .describe("If the family is already loaded: overwrite its parameter values (default true)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
