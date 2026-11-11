import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
  vector3,
} from "../utils/familyTool.js";

const plane = z.object({
  name: z.string().describe("Unique reference plane name"),
  axis: z
    .enum(["X", "Y", "Z"])
    .optional()
    .describe(
      "Plane perpendicular to this axis: X gives the plane x = offset, Y gives y = offset, Z gives the horizontal plane z = offset"
    ),
  offset: z.number().optional().describe("Position along axis, mm (default 0)"),
  origin: vector3.optional().describe("Alternative to axis: a point on the plane, mm"),
  normal: vector3.optional().describe("Alternative to axis: plane normal"),
  isReference: z
    .enum([
      "Left",
      "CenterLeftRight",
      "Right",
      "Front",
      "CenterFrontBack",
      "Back",
      "Bottom",
      "CenterElevation",
      "Top",
      "NotAReference",
      "StrongReference",
      "WeakReference",
    ])
    .optional()
    .describe("Is Reference property: how the plane can be dimensioned or snapped to in a project"),
  definesOrigin: z.boolean().optional().describe("Defines Origin property"),
  extent: z.number().positive().optional().describe("Half-length of the drawn plane line, mm (default 1000)"),
  view: z.string().optional().describe("View to draw in; chosen automatically when omitted"),
});

export function registerCreateFamilyReferencePlanesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_reference_planes",
    `Create named reference planes in a family (batch, one transaction). Reference planes are the skeleton of a parametric family: dimension them with create_family_dimensions (label = family parameter), then lock form faces to them with align_family_elements. ${sessionNote}`,
    {
      familyName: familyNameField,
      planes: z.array(plane).min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
