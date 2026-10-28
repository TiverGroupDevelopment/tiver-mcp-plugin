import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
} from "../utils/familyTool.js";

export function registerAddFamilyTypeTool(server: McpServer) {
  registerFamilyTool(
    server,
    "add_family_type",
    "Create a new family type (copies values of baseType or of the current type) and optionally set its parameter values.",
    {
      familyName: familyNameField,
      name: z.string().describe("New type name"),
      baseType: z.string().optional().describe("Existing type to copy values from"),
      values: z.record(z.union([z.number(), z.string(), z.boolean(), z.null()])).optional(),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
