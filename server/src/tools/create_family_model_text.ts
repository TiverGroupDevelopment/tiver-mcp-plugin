import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  point2d,
  registerFamilyTool,
  sessionNote,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyModelTextTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_model_text",
    `Create 3D model text in a family: marking, labels, engraving-like lettering on a face. The text lies in the work plane and is extruded along its normal by depthMm. Font height and font come from the model text type; with heightMm/font a matching type is found or created. ${sessionNote}`,
    {
      familyName: familyNameField,
      text: z.string().describe("Text"),
      workPlane: workPlaneField.describe(
        "Plane of the text: XY / XZ / YZ with offset, or a reference plane (default XY). E.g. {plane:'XY', offset: 17.4} for a face at z = 17.4"
      ),
      position: point2d.optional().describe("Text insertion point in work plane local coordinates, mm (default 0, 0)"),
      align: z.enum(["left", "center", "right"]).optional().describe("Horizontal alignment at the insertion point (default center)"),
      heightMm: z.number().positive().optional().describe("Character height, mm"),
      font: z.string().optional().describe("Font name, e.g. 'Yu Gothic Light', 'Arial'"),
      depthMm: z.number().positive().optional().describe("Extrusion depth, mm (default 1)"),
      material: z.string().optional().describe("Material name in the family"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
