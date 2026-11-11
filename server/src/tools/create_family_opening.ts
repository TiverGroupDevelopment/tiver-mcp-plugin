import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  profileLoop,
  registerFamilyTool,
  sessionNote,
  workPlaneField,
} from "../utils/familyTool.js";

export function registerCreateFamilyOpeningTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_opening",
    `Cut an opening in the host of a hosted family (the wall, floor, ceiling or roof that comes with wall-based and similar templates), as for doors and windows. The profile is one closed loop in the work plane (mm); default plane is XZ for a wall host and XY otherwise. Returns the opening faces with indexes, so they can be aligned to reference planes. ${sessionNote}`,
    {
      familyName: familyNameField,
      hostId: z.number().int().optional().describe("Host element id (default: the first host in the family)"),
      workPlane: workPlaneField,
      profile: profileLoop,
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
