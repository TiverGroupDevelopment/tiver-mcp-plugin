import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { familyNameField, sessionNote } from "../utils/familyTool.js";

type ExtractedImage = { data: string; mimeType: string };

// Достаёт imageBase64 из ответа плагина (на любой глубине) и убирает его из JSON,
// чтобы картинка ушла клиенту блоком image, а не строкой в тексте.
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

export function registerCaptureFamilyViewTool(server: McpServer) {
  server.tool(
    "capture_family_view",
    `Render a family view to a PNG and return it as an image, to check the modelled geometry visually. Without view, a temporary isometric 3D view aimed at the forms is rendered (works for small parts too). With a plan or elevation name, reference planes, levels and dimensions are hidden by default so the image fits the geometry. Temporary changes are rolled back; the family is not modified. Uses Revit image export, so it does not depend on the Revit window being visible. ${sessionNote}`,
    {
      familyName: familyNameField,
      view: z
        .string()
        .optional()
        .describe("View name (see get_family_geometry → views). Default: temporary isometric 3D view"),
      direction: z
        .union([z.tuple([z.number(), z.number(), z.number()]), z.object({ x: z.number(), y: z.number(), z: z.number() })])
        .optional()
        .describe("Viewing direction for the temporary 3D view (default [-1, 1, -1]: from front-right-top)"),
      style: z
        .enum(["wireframe", "hiddenLine", "shaded", "shadedWithEdges", "realistic"])
        .optional()
        .describe("Display style (default shadedWithEdges)"),
      hideDatums: z
        .boolean()
        .optional()
        .describe("For plans and elevations: hide reference planes, levels, reference lines and dimensions (default true)"),
      showConnectors: z
        .boolean()
        .optional()
        .describe("Show MEP connector symbols (default false: their screen-size symbols would shrink a small part to a dot)"),
      pixels: z
        .number()
        .int()
        .min(256)
        .max(4096)
        .optional()
        .describe("Image width in pixels (default 1024)"),
    },
    async (args: Record<string, unknown>) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("capture_family_view", args);
        });

        const images: ExtractedImage[] = [];
        const stripped = extractImage(response, images);

        return {
          content: [
            ...images.map((image) => ({
              type: "image" as const,
              data: image.data,
              mimeType: image.mimeType,
            })),
            { type: "text" as const, text: JSON.stringify(stripped, null, 2) },
          ],
          isError: images.length === 0,
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text" as const,
              text: `capture_family_view failed: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}
