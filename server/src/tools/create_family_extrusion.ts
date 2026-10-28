import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  formFinishFields,
  overwriteProjectValuesField,
  profileLoops,
  registerFamilyTool,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyExtrusionTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_extrusion",
    "Create a solid or void extrusion in a family. Profile is drawn in work plane local coordinates (mm) and extruded along the plane normal from start to end.",
    {
      familyName: familyNameField,
      workPlane: workPlaneField,
      profile: profileLoops,
      start: z.number().optional().describe("Extrusion start, mm (default 0)"),
      end: z.number().describe("Extrusion end, mm"),
      ...formFinishFields,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
