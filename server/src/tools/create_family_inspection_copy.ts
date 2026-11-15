import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerCreateFamilyInspectionCopyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_inspection_copy",
    `Make an inspection copy of a family for a reviewer: '<name>_инспекция.rfa' next to the source, with reference planes 'ИНСП_…' at the geometry extremes and overall dimensions on the plan and elevations, view scale fitted to the part. The source family is not changed. By default the copy opens in a Revit editor tab on the front elevation. The current family must be saved (the copy is made from the file). ${sessionNote}`,
    {
      path: z.string().optional().describe("Source .rfa (default: current family's file)"),
      familyName: familyNameField,
      targetPath: z.string().optional().describe("Copy path (default: '<source>_инспекция.rfa' next to the source)"),
      overwrite: z.boolean().optional().describe("Replace an existing copy (default true)"),
      open: z.boolean().optional().describe("Open the copy in a Revit editor tab (default true)"),
      offsetMm: z.number().positive().optional().describe("Distance from the geometry to dimension lines, mm (default 15% of the largest size)"),
    }
  );
}
