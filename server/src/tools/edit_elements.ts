import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerEditElementsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "edit_elements",
    "Set INSTANCE parameters of elements by id and/or flip walls (Wall.Flip) and family instances (facing). Same value rules as edit_element_type: built-in name or display name as key; numbers in mm / degrees; references by name; list values by displayed text. Examples: WALL_TOP_OFFSET -50, WALL_HEIGHT_TYPE 'Уровень 3', ALL_MODEL_INSTANCE_COMMENTS 'text'. One transaction; any error rolls everything back.",
    {
      ids: z.array(z.number().int()).min(1),
      parameters: z.record(z.any()).optional().describe("{ parameterKey: value }"),
      flip: z.boolean().optional().describe("Flip walls / instance facing"),
      project: z.string().optional(),
    }
  );
}
