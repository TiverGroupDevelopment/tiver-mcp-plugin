import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerRestoreFamilyCheckpointTool(server: McpServer) {
  registerFamilyTool(
    server,
    "restore_family_checkpoint",
    "Roll the pinned family session back to a checkpoint made with checkpoint_family: everything done after it is discarded and the session continues from the checkpoint state. Element ids may differ after a restore, so read the geometry again.",
    {
      id: z.number().int().optional().describe("Checkpoint id (default: the latest one)"),
    }
  );
}
