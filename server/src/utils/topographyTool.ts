import { z } from "zod";

export const topographyElementIdField = z
  .number()
  .int()
  .optional()
  .describe(
    "Id of a topography surface or toposolid (see get_topography). May be omitted when the project has exactly one."
  );

export const point3d = z
  .union([
    z.object({ x: z.number(), y: z.number(), z: z.number() }),
    z.tuple([z.number(), z.number(), z.number()]),
  ])
  .describe("Point in mm, project internal coordinates; z is the absolute elevation");

export const planPoint = z
  .union([
    z.object({ x: z.number(), y: z.number() }),
    z.tuple([z.number(), z.number()]),
  ])
  .describe("Plan point in mm, project internal coordinates");

export const planLoop = z
  .union([
    z.array(planPoint).min(3),
    z.object({ points: z.array(planPoint).min(3) }),
    z.object({ rectangle: z.object({ min: planPoint, max: planPoint }) }),
    z.object({ circle: z.object({ center: planPoint, radius: z.number().positive() }) }),
  ])
  .describe(
    "Closed plan loop: array of points, {points}, {rectangle:{min,max}} or {circle:{center,radius}}. Polylines are closed automatically."
  );

export const planLoops = z
  .array(planLoop)
  .min(1)
  .describe("Outer loop first, then inner loops (holes). Loops must not intersect.");
