import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

type ExtractedImage = { data: string; mimeType: string };

function extractImage(node: unknown, found: ExtractedImage[]): unknown {
  if (Array.isArray(node)) return node.map((item) => extractImage(item, found));
  if (node === null || typeof node !== "object") return node;
  const source = node as Record<string, unknown>;
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(source)) {
    if (key.toLowerCase() === "imagebase64" && typeof value === "string") {
      const mime = Object.entries(source).find(([k]) => k.toLowerCase() === "mimetype")?.[1];
      found.push({ data: value, mimeType: typeof mime === "string" ? mime : "image/png" });
      continue;
    }
    result[key] = extractImage(value, found);
  }
  return result;
}

const vector3 = z.union([z.tuple([z.number(), z.number(), z.number()]), z.object({ x: z.number(), y: z.number(), z: z.number() })]);

export function registerCaptureProjectViewTool(server: McpServer) {
  server.tool(
    "capture_project_view",
    "Create or update a named 3D view of the PROJECT, crop it with a section box (by coordinates or around elementIds), set viewing direction and style, hide element types (e.g. hide cladding to show the structure behind it), export it to PNG and return the image. Levels, grids and the section box outline are hidden. The view stays in the project and is opened in Revit (open: true).",
    {
      name: z.string().optional().describe("3D view name (default MCP_3D); an existing view with this name is updated"),
      sectionBox: z.object({ min: vector3, max: vector3 }).optional().describe("Section box in mm"),
      elementIds: z.array(z.number().int()).optional().describe("Fit the section box around these elements"),
      marginMm: z.number().optional().describe("Margin around elementIds (default 100)"),
      direction: vector3.optional().describe("Viewing direction (default [-1, 1, -1])"),
      style: z.enum(["shaded", "shadedWithEdges", "hiddenLine", "realistic", "wireframe"]).optional().describe("Default shaded"),
      hideTypes: z.array(z.string()).optional().describe("Hide elements of these type names in the view"),
      pixels: z.number().int().min(256).max(4096).optional().describe("Image width (default 1400)"),
      open: z.boolean().optional().describe("Open the view in Revit (default true)"),
      project: z.string().optional(),
    },
    async (args) => {
      try {
        const response = await withRevitConnection((client) => client.sendCommand("capture_project_view", args));
        const images: ExtractedImage[] = [];
        const rest = extractImage(response, images);
        return {
          content: [
            ...images.map((image) => ({ type: "image" as const, data: image.data, mimeType: image.mimeType })),
            { type: "text" as const, text: JSON.stringify(rest, null, 2) },
          ],
        };
      } catch (error) {
        return { content: [{ type: "text" as const, text: `capture_project_view failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
