import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
  vector3,
} from "../utils/familyTool.js";

export function registerTransformFamilyElementsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "transform_family_elements",
    `Move, copy, rotate or mirror family elements (forms, reference planes, lines). Pass exactly one of move / copy / rotate / mirror. ${sessionNote}`,
    {
      familyName: familyNameField,
      elementIds: z.array(z.number().int()).min(1),
      move: vector3.optional().describe("Translation [dx, dy, dz], mm"),
      copy: vector3.optional().describe("Copy with translation [dx, dy, dz], mm; returns createdIds"),
      rotate: z
        .object({
          angle: z.number().describe("Degrees, counter-clockwise looking against the axis direction"),
          origin: vector3.optional().describe("Point on the axis, mm (default origin)"),
          axis: vector3.optional().describe("Axis direction (default Z)"),
        })
        .optional(),
      mirror: z
        .object({
          referencePlane: z.string().optional().describe("Mirror about this reference plane"),
          origin: vector3.optional().describe("Point on the mirror plane, mm (when no referencePlane)"),
          normal: vector3.optional().describe("Mirror plane normal (when no referencePlane)"),
          copy: z.boolean().optional().describe("Keep the original and create a mirrored copy (default true)"),
        })
        .optional(),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
