import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  formFinishFields,
  overwriteProjectValuesField,
  point2d,
  profileLoops,
  registerFamilyTool,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyRevolutionTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_revolution",
    "Create a solid or void revolution. Profile and axis lie in the same work plane (default XZ); the profile must not cross the axis.",
    {
      familyName: familyNameField,
      workPlane: workPlaneField,
      profile: profileLoops,
      axis: z.object({ start: point2d, end: point2d }),
      startAngle: z.number().optional().describe("Degrees, default 0"),
      endAngle: z.number().optional().describe("Degrees, default 360"),
      ...formFinishFields,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
