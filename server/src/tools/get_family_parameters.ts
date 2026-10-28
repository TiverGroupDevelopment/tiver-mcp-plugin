import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool } from "../utils/familyTool.js";

export function registerGetFamilyParametersTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_family_parameters",
    "Read family parameters: kind, group, instance/type, formula and value for each family type. Units: length mm, area m², volume m³, angle degrees. Works on the active family document or on a family loaded in the active project (familyName).",
    {
      familyName: familyNameField,
      nameFilter: z.string().optional().describe("Case-insensitive substring of parameter name"),
      typeName: z.string().optional().describe("Return values only for this family type"),
      includeBuiltIn: z.boolean().optional().describe("Include built-in family parameters (default false)"),
    }
  );
}
