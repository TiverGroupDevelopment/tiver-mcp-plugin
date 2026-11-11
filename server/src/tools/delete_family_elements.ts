import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerDeleteFamilyElementsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "delete_family_elements",
    `Delete forms, reference planes, dimensions, alignments or lines from a family. Revit also deletes dependents (dimensions and alignments attached to the element); their ids are returned in alsoDeleted. ${sessionNote}`,
    {
      familyName: familyNameField,
      elementIds: z.array(z.number().int()).min(1),
      force: z
        .boolean()
        .optional()
        .describe("Allow deleting other element kinds such as views, levels or types (default false)"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
