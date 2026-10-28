import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  formFinishFields,
  overwriteProjectValuesField,
  profileLoop,
  registerFamilyTool,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyBlendTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_blend",
    "Create a solid or void blend between a base loop on the work plane and a top loop at the given height along the plane normal.",
    {
      familyName: familyNameField,
      workPlane: workPlaneField,
      baseProfile: profileLoop,
      topProfile: profileLoop,
      height: z.number().positive().describe("Distance between base and top, mm"),
      ...formFinishFields,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
