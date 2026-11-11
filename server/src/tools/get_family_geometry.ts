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
    }
  );
}
