import { z, ZodRawShape } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "./ConnectionManager.js";

export const familyNameField = z
  .string()
  .optional()
  .describe(
    "Name of a family loaded in the active project. When the active document is a project, the family is opened via EditFamily, changed and loaded back. Omit when a family document is active."
  );

export const overwriteProjectValuesField = z
  .boolean()
  .optional()
  .describe(
    "Only when editing a family from a project: overwrite type parameter values in the project on reload (default true)."
  );

export const point2d = z
  .union([
    z.object({ x: z.number(), y: z.number() }),
    z.tuple([z.number(), z.number()]),
  ])
  .describe("2D point in mm in work plane local coordinates");

const segment = z.object({
  type: z.enum(["line", "arc"]).optional().describe("Default: line"),
  start: point2d,
  end: point2d,
  mid: point2d.optional().describe("Point on the arc, required for type=arc"),
});

export const profileLoop = z
  .union([
    z.array(point2d).min(3),
    z.object({ points: z.array(point2d).min(3) }),
    z.object({ rectangle: z.object({ min: point2d, max: point2d }) }),
    z.object({
      circle: z.object({ center: point2d.optional(), radius: z.number().positive() }),
    }),
    z.object({ segments: z.array(segment).min(2) }),
  ])
  .describe(
    "Closed loop: array of points, {points}, {rectangle:{min,max}}, {circle:{center,radius}} or {segments:[{type,start,end,mid}]}. Polylines are closed automatically."
  );

export const profileLoops = z
  .array(profileLoop)
  .min(1)
  .describe("Outer loop first, then inner loops (holes). Loops must not intersect.");

export const workPlaneField = z
  .object({
    plane: z
      .enum(["XY", "XZ", "YZ"])
      .optional()
      .describe("XY: normal +Z; XZ: local x=X, y=Z, normal -Y; YZ: local x=Y, y=Z, normal +X"),
    referencePlane: z
      .string()
      .optional()
      .describe("Reference plane name in the family (see get_family_geometry). Takes priority over plane; with offset 0 the sketch is hosted on it."),
    offset: z.number().optional().describe("Offset along the plane normal, mm"),
  })
  .optional();

export const formFinishFields = {
  isSolid: z.boolean().optional().describe("false creates a void (default true)"),
  material: z.string().optional().describe("Material name in the family document (solids only)"),
  cutIntersectingSolids: z
    .boolean()
    .optional()
    .describe("For voids: cut intersecting solid forms (default true)"),
};

export const vector3 = z
  .union([
    z.tuple([z.number(), z.number(), z.number()]),
    z.object({ x: z.number(), y: z.number(), z: z.number() }),
  ])
  .describe("[x, y, z] or {x, y, z}");

export const referenceSpec = z
  .union([
    z.string().describe("Reference plane name"),
    z.object({ referencePlane: z.string() }),
    z.object({ referencePlaneId: z.number().int() }),
    z.object({ level: z.string().describe("Level name in the family") }),
    z.object({
      elementId: z.number().int().describe("Form id"),
      faceIndex: z
        .number()
        .int()
        .describe("Planar face index from get_family_geometry with detail: true (faces[].index)"),
    }),
  ])
  .describe(
    "A plane to dimension or align to: reference plane name, {referencePlane}, {referencePlaneId}, {level} or a planar form face {elementId, faceIndex}."
  );

export const pathSpec = z
  .union([
    z.array(point2d).min(2),
    z.object({ points: z.array(point2d).min(2), closed: z.boolean().optional() }),
    z.object({ segments: z.array(segment).min(1) }),
    z.object({
      circle: z.object({ center: point2d.optional(), radius: z.number().positive() }),
    }),
  ])
  .describe(
    "Sweep path in work plane local coordinates (mm): array of points (open polyline), {points, closed}, {segments:[{type,start,end,mid}]} or {circle}."
  );

export const familyElementVisibility = z
  .object({
    coarse: z.boolean().optional(),
    medium: z.boolean().optional(),
    fine: z.boolean().optional(),
    planRcp: z.boolean().optional().describe("Plan / RCP views"),
    frontBack: z.boolean().optional().describe("Front / back views"),
    leftRight: z.boolean().optional().describe("Left / right views"),
    topBottom: z.boolean().optional().describe("Top / bottom views (symbolic lines)"),
    onlyWhenCut: z.boolean().optional().describe("Show only when the instance is cut"),
  })
  .describe("Family Element Visibility Settings; omitted keys keep their current value");

export const colorSpec = z
  .union([
    z.tuple([z.number().int(), z.number().int(), z.number().int()]),
    z.object({ r: z.number().int(), g: z.number().int(), b: z.number().int() }),
    z.string().describe("#RRGGBB"),
  ])
  .describe("RGB color 0-255: [r, g, b], {r, g, b} or #RRGGBB");

export const sessionNote =
  "Targets the pinned family session if one is open (new_family / open_family), otherwise the active family document or familyName from the active project.";

export function registerFamilyTool(
  server: McpServer,
  name: string,
  description: string,
  shape: ZodRawShape
) {
  server.tool(name, description, shape, async (args: Record<string, unknown>) => {
    try {
      const response = await withRevitConnection(async (revitClient) => {
        return await revitClient.sendCommand(name, args);
      });

      return {
        content: [{ type: "text" as const, text: JSON.stringify(response, null, 2) }],
      };
    } catch (error) {
      return {
        content: [
          {
            type: "text" as const,
            text: `${name} failed: ${error instanceof Error ? error.message : String(error)}`,
          },
        ],
        isError: true,
      };
    }
  });
}
