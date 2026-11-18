import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerCreateScheduleTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_schedule",
    "Create (or replace by name) a Revit schedule for a category with the given fields, grouping and totals, and return its rows as text. Use it to cross-check numbers with Revit's own schedule (e.g. curtain wall mullions by type and length). Fields: built-in parameter name (ELEM_TYPE_PARAM, CURVE_ELEM_LENGTH, ...), display name, or 'count'.",
    {
      category: z.string().describe("OST_... category, e.g. OST_CurtainWallMullions, OST_GenericModel"),
      name: z.string().describe("Schedule name"),
      fields: z.array(z.string()).min(1),
      groupBy: z.array(z.string()).optional().describe("Fields to sort and group by (must be in fields)"),
      itemized: z.boolean().optional().describe("Show every instance (default false: one row per group)"),
      open: z.boolean().optional().describe("Open the schedule in Revit (default false)"),
      project: z.string().optional(),
    }
  );
}
