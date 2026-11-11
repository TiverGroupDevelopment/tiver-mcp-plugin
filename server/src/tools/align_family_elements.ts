import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  referenceSpec,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

const alignment = z.object({
  reference: referenceSpec.describe("The plane that stays put, usually a reference plane or level"),
  target: referenceSpec.describe("The plane locked to it, usually a form face {elementId, faceIndex}"),
  view: z.string().optional().describe("View for the constraint; chosen automatically when omitted"),
});

export function registerAlignFamilyElementsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "align_family_elements",
    `Lock planar form faces to reference planes or levels (alignment constraint), so the geometry follows when a parameter moves the plane. The two planes must already coincide: the tool only locks them and reports the gap in mm otherwise. Get face indexes from get_family_geometry with detail: true. ${sessionNote}`,
    {
      familyName: familyNameField,
      alignments: z.array(alignment).min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
