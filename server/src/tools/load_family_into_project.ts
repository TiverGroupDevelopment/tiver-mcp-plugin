import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerLoadFamilyIntoProjectTool(server: McpServer) {
  registerFamilyTool(
    server,
    "load_family_into_project",
    "Load the current state of the family being edited (pinned session or active family document) into an open project without closing the session, to try it out there. The target is `project`, else the project the family was opened from, else the active project, else the only open project.",
    {
      project: z.string().optional().describe("Title of an open project document"),
      overwriteProjectValues: z
        .boolean()
        .optional()
        .describe("Overwrite type parameter values in the project (default true)"),
    }
  );
}
