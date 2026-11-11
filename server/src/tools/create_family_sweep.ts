import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  formFinishFields,
  overwriteProjectValuesField,
  pathSpec,
  profileLoops,
  registerFamilyTool,
  sessionNote,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilySweepTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_sweep",
    `Create a solid or void sweep in a family: a closed profile driven along a path. The path is drawn in the work plane (mm). The profile is drawn in its own local XY plane around the origin (mm); Revit places that origin on the path, perpendicular to it. ${sessionNote}`,
    {
      familyName: familyNameField,
      workPlane: workPlaneField,
      path: pathSpec,
      profile: profileLoops.describe(
        "Profile loops in profile-local coordinates (mm), origin on the path. Outer loop first, then holes."
      ),
      profileLocation: z
        .enum(["start", "middle", "end"])
        .optional()
        .describe("Where on the path curve the profile plane sits (default start)"),
      profileCurveIndex: z
        .number()
        .int()
        .min(0)
        .optional()
        .describe("Index of the path curve that carries the profile plane (default 0)"),
      ...formFinishFields,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
