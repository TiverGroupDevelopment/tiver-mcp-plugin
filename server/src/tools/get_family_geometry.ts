import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerGetFamilyGeometryTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_family_geometry",
    `List family forms (extrusions, blends, revolutions, sweeps) with ids, bounding boxes in mm and parameters that can be associated with family parameters; reference planes with their Is Reference setting; dimensions and alignments with labels; levels, views and materials. With detail: true each form also gets its volume, sketch profile and planar faces with indexes — use those face indexes in create_family_dimensions and align_family_elements. ${sessionNote}`,
    {
      familyName: familyNameField,
      detail: z
        .boolean()
        .optional()
        .describe("Include volume, sketch profile and indexed faces for each form (default false)"),
      elementIds: z.array(z.number().int()).optional().describe("Limit forms to these ids"),
      formsOnly: z
        .boolean()
        .optional()
        .describe("Return only forms, without reference planes, constraints, levels, views and materials (shorter answer)"),
      includeProfile: z
        .boolean()
        .optional()
        .describe("With detail: include the sketch profile and extents of each form (default true); false shortens the answer when you only need faces"),
      faces: z
        .enum(["all", "planar", "none"])
        .optional()
        .describe(
          "With detail: which faces to list (default all). Revolved and cylindrical faces are split in two halves by Revit, so 'planar' shortens the answer a lot. Face indexes stay the same as in the full list."
        ),
      faceNormal: z
        .union([z.tuple([z.number(), z.number(), z.number()]), z.object({ x: z.number(), y: z.number(), z: z.number() })])
        .optional()
        .describe("With detail: only planar faces with this outward normal, e.g. [0, 0, -1] for faces looking down"),
      faceOffset: z
        .number()
        .optional()
        .describe("With faceNormal: only faces at this distance from the origin along the normal, mm (e.g. faceNormal [0,0,-1] and faceOffset -15.5 for a face at z = 15.5)"),
    }
  );
}
