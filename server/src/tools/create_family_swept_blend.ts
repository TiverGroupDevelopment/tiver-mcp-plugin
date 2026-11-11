import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  formFinishFields,
  overwriteProjectValuesField,
  pathSpec,
  profileLoop,
  registerFamilyTool,
  sessionNote,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilySweptBlendTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_swept_blend",
    `Create a solid or void swept blend in a family: one profile at the start of a path morphing into another at its end. The path must be a single curve (two points, or one arc segment) in the work plane. Each profile is one closed loop in profile-local XY coordinates around the origin (mm). ${sessionNote}`,
    {
      familyName: familyNameField,
      workPlane: workPlaneField,
      path: pathSpec,
      bottomProfile: profileLoop.describe("Profile at the path start, profile-local coordinates (mm)"),
      topProfile: profileLoop.describe("Profile at the path end, profile-local coordinates (mm)"),
      ...formFinishFields,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
