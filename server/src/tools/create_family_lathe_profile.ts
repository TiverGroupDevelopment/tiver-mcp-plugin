import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { familyNameField, overwriteProjectValuesField, sessionNote } from "../utils/familyTool.js";

// Минимальная длина кривой в Revit (ShortCurveTolerance ≈ 0.78 мм) с запасом.
const MIN_SEGMENT_MM = 0.8;

const section = z
  .object({
    length: z.number().positive().describe("Section length along the axis, mm"),
    diameter: z.number().positive().optional().describe("Cylinder diameter, mm"),
    from: z.number().positive().optional().describe("Cone: diameter at the lower end, mm"),
    to: z.number().positive().optional().describe("Cone: diameter at the upper end, mm"),
  })
  .describe("Outer section from bottom to top: a cylinder (diameter) or a cone/chamfer (from → to)");

const boreStep = z.object({
  depth: z.number().positive().describe("Depth of this step from the previous one, mm"),
  diameter: z.number().positive().describe("Bore diameter, mm"),
});

type Point = [number, number];

function round(value: number): number {
  return Math.round(value * 1e6) / 1e6;
}

function pushPoint(points: Point[], point: Point) {
  const last = points[points.length - 1];
  if (last && Math.abs(last[0] - point[0]) < 1e-9 && Math.abs(last[1] - point[1]) < 1e-9) return;
  points.push([round(point[0]), round(point[1])]);
}

/** Строит замкнутый полупрофиль (x = радиус, y = высота) тела вращения вокруг оси Z. */
export function buildLatheProfile(args: {
  sections: { length: number; diameter?: number; from?: number; to?: number }[];
  bore?: {
    from?: "bottom" | "top";
    steps: { depth: number; diameter: number }[];
    end?: "flat" | "drill";
    drillAngle?: number;
    entryChamfer?: number;
  };
  baseZ?: number;
}) {
  const baseZ = args.baseZ ?? 0;
  const outer: Point[] = [];
  const radiusAt: { z0: number; z1: number; r0: number; r1: number }[] = [];
  let z = 0;
  args.sections.forEach((s, index) => {
    const d0 = s.diameter ?? s.from;
    const d1 = s.diameter ?? s.to;
    if (d0 === undefined || d1 === undefined) {
      throw new Error(`sections[${index}]: задайте diameter (цилиндр) или from и to (конус)`);
    }
    pushPoint(outer, [d0 / 2, z]);
    pushPoint(outer, [d1 / 2, z + s.length]);
    radiusAt.push({ z0: z, z1: z + s.length, r0: d0 / 2, r1: d1 / 2 });
    z += s.length;
  });
  const height = z;

  const outerRadius = (zz: number) => {
    const part = radiusAt.find((p) => zz >= p.z0 - 1e-9 && zz <= p.z1 + 1e-9) ?? radiusAt[radiusAt.length - 1];
    const t = part.z1 === part.z0 ? 0 : (zz - part.z0) / (part.z1 - part.z0);
    return part.r0 + (part.r1 - part.r0) * t;
  };

  let polygon: Point[];
  if (!args.bore || args.bore.steps.length === 0) {
    polygon = [[0, 0], ...outer, [0, height]];
  } else {
    const bore = args.bore;
    const fromTop = bore.from === "top";
    const chamfer = bore.entryChamfer ?? 0;
    // Внутренний контур от устья вглубь: (радиус, глубина от устья).
    const inner: Point[] = [];
    let depth = 0;
    bore.steps.forEach((step, index) => {
      const r = step.diameter / 2;
      if (index === 0) {
        if (chamfer > 0) {
          pushPoint(inner, [r + chamfer, 0]);
          pushPoint(inner, [r, chamfer]);
        } else {
          pushPoint(inner, [r, 0]);
        }
      } else {
        pushPoint(inner, [r, depth]);
      }
      depth += step.depth;
      pushPoint(inner, [r, depth]);
    });
    const lastR = bore.steps[bore.steps.length - 1].diameter / 2;
    const angle = bore.drillAngle ?? 118;
    const tip = (bore.end ?? "drill") === "drill" ? lastR / Math.tan(((angle / 2) * Math.PI) / 180) : 0;
    const bottom = depth + tip;
    if (bottom >= height) {
      throw new Error(`Отверстие (${round(bottom)} мм с конусом сверла) не короче детали (${round(height)} мм): деталь станет сквозной — уменьшите глубину`);
    }

    for (const [index, [r, d]] of inner.entries()) {
      const zz = fromTop ? height - d : d;
      // На устье фаска отверстия может сходиться с наружной кромкой (острая кромка), глубже — только меньше.
      const limit = outerRadius(zz) + (index === 0 ? 1e-9 : -1e-9);
      if (index === 0 ? r > limit : r >= limit) {
        throw new Error(`Отверстие Ø${round(r * 2)} на высоте ${round(zz)} мм не меньше наружного Ø${round(outerRadius(zz) * 2)}`);
      }
    }

    const toWorld = ([r, d]: Point): Point => [r, fromTop ? height - d : d];
    const innerWorld = inner.map(toWorld);
    const apex: Point = [0, fromTop ? height - bottom : bottom];
    if (!fromTop) {
      // Устье внизу: от устья по наружному контуру вверх, по оси вниз до дна и по отверстию обратно к устью.
      polygon = [innerWorld[0], ...outer, [0, height], apex];
      if (tip === 0) polygon.push([lastR, depth]);
      polygon.push(...innerWorld.slice(1).reverse());
    } else {
      polygon = [[0, 0], ...outer, innerWorld[0], ...innerWorld.slice(1)];
      if (tip === 0) pushPoint(polygon, [0, height - depth]);
      else polygon.push(apex);
    }
  }

  const points: Point[] = [];
  polygon.forEach((p) => pushPoint(points, p));
  if (points.length > 1 && points[0][0] === points[points.length - 1][0] && points[0][1] === points[points.length - 1][1]) {
    points.pop();
  }

  const short: string[] = [];
  for (let i = 0; i < points.length; i++) {
    const a = points[i];
    const b = points[(i + 1) % points.length];
    const length = Math.hypot(b[0] - a[0], b[1] - a[1]);
    if (length < MIN_SEGMENT_MM) {
      short.push(`(${a[0]}, ${a[1]})→(${b[0]}, ${b[1]}): ${round(length)} мм`);
    }
  }
  if (short.length > 0) {
    throw new Error(
      `Отрезки профиля короче ${MIN_SEGMENT_MM} мм — Revit их не построит: ${short.join("; ")}. ` +
        "Увеличьте уступы, фаски или длины участков (рёбра делайте конусами from→to длиной от 0.8 мм)."
    );
  }

  // Объём по теореме Паппа—Гульдина: V = 2π · Площадь · x̄ (мм³).
  let area = 0;
  let moment = 0;
  for (let i = 0; i < points.length; i++) {
    const [x0, y0] = points[i];
    const [x1, y1] = points[(i + 1) % points.length];
    const cross = x0 * y1 - x1 * y0;
    area += cross / 2;
    moment += ((x0 + x1) * cross) / 6;
  }
  const volumeCm3 = Math.abs(2 * Math.PI * moment) / 1000;

  return {
    profile: points.map(([x, y]) => [x, round(y + baseZ)] as Point),
    axis: { start: [0, baseZ] as Point, end: [0, round(baseZ + height)] as Point },
    heightMm: round(height),
    volumeCm3: Math.round(volumeCm3 * 1000) / 1000,
  };
}

export function registerCreateFamilyLatheProfileTool(server: McpServer) {
  server.tool(
    "create_family_lathe_profile",
    `Create a turned (lathe) part as one revolution around the vertical Z axis, described like a drawing: outer sections from bottom to top (cylinders by diameter, cones/chamfers by from→to) and an optional bore from the bottom or the top in steps, with a drill point or flat end and an entry chamfer. Barbs and ribs are cones. The half-profile is generated, checked for segments shorter than Revit's 0.8 mm minimum, and sent to create_family_revolution in the XZ plane. Returns the Revit form plus the profile and the theoretical volume (Pappus) for a mass check. ${sessionNote}`,
    {
      familyName: familyNameField,
      sections: z.array(section).min(1).describe("Outer sections from bottom (z = baseZ) to top"),
      bore: z
        .object({
          from: z.enum(["bottom", "top"]).optional().describe("Where the bore opens (default bottom)"),
          steps: z.array(boreStep).min(1).describe("Bore steps from the opening inwards"),
          end: z.enum(["flat", "drill"]).optional().describe("Bottom of the bore: drill point cone or flat (default drill)"),
          drillAngle: z.number().positive().max(179).optional().describe("Drill point angle, degrees (default 118)"),
          entryChamfer: z.number().min(0).optional().describe("45° chamfer at the bore opening, mm"),
        })
        .optional(),
      baseZ: z.number().optional().describe("Elevation of the bottom of the part, mm (default 0)"),
      material: z.string().optional().describe("Material name in the family"),
      isSolid: z.boolean().optional().describe("false creates a void (default true)"),
      cutIntersectingSolids: z.boolean().optional().describe("For voids: cut intersecting solid forms (default true)"),
      overwriteProjectValues: overwriteProjectValuesField,
    },
    async (args: Record<string, unknown>) => {
      try {
        const built = buildLatheProfile(args as Parameters<typeof buildLatheProfile>[0]);
        const command: Record<string, unknown> = {
          profile: [built.profile],
          axis: built.axis,
          workPlane: { plane: "XZ" },
        };
        for (const key of ["familyName", "material", "isSolid", "cutIntersectingSolids", "overwriteProjectValues"]) {
          if (args[key] !== undefined) command[key] = args[key];
        }

        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_family_revolution", command);
        });

        return {
          content: [
            {
              type: "text" as const,
              text: JSON.stringify(
                { revit: response, heightMm: built.heightMm, theoreticalVolumeCm3: built.volumeCm3, profile: built.profile },
                null,
                2
              ),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text" as const,
              text: `create_family_lathe_profile failed: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}
