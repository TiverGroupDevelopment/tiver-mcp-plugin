import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, overwriteProjectValuesField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

const form = z.object({
  elementId: z.number().int().describe("Extrusion or revolution id"),
  planes: z
    .array(z.string())
    .min(1)
    .describe("Reference plane names this form's sketch may be locked to; each straight sketch line is locked to every listed plane it lies in"),
  axis: z.boolean().optional().describe("Revolution: also lock the axis line to the listed plane it lies in (default true)"),
});

export function registerLockFamilySketchTool(server: McpServer) {
  registerFamilyTool(
    server,
    "lock_family_sketch",
    `Lock the sketch lines of extrusions and revolutions (and the revolution axis) to reference planes with alignment constraints, so the form follows when a labelled dimension moves the plane. align_family_elements only locks planar faces; this locks the sketch itself, e.g. every station and radius line of a revolution profile, the axis of a revolution placed off the origin, or each rectangle of a multi-loop extrusion. Lines must already lie in the plane. Returns locked lines, locked axes, and straight lines that matched no listed plane (free) — sloped chamfer lines cannot be locked. ${sessionNote}`,
    {
      familyName: familyNameField,
      forms: z.array(form).min(1),
      toleranceMm: z.number().positive().optional().describe("How close a line must be to a plane to count as lying in it, mm (default 0.003)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
