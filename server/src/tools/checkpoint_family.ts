import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerCheckpointFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "checkpoint_family",
    "Save a restore point of the pinned family session before a risky step. The current state is written to a temporary copy; the family's own file is not touched until you save. Go back with restore_family_checkpoint. Needs a session opened with new_family or open_family (not one pinned to a document open in the Revit UI). Checkpoints are deleted when the session is closed.",
    {
      label: z.string().optional().describe("Short note on what state this is, e.g. skeleton done"),
    }
  );
}
