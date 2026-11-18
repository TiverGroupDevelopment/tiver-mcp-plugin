import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { registerFamilyTool } from "../utils/familyTool.js";

const point2 = z
  .union([z.tuple([z.number(), z.number()]), z.object({ x: z.number(), y: z.number() })])
  .describe("Plan point [x, y] in mm");

export function registerCreateCurtainWallTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_curtain_wall",
    "Create a curtain wall (or any wall type) along a plan line from start to end, mm. The curtain grid, mullions and panels come from the type (set them with edit_element_type). offset shifts the line sideways: positive = to the LEFT of the start-to-end direction (use it to place a screen in front of a wall). Height: topLevel (+topOffset) or height. Returns a grid summary and the wall orientation. Note: a basic wall drawn start-to-end faces its exterior to the LEFT (+Y for a wall along +X), so offset > 0 puts a screen on the exterior side; Revit reports curtain wall orientation the other way round, keep glazing and screen consistent (flip). Changing border mullions in the type does not rebuild existing curtain walls: recreate them.",
    {
      type: z.string().describe("Wall or curtain wall type name"),
      start: point2,
      end: point2,
      baseLevel: z.string().describe("Base level name"),
      baseOffset: z.number().optional().describe("mm"),
      topLevel: z.string().optional().describe("Top constraint level name"),
      topOffset: z.number().optional().describe("mm, with topLevel"),
      height: z.number().optional().describe("Unconnected height, mm (default 3000, used without topLevel)"),
      offset: z.number().optional().describe("Sideways shift of the line, mm; positive to the left of start-to-end"),
      flip: z.boolean().optional().describe("Flip the wall orientation"),
      allowJoins: z.boolean().optional().describe("false: disallow joins at both ends (stand-alone screen). Default true"),
      select: z.boolean().optional().describe("Select the new wall (default true)"),
      project: z.string().optional().describe("Project title (default: active project)"),
    }
  );
}
