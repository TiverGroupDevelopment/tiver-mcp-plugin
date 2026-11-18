import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

const wallSelector = z.object({
  ids: z.array(z.number().int()).optional(),
  wallType: z.string().optional(),
});

export function registerCheckCurtainLayoutTool(server: McpServer) {
  registerFamilyTool(
    server,
    "check_curtain_layout",
    "Check curtain wall layout against rules and return pass/fail with the offending elements (they are selected in Revit). maxMullionLength: every mullion (optionally of one type) is not longer than maxMm, e.g. a mullion must not exceed the maximum product length. alignWith: every vertical grid line of the reference curtain walls that falls within a checked wall has a matching vertical grid line on it (a screen in front of glazing shares the same axes). Read-only.",
    {
      ids: z.array(z.number().int()).optional().describe("Walls to check (default: all curtain walls, or by wallType)"),
      wallType: z.string().optional(),
      maxMullionLength: z
        .object({ maxMm: z.number().positive(), mullionType: z.string().optional().describe("Only mullions of this type") })
        .optional(),
      alignWith: wallSelector.optional().describe("Reference curtain walls: {ids} or {wallType}"),
      toleranceMm: z.number().optional().describe("Default 1"),
      selectFailed: z.boolean().optional().describe("Select failing elements in Revit (default true)"),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
