import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerTestFamilyFlexTool(server: McpServer) {
  registerFamilyTool(
    server,
    "test_family_flex",
    `Flex-test a parametric family: for each case, apply the parameter values, regenerate, and report Revit errors and warnings plus the resulting volume and size of every form. Every case is rolled back, so the family is left unchanged. Use it after building constraints to find parameter combinations that break the geometry (constraints not satisfied, zero-thickness forms). ${sessionNote}`,
    {
      familyName: familyNameField,
      cases: z
        .array(
          z.object({
            name: z.string().optional().describe("Label for the report"),
            type: z.string().optional().describe("Family type to start from (default: current type)"),
            values: z
              .record(z.union([z.number(), z.string(), z.boolean()]))
              .describe("Parameter name → value. Length in mm, angle in degrees, area in m², volume in m³."),
          })
        )
        .min(1)
        .max(50),
    }
  );
}
