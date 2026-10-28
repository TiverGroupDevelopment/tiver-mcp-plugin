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
