import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { familyNameField, overwriteProjectValuesField, profileLoops, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerCreateFamilyProfileTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_profile",
    `Draw a closed profile (outer loop and inner loops/holes, lines and arcs) as detail lines in a PROFILE family (category Profiles, e.g. from template "Metric Profile-Mullion.rft" / "Metric Profile.rft"). Use for custom mullion, sweep and railing profiles. Coordinates are mm in the profile plane around the family origin (0,0). For a mullion profile the origin sits on the curtain grid line. ${sessionNote}`,
    {
      familyName: familyNameField,
      loops: profileLoops,
      clear: z.boolean().optional().describe("Delete existing detail lines first (default true)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
