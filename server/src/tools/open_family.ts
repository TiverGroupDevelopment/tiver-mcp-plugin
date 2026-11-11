import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerOpenFamilyTool(server: McpServer) {
  registerFamilyTool(
    server,
    "open_family",
    "Open a family for a multi-step editing session and pin it: every following family tool edits this document until close_family. Use `path` for an .rfa on disk, `familyName` for a family loaded in the active project (opened via EditFamily; loaded back once, on close_family), or neither to pin the active family document. Only one session can be open at a time.",
    {
      path: z.string().optional().describe("Path to an .rfa file on the Revit machine"),
      familyName: z.string().optional().describe("Name of a family loaded in the active project"),
    }
  );
}
